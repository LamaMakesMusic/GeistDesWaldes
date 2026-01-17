using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.CommandMeta;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GeistDesWaldes.UserCommands
{
    [Serializable]
    public class CustomCommand
    {
        [XmlIgnore] public int NameHash;
        public string Name;
        public CommandMetaInfo[] CommandsToExecute;

        public ulong TextChannelContextId;
        public float CooldownInSeconds;
        public int PriceTag;

        public bool Embed;
        public bool ShowCommandInfo;
        
        public bool IsEvent;
        public bool IsBirthday;


        [XmlIgnore] [NonSerialized] public Server Server;

        [XmlIgnore] [NonSerialized] public float RealCooldownInSeconds;
        [XmlIgnore] [NonSerialized] public int RealPriceTag;

        [XmlIgnore] [NonSerialized] public CustomCommandCategory Category;


        public CustomCommand()
        {

        }
        public CustomCommand(Server server, string commandName, CommandMetaInfo[] commands, ulong textChannelContext, float cooldownInSeconds = 0f, int fee = 0, bool embed = true, bool showInfo = false)
        {
            this.Server = server;

            SetName(commandName);
            CommandsToExecute = commands;
            TextChannelContextId = textChannelContext;
            CooldownInSeconds = cooldownInSeconds;
            PriceTag = fee;
            Embed = embed;
            ShowCommandInfo = showInfo;

            EnsureParameterQuotes();
        }
        
        /// <summary>
        /// Call this after load to initialize non-serialized values,
        /// and i.a. make up for errors when edititng files manually outside of the bot.
        /// </summary>
        /// <param name="server"></param>
        public void InitAfterLoadFromFile(Server server)
        {
            Server = server;
            SetName(Name);
            EnsureParameterQuotes();
        }


        private void SetName(string commandName)
        {
            Name = commandName;
            NameHash = commandName.ToLower().GetHashCode();
        }
        public Task<CustomRuntimeResult> SetCategory(Server server, CustomCommandCategory category)
        {
            return Task.Run(() =>
            {
                if (Category != null)
                {
                    if (Category.Commands.Contains(this.Name))
                        Category.Commands.Remove(this.Name);

                    if (Category.Commands.Count == 0)
                        server.GetModule<CustomCommandHandler>().CustomCommands.Categories.Remove(Category);

                    Category = null;
                }

                Category = category;
                Category?.Commands.Add(this.Name);

                return CustomRuntimeResult.FromSuccess();
            });
        }

        public void EnsureParameterQuotes()
        {
            if (CommandsToExecute != null)
            {
                foreach (CommandMetaInfo cmd in CommandsToExecute)
                {
                    if (cmd.RuntimeParameters != null)
                    {
                        foreach (RuntimeParameterInfo param in cmd.RuntimeParameters)
                        {
                            if (param.Value is string str)
                            {
                                if (!str.TrimStart().StartsWith("\""))
                                    str = $"\"{str}";
                                if (!str.TrimEnd().EndsWith("\"") || str.Length == 1)
                                    str = $"{str}\"";

                                param.Value = str;
                            }
                        }
                    }
                }
            }
        }


        public async Task Execute(ICommandContext context, string[] additionalParameters = null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(Execute), $"Executing custom command '{Name}'"));

            string[] commandLines = ConvertToCommandLines(CommandsToExecute, additionalParameters);

            if (commandLines == null || commandLines.Length == 0 || !GetChannelContext(context, out IMessageChannel channelContext))
                return;

            IDisposable typeState = null;
            if (!(context?.Message is MetaCommandMessage))
                typeState = channelContext.EnterTypingState();

            using (typeState)
            {
                // Bundles Messages to avoid bot spamming a channel
                CommandBundleEntry[] entries = new CommandBundleEntry[commandLines.Length];
                ChannelMessage[] messageBundle = new ChannelMessage[commandLines.Length];

                for (int i = 0; i < commandLines.Length; i++)
                {
                    entries[i] = new CommandBundleEntry(i, messageBundle);
                    await Server.ExecuteMetaCommandAsync(commandLines[i], channelContext, context?.User, entries[i]);
                }

                int timeout = 3000;
                while (entries.Any(e => e.IsDone == false))
                {
                    timeout -= 500;
                    await Task.Delay(500);

                    if (timeout < 0)
                    {
                        await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(Execute), $"{nameof(CustomCommand)} '{Name}' timed out waiting for all commands to execute! Message bundle might be incomplete!"));
                        
                        for (int d = 0; d < commandLines.Length; d++)
                        {
                            if (messageBundle[d] != null)
                                continue;

                            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(Execute), $"{nameof(CustomCommand)} '{Name}' never got answer for '{@commandLines[d]}'!"));
                        }
                        break;
                    }
                }

                ChannelMessage msg = new ChannelMessage(context).SetChannel(channelContext);

                if (IsEvent)
                {
                    msg.SetTemplate(ChannelMessage.MessageTemplateOption.Events);
                    msg.AddContent(new ChannelMessageContent().SetTitle(Name));
                }
                else if (IsBirthday)
                {
                    msg.SetTemplate(ChannelMessage.MessageTemplateOption.Birthday);
                }
                else if (ShowCommandInfo)
                {
                    msg.SetTemplate(ChannelMessage.MessageTemplateOption.Neutral);

                    ChannelMessageContent titleMessage = messageBundle.FirstOrDefault(m => m != null)?.Contents.FirstOrDefault(c => c.Title.text != null);

                    if (titleMessage == default)
                        msg.AddContent(new ChannelMessageContent().SetTitle($"[ !{Name} ]"));
                    else
                        titleMessage.Title.text = $"[ !{Name} ] {titleMessage.Title.text}";

                        
                    msg.SetFooter(RealCostsToString());
                }


                for (int i = 0; i < messageBundle.Length; i++)
                {
                    if (messageBundle[i] != null)
                        msg.AppendContent(messageBundle[i]);
                }


                await msg.SendAsync(!Embed);
            }
        }

        private bool GetChannelContext(ICommandContext context, out IMessageChannel channelContext)
        {
            channelContext = null;

            if (TextChannelContextId != default)
                channelContext = Task.Run(() => Launcher.Instance.GetChannel<IMessageChannel>(TextChannelContextId)).GetAwaiter().GetResult();

            if (channelContext == null)
                channelContext = context?.Channel;

            if (channelContext == null)
                channelContext = Task.Run(() => Launcher.Instance.GetChannel<IMessageChannel>(Server.Config.DiscordSettings.DefaultBotTextChannel)).GetAwaiter().GetResult();

            if (channelContext == null)
                Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(GetChannelContext), $"Could not get {nameof(IMessageChannel)} from {nameof(TextChannelContextId)} '{TextChannelContextId}' nor {nameof(ICommandContext)}!"));

            return channelContext != null;
        }

        private string[] ConvertToCommandLines(CommandMetaInfo[] commandsToExecute, string[] additionalParameters = null)
        {
            string[] converted = new string[commandsToExecute != null ? commandsToExecute.Length : 0];

            for (int i = 0; i < converted.Length; i++)
            {
                if (commandsToExecute != null)
                    converted[i] = commandsToExecute[i].GetCommandLineString(Server.CultureInfo, additionalParameters);
            }

            return converted;
        }


        public string[] ActionsToArray()
        {
            List<string> result = [];

            if (CommandsToExecute == null || CommandsToExecute.Length < 1)
                result.Add(" - ");
            else
            {
                StringBuilder line = new StringBuilder();

                for (int i = 0; i < CommandsToExecute?.Length; i++)
                {
                    line.Append($"{CommandsToExecute[i].FullName}(");

                    if (CommandsToExecute[i].RuntimeParameters.Length > 0)
                    {
                        for (int j = 0; j < CommandsToExecute[i].RuntimeParameters.Length; j++)
                            line.Append($"{CommandsToExecute[i].RuntimeParameters[j].Value}, ");

                        line.Remove(line.Length - 2, 2);
                    }
                    line.Append("); ");

                    result.Add(line.ToString());
                    line.Clear();
                }
            }

            return result.ToArray();
        }
        public string ActionsToString()
        {
            return Utility.ActionsToString(CommandsToExecute);
        }
        public string CostsToString()
        {
            return Utility.CreateCostsString(CooldownInSeconds, PriceTag);
        }
        public string RealCostsToString()
        {
            return Utility.CreateCostsString(RealCooldownInSeconds, RealPriceTag);
        }
        public string TargetChannelToString()
        {
            return $"[{(TextChannelContextId != 0 ? Task.Run(() => Launcher.Instance.GetChannel<IChannel>(TextChannelContextId)).GetAwaiter().GetResult()?.Name : "Default")}]";
        }

        public ChannelMessage ToMessage(ChannelMessage.MessageTemplateOption template = ChannelMessage.MessageTemplateOption.Information)
        {
            string header = Name;
            header = $"{header} | {RealCostsToString()}";

            string channelTarget = TargetChannelToString();
            string descr = $"{ReplyDictionary.CATEGORY}: {(Category != null ? Category.Name : "-")}";

            if (!string.IsNullOrWhiteSpace(channelTarget))
                descr = $"{descr} | {channelTarget}";

            string footer = $"{CostsToString()} | {ReplyDictionary.CATEGORY}:";
            footer = $"{footer} {(Category == null ? "-" : Utility.CreateCostsString(Category.CategoryCooldownInSeconds, Category.PriceTag))}";


            var msg = new ChannelMessage(null)
                .SetTemplate(template)
                .AddContent(
                    new ChannelMessageContent()
                    .SetTitle(header)
                    .SetDescription(descr))
                .AddContent(ActionsToMessageContent())
                .SetFooter(footer);


            return msg;
        }
        public ChannelMessageContent ActionsToMessageContent(ChannelMessageContent.DescriptionStyleOption style = ChannelMessageContent.DescriptionStyleOption.CodeBlock)
        {
            return new ChannelMessageContent()
                    .SetTitle($"{ReplyDictionary.ACTIONS}:")
                    .SetDescription(Utility.ActionsToString(CommandsToExecute), (int)style);
        }

        public Task ExecuteCallback(ICommandContext arg1, object[] arg2, IServiceProvider arg3, CommandInfo arg4)
        {
            string[] parameters = null;
            if (arg1 != null)
            {
                parameters = [arg1.User?.Username];
            }

            return Execute(arg1, parameters);
        }

        public async Task<CustomRuntimeResult> TestCommandExecution(CommandService commandService, IServiceProvider services)
        {
            if (CommandsToExecute == null || CommandsToExecute.Length == 0)
                return CustomRuntimeResult.FromError($"......No commands to execute!");

            var builder = new StringBuilder();
            int startLength = builder.Length;

            for (int i = 0; i < CommandsToExecute.Length; i++)
            {
                bool issueOccured = false;
                var subBuilder = new StringBuilder($"......[{i}] | ");

                if (CommandsToExecute[i] != null)
                {
                    var testResult = await CommandsToExecute[i].TestCommandExecution(commandService, services, Server.CultureInfo);

                    if (!testResult.IsSuccess)
                    {
                        issueOccured = true;
                        subBuilder.Append(testResult.Reason);
                    }
                }
                else
                {
                    issueOccured = true;
                    subBuilder.Append("NULL");
                }

                if (issueOccured)
                    builder.AppendLine(subBuilder.ToString());
            }

            if (builder.Length > startLength)
                return CustomRuntimeResult.FromError(builder.ToString());
            else
                return CustomRuntimeResult.FromSuccess();
        }

        public int CompareTo(CustomCommand c2)
        {
            if (Category == null && c2.Category == null)
                return string.Compare(Name, c2.Name, Server.CultureInfo, CompareOptions.Ordinal);

            if (Category == null && c2.Category != null)
                return -1;

            if (Category != null && c2.Category == null)
                return 1;

            return string.Compare(Category.Name, c2.Category.Name, Server.CultureInfo, CompareOptions.Ordinal);
        }
    }

    public class CommandBundleEntry
    {
        public int Position;
        public ChannelMessage[] Bundle;
        
        private bool _isDone;
        public bool IsDone => _isDone;


        public CommandBundleEntry(int position, ChannelMessage[] bundle)
        {
            Position = position;
            Bundle = bundle;
        }


        public void SetMessage(ChannelMessage message)
        {
            Bundle[Position] = message;
        }

        public void SetCompleted()
        {
            _isDone = true;
        }
    }
}
