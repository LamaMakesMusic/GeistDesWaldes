using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Discord;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;

namespace GeistDesWaldes.Misc;

public class WikipediaWrapper
{
    private const string API_URL = @"https://de.wikipedia.org/w/api.php";
    private const string PAGE_URL = @"https://de.wikipedia.org/wiki/";
    private const string PAGE_DATE_FORMAT = "d._MMMM";

    private const string BASE_PARAMS = "format=xml&action=parse";
    private const string SECTIONS_PARAMS = "prop=sections";
    private const string CONTENT_PARAMS = "prop=wikitext";

    private const string REGEX_ENTRY = @"(?<=\s\*\s)([^\*]+)(?=\s\*\s)";
    private const string REGEX_HEADER = @"([0-9]*:{1})";
    private const string REGEX_LINKS = @"(\[{2}[^[]*\|{1}([^]]*)\]{2})";
    private const string REGEX_ATTACHMENTS = @"(\[{2}(Datei:).*\]{2})";

    private static SectionContent[] _cachedEntries;
    private static SectionContent[] _randomEntries;

    private static DateTime _lastUpdate;

    private static readonly CultureInfo _deCulture = CultureInfo.GetCultureInfo("de-DE");


    public static async Task<CustomRuntimeResult<SectionContent>> GetRandomEntry()
    {
        CustomRuntimeResult<SectionContent[]> result = await EnsureEntryCache();

        if (result.IsSuccess)
        {
            try
            {
                Random ran = new((int)DateTime.Now.Ticks);
                int entryIndex = ran.Next(0, _randomEntries.Length);
                SectionContent entry = _randomEntries[entryIndex];

                var temp = new List<SectionContent>();
                for (int i = 0; i < _randomEntries.Length; i++)
                {
                    if (i == entryIndex)
                    {
                        continue;
                    }

                    temp.Add(_randomEntries[i]);
                }

                _randomEntries = temp.ToArray();

                return CustomRuntimeResult<SectionContent>.FromSuccess(value: entry);
            }
            catch (Exception e)
            {
                return CustomRuntimeResult<SectionContent>.FromError(e.ToString());
            }
        }

        return CustomRuntimeResult<SectionContent>.FromError(result.Reason);
    }

    private static async Task<CustomRuntimeResult<SectionContent[]>> EnsureEntryCache()
    {
        bool updateCache = _cachedEntries == null || _randomEntries == null || _lastUpdate != DateTime.Today;

        if (updateCache)
        {
            CustomRuntimeResult result = await UpdateCache(DateTime.Today);

            if (!result.IsSuccess)
            {
                return CustomRuntimeResult<SectionContent[]>.FromError(result.Reason);
            }
        }

        return CustomRuntimeResult<SectionContent[]>.FromSuccess(value: _cachedEntries);
    }

    private static async Task<CustomRuntimeResult> UpdateCache(DateTime date)
    {
        try
        {
            _cachedEntries = null;
            _randomEntries = null;

            string baseQuery = $"{API_URL}?{BASE_PARAMS}&page={date.ToString(PAGE_DATE_FORMAT, _deCulture)}";
            string sectionsQuery = $"{baseQuery}&{SECTIONS_PARAMS}";

            string webString = await Utility.DownloadWebString(sectionsQuery);

            if (string.IsNullOrWhiteSpace(webString))
            {
                return CustomRuntimeResult.FromError($"Failed downloading webstring for '{sectionsQuery}'");
            }

            XmlDocument xmlDoc = new();
            xmlDoc.LoadXml(webString);

            List<SectionContent> newEntries = new();

            SectionInfo[] sections = await ParseSections(xmlDoc);

            foreach (SectionInfo parentSection in sections)
            {
                foreach (SectionInfo section in parentSection.SubSections)
                {
                    await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(UpdateCache), $"Found Section: {section.Name}"));
                    string contentQuery = $"{baseQuery}&{CONTENT_PARAMS}&section={section.Index}";

                    xmlDoc.RemoveAll();

                    try
                    {
                        webString = await Utility.DownloadWebString(contentQuery);

                        if (string.IsNullOrWhiteSpace(webString))
                        {
                            await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(UpdateCache), $"Failed downloading webstring for section '{section.Name}'"));
                            continue;
                        }

                        xmlDoc.LoadXml(webString);

                        foreach (SectionContent content in await ParseSectionContents(xmlDoc, section))
                        {
                            await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Debug, nameof(UpdateCache), $"--- Found Entry: {content.Year} -> {content.Content}"));

                            content.Source = $@"{PAGE_URL}{date.ToString(PAGE_DATE_FORMAT, _deCulture)}";

                            newEntries.Add(content);
                        }
                    }
                    catch (Exception e)
                    {
                        await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(WikipediaWrapper), e.ToString()));
                    }
                }
            }

            if (newEntries.Count == 0)
            {
                newEntries.Add(new SectionContent
                {
                    Year = "----",
                    Section = "-",
                    Content = ReplyDictionary.NOTHING_HAPPENED_TODAY,
                    Source = $@"{PAGE_URL}{date.ToString(PAGE_DATE_FORMAT, _deCulture)}"
                });
            }

            _cachedEntries = newEntries.ToArray();
            _randomEntries = _cachedEntries;
            _lastUpdate = DateTime.Today;

            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            _cachedEntries = null;
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    private static Task<SectionInfo[]> ParseSections(XmlDocument doc)
    {
        return Task.Run(() =>
        {
            XmlNodeList nodes = doc.GetElementsByTagName("sections");

            if (nodes == null || nodes.Count == 0 || nodes[0] == null || nodes[0].ChildNodes == null)
            {
                return [];
            }

            List<SectionInfo> sections = new();

            foreach (XmlElement section in nodes?[0]?.ChildNodes)
            {
                if (!section.HasAttribute("toclevel") || !section.HasAttribute("line") || !section.HasAttribute("index"))
                {
                    continue;
                }

                string toclevel = section.GetAttribute("toclevel");

                SectionInfo info = new(section.GetAttribute("index"), section.GetAttribute("line"));

                if (toclevel.Equals("1"))
                {
                    sections.Add(info);
                }
                else if (sections.Count > 0)
                {
                    info.Type = sections[sections.Count - 1].Type;

                    if (info.Type == SectionContent.SectionTypeOption.Event && toclevel.Equals("2"))
                    {
                        sections[sections.Count - 1].SubSections.Add(info);
                    }
                    //else if ((info.Type == SectionContent.SectionTypeOption.Birthday || info.Type == SectionContent.SectionTypeOption.Death) && toclevel.Equals("3"))
                    //    sections[sections.Count - 1].SubSections.Add(info);
                }
            }

            return sections.ToArray();
        });
    }

    private static Task<SectionContent[]> ParseSectionContents(XmlDocument doc, SectionInfo sectionInfo)
    {
        return Task.Run(() =>
        {
            var contents = new List<SectionContent>();
            XmlNode textNode = doc.GetElementsByTagName("wikitext")[0];

            string parsedNode = HttpUtility.HtmlDecode(textNode.InnerText);

            foreach (Match entry in Regex.Matches(parsedNode, REGEX_ENTRY, RegexOptions.None))
            {
                string line = entry.Groups[0].Value;
                SectionContent content = new(sectionInfo.Name, sectionInfo.Type);

                // Get Year
                Match year = Regex.Match(line, REGEX_HEADER, RegexOptions.None);
                if (!year.Success)
                {
                    continue;
                }

                content.Year = year.Value.Replace(":", "");

                // Remove leading zeros
                while (line.StartsWith("{{0}}"))
                {
                    line = line.Remove(0, 5);
                }

                line = line.Remove(0, year.Length);

                // Remove Attachment Links
                while (Regex.Match(line, REGEX_ATTACHMENTS, RegexOptions.None) is Match attachment && attachment.Success)
                {
                    line = line.Remove(attachment.Index, attachment.Length);
                }

                // Replace Links with the link's display name
                while (Regex.Match(line, REGEX_LINKS, RegexOptions.None) is Match link && link.Success)
                {
                    line = line.Remove(link.Index, link.Length).Insert(link.Index, link.Groups[2].Value);
                }

                // Clean Up remaining stuff
                line = line.Replace("[[", "").Replace("]]", "").Replace("''", "\"").Replace("{{0}}", "").Replace("<br />", "").TrimStart().TrimEnd();

                content.Content = line;
                contents.Add(content);
            }

            return contents.ToArray();
        });
    }


    private class SectionInfo
    {
        public readonly string Index;
        public readonly string Name;

        public readonly List<SectionInfo> SubSections = new();

        public SectionContent.SectionTypeOption Type;


        public SectionInfo(string Index, string Name)
        {
            this.Index = Index;
            this.Name = Name;
            EvaluateSectionType();
        }

        public void EvaluateSectionType()
        {
            if (Name.IndexOf("Ereignisse", StringComparison.OrdinalIgnoreCase) > -1)
            {
                Type = SectionContent.SectionTypeOption.Event;
            }
            else if (Name.IndexOf("Geboren", StringComparison.OrdinalIgnoreCase) > -1)
            {
                Type = SectionContent.SectionTypeOption.Birthday;
            }
            else if (Name.IndexOf("Gestorben", StringComparison.OrdinalIgnoreCase) > -1)
            {
                Type = SectionContent.SectionTypeOption.Death;
            }
            else
            {
                Type = SectionContent.SectionTypeOption.None;
            }
        }
    }

    public class SectionContent
    {
        public enum SectionTypeOption
        {
            Event = 0,
            Birthday = 1,
            Death = 2,
            None = 3
        }

        public string Content;

        public string Section;
        public string Source;

        public SectionTypeOption Type;
        public string Year;

        public SectionContent()
        {
        }

        public SectionContent(string section, SectionTypeOption type)
        {
            Section = section;
            Type = type;
        }
    }
}