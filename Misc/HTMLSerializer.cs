using Discord;
using HtmlTags;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.Misc
{
    public class HTMLSerializer
    {
        public static HtmlDocument GenerateHTMLDocument(string pageTitle, string pageDescription = null, string scriptType = null)
        {
            /*
                <!doctype html>
                <html lang="en">
                    <head>
                        <meta charset="utf-8">

                        <title>The HTML5 Herald</title>
                        <meta name="description" content="The HTML5 Herald">
                        <meta name="author" content="SitePoint">

                        <link rel="stylesheet" href="css/styles.css?v=1.0">
                    </head>

                <body>
                    <script type="text/javascript"> </script>
                </body>
                </html>
            */

            HtmlDocument htmlDocument = new HtmlDocument();

            htmlDocument.Head.Append(new HtmlTag("meta").Attr("charset", "utf-8"));
            htmlDocument.Head.Append(new HtmlTag("title").Text(pageTitle ?? ""));
            htmlDocument.Head.Append(new HtmlTag("meta").Attr("name", "description").Attr("content", pageDescription ?? ""));

            if (!string.IsNullOrWhiteSpace(scriptType))
                htmlDocument.Body.Append(new HtmlTag("script").Attr("type", scriptType));

            return htmlDocument;
        }

        public static async Task SaveHTMLToFile(HtmlDocument document, string directory, string fileName)
        {
            try
            {
                fileName = fileName.TrimEnd();

                if (!fileName.EndsWith(".html"))
                    fileName = $"{fileName}.html";

                string directoryPath = Path.Combine(directory);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SaveHTMLToFile), $"Created Directory: {directoryPath}"));
                }

                string filePath = Path.Combine(directoryPath, fileName);

                File.WriteAllText(filePath, document.ToString());

                await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SaveHTMLToFile), $"Exported HTML: '{filePath}'"));
            }
            catch (Exception e)
            {
                await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(SaveHTMLToFile), "", e));
            }
        }

        public static async Task SaveTextToFile(StringBuilder builder, string directory, string fileName)
        {
            try
            {
                string directoryPath = Path.Combine(directory);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SaveTextToFile), $"Created Directory: {directoryPath}"));
                }

                string filePath = Path.Combine(directoryPath, fileName);

                File.WriteAllText(filePath, builder.ToString());

                await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SaveTextToFile), $"Exported: '{filePath}'"));
            }
            catch (Exception e)
            {
                await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(SaveTextToFile), "", e));
            }    
        }
    }
}
