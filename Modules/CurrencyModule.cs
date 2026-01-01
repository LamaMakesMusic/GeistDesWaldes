using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Currency;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Users;
using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Modules
{

    [RequireForestUser]
    [RequireTimeJoined("0", "1", Group = "CurrencyPermission")]
    [RequireIsFollower(Group = "CurrencyPermission")]
    [RequireIsBot(Group = "CurrencyPermission")]
    [Group("point")]
    [Alias("points")]
    public class CurrencyModule : ModuleBase<CommandContext>, IServerModule
    {
        public Server _Server { get; set; }

        [Priority(-1)]
        [Command]
        [Summary("Prints the amount of currency a user has.")]
        public async Task<RuntimeResult> GetPointsAsync()
        {
            try
            {
                var getResult = await _Server.CurrencyHandler.GetPointsAsync(Context.User);

                if (getResult.IsSuccess)
                {
                    string body = _Server.CurrencyHandler.CustomizationData.PointsToStringMessage;
                    body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{x}", Context.User.Username);
                    body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", getResult.ResultValue.ToString());


                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Points)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(Context.User.Username)
                                .SetDescription(body)
                            );

                    await msg.SendAsync();
                }

                return getResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("transfer")]
        [Alias("give")]
        [Summary("Transfers an amount of currency to another user.")]
        public async Task<RuntimeResult> TransferPointsAsync(int amount, IUser targetUser)
        {
            try
            {
                if (targetUser == null)
                    return CustomRuntimeResult.FromError(ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY);

                if (amount < 1)
                    return CustomRuntimeResult.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.PARAMETER_MUST_BE_GREATER_X, "{x}", "0"));


                var getUserResult = await _Server.ForestUserHandler.GetUser(Context.User);
                if (getUserResult.IsSuccess)
                {
                    ForestUser sender = getUserResult.ResultValue;

                    getUserResult = await _Server.ForestUserHandler.GetUser(targetUser);
                    if (getUserResult.IsSuccess)
                    {
                        ForestUser receiver = getUserResult.ResultValue;

                        var transferResult = await _Server.CurrencyHandler.TransferCurrencyBetweenUsers(sender, receiver, amount);
                        if (transferResult.IsSuccess)
                        {
                            string body = _Server.CurrencyHandler.CustomizationData.TransferedPointsMessage;
                            body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{x}", Context.User.Username);
                            body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", amount.ToString());
                            body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{z}", targetUser.Username);


                            ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Points)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                            await msg.SendAsync();
                        }

                        return transferResult;
                    }
                }

                return getUserResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        //[Command("score")]
        //[Summary("Shows currency user ranking.")]
        //public async Task<RuntimeResult> GetLeaderboardAsync([Summary("Show user rank")]IUser user = null)
        //{
        //    try
        //    {
        //        var points = TwitchPointsHandler.TwitchPointsSave.Points;
        //        int firstEntry = 0;
        //        int lastEntry = 10;


        //        if (user != null && user is TwitchUser twitchUser)
        //        {
        //            var userEntry = points.Find(p => p.UserId == twitchUser.TwitchId);
        //            if (userEntry != default)
        //            {
        //                firstEntry = points.IndexOf(userEntry);

        //                // Try to show 5 entries ahead of the users and 5 entries behind
        //                lastEntry = firstEntry + 5;
        //                firstEntry -= 5;
        //            }
        //        }

        //        // Clamp indices
        //        if (firstEntry < 0)
        //        {
        //            lastEntry += (firstEntry * -1);
        //            firstEntry = 0;
        //        }
        //        if (lastEntry >= points.Count)
        //            lastEntry = points.Count - 1;


        //        string header = InterfaceHandler.GetFormattedHeader(ReplyDictionary.LEADERBOARD, EmojiDictionary.TROPHY);
        //        var body = new StringBuilder();

        //        for (int i = firstEntry; i <= lastEntry; i++)
        //        {
        //            if (i < 9)
        //            {
        //                if (i > 2)
        //                    body.Append($"0{i + 1}. | ");
        //                else if (i == 0)
        //                    body.Append($"{EmojiDictionary.GetEmoji(EmojiDictionary.FIRST_PLACE_MEDAL)} | ");
        //                else if (i == 1)
        //                    body.Append($"{EmojiDictionary.GetEmoji(EmojiDictionary.SECOND_PLACE_MEDAL)} | ");
        //                else if (i == 2)
        //                    body.Append($"{EmojiDictionary.GetEmoji(EmojiDictionary.THIRD_PLACE_MEDAL)} | ");
        //            }
        //            else
        //                body.Append($"{i + 1}.  | ");

        //            body.Append($"  {points[i].Amount} - { (string.IsNullOrWhiteSpace(points[i].Username) ? "???" : points[i].Username) }\n");
        //        }

        //        await InterfaceHandler.SayAndLog(header, body.ToString(), Context);

        //        return CustomRuntimeResult.FromSuccess();
        //    }
        //    catch (Exception e)
        //    {
        //        return CustomRuntimeResult.FromError(e.ToString());
        //    }
        //}

        [RequireUserPermission(GuildPermission.Administrator, Group = "CurrencyAdminPermission")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "CurrencyAdminPermission")]
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "CurrencyAdminPermission")]
        public class TwitchPointsAdminModule : ModuleBase<CommandContext>, IServerModule
        {
            public Server _Server { get; set; }

            [Command("add")]
            [Summary("Adds currency to a User.")]
            public async Task<RuntimeResult> AddTwitchPoints(int amount, IUser targetUser = null)
            {
                try
                {
                    if (targetUser == null)
                        targetUser = Context.User;

                    amount = Math.Abs(amount);

                    var result = await _Server.CurrencyHandler.AddCurrencyToUser(targetUser, amount);
                    if (result.IsSuccess)
                    {
                        string body = ReplyDictionary.X_RECEIVED_Y_POINTS;
                        body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{x}", targetUser.Username);
                        body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", amount.ToString());


                        ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Points)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(targetUser.Username, EmojiDictionary.FLOPPY_DISC)
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

            [Command("remove")]
            [Summary("Removes currency from a User.")]
            public async Task<RuntimeResult> RemoveTwitchPoints(int amount, IUser targetUser = null)
            {
                try
                {
                    if (targetUser == null)
                        targetUser = Context.User;

                    amount = Math.Abs(amount);

                    var result = await _Server.CurrencyHandler.AddCurrencyToUser(targetUser, -1 * amount);

                    if (result.IsSuccess)
                    {
                        string body = ReplyDictionary.REMOVED_Y_POINTS_FROM_X;
                        body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{x}", targetUser.Username);
                        body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", amount.ToString());


                        ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Points)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(targetUser.Username, EmojiDictionary.FLOPPY_DISC)
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

            [Command("get")]
            [Summary("Prints the amount of currency a specified user has.")]
            public async Task<RuntimeResult> GetPointsAsync(IUser user)
            {
                try
                {
                    var getResult = await _Server.CurrencyHandler.GetPointsAsync(user);

                    if (getResult.IsSuccess)
                    {
                        string body = _Server.CurrencyHandler.CustomizationData.PointsToStringMessage;
                        body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{x}", user.Username);
                        body = await ReplyDictionary.ReplaceStringInvariantCase(body, "{y}", getResult.ResultValue.ToString());


                        ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Points)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(user.Username, EmojiDictionary.INFO)
                            .SetDescription(body)
                        );

                        await msg.SendAsync();
                    }

                    return getResult;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("SetMessage")]
            [Summary("Sets 'ToString' - Message.")]
            public async Task<RuntimeResult> SetToStringMessage(CurrencyCustomization.ToStringType type, string toStringMessage)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(toStringMessage))
                        return await ResetToStringMessage(type);

                    _Server.CurrencyHandler.CustomizationData.SetToStringMessage(toStringMessage, type);

                    ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Points)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(_Server.CurrencyHandler.CustomizationData.GetToStringMessage(type))
                        );

                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("GetMessage")]
            [Summary("Returns 'ToString' - Message.")]
            public async Task<RuntimeResult> GetToStringMessage(CurrencyCustomization.ToStringType type)
            {
                try
                {
                    ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Points)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle($"{ReplyDictionary.CATEGORY}: '{ReplyDictionary.GetOutputTextForEnum(type)}'", EmojiDictionary.INFO)
                            .SetDescription($"'{_Server.CurrencyHandler.CustomizationData.GetToStringMessage(type)}'")
                        );

                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("ClearMessage")]
            [Summary("Resets 'ToString' - Message.")]
            public async Task<RuntimeResult> ResetToStringMessage(CurrencyCustomization.ToStringType type)
            {
                try
                {
                    _Server.CurrencyHandler.CustomizationData.ResetToStringMessage(type);

                    ChannelMessage msg = new ChannelMessage(Context)
                        .SetTemplate(ChannelMessage.MessageTemplateOption.Points)
                        .AddContent(new ChannelMessageContent()
                            .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                            .SetDescription(_Server.CurrencyHandler.CustomizationData.GetToStringMessage(type))
                        );

                    await msg.SendAsync();

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
