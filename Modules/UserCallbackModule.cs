using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.UserCommands;
using System;
using System.Threading.Tasks;
using static GeistDesWaldes.UserCommands.UserCallbackDictionary;

namespace GeistDesWaldes.Modules
{
    [RequireUserPermission(GuildPermission.Administrator, Group = "CallbackPermissions")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "CallbackPermissions")]
    [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "CallbackPermissions")]
    [RequireIsBot(Group = "CallbackPermissions")]
    [Group("callback")]
    [Alias("callbacks")]
    public class UserCallbackModule : ModuleBase<CommandContext>, IServerModule
    {
        public Server _Server { get; set; }


        [Group("discord")]
        public class CallbackDiscordSubModule : ModuleBase<CommandContext>, IServerModule
        {
            public Server _Server { get; set; }

            [Command("set")]
            [Summary("Sets new callback action, overwriting the current action!")]
            public async Task<RuntimeResult> RegisterCallbackAction(DiscordCallbackTypes callbackType, string[] commands, IChannel channel = null)
            {
                var parseResult = await _Server.CommandInfoHandler.ParseToSerializableCommandInfo(commands, Context);
                if (parseResult.IsSuccess)
                {
                    CustomRuntimeResult addResult = await _Server.UserCallbackHandler.SetCallback(callbackType, new CustomCommand(_Server, callbackType.ToString(), parseResult.ResultValue, channel != null ? channel.Id : 0));

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
                var result = await _Server.UserCallbackHandler.GetCallbackCommand(callbackType);

                if (result.IsSuccess)
                {
                    result.ResultValue.TextChannelContextID = channel != null ? channel.Id : default;

                    await _Server.UserCallbackHandler.SaveUserCallbacksToFile();

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
                var result = await _Server.UserCallbackHandler.GetCallbackCommand(callbackType);

                if (result.IsSuccess)
                {
                    result.ResultValue.Embed = embed;

                    await _Server.UserCallbackHandler.SaveUserCallbacksToFile();

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
                var result = await _Server.UserCallbackHandler.SetCallback(callbackType, null);

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
                var result = await _Server.UserCallbackHandler.GetCallbackCommand(callbackType);

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
                var result = await _Server.UserCallbackHandler.GetCallbackCommand(callbackType);

                if (result.IsSuccess && result.ResultValue is CustomCommand cc && cc != null)
                {
                    ulong origChannel = cc.TextChannelContextID;

                    try
                    {
                        cc.TextChannelContextID = Context.Channel.Id;
                        await cc.Execute(Context, additionalParameters);
                    }
                    catch (Exception e)
                    {
                        await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(TestCallbackAction), "", e));
                    }
                    finally
                    {
                        cc.TextChannelContextID = origChannel;
                    }
                }

                return result;
            }
        }

        [Group("twitch")]
        public class CallbackTwitchSubModule : ModuleBase<CommandContext>, IServerModule
        {
            public Server _Server { get; set; }

            [Command("set")]
            [Summary("Sets new callback action, overwriting the current action!")]
            public async Task<RuntimeResult> RegisterCallbackAction(TwitchCallbackTypes callbackType, string[] commands, IChannel channel)
            {
                var parseResult = await _Server.CommandInfoHandler.ParseToSerializableCommandInfo(commands, Context);

                if (parseResult.IsSuccess)
                {
                    CustomRuntimeResult addResult = await _Server.UserCallbackHandler.SetCallback(callbackType, new CustomCommand(_Server, callbackType.ToString(), parseResult.ResultValue, channel != null ? channel.Id : 0));

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
                var result = await _Server.UserCallbackHandler.GetCallbackCommand(callbackType);

                if (result.IsSuccess)
                {
                    result.ResultValue.TextChannelContextID = channel != null ? channel.Id : default;

                    await _Server.UserCallbackHandler.SaveUserCallbacksToFile();

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
                var result = await _Server.UserCallbackHandler.GetCallbackCommand(callbackType);

                if (result.IsSuccess)
                {
                    result.ResultValue.Embed = embed;

                    await _Server.UserCallbackHandler.SaveUserCallbacksToFile();

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
                var result = await _Server.UserCallbackHandler.SetCallback(callbackType, null);

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
                var result = await _Server.UserCallbackHandler.GetCallbackCommand(callbackType);

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
                var result = await _Server.UserCallbackHandler.GetCallbackCommand(callbackType);

                if (result.IsSuccess && result.ResultValue is CustomCommand cc && cc != null)
                {
                    ulong origChannel = cc.TextChannelContextID;

                    try
                    {
                        cc.TextChannelContextID = Context.Channel.Id;
                        await cc.Execute(Context, additionalParameters);
                    }
                    catch (Exception e)
                    {
                        await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(TestCallbackAction), "", e));
                    }
                    finally
                    {
                        cc.TextChannelContextID = origChannel;
                    }
                }

                return result;
            }
        }

    }

}
