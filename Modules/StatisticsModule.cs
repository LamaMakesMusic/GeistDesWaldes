using Discord.Commands;
using GeistDesWaldes.Attributes;
using System.Threading.Tasks;
using System;
using GeistDesWaldes.Statistics;
using GeistDesWaldes.Communication;
using System.Text;
using Discord;
using GeistDesWaldes.Dictionaries;

namespace GeistDesWaldes.Modules
{
    [RequireTimeJoined("0", "1", Group = "StatisticsPermissions")]
    [RequireIsFollower(Group = "StatisticsPermissions")]
    [RequireIsBot(Group = "StatisticsPermissions")]
    [Group("statistic")]
    [Alias("statistics")]
    public class StatisticsModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Group("command")]
        [Alias("commands")]
        public class CommandStatisticsModule : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }

            [Command("get")]
            [Summary("Get statistics for called commands.")]
            public async Task<RuntimeResult> GetCommandStatistics(string name = null, bool includeInactive = true)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return await ListAllCommandStatistics(includeInactive);

                try
                {
                    CustomRuntimeResult<CommandStatistic> getStatResult = Server.GetModule<CommandStatisticsHandler>().GetStatistic(name);

                    if (!getStatResult.IsSuccess)
                        return getStatResult;

                    ChannelMessage message = new(Context);
                    message.SetTemplate(ChannelMessage.MessageTemplateOption.Statistics);
                    message.AppendContent(getStatResult.ResultValue.ToMessage());

                    await message.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            private async Task<RuntimeResult> ListAllCommandStatistics(bool includeInactive = false)
            {
                try
                {
                    ChannelMessage message = new(Context);
                    message.SetTemplate(ChannelMessage.MessageTemplateOption.Statistics);

                    StringBuilder nameListBuilder = new();

                    foreach (CommandStatistic stat in Server.GetModule<CommandStatisticsHandler>().GetStatistics(includeInactive))
                    {
                        nameListBuilder.AppendLine(stat.ToString());
                    }

                    message.AddContent(new ChannelMessageContent().SetDescription(nameListBuilder.Length == 0 ? "-" : nameListBuilder.ToString()));

                    await message.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }


            [RequireUserPermission(GuildPermission.Administrator, Group = "StatisticsAdminPermissions")]
            [RequireUserPermission(GuildPermission.ManageChannels, Group = "StatisticsAdminPermissions")]
            [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "StatisticsAdminPermissions")]
            [RequireIsBot(Group = "StatisticsAdminPermissions")]
            // [Group("admin")]
            public class StatisticsAdminModule : ModuleBase<CommandContext>, ICommandModule
            {
                public Server Server { get; set; }


                [Command("create")]
                [Summary("Adds statistic about called commands.")]
                public async Task<RuntimeResult> AddCommandStatistics(string name, DateTime start, DateTime end)
                {
                    try
                    {
                        CustomRuntimeResult<CommandStatistic> createResult = await Server.GetModule<CommandStatisticsHandler>().CreateStatistic(name, start, end);

                        if (!createResult.IsSuccess)
                            return createResult;

                        ChannelMessage message = new(Context);
                        message.SetTemplate(ChannelMessage.MessageTemplateOption.Statistics);
                        message.AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.STATISTICS_CREATED_SUCCESSFULLY, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(createResult.ResultValue.ToString()));

                        await message.SendAsync();

                        return CustomRuntimeResult.FromSuccess();
                    }
                    catch (Exception e)
                    {
                        return CustomRuntimeResult.FromError(e.ToString());
                    }
                }

                [Command("delete")]
                [Summary("Deletes statistic about called commands.")]
                public async Task<RuntimeResult> DeleteCommandStatistics(string name)
                {
                    try
                    {
                        CustomRuntimeResult<CommandStatistic> deleteResult = await Server.GetModule<CommandStatisticsHandler>().DeleteStatistic(name);

                        if (!deleteResult.IsSuccess)
                            return deleteResult;

                        ChannelMessage message = new(Context);
                        message.SetTemplate(ChannelMessage.MessageTemplateOption.Statistics);
                        message.AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.STATISTICS_REMOVED_SUCCESSFULLY, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(deleteResult.ResultValue.ToString()));
                    
                        await message.SendAsync();

                        return CustomRuntimeResult.FromSuccess();
                    }
                    catch (Exception e)
                    {
                        return CustomRuntimeResult.FromError(e.ToString());
                    }
                }


                [Command("startRecording")]
                [Summary("Starts statistic about called commands.")]
                public async Task<RuntimeResult> StartRecordingCommandStatistics(string name)
                {
                    try
                    {
                        CustomRuntimeResult<CommandStatistic> result = await Server.GetModule<CommandStatisticsHandler>().StartRecordingStatistic(name);

                        if (!result.IsSuccess)
                            return result;

                        ChannelMessage message = new(Context);
                        message.SetTemplate(ChannelMessage.MessageTemplateOption.Statistics);
                        message.AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.STATISTICS_STARTED_RECORDING, EmojiDictionary.REC_BUTTON)
                            .SetDescription(result.ResultValue.ToString()));

                        await message.SendAsync();

                        return CustomRuntimeResult.FromSuccess();
                    }
                    catch (Exception e)
                    {
                        return CustomRuntimeResult.FromError(e.ToString());
                    }
                }


                [Command("stopRecording")]
                [Summary("Stops statistic about called commands.")]
                public async Task<RuntimeResult> StopRecordingCommandStatistics(string name)
                {
                    try
                    {
                        CustomRuntimeResult<CommandStatistic> result = await Server.GetModule<CommandStatisticsHandler>().StopRecordingStatistic(name);

                        if (!result.IsSuccess)
                            return result;

                        ChannelMessage message = new(Context);
                        message.SetTemplate(ChannelMessage.MessageTemplateOption.Statistics);
                        message.AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.STATISTICS_STOPPED_RECORDING, EmojiDictionary.STOP_BUTTON)
                            .SetDescription(result.ResultValue.ToString()));

                        await message.SendAsync();

                        return CustomRuntimeResult.FromSuccess();
                    }
                    catch (Exception e)
                    {
                        return CustomRuntimeResult.FromError(e.ToString());
                    }
                }
            }
        }
    }
}
