using System;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.TwitchIntegration;
using GeistDesWaldes.TwitchIntegration.IntervalActions;
using GeistDesWaldes.UserCommands;

namespace GeistDesWaldes.Modules;

[RequireUserPermission(GuildPermission.Administrator, Group = "AdminPermissions")]
[RequireUserPermission(GuildPermission.ManageChannels, Group = "AdminPermissions")]
[RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "AdminPermissions")]
[Group("admin")]
public class AdminModule : ModuleBase<CommandContext>, ICommandModule
{
    public Server Server { get; set; }

    [Command("Restart")]
    [Summary("Restarts the server.")]
    public async Task<RuntimeResult> RestartServer()
    {
        await Launcher.Instance.RestartServer(Server);
        return CustomRuntimeResult.FromSuccess();
    }


    [Group("general")]
    public class AdminModuleGeneral : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Command("SetCommandPrefix")]
        [Summary("Sets the command prefix to the specified character.")]
        public async Task<RuntimeResult> SetCommandPrefix(char prefix)
        {
            try
            {
                if (char.IsWhiteSpace(prefix))
                {
                    throw new ArgumentNullException(nameof(prefix), "Prefix can not be space!");
                }

                if (prefix == Server.Config.GeneralSettings.PollVotePrefix)
                {
                    throw new Exception("Command Prefix and Poll Vote Prefix cannot be the same character!");
                }

                char oldValue = Server.Config.GeneralSettings.CommandPrefix;

                Server.Config.GeneralSettings.CommandPrefix = prefix;
                await ConfigurationHandler.SaveConfigToFile(Server.Config);

                ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(GeneralSettingsEntry.CommandPrefix), $"{oldValue}", $"{prefix}");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("GetCommandPrefix")]
        [Summary("Gets the currently set command prefix.")]
        public async Task<RuntimeResult> GetCommandPrefix()
        {
            try
            {
                ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(GeneralSettingsEntry.CommandPrefix), $"{Server.Config.GeneralSettings.CommandPrefix}");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [Command("SetPollPrefix")]
        [Summary("Sets the poll vote prefix to the specified character.")]
        public async Task<RuntimeResult> SetPollVotePrefix(char prefix)
        {
            try
            {
                if (char.IsWhiteSpace(prefix))
                {
                    throw new ArgumentNullException(nameof(prefix), "Prefix can not be space!");
                }

                if (prefix == Server.Config.GeneralSettings.CommandPrefix)
                {
                    throw new Exception("Poll Vote Prefix and Command Prefix cannot be the same character!");
                }

                char oldValue = Server.Config.GeneralSettings.PollVotePrefix;

                Server.Config.GeneralSettings.PollVotePrefix = prefix;
                await ConfigurationHandler.SaveConfigToFile(Server.Config);

                ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(GeneralSettingsEntry.PollVotePrefix), $"{oldValue}", $"{prefix}");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("GetPollPrefix")]
        [Summary("Gets the currently set poll vote prefix.")]
        public async Task<RuntimeResult> GetPollVotePrefix()
        {
            try
            {
                ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(GeneralSettingsEntry.PollVotePrefix), $"{Server.Config.GeneralSettings.PollVotePrefix}");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [Command("SetCommandCooldown")]
        [Summary("Sets command cooldown for users in seconds.")]
        public async Task<RuntimeResult> SetCommandCooldown(double seconds)
        {
            try
            {
                if (seconds < .5)
                {
                    seconds = .5;
                }

                double oldValue = Server.Config.UserSettings.UserCooldownInSeconds;

                Server.Config.UserSettings.UserCooldownInSeconds = seconds;
                await ConfigurationHandler.SaveConfigToFile(Server.Config);

                ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(UserSettingsEntry.UserCooldownInSeconds), $"{oldValue}s", $"{seconds}s");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("GetCommandCooldown")]
        [Summary("Gets the currently set command cooldown.")]
        public async Task<RuntimeResult> GetCommandCooldown()
        {
            try
            {
                ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(UserSettingsEntry.UserCooldownInSeconds), $"{Server.Config.UserSettings.UserCooldownInSeconds}s");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
    }


    [Group("discord")]
    public class AdminModuleDiscord : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }


        [Command("SetDefaultTextChannel")]
        [Summary("Sets the bots default discord text channel.")]
        public async Task<RuntimeResult> SetDefaultTextChannel(ITextChannel textChannel)
        {
            try
            {
                if (textChannel == null)
                {
                    throw new ArgumentNullException(nameof(textChannel));
                }

                ITextChannel oldValue = await Launcher.Instance.GetChannel<ITextChannel>(Server.Config.DiscordSettings.DefaultBotTextChannel);

                Server.Config.DiscordSettings.DefaultBotTextChannel = textChannel.Id;
                await ConfigurationHandler.SaveConfigToFile(Server.Config);

                ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(DiscordSettingsEntry.DefaultBotTextChannel), oldValue != null ? oldValue.Name : "null", textChannel.Name);
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("GetDefaultTextChannel")]
        [Summary("Gets the bots currently set default discord text channel.")]
        public async Task<RuntimeResult> GetDefaultTextChannel()
        {
            try
            {
                ITextChannel oldValue = await Launcher.Instance.GetChannel<ITextChannel>(Server.Config.DiscordSettings.DefaultBotTextChannel);
                ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(DiscordSettingsEntry.DefaultBotTextChannel), oldValue != null ? oldValue.Name : "null");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [Command("SetDefaultVoiceChannel")]
        [Summary("Sets the bots default discord voice channel.")]
        public async Task<RuntimeResult> SetDefaultVoiceChannel(IVoiceChannel voiceChannel)
        {
            try
            {
                if (voiceChannel == null)
                {
                    throw new ArgumentNullException(nameof(voiceChannel));
                }

                IVoiceChannel oldValue = await Launcher.Instance.GetChannel<IVoiceChannel>(Server.Config.DiscordSettings.DefaultBotVoiceChannel);

                Server.Config.DiscordSettings.DefaultBotVoiceChannel = voiceChannel.Id;
                await ConfigurationHandler.SaveConfigToFile(Server.Config);

                ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(DiscordSettingsEntry.DefaultBotVoiceChannel), oldValue != null ? oldValue.Name : "null", voiceChannel.Name);
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("GetDefaultVoiceChannel")]
        [Summary("Gets the bots currently set default discord voice channel.")]
        public async Task<RuntimeResult> GetDefaultVoiceChannel()
        {
            try
            {
                IVoiceChannel oldValue = await Launcher.Instance.GetChannel<IVoiceChannel>(Server.Config.DiscordSettings.DefaultBotVoiceChannel);
                ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(DiscordSettingsEntry.DefaultBotVoiceChannel), oldValue != null ? oldValue.Name : "null");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
    }


    [Group("twitch")]
    public class AdminModuleTwitch : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }


        [Command("SetTwitchChannelName")]
        [Summary("Sets the twitch channel to which the bot should join.")]
        public async Task<RuntimeResult> SetTwitchChannel(string channelName)
        {
            try
            {
                channelName = channelName.Trim().ToLower();

                string oldValue = Server.Config.TwitchSettings.TwitchChannelName;

                Server.Config.TwitchSettings.TwitchChannelName = channelName;
                await ConfigurationHandler.SaveConfigToFile(Server.Config);

                if (!string.Equals(channelName, oldValue, StringComparison.OrdinalIgnoreCase))
                {
                    TwitchIntegrationHandler.Instance.StopListening(Server);
                    await Task.Delay(3000);
                    await TwitchIntegrationHandler.Instance.StartListening(Server);
                }

                ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(TwitchSettingsEntry.TwitchChannelName), oldValue, channelName);
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("GetTwitchChannelName")]
        [Summary("Gets the currently set twitch channel to which the bot should join.")]
        public async Task<RuntimeResult> GetTwitchChannel()
        {
            try
            {
                ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(TwitchSettingsEntry.TwitchChannelName), Server.Config.TwitchSettings.TwitchChannelName);
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [Command("ResetTwitchChannelId")]
        [Summary("Generates a new Twitch Message Channel Id. (Breaks events that reference the current id!)")]
        public async Task<RuntimeResult> ResetTwitchChannelId()
        {
            try
            {
                ulong oldValue = Server.Config.TwitchSettings.TwitchMessageChannelId;
                ulong newValue = TwitchSettingsEntry.GenerateTwitchMessageChannelId();

                Server.Config.TwitchSettings.TwitchMessageChannelId = newValue;
                await ConfigurationHandler.SaveConfigToFile(Server.Config);

                ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(TwitchSettingsEntry.TwitchMessageChannelId), $"{oldValue}", $"{newValue}");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("GetTwitchChannelId")]
        [Summary("Gets the currently set Twitch Message Channel Id.")]
        public async Task<RuntimeResult> GetTwitchChannelId()
        {
            try
            {
                ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(TwitchSettingsEntry.TwitchMessageChannelId), $"{Server.Config.TwitchSettings.TwitchMessageChannelId}");
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [Group("livestream")]
        public class AdminModuleTwitchLivestream : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }


            [Command("SetLivestreamOneShotWindow")]
            [Summary("Sets time window (minutes) in which the one shot events for livestreams can be triggered.")]
            public async Task<RuntimeResult> SetLivestreamOneShotWindow(int minutes)
            {
                try
                {
                    if (minutes < 1)
                    {
                        minutes = 1;
                    }

                    int oldValue = Server.Config.TwitchSettings.LivestreamOneShotWindowInMinutes;

                    Server.Config.TwitchSettings.LivestreamOneShotWindowInMinutes = minutes;
                    await ConfigurationHandler.SaveConfigToFile(Server.Config);

                    ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(TwitchSettingsEntry.LivestreamOneShotWindowInMinutes), $"{oldValue} minutes", $"{minutes} minutes");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("GetLivestreamOneShotWindow")]
            [Summary("Gets currently set time window (minutes) in which the one shot events for livestreams can be triggered.")]
            public async Task<RuntimeResult> GetLivestreamOneShotWindow()
            {
                try
                {
                    ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(TwitchSettingsEntry.LivestreamOneShotWindowInMinutes), $"{Server.Config.TwitchSettings.LivestreamOneShotWindowInMinutes} minutes");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }


            [Command("SetActionIntervalMinMinutes")]
            [Summary("Sets minimum required minutes between livestream actions.")]
            public async Task<RuntimeResult> SetActionIntervalMinMinutes(int minutes)
            {
                try
                {
                    if (minutes < 1)
                    {
                        minutes = 1;
                    }

                    int oldValue = Server.Config.TwitchSettings.LivestreamActionIntervalMinMinutes;

                    Server.Config.TwitchSettings.LivestreamActionIntervalMinMinutes = minutes;
                    await ConfigurationHandler.SaveConfigToFile(Server.Config);

                    ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(TwitchSettingsEntry.LivestreamActionIntervalMinMinutes), $"{oldValue} minutes", $"{minutes} minutes");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("GetActionIntervalMinMinutes")]
            [Summary("Gets minimum required minutes between livestream actions.")]
            public async Task<RuntimeResult> GetActionIntervalMinMinutes()
            {
                try
                {
                    ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(TwitchSettingsEntry.LivestreamActionIntervalMinMinutes), $"{Server.Config.TwitchSettings.LivestreamActionIntervalMinMinutes} minutes");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }


            [Command("SetActionIntervalMinMessages")]
            [Summary("Sets minimum required chat messages between livestream actions.")]
            public async Task<RuntimeResult> SetActionIntervalMinMessages(int messages)
            {
                try
                {
                    if (messages < 1)
                    {
                        messages = 1;
                    }

                    int oldValue = Server.Config.TwitchSettings.LivestreamActionIntervalMinMessages;

                    Server.Config.TwitchSettings.LivestreamActionIntervalMinMessages = messages;
                    await ConfigurationHandler.SaveConfigToFile(Server.Config);

                    ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(TwitchSettingsEntry.LivestreamActionIntervalMinMessages), $"{oldValue} messages", $"{messages} messages");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("GetActionIntervalMinMessages")]
            [Summary("Gets minimum required chat messages between livestream actions.")]
            public async Task<RuntimeResult> GetActionIntervalMinMessages()
            {
                try
                {
                    ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(TwitchSettingsEntry.LivestreamActionIntervalMinMessages), $"{Server.Config.TwitchSettings.LivestreamActionIntervalMinMessages} messages");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }
        }


        [Group("points")]
        public class AdminModuleTwitchPoints : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }


            [Command("SetPointsPerMonitorInterval")]
            [Summary("Sets twitch points a viewer gets each monitor interval.")]
            public async Task<RuntimeResult> SetPointsPerMonitorInterval(int points)
            {
                try
                {
                    int oldValue = Server.Config.TwitchSettings.TwitchPointsPerMonitorInterval = points;

                    Server.Config.TwitchSettings.TwitchPointsPerMonitorInterval = points;
                    await ConfigurationHandler.SaveConfigToFile(Server.Config);

                    ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(TwitchSettingsEntry.TwitchPointsPerMonitorInterval), $"{oldValue}", $"{points}");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("GetPointsPerMonitorInterval")]
            [Summary("Gets currently set twitch points a viewer gets each monitor interval.")]
            public async Task<RuntimeResult> GetPointsPerMonitorInterval()
            {
                try
                {
                    ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(TwitchSettingsEntry.TwitchPointsPerMonitorInterval), $"{Server.Config.TwitchSettings.TwitchPointsPerMonitorInterval}");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }


            [Command("SetBonusPoints")]
            [Summary("Sets bonus points for active chatters.")]
            public async Task<RuntimeResult> SetBonusPoints(int points)
            {
                try
                {
                    int oldValue = Server.Config.TwitchSettings.TwitchPointsBonusForActiveChatters;

                    Server.Config.TwitchSettings.TwitchPointsBonusForActiveChatters = points;
                    await ConfigurationHandler.SaveConfigToFile(Server.Config);

                    ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(TwitchSettingsEntry.TwitchPointsBonusForActiveChatters), $"{oldValue}", $"{points}");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("GetBonusPoints")]
            [Summary("Gets currently set bonus points for active chatters.")]
            public async Task<RuntimeResult> GetBonusPoints()
            {
                try
                {
                    ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(TwitchSettingsEntry.TwitchPointsBonusForActiveChatters), $"{Server.Config.TwitchSettings.TwitchPointsBonusForActiveChatters}");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }


            [Command("SetActiveChatterWindow")]
            [Summary("Sets time window (minutes) in which user has to chat in order to be active.")]
            public async Task<RuntimeResult> SetActiveChatterWindow(int minutes)
            {
                try
                {
                    int oldValue = Server.Config.TwitchSettings.ActiveChatterWindowInMinutes;

                    Server.Config.TwitchSettings.ActiveChatterWindowInMinutes = minutes;
                    await ConfigurationHandler.SaveConfigToFile(Server.Config);

                    ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(TwitchSettingsEntry.ActiveChatterWindowInMinutes), $"{oldValue} minutes", $"{minutes} minutes");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("GetActiveChatterWindow")]
            [Summary("Gets currently set time window (minutes) in which user has to chat in order to be active.")]
            public async Task<RuntimeResult> GetActiveChatterWindow()
            {
                try
                {
                    ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(TwitchSettingsEntry.ActiveChatterWindowInMinutes), $"{Server.Config.TwitchSettings.ActiveChatterWindowInMinutes} minutes");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }
        }


        [Group("alerts")]
        public class AdminModuleTwitchAlerts : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }


            [Command("SetFollowAlertCooldown")]
            [Summary("Sets cooldown for the follow alert in minutes.")]
            public async Task<RuntimeResult> SetFollowAlertCooldown(int minutes)
            {
                try
                {
                    int oldValue = Server.Config.TwitchSettings.TwitchFollowAlertCooldownInMinutes;

                    Server.Config.TwitchSettings.TwitchFollowAlertCooldownInMinutes = minutes;
                    await ConfigurationHandler.SaveConfigToFile(Server.Config);

                    ChannelMessage msg = ReplyDictionary.GetValueModifiedMessage(Context, nameof(TwitchSettingsEntry.TwitchFollowAlertCooldownInMinutes), $"{oldValue} minutes", $"{minutes} minutes");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("GetFollowAlertCooldown")]
            [Summary("Gets currently set cooldown for the follow alert in minutes.")]
            public async Task<RuntimeResult> GetFollowAlertCooldown()
            {
                try
                {
                    ChannelMessage msg = ReplyDictionary.GetValueMessage(Context, nameof(TwitchSettingsEntry.TwitchFollowAlertCooldownInMinutes), $"{Server.Config.TwitchSettings.TwitchFollowAlertCooldownInMinutes} minutes");
                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }
        }


        [Group("actions")]
        public class AdminModuleTwitchActions : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }


            [Command("AddIntervalAction")]
            [Summary("Adds livestream interval action.")]
            public async Task<RuntimeResult> AddIntervalAction(string name, string[] commandsToExecute, IChannel channel = null)
            {
                channel ??= await Launcher.Instance.GetChannel<IChannel>(Server.Config.TwitchSettings.TwitchMessageChannelId);

                RuntimeResult addResult = await Server.GetModule<TwitchLivestreamIntervalActionHandler>().TryAddAction(Context, name, commandsToExecute, channel);

                if (addResult.IsSuccess)
                {
                    string body = ReplyDictionary.INTERVAL_ACTION_X_CREATED.ReplaceStringInvariantCase("{x}", name);

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

            [Command("RemoveIntervalAction")]
            [Summary("Removes livestream interval action.")]
            public async Task<RuntimeResult> RemoveIntervalAction(string name)
            {
                name = name.ToLower();
                RuntimeResult result = await Server.GetModule<TwitchLivestreamIntervalActionHandler>().TryRemoveAction(name);

                if (result.IsSuccess)
                {
                    string body = ReplyDictionary.INTERVAL_ACTION_X_REMOVED.ReplaceStringInvariantCase("{x}", name);

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

            [Command("GetIntervalAction")]
            [Summary("Gets livestream interval action.")]
            public async Task<RuntimeResult> GetIntervalAction(string name = null)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    CustomCommand[] result = Server.GetModule<TwitchLivestreamIntervalActionHandler>().GetAllActions();

                    string body = "-";

                    if (result != null && result.Length > 0)
                    {
                        StringBuilder strBuilder = new();

                        foreach (CustomCommand command in result)
                        {
                            strBuilder.AppendLine(command.Name);
                        }

                        body = strBuilder.ToString();
                    }

                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(null, EmojiDictionary.INFO)
                                                     .SetDescription(body)
                                         );

                    await msg.SendAsync();
                    return CustomRuntimeResult.FromSuccess();
                }
                else
                {
                    CustomRuntimeResult<CustomCommand> result = Server.GetModule<TwitchLivestreamIntervalActionHandler>().TryGetAction(name);

                    if (result.IsSuccess)
                    {
                        ChannelMessage msg = new ChannelMessage(Context)
                                             .SetTemplate(ChannelMessage.MessageTemplateOption.Information)
                                             .AddContent(new ChannelMessageContent().SetTitle(result.ResultValue.Name))
                                             .AddContent(result.ResultValue.ActionsToMessageContent());

                        await msg.SendAsync();
                    }

                    return result;
                }
            }


            [Command("ShuffleActions")]
            [Summary("Randomizes action order.")]
            public async Task<RuntimeResult> ShuffleActions()
            {
                RuntimeResult result = await Server.GetModule<TwitchLivestreamIntervalActionHandler>().ShuffleActions();

                if (result.IsSuccess)
                {
                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                     .SetDescription(ReplyDictionary.INTERVAL_ACTIONS_SORTED)
                                         );

                    await msg.SendAsync();
                }

                return result;
            }

            [Command("SortActionsByName")]
            [Summary("Sorts action order by name.")]
            public async Task<RuntimeResult> SortActionsByName()
            {
                RuntimeResult result = await Server.GetModule<TwitchLivestreamIntervalActionHandler>().SortActionsByName();

                if (result.IsSuccess)
                {
                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                                     .SetDescription(ReplyDictionary.INTERVAL_ACTIONS_SORTED)
                                         );

                    await msg.SendAsync();
                }

                return result;
            }


            [Command("ForceExecuteRandomAction")]
            [Summary("Executes next action.")]
            public async Task<RuntimeResult> ForceRandomAction()
            {
                try
                {
                    Server.GetModule<TwitchLivestreamIntervalActionHandler>().GetNextAction(0, out CustomCommand command);

                    if (command != null)
                        await command.Execute(null);
                }
                catch (Exception ex)
                {
                    return CustomRuntimeResult.FromError(ex.Message);
                }
                
                return CustomRuntimeResult.FromSuccess();
            }
        }
    }
}