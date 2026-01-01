using Discord;
using Discord.Commands;
using Discord.WebSocket;
using FlickrNet;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.TwitchIntegration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.Modules
{
    [RequireTimeJoined("0", "1", Group = "Free4AllPermission")]
    [RequireIsFollower(Group = "Free4AllPermission")]
    [RequireIsBot(Group = "Free4AllPermission")]
    public class FreeForAllModule : ModuleBase<CommandContext>, IServerModule
    {
        public Server _Server { get; set; }

        [Command("help")]
        [Summary("Lists available commands.")]
        public async Task HelpAsync()
        {
            ChannelMessage msg = _Server.CommandInfoHandler.FactoryCommandHelpMessage;
            msg.AppendContent(_Server.CommandInfoHandler.CustomCommandHelpMessage);

            msg.Channel = Context.Channel;
            
            await msg.SendAsync();
        }
        [Command("help")]
        [Summary("Lists detailed info for given command or group.")]
        public async Task<RuntimeResult> HelpAsync([Remainder] string commandName)
        {
            CustomRuntimeResult result = null;

            string header = string.Empty;
            string body = string.Empty;

            string[] groupQuery = commandName.TrimStart().TrimEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var groupResult = await _Server.CommandInfoHandler.GetCommandsInGroupAsync(groupQuery);
            result = groupResult;

            if (groupResult.IsSuccess && groupResult.ResultValue != null)
            {
                StringBuilder bodyBuilder = new StringBuilder();

                List<string> filtered = new List<string>();
                for (int i = 0; i < groupResult.ResultValue.Length; i++)
                {
                    string line;
                    if (groupResult.ResultValue[i].Groups.Count == groupQuery.Length)
                    {
                        if (string.IsNullOrEmpty(groupResult.ResultValue[i].Name))
                            line = await _Server.CommandInfoHandler.GetCommandInfoStringAsync(groupResult.ResultValue[i], 99);
                        else
                            line = await _Server.CommandInfoHandler.GetCommandInfoStringAsync(groupResult.ResultValue[i], groupQuery.Length + 2);
                    }
                    else
                        line = await _Server.CommandInfoHandler.GetCommandInfoStringAsync(groupResult.ResultValue[i], groupQuery.Length + 1);

                    if (!filtered.Contains(line))
                        filtered.Add(line);
                }

                for (int i = 0; i < filtered.Count; i++)
                    bodyBuilder.AppendLine(filtered[i]);


                header = commandName;
                body = bodyBuilder.ToString();
            }
            else
            {
                var getCommandResult = await _Server.CommandInfoHandler.GetCommandInfoAsync(commandName);
                if (getCommandResult.IsSuccess && getCommandResult.ResultValue is CommandMeta.CommandMetaInfo command)
                {
                    header = command.GetFullName();
                    body = await _Server.CommandInfoHandler.GetCommandInfoStringAsync(command, 99);

                    result = getCommandResult;
                }
            }


            if (result != null && result.IsSuccess)
            {
                ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Information)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(header)
                                .SetDescription(body.ToString())
                            );

                await msg.SendAsync();   
            }

            return result;
        }

        [Command("echo")]
        [Summary("Repeats a message.")]
        public async Task SayAsync([Summary("The text to repeat.")] [Remainder] string textToEcho)
        {
            if (textToEcho.StartsWith("\"") && textToEcho.EndsWith("\""))
            {
                textToEcho = textToEcho.Remove(0, 1);
                textToEcho = textToEcho.Remove(textToEcho.Length - 1);
            }

            ChannelMessage msg = new ChannelMessage(Context);
            
            foreach (string line in textToEcho.Split("\\n", StringSplitOptions.RemoveEmptyEntries))
                msg.AddContent(new ChannelMessageContent().SetDescription(line));

            await msg.SendAsync(true);
        }

        [Command("jointime")]
        [Summary("Prints time user has been part of the guild.")]
        public async Task<RuntimeResult> JoinedAgoAsync(SocketUser user = null)
        {
            try
            {
                if (Context.Channel is TwitchMessageChannel twitchChannel)
                    return CustomRuntimeResult.FromError($"{ReplyDictionary.COMMAND_ONLY_VALID_ON_DISCORD} -> '{Context?.Channel?.Name}' is a {nameof(TwitchMessageChannel)}.");

                if (user == null)
                    user = (SocketUser)Context.User;

                var time = await RequireTimeJoined.GetTimeJoinedAsync(user, _Server.LogHandler);

                StringBuilder timeBuilder = new StringBuilder();
                string body = await ReplyDictionary.ReplaceStringInvariantCase(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.X_HAS_BEEN_PART_OF_Y_FOR_Z_TIME, "{x}", $"_{user.Username}_"), "{y}", $"_{Context.Guild.Name}_");

                if (time.Years > 0)
                    timeBuilder.Append($" `{time.Years} {(time.Years == 1 ? ReplyDictionary.YEAR : ReplyDictionary.YEARS)}` ");
                if (time.Months > 0)
                    timeBuilder.Append($" `{time.Months} {(time.Months == 1 ? ReplyDictionary.MONTH : ReplyDictionary.MONTHS)}` ");
                if (time.Days > 0)
                    timeBuilder.Append($" `{time.Days} {(time.Days == 1 ? ReplyDictionary.DAY : ReplyDictionary.DAYS)}` ");
                if (time.Hours > 0)
                    timeBuilder.Append($" `{time.Hours} {(time.Hours == 1 ? ReplyDictionary.HOUR : ReplyDictionary.HOURS)}` ");
                if (time.Minutes > 0)
                    timeBuilder.Append($" `{time.Minutes} {(time.Minutes == 1 ? ReplyDictionary.MINUTE : ReplyDictionary.MINUTES)}` ");
                if (time.Seconds > 0)
                    timeBuilder.Append($" `{time.Seconds} {(time.Seconds == 1 ? ReplyDictionary.SECOND : ReplyDictionary.SECONDS)}` ");

                body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{z}", timeBuilder.ToString());


                ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Information)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(user.Username)
                                .SetDescription(body)
                            );

                await msg.SendAsync();


                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("random")]
        [Summary("Prints a randomly selected parameter.")]
        public async Task RandomAsync(string[] parameters)
        {
            string n = parameters[Launcher.Random.Next(parameters.Length)];

            ChannelMessage msg = new ChannelMessage(Context)
                .AddContent(new ChannelMessageContent().SetDescription(n));

            await msg.SendAsync(true);
        }

        [Command("today")]
        [Summary("Prints a random event that happened on this day.")]
        public async Task<RuntimeResult> TodayEventAsync()
        {
            var result = await WikipediaWrapper.GetRandomEntry();

            if (result.IsSuccess && result.ResultValue is WikipediaWrapper.SectionContent entry)
            {
                string body = entry.Content;

                if (entry.Type == WikipediaWrapper.SectionContent.SectionTypeOption.Birthday)
                    body = $"{EmojiDictionary.GetEmoji(EmojiDictionary.BIRTHDAY_CAKE)} {body}";
                else if (entry.Type == WikipediaWrapper.SectionContent.SectionTypeOption.Death)
                    body = $"{EmojiDictionary.GetEmoji(EmojiDictionary.GHOST)} {body}";

                ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle($"{DateTime.Today.ToString("dd. MMMM", _Server.CultureInfo)} {entry.Year} [{entry.Section}]")
                                .SetDescription(body))
                            .SetFooter(entry.Source, EmojiDictionary.WIKIPEDIA_LOGO)
                            .SetURL(entry.Source);

                await msg.SendAsync();
            }

            return result;
        }

        [Command("until")]
        [Summary("Returns the time until a certain date is reached.")]
        public async Task<RuntimeResult> UntilAsync(DateTime inputDate)
        {
            try
            {
                TimeSpan span = (inputDate - DateTime.Now);

                string d = Math.Abs(span.Days) > 1 ? "dd' Tage '" : Math.Abs(span.Days) > 0 ? "dd' Tag '" : "";
                string h = Math.Abs(span.Hours) > 1 ? "hh' Stunden '" : Math.Abs(span.Hours) > 0 ? "hh' Stunde '" : "";
                string m = Math.Abs(span.Minutes) > 1 ? "mm' Minuten '" : Math.Abs(span.Minutes) > 0 ? "dd' Minute '" : "";
                string s = Math.Abs(span.Seconds) > 1 ? "ss' Sekunden '" : Math.Abs(span.Seconds) > 0 ? "ss' Sekunde '" : "";

                await new ChannelMessage(Context)
                    .AddContent(new ChannelMessageContent().SetDescription(span.ToString($"{d}{h}{m}{s}")))
                    .SendAsync(true);

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "Free4AllAdminPermission")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "Free4AllAdminPermission")]
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "Free4AllAdminPermission")]
        [RequireIsBot(Group = "Free4AllAdminPermission")]
        public class FreeForAllAdminModule : ModuleBase<CommandContext>, IServerModule
        {
            public Server _Server { get; set; }

            [Command("echoTitle")]
            [Summary("Repeats Message as a Title.")]
            public async Task SayTitleAsync([Summary("The text to repeat.")][Remainder] string textToEcho)
            {
                if (textToEcho.StartsWith("\"") && textToEcho.EndsWith("\""))
                {
                    textToEcho = textToEcho.Remove(0, 1);
                    textToEcho = textToEcho.Remove(textToEcho.Length - 1);
                }

                ChannelMessage msg = new ChannelMessage(Context);

                msg.AddContent(new ChannelMessageContent().SetTitle(textToEcho));

                await msg.SendAsync();
            }

            [Command("echoFooter")]
            [Summary("Repeats Message as a footer.")]
            public async Task SayFooterAsync([Summary("The text to repeat.")][Remainder] string textToEcho)
            {
                if (textToEcho.StartsWith("\"") && textToEcho.EndsWith("\""))
                {
                    textToEcho = textToEcho.Remove(0, 1);
                    textToEcho = textToEcho.Remove(textToEcho.Length - 1);
                }

                ChannelMessage msg = new ChannelMessage(Context);

                msg.SetFooter(textToEcho);

                await msg.SendAsync();
            }


            [Command("flickr")]
            [Summary("Gets a picture with a certain keyword from flickr.")]
            public async Task<RuntimeResult> RandomFlickrPic(string keyword)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    return CustomRuntimeResult.FromError(ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY);

                CustomRuntimeResult<Photo> imageResult = await  _Server.FlickrHandler.GetRandomImage(keyword);

                if (!imageResult.IsSuccess)
                    return imageResult;

                if (imageResult.ResultValue is not Photo img || img == null)
                    return CustomRuntimeResult.FromError($"Could not get {nameof(Photo)} from Result Value!");

                ChannelMessage msg = new ChannelMessage(Context)
                .AddContent(new ChannelMessageContent()
                    .SetTitle(img.Title)
                    .SetDescription($"by {img.OwnerName}\n{img.WebUrl}"))
                .SetFooter(FlickrHandler.PIC_SOURCE_MAIN, FlickrHandler.PIC_SOURCE_ICON)
                .SetImageURL(img.SmallUrl);

                await msg.SendAsync();

                return imageResult;
            }
        }
    }
}
