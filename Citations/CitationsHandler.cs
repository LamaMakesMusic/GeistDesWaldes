using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;

namespace GeistDesWaldes.Citations;

public class CitationsHandler : BaseHandler
{
    public override int Priority => -12;
    
    private const string CITATIONS_FILE_NAME = "Quotes";
    private List<Citation> _quotes = new();


    public CitationsHandler(Server server) : base(server)
    {
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();
        await InitializeCitationsHandler();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();

        await CheckIntegrity();
    }

    private async Task InitializeCitationsHandler()
    {
        await GenericXmlSerializer.EnsurePathExistence(Server.LogHandler, Server.ServerFilesDirectoryPath, CITATIONS_FILE_NAME, _quotes);

        await LoadQuotesFromFile();
    }

    private async Task CheckIntegrity(bool skipFix = false)
    {
        bool cleanupIDs = false;
        List<(int idx, string error)> problematicEntries = [];

        for (int i = 0; i < _quotes.Count; i++)
        {
            if (_quotes[i] == null)
            {
                problematicEntries.Add((i, "NULL"));
            }
            else
            {
                (int, string) entry = default;

                if (_quotes[i].ID < 0)
                {
                    entry = (i, $"ID ({_quotes[i].ID}) < 0");
                }

                if (_quotes.Find(q => q.ID == _quotes[i].ID && _quotes[i] != q) is not null)
                {
                    if (entry == default)
                    {
                        entry = (i, $"Duplicate IDs ({_quotes[i].ID})!");
                    }
                    else
                    {
                        entry.Item2 = $"{entry.Item2} | Duplicate IDs ({_quotes[i].ID})!";
                    }
                }

                if (entry != default)
                {
                    problematicEntries.Add(entry);
                    cleanupIDs = true;
                    break;
                }
            }
        }

        if (cleanupIDs && !skipFix)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), "Attempting Clean Up..."));

            ResetIDs();

            await CheckIntegrity(true);
        }

        if (problematicEntries.Count > 0)
        {
            StringBuilder builder = new("Citations ERROR:\n");

            for (int i = 0; i < problematicEntries.Count; i++)
            {
                builder.AppendLine($"...[{problematicEntries[i].idx}] | {problematicEntries[i].error}");
            }

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
        }
        else
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Citations OK."), (int)ConsoleColor.DarkGreen);
        }
    }


    public Task SaveQuotesToFile()
    {
        _quotes.Sort((q1, q2) => { return q1.ID.CompareTo(q2.ID); });

        return GenericXmlSerializer.SaveAsync<List<Citation>>(Server.LogHandler, _quotes, CITATIONS_FILE_NAME, Server.ServerFilesDirectoryPath);
    }

    public async Task LoadQuotesFromFile()
    {
        var loadedQuotes = await GenericXmlSerializer.LoadAsync<List<Citation>>(Server.LogHandler, CITATIONS_FILE_NAME, Server.ServerFilesDirectoryPath);

        if (loadedQuotes == default)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(LoadQuotesFromFile), $"Loaded {nameof(loadedQuotes)} == DEFAULT"));
        }
        else
        {
            _quotes = loadedQuotes;
        }
    }

    private void ResetIDs()
    {
        try
        {
            for (int i = 0; i < _quotes?.Count; i++)
            {
                _quotes[i].ID = i;
            }
        }
        catch (Exception e)
        {
            Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(ResetIDs), string.Empty, e));
        }
    }

    public async Task<RuntimeResult> AddQuote(Citation quote)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(quote.Content))
            {
                return CustomRuntimeResult.FromError(ReplyDictionary.QUOTE_CONTENT_IS_EMPTY);
            }

            if (string.IsNullOrWhiteSpace(quote.Author))
            {
                quote.Author = ReplyDictionary.UNKNOWN_AUTHOR;
            }

            quote.ID = GetNextId();

            _quotes.Add(quote);

            await SaveQuotesToFile();

            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    private int GetNextId()
    {
        int nextId = 0;

        for (int i = 0; i < _quotes?.Count; i++)
        {
            if (_quotes[i].ID > nextId)
            {
                nextId = _quotes[i].ID;
            }
        }

        return nextId + 1;
    }

    public async Task<RuntimeResult> RemoveQuote(int quoteId)
    {
        CustomRuntimeResult<Citation> result = GetQuote(quoteId);

        if (result.IsSuccess)
        {
            return await RemoveQuote(result.ResultValue);
        }

        return result;
    }

    public async Task<RuntimeResult> RemoveQuote(Citation quote)
    {
        try
        {
            _quotes.Remove(quote);

            await SaveQuotesToFile();

            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    public CustomRuntimeResult<Citation> GetQuote(int id)
    {
        try
        {
            Citation result = _quotes.Find(q => q.ID == id);

            if (result == null)
                return CustomRuntimeResult<Citation>.FromError(ReplyDictionary.QUOTE_WITH_ID_X_NOT_FOUND.ReplaceStringInvariantCase("{x}", id.ToString()));

            return CustomRuntimeResult<Citation>.FromSuccess(value: result);
        }
        catch (Exception e)
        {
            return CustomRuntimeResult<Citation>.FromError(e.ToString());
        }
    }

    public CustomRuntimeResult<Citation[]> GetAllQuotes()
    {
        try
        {
            return CustomRuntimeResult<Citation[]>.FromSuccess(value: _quotes.ToArray());
        }
        catch (Exception e)
        {
            return CustomRuntimeResult<Citation[]>.FromError(e.ToString());
        }
    }

    public CustomRuntimeResult<Citation[]> FindQuotes(DateTime? date = null, string author = null, string content = null)
    {
        try
        {
            bool useDate = date.HasValue;
            bool useAuthor = !string.IsNullOrWhiteSpace(author);
            bool useContent = !string.IsNullOrWhiteSpace(content);

            var filtered = new List<Citation>();

            // Filter by Date
            if (useDate)
            {
                filtered = _quotes.FindAll(q => q.Date.Year == date.Value.Year
                                               && q.Date.Month == date.Value.Month
                                               && q.Date.Day == date.Value.Day);
            }

            // Filter by Author
            if (useAuthor)
            {
                filtered = (useDate ? filtered : _quotes).FindAll(q => q.Author.Equals(author, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by Content
            if (useContent)
            {
                string[] keywords = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < keywords.Length; i++)
                {
                    keywords[i] = keywords[i].Trim();
                }


                if (keywords.Length > 0)
                {
                    if (!useDate && !useAuthor)
                    {
                        filtered = new List<Citation>(_quotes);
                    }

                    for (int i = filtered.Count - 1; i >= 0; i--)
                    {
                        int matches = 0;

                        for (int j = 0; j < keywords.Length; j++)
                        {
                            if (filtered[i].Content.IndexOf(keywords[j], StringComparison.OrdinalIgnoreCase) > -1)
                            {
                                matches++;
                            }
                        }

                        if (matches > keywords.Length * .5f)
                        {
                            continue;
                        }

                        filtered.RemoveAt(i);
                    }
                }
            }


            return CustomRuntimeResult<Citation[]>.FromSuccess(value: filtered.ToArray());
        }
        catch (Exception e)
        {
            return CustomRuntimeResult<Citation[]>.FromError(e.ToString());
        }
    }
}