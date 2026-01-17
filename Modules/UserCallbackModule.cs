using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.CommandMeta;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.UserCommands;
using static GeistDesWaldes.UserCommands.UserCallbackDictionary;

namespace GeistDesWaldes.Modules;

[RequireUserPermission(GuildPermission.Administrator, Group = "CallbackPermissions")]
[RequireUserPermission(GuildPermission.ManageChannels, Group = "CallbackPermissions")]
[RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "CallbackPermissions")]
[RequireIsBot(Group = "CallbackPermissions")]
[Group("callback")]
[Alias("callbacks")]
public class UserCallbackModule : ModuleBase<CommandContext>, ICommandModule
{
    public Server Server { get; set; }


    [Group("discord")]
    public class CallbackDiscordSubModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Command("set")]
        [Summary("Sets new callback action, overwriting the current action!")]
        public async Task<RuntimeResult> RegisterCallbackAction(DiscordCallbackTypes callbackType, string[] commands, IChannel channel = null)
        {
            CustomRuntimeResult<CommandMetaInfo[]> parseResult = await Server.GetModule<CommandInfoHandler>().ParseToSerializableCommandInfo(commands, Context);
            if (parseResult.IsSuccess)
            {
                CustomRuntimeResult addResult = await Server.GetModule<UserCallbackHandler>().SetCallback(callbackType, new CustomCommand(Server, callbackType.ToString(), parseResult.ResultValue, channel?.Id ?? 0));

                if (addResult.IsSuccess)
                {
                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CALLBACK_X_CREATED, "{x}", callbackType.ToString());

                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                     .SetDescription(body)
                                         );

                    await msg.SendAsync();
                }

                return addResult;
            }

            return parseResult;
        }

        [Command("SetChannel")]
        [Summary("Changes channel of existing callback.")]
        public async Task<RuntimeResult> SetCallbackChannel(DiscordCallbackTypes callbackType, IChannel channel)
        {
            CustomRuntimeResult<CustomCommand> result = await Server.GetModule<UserCallbackHandler>().GetCallbackCommand(callbackType);

            if (result.IsSuccess)
            {
                result.ResultValue.TextChannelContextId = channel?.Id ?? default;

                await Server.GetModule<UserCallbackHandler>().SaveUserCallbacksToFile();

                string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.UPDATED_CALLBACK_X, "{x}", callbackType.ToString());

                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                 .SetDescription(body)
                                     );

                await msg.SendAsync();
            }

            return result;
        }

        [Command("SetEmbed")]
        [Summary("Should callback messages be embedded?")]
        public async Task<RuntimeResult> SetCallbackEmbed(DiscordCallbackTypes callbackType, bool embed)
        {
            CustomRuntimeResult<CustomCommand> result = await Server.GetModule<UserCallbackHandler>().GetCallbackCommand(callbackType);

            if (result.IsSuccess)
            {
                result.ResultValue.Embed = embed;

                await Server.GetModule<UserCallbackHandler>().SaveUserCallbacksToFile();

                string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.UPDATED_CALLBACK_X, "{x}", callbackType.ToString());

                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                 .SetDescription(body)
                                     );

                await msg.SendAsync();
            }

            return result;
        }

        [Command("clear")]
        [Summary("Clears set callback action!")]
        public async Task<RuntimeResult> ClearCallbackAction(DiscordCallbackTypes callbackType)
        {
            CustomRuntimeResult result = await Server.GetModule<UserCallbackHandler>().SetCallback(callbackType, null);

            if (result.IsSuccess)
            {
                string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CALLBACK_X_CLEARED, "{X}", callbackType.ToString());

                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                 .SetDescription(body)
                                     );

                await msg.SendAsync();
            }

            return result;
        }

        [Command("get")]
        [Summary("Shows currently set callback action.")]
        public async Task<RuntimeResult> GetCallbackAction(DiscordCallbackTypes callbackType)
        {
            CustomRuntimeResult<CustomCommand> result = await Server.GetModule<UserCallbackHandler>().GetCallbackCommand(callbackType);

            if (result.IsSuccess)
            {
                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle($"{ReplyDictionary.GetOutputTextForEnum(callbackType)}", EmojiDictionary.INFO)
                                                 .SetDescription("Callback"))
                                     .AddContent(result.ResultValue.ActionsToMessageContent())
                                     .SetFooter(result.ResultValue.TargetChannelToString());

                await msg.SendAsync();
            }

            return result;
        }

        [Command("test")]
        [Summary("Tests execution of the given callback.")]
        public async Task<RuntimeResult> TestCallbackAction(DiscordCallbackTypes callbackType, string[] additionalParameters = null)
        {
            CustomRuntimeResult<CustomCommand> result = await Server.GetModule<UserCallbackHandler>().GetCallbackCommand(callbackType);

            if (result.IsSuccess && result.ResultValue is { } cc)
            {
                ulong origChannel = cc.TextChannelContextId;

                try
                {
                    cc.TextChannelContextId = Context.Channel.Id;
                    await cc.Execute(Context, additionalParameters);
                }
                catch (Exception e)
                {
                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(TestCallbackAction), "", e));
                }
                finally
                {
                    cc.TextChannelContextId = origChannel;
                }
            }

            return result;
        }
    }

    [Group("twitch")]
    public class CallbackTwitchSubModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Command("set")]
        [Summary("Sets new callback action, overwriting the current action!")]
        public async Task<RuntimeResult> RegisterCallbackAction(TwitchCallbackTypes callbackType, string[] commands, IChannel channel)
        {
            CustomRuntimeResult<CommandMetaInfo[]> parseResult = await Server.GetModule<CommandInfoHandler>().ParseToSerializableCommandInfo(commands, Context);

            if (parseResult.IsSuccess)
            {
                CustomRuntimeResult addResult = await Server.GetModule<UserCallbackHandler>().SetCallback(callbackType, new CustomCommand(Server, callbackType.ToString(), parseResult.ResultValue, channel?.Id ?? 0));

                if (addResult.IsSuccess)
                {
                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CALLBACK_X_CREATED, "{X}", callbackType.ToString());


                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                     .SetDescription(body)
                                         );

                    await msg.SendAsync();
                }

                return addResult;
            }

            return parseResult;
        }

        [Command("SetChannel")]
        [Summary("Changes channel of existing callback.")]
        public async Task<RuntimeResult> SetCallbackChannel(TwitchCallbackTypes callbackType, IChannel channel)
        {
            CustomRuntimeResult<CustomCommand> result = await Server.GetModule<UserCallbackHandler>().GetCallbackCommand(callbackType);

            if (result.IsSuccess)
            {
                result.ResultValue.TextChannelContextId = channel?.Id ?? default;

                await Server.GetModule<UserCallbackHandler>().SaveUserCallbacksToFile();

                string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.UPDATED_CALLBACK_X, "{x}", callbackType.ToString());

                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                 .SetDescription(body)
                                     );

                await msg.SendAsync();
            }

            return result;
        }

        [Command("SetEmbed")]
        [Summary("Should callback messages be embedded?")]
        public async Task<RuntimeResult> SetCallbackEmbed(TwitchCallbackTypes callbackType, bool embed)
        {
            CustomRuntimeResult<CustomCommand> result = await Server.GetModule<UserCallbackHandler>().GetCallbackCommand(callbackType);

            if (result.IsSuccess)
            {
                result.ResultValue.Embed = embed;

                await Server.GetModule<UserCallbackHandler>().SaveUserCallbacksToFile();

                string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.UPDATED_CALLBACK_X, "{x}", callbackType.ToString());

                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                 .SetDescription(body)
                                     );

                await msg.SendAsync();
            }

            return result;
        }

        [Command("clear")]
        [Summary("Clears set callback action!")]
        public async Task<RuntimeResult> ClearCallbackAction(TwitchCallbackTypes callbackType)
        {
            CustomRuntimeResult result = await Server.GetModule<UserCallbackHandler>().SetCallback(callbackType, null);

            if (result.IsSuccess)
            {
                string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CALLBACK_X_CLEARED, "{X}", callbackType.ToString());

                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                 .SetDescription(body)
                                     );

                await msg.SendAsync();
            }

            return result;
        }

        [Command("get")]
        [Summary("Shows currently set callback action.")]
        public async Task<RuntimeResult> GetCallbackAction(TwitchCallbackTypes callbackType)
        {
            CustomRuntimeResult<CustomCommand> result = await Server.GetModule<UserCallbackHandler>().GetCallbackCommand(callbackType);

            if (result.IsSuccess)
            {
                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle($"{ReplyDictionary.GetOutputTextForEnum(callbackType)}", EmojiDictionary.INFO)
                                                 .SetDescription("Callback"))
                                     .AddContent(result.ResultValue.ActionsToMessageContent())
                                     .SetFooter(result.ResultValue.TargetChannelToString());

                await msg.SendAsync();
            }

            return result;
        }

        [Command("test")]
        [Summary("Tests execution of the given callback.")]
        public async Task<RuntimeResult> TestCallbackAction(TwitchCallbackTypes callbackType, string[] additionalParameters = null)
        {
            CustomRuntimeResult<CustomCommand> result = await Server.GetModule<UserCallbackHandler>().GetCallbackCommand(callbackType);

            if (result.IsSuccess && result.ResultValue is { } cc)
            {
                ulong origChannel = cc.TextChannelContextId;

                try
                {
                    cc.TextChannelContextId = Context.Channel.Id;
                    await cc.Execute(Context, additionalParameters);
                }
                catch (Exception e)
                {
                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(TestCallbackAction), "", e));
                }
                finally
                {
                    cc.TextChannelContextId = origChannel;
                }
            }

            return result;
        }
    }
}