using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Citations;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using System;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.Modules
{
    [RequireIsFollower(Group = "CitationsPermission")]
    [RequireTimeJoined("0", "1", Group = "CitationsPermission")]
    [RequireIsBot(Group = "CitationsPermission")]
    [Group("quote")]
    [Alias("quotes", "citation", "citations")]
    public class CitationModule : ModuleBase<CommandContext>, IServerModule
    {
        public Server _Server { get; set; }

        [Priority(-1)]
        [Command]
        [Summary("Returns a random quote.")]
        public async Task<RuntimeResult> RandomCitationAsync(IUser author = null)
        {
            try
            {
                CustomRuntimeResult<Citation[]> result;

                if (author != null)
                    result = _Server.CitationsHandler.FindQuotes(author: author?.Username);
                else
                    result = _Server.CitationsHandler.GetAllQuotes();

                if (result.IsSuccess)
                {
                    Citation quote;

                    if (result.ResultValue?.Length > 0)
                        quote = result.ResultValue[Launcher.Random.Next(0, result.ResultValue.Length)];
                    else
                        quote = new Citation(Launcher.Instance.DiscordClient.CurrentUser.Username, ReplyDictionary.NO_QUOTES_CREATED, DateTime.Today);

                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Citations)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(quote.ToStringHeader(_Server.CultureInfo))
                                .SetDescription(quote.ToStringBody())
                            );

                    await msg.SendAsync();
                }

                return result;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("get")]
        [Summary("Returns a quote by ID.")]
        public async Task<RuntimeResult> GetCitationByIdAsync(int quoteId)
        {
            try
            {
                var result = await _Server.CitationsHandler.GetQuote(quoteId);

                if (result.IsSuccess && result.ResultValue is Citation quote)
                {
                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Citations)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(quote.ToStringHeader(_Server.CultureInfo))
                                .SetDescription(quote.ToStringBody())
                            );

                    await msg.SendAsync();
                }

                return result;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("find")]
        [Summary("Finds a quote based on keywords.")]
        public async Task<RuntimeResult> FindCitationAsync(string content = null, IUser author = null, DateTime? date = default)
        {
            try
            {
                var result = _Server.CitationsHandler.FindQuotes(date, author?.Username, content);
                if (result.IsSuccess)
                {
                    StringBuilder bodyBuilder = new StringBuilder();

                    if (result.ResultValue?.Length > 0)
                    {
                        foreach (Citation quote in result.ResultValue)
                            bodyBuilder.AppendLine(quote.ToString());
                    }
                    else
                        bodyBuilder.AppendLine("-");

                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Citations)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle($"{result.ResultValue?.Length} {ReplyDictionary.RESULTS}")
                                .SetDescription(bodyBuilder.ToString())
                            );

                    await msg.SendAsync();
                }

                return result;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("add")]
        [Summary("Adds a new quote.")]
        public async Task<RuntimeResult> AddNewCitationAsync(IUser author, string content)
        {
            try
            {
                if (author == null)
                    throw new Exception(ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY);

                if (string.IsNullOrWhiteSpace(content))
                    return CustomRuntimeResult.FromError(ReplyDictionary.QUOTE_CONTENT_IS_EMPTY);

                if (content.Length > 245)
                    content = $"{content.Substring(0, 245)} [...]";

                Citation quote = new Citation(author.Username, content, DateTime.Today);
                var result = await _Server.CitationsHandler.AddQuote(quote);

                if (result.IsSuccess)
                {
                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Citations)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription($"{ReplyDictionary.QUOTE_SAVED}\n {quote}")
                            );

                    await msg.SendAsync();
                }

                return result;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [RequireUserPermission(GuildPermission.Administrator, Group = "CitationsAdminPermission")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "CitationsAdminPermission")] 
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "CitationsAdminPermission")]
        public class CitationAdminModule : ModuleBase<CommandContext>, IServerModule
        {
            public Server _Server { get; set; }

            [Command("remove")]
            [Summary("Removes a quote.")]
            public async Task<RuntimeResult> RemoveCitationAsync(int citationID)
            {
                try
                {
                    var result = await _Server.CitationsHandler.RemoveQuote(citationID);
                    if (result.IsSuccess)
                    {
                        ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Citations)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(ReplyDictionary.QUOTE_DELETED)
                            );

                        await msg.SendAsync();
                    }

                    return result;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("edit")]
            [Summary("Edits a quote.")]
            public async Task<RuntimeResult> AddNewCitationAsync(int quoteId, string newContent)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(newContent))
                        return CustomRuntimeResult.FromError(ReplyDictionary.QUOTE_CONTENT_IS_EMPTY);

                    if (newContent.Length > 245)
                        newContent = $"{newContent.Substring(0, 245)} [...]";

                    var result = await _Server.CitationsHandler.GetQuote(quoteId);

                    if (result.IsSuccess)
                    {
                        string body = $"{ReplyDictionary.QUOTE_MODIFIED}\n#{quoteId}: \"{result.ResultValue.Content}\" -> \"{newContent}\"";

                        result.ResultValue.Content = newContent;

                        await _Server.CitationsHandler.SaveQuotesToFile();

                        ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Citations)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                        await msg.SendAsync();
                    }

                    return result;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

        }
    }
}
