using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Decoration;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using static GeistDesWaldes.Decoration.ChannelLayoutMap;

namespace GeistDesWaldes.Modules;

[RequireUserPermission(GuildPermission.Administrator, Group = "LayoutModulePermission")]
[RequireUserPermission(GuildPermission.ManageChannels, Group = "LayoutModulePermission")]
[RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "LayoutModulePermission")]
[RequireIsBot(Group = "LayoutModulePermission")]
[Group("layout")]
[Alias("layouts")]
public class LayoutModule : ModuleBase<CommandContext>, ICommandModule
{
    public Server Server { get; set; }

    [Group("template")]
    [Alias("templates")]
    public class TemplateModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Priority(-1)]
        [Command]
        [Summary("Lists all existing layout templates.")]
        public async Task<RuntimeResult> ListTemplates()
        {
            try
            {
                StringBuilder body = new();

                if (Server.GetModule<LayoutTemplateHandler>().TemplateDictionary.Templates.Count == 0)
                {
                    body.Append("-");
                }
                else
                {
                    foreach (LayoutTemplate template in Server.GetModule<LayoutTemplateHandler>().TemplateDictionary.Templates)
                    {
                        body.AppendLine(template.TemplateName);
                    }
                }

                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(ReplyDictionary.TEMPLATES)
                                                 .SetDescription(body.ToString())
                                     );

                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("active")]
        [Summary("Gets the currently active template.")]
        public async Task<RuntimeResult> GetActiveTemplate()
        {
            try
            {
                ChannelMessage msg = new ChannelMessage(Context)
                                     .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                     .AddContent(new ChannelMessageContent()
                                                 .SetTitle(ReplyDictionary.ACTIVE_TEMPLATE)
                                                 .SetDescription(Server.GetModule<LayoutTemplateHandler>().TemplateDictionary.ActiveTemplate ?? "-")
                                     );

                await msg.SendAsync();


                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("revert")]
        [Summary("Reverts currently active template.")]
        public async Task<RuntimeResult> RevertActiveTemplate()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Server.GetModule<LayoutTemplateHandler>().TemplateDictionary.ActiveTemplate))
                {
                    string body = ReplyDictionary.TEMPLATE_X_REVERTED.ReplaceStringInvariantCase("{x}", Server.GetModule<LayoutTemplateHandler>().TemplateDictionary.ActiveTemplate);

                    CustomRuntimeResult result = await Server.GetModule<LayoutTemplateHandler>().RevertActiveTemplate();

                    if (result.IsSuccess)
                    {
                        await Server.GetModule<LayoutTemplateHandler>().SaveTemplatesToFile();

                        ChannelMessage msg = new ChannelMessage(Context)
                                             .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                             .AddContent(new ChannelMessageContent()
                                                         .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                                         .SetDescription(body)
                                             );

                        await msg.SendAsync();
                    }

                    return result;
                }

                return CustomRuntimeResult.FromError(ReplyDictionary.COULD_NOT_FIND_ACTIVE_TEMPLATE);
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [Command("create")]
        [Summary("Creates new layout template.")]
        public async Task<RuntimeResult> CreateTemplate([Summary("Name of the template")] string name, bool autofillChannels = false)
        {
            try
            {
                CustomRuntimeResult creationResult = Server.GetModule<LayoutTemplateHandler>().CreateTemplate(name);
                if (creationResult.IsSuccess)
                {
                    StringBuilder autofillResult = new();
                    if (autofillChannels)
                    {
                        if (Context.Guild != null)
                        {
                            CustomRuntimeResult<LayoutTemplate> getTemplateResult = Server.GetModule<LayoutTemplateHandler>().GetTemplate(name);
                            if (getTemplateResult.IsSuccess)
                            {
                                foreach (IGuildChannel channel in await Context.Guild.GetChannelsAsync())
                                {
                                    try
                                    {
                                        autofillResult.Append($"\n{channel.Name}: ");

                                        if (channel is ITextChannel or IVoiceChannel)
                                        {
                                            CustomRuntimeResult addResult = getTemplateResult.ResultValue.AddChannelLayout(channel.Id);

                                            if (addResult.IsSuccess)
                                            {
                                                autofillResult.Append(EmojiDictionary.GetEmoji(EmojiDictionary.CHECK_MARK));
                                            }
                                            else
                                            {
                                                autofillResult.Append($"{EmojiDictionary.GetEmoji(EmojiDictionary.CROSS_MARK)} {addResult.Reason}");
                                            }
                                        }
                                        else
                                        {
                                            autofillResult.Append($"{EmojiDictionary.GetEmoji(EmojiDictionary.CROSS_MARK)} {ReplyDictionary.CHANNEL_IS_NOT_TEXT_NOR_VOICE_CHANNEL}");
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        autofillResult.Append($"{EmojiDictionary.GetEmoji(EmojiDictionary.CROSS_MARK)} {e}");
                                    }
                                }
                            }
                            else
                            {
                                autofillResult.AppendLine(ReplyDictionary.TEMPLATE_NAMED_X_DOES_NOT_EXISTS);
                            }
                        }
                        else
                        {
                            autofillResult.AppendLine(ReplyDictionary.COULD_NOT_GET_GUILD_FROM_COMMANDCONTEXT);
                        }
                    }


                    string body = ReplyDictionary.TEMPLATE_X_CREATED.ReplaceStringInvariantCase("{x}", name);

                    if (autofillChannels)
                    {
                        body = $"{body} \nAutofill Channels: {autofillResult}";
                    }


                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                                     .SetDescription(body)
                                         );

                    await msg.SendAsync();


                    await Server.GetModule<LayoutTemplateHandler>().SaveTemplatesToFile();
                }

                return creationResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("get")]
        [Summary("Gets layout template.")]
        public async Task<RuntimeResult> GetTemplate([Summary("Name of the template")] string name)
        {
            try
            {
                CustomRuntimeResult<LayoutTemplate> getResult = Server.GetModule<LayoutTemplateHandler>().GetTemplate(name);

                if (getResult.IsSuccess)
                {
                    string body = getResult.ResultValue.DetailsToString();

                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(getResult.ResultValue.TemplateName)
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

        [Command("remove")]
        [Summary("Removes layout template.")]
        public async Task<RuntimeResult> RemoveTemplate([Summary("Name of the template")] string name)
        {
            try
            {
                CustomRuntimeResult removeResult = await Server.GetModule<LayoutTemplateHandler>().RemoveTemplate(name);

                if (removeResult.IsSuccess)
                {
                    string body = ReplyDictionary.TEMPLATE_X_REMOVED.ReplaceStringInvariantCase("{x}", name);


                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                                     .SetDescription(body)
                                         );

                    await msg.SendAsync();


                    await Server.GetModule<LayoutTemplateHandler>().SaveTemplatesToFile();
                }

                return removeResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("apply")]
        [Summary("Applies provided layout.")]
        public async Task<RuntimeResult> ApplyTemplate([Summary("Name of the template")] string name)
        {
            try
            {
                CustomRuntimeResult applyResult = await Server.GetModule<LayoutTemplateHandler>().ApplyTemplate(name);

                if (applyResult.IsSuccess)
                {
                    string body = ReplyDictionary.TEMPLATE_X_APPLIED.ReplaceStringInvariantCase("{x}", name);


                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                                     .SetDescription(body)
                                         );

                    await msg.SendAsync();


                    await Server.GetModule<LayoutTemplateHandler>().SaveTemplatesToFile();
                }

                return applyResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }
    }


    [Group("channel")]
    [Alias("channels")]
    public class ChannelModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Command("list")]
        [Summary("Lists set channels of existing layout template.")]
        public async Task<RuntimeResult> ListChannels([Summary("Name of existing template")] string name)
        {
            try
            {
                CustomRuntimeResult<LayoutTemplate> getTemplateResult = Server.GetModule<LayoutTemplateHandler>().GetTemplate(name);
                if (getTemplateResult.IsSuccess)
                {
                    StringBuilder body = new();

                    if (Server.GetModule<LayoutTemplateHandler>().TemplateDictionary.Templates.Count == 0)
                    {
                        body.Append("-");
                    }
                    else
                    {
                        foreach (ChannelLayout channel in getTemplateResult.ResultValue.ChannelLayouts)
                        {
                            body.Append(channel.DetailsToString());
                        }
                    }


                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(getTemplateResult.ResultValue.TemplateName)
                                                     .SetDescription(body.ToString())
                                         );

                    await msg.SendAsync();
                }

                return getTemplateResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("add")]
        [Summary("Adds channel layout to an existing template.")]
        public async Task<RuntimeResult> CreateChannelLayout([Summary("Name of existing template")] string name, [Summary("ChannelId Array")] string[] channelIds)
        {
            try
            {
                CustomRuntimeResult<LayoutTemplate> getTemplateResult = Server.GetModule<LayoutTemplateHandler>().GetTemplate(name);
                if (getTemplateResult.IsSuccess)
                {
                    StringBuilder body = new();

                    foreach (string channelId in channelIds)
                    {
                        if (!ulong.TryParse(channelId, out ulong parsedChannelId))
                        {
                            return CustomRuntimeResult.FromError(ReplyDictionary.COULD_NOT_PARSE_X_TO_Y.ReplaceStringInvariantCase("{x}", nameof(channelId)).ReplaceStringInvariantCase("{y}", typeof(ulong).Name));
                        }

                        CustomRuntimeResult creationResult = getTemplateResult.ResultValue.AddChannelLayout(parsedChannelId);
                        if (creationResult.IsSuccess)
                        {
                            body.AppendLine(ReplyDictionary.CHANNEL_X_ADDED_TO_TEMPLATE.ReplaceStringInvariantCase("{x}", $"{(await Launcher.Instance.GetChannel<IChannel>(parsedChannelId))?.Name} ({channelId})"));
                        }
                        else
                        {
                            body.AppendLine(creationResult.Reason);
                        }
                    }


                    await Server.GetModule<LayoutTemplateHandler>().SaveTemplatesToFile();


                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(getTemplateResult.ResultValue.TemplateName, EmojiDictionary.PENCIL)
                                                     .SetDescription(body.ToString())
                                         );

                    await msg.SendAsync();


                    return CustomRuntimeResult.FromSuccess();
                }

                return getTemplateResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }

        [Command("get")]
        [Summary("Gets channel layout of an existing template.")]
        public async Task<RuntimeResult> GetChannelLayout([Summary("Name of existing template")] string name, IChannel channel)
        {
            CustomRuntimeResult<LayoutTemplate> getResult = Server.GetModule<LayoutTemplateHandler>().GetTemplate(name);

            if (getResult.IsSuccess)
            {
                if (channel == null)
                {
                    return CustomRuntimeResult.FromError(ReplyDictionary.CHANNEL_ID_MUST_NOT_BE_EMPTY);
                }

                CustomRuntimeResult<ChannelLayout> getLayoutResult = getResult.ResultValue.GetChannelLayout(channel.Id);
                if (getLayoutResult.IsSuccess)
                {
                    string body = getLayoutResult.ResultValue.DetailsToString();

                    ChannelMessage msg = new ChannelMessage(Context)
                                         .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                         .AddContent(new ChannelMessageContent()
                                                     .SetTitle(getResult.ResultValue.TemplateName)
                                                     .SetDescription(body)
                                         );

                    await msg.SendAsync();
                }

                return getLayoutResult;
            }

            return getResult;
        }

        [Command("remove")]
        [Summary("Removes channel layout from an existing template.")]
        public async Task<RuntimeResult> RemoveChannelLayout([Summary("Name of existing template")] string name, IChannel channel)
        {
            try
            {
                CustomRuntimeResult<LayoutTemplate> getResult = Server.GetModule<LayoutTemplateHandler>().GetTemplate(name);
                if (getResult.IsSuccess)
                {
                    if (channel == null)
                    {
                        return CustomRuntimeResult.FromError(ReplyDictionary.CHANNEL_ID_MUST_NOT_BE_EMPTY);
                    }

                    CustomRuntimeResult removalResult = await getResult.ResultValue.RemoveChannelLayout(Server, channel.Id);
                    if (removalResult.IsSuccess)
                    {
                        string body = ReplyDictionary.CHANNEL_X_REMOVED_FROM_TEMPLATE.ReplaceStringInvariantCase("{x}", $"{channel.Name} ({channel.Id})");

                        await Server.GetModule<LayoutTemplateHandler>().SaveTemplatesToFile();

                        ChannelMessage msg = new ChannelMessage(Context)
                                             .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                             .AddContent(new ChannelMessageContent()
                                                         .SetTitle(getResult.ResultValue.TemplateName, EmojiDictionary.PENCIL)
                                                         .SetDescription(body)
                                             );

                        await msg.SendAsync();
                    }

                    return removalResult;
                }

                return getResult;
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }
        }


        [Group("map")]
        [Alias("maps")]
        public class MappingModule : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }

            [Command("add")]
            [Summary("Adds a map to a channel in an existing template.")]
            public async Task<RuntimeResult> AddMapToChannel([Summary("Name of existing template")] string name, [Summary("ChannelId")] string channelId, LayoutTargetOption layoutTarget, [Summary("{index|value}")] IndexValuePair[] maps)
            {
                try
                {
                    if (maps == null)
                    {
                        return CustomRuntimeResult.FromError($"{ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY} -> '{nameof(maps)}'");
                    }

                    CustomRuntimeResult<LayoutTemplate> getTemplateResult = Server.GetModule<LayoutTemplateHandler>().GetTemplate(name);
                    if (getTemplateResult.IsSuccess)
                    {
                        if (!ulong.TryParse(channelId, out ulong parsedChannelId))
                        {
                            return CustomRuntimeResult.FromError(ReplyDictionary.COULD_NOT_PARSE_X_TO_Y.ReplaceStringInvariantCase("{x}", nameof(channelId)).ReplaceStringInvariantCase("{y}", typeof(ulong).Name));
                        }

                        CustomRuntimeResult<ChannelLayout> getChannelResult = getTemplateResult.ResultValue.GetChannelLayout(parsedChannelId);
                        if (getChannelResult.IsSuccess)
                        {
                            StringBuilder issues = new();

                            foreach (IndexValuePair map in maps)
                            {
                                CustomRuntimeResult addResult = await getChannelResult.ResultValue.AddLayoutMap(Server, new ChannelLayoutMap(map.Index, map.Value, layoutTarget));

                                if (!addResult.IsSuccess)
                                {
                                    issues.AppendLine($"{map}: {addResult}");
                                }
                            }


                            string body = $"{ReplyDictionary.MAP_ADDED_TO_CHANNEL}: {(await Launcher.Instance.GetChannel<IChannel>(parsedChannelId))?.Name}";

                            if (issues.Length > 0)
                            {
                                body += $" \nIssues: {issues}";
                            }


                            await Server.GetModule<LayoutTemplateHandler>().SaveTemplatesToFile();


                            ChannelMessage msg = new ChannelMessage(Context)
                                                 .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                                 .AddContent(new ChannelMessageContent()
                                                             .SetTitle(getTemplateResult.ResultValue.TemplateName, EmojiDictionary.PENCIL)
                                                             .SetDescription(body)
                                                 );

                            await msg.SendAsync();

                            return CustomRuntimeResult.FromSuccess();
                        }

                        return getChannelResult;
                    }

                    return getTemplateResult;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("get")]
            [Summary("Gets a map of a channel in an existing template.")]
            public async Task<RuntimeResult> GetMapOfChannel([Summary("Name of existing template")] string name, [Summary("ChannelId")] string channelId, LayoutTargetOption layoutTarget)
            {
                try
                {
                    CustomRuntimeResult<LayoutTemplate> getTemplateResult = Server.GetModule<LayoutTemplateHandler>().GetTemplate(name);
                    if (getTemplateResult.IsSuccess)
                    {
                        if (!ulong.TryParse(channelId, out ulong parsedChannelId))
                        {
                            return CustomRuntimeResult.FromError(ReplyDictionary.COULD_NOT_PARSE_X_TO_Y.ReplaceStringInvariantCase("{x}", nameof(channelId)).ReplaceStringInvariantCase("{y}", typeof(ulong).Name));
                        }

                        CustomRuntimeResult<ChannelLayout> getChannelResult = getTemplateResult.ResultValue.GetChannelLayout(parsedChannelId);
                        if (getChannelResult.IsSuccess)
                        {
                            List<ChannelLayoutMap> matches = getChannelResult.ResultValue.LayoutMaps.FindAll(lm => lm.LayoutTarget == layoutTarget);

                            StringBuilder body = new();
                            if (matches.Count > 0)
                            {
                                foreach (ChannelLayoutMap match in matches)
                                {
                                    body.Append($"{match} | ");
                                }

                                body.Remove(body.Length - 2, 2);
                            }
                            else
                            {
                                body.Append("-");
                            }

                            ChannelMessage msg = new ChannelMessage(Context)
                                                 .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                                 .AddContent(new ChannelMessageContent()
                                                             .SetTitle(getTemplateResult.ResultValue.TemplateName, EmojiDictionary.PENCIL)
                                                             .SetDescription(body.ToString())
                                                 );

                            await msg.SendAsync();

                            return CustomRuntimeResult.FromSuccess();
                        }

                        return getChannelResult;
                    }

                    return getTemplateResult;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }

            [Command("remove")]
            [Summary("Removes a map from a channel in an existing template.")]
            public async Task<RuntimeResult> RemoveMapFromChannel([Summary("Name of existing template")] string name, [Summary("ChannelId")] string channelId, LayoutTargetOption layoutTarget, [Summary("{index|value}")] IndexValuePair[] maps)
            {
                try
                {
                    if (maps == null)
                    {
                        return CustomRuntimeResult.FromError($"{ReplyDictionary.PARAMETER_MUST_NOT_BE_EMPTY} -> '{nameof(maps)}'");
                    }

                    CustomRuntimeResult<LayoutTemplate> getResult = Server.GetModule<LayoutTemplateHandler>().GetTemplate(name);
                    if (getResult.IsSuccess)
                    {
                        if (!ulong.TryParse(channelId, out ulong parsedChannelId))
                        {
                            return CustomRuntimeResult.FromError(ReplyDictionary.COULD_NOT_PARSE_X_TO_Y.ReplaceStringInvariantCase("{x}", nameof(channelId)).ReplaceStringInvariantCase("{y}", nameof(UInt64)));
                        }

                        CustomRuntimeResult<ChannelLayout> getChannelResult = getResult.ResultValue.GetChannelLayout(parsedChannelId);
                        if (getChannelResult.IsSuccess)
                        {
                            StringBuilder issues = new();

                            foreach (IndexValuePair map in maps)
                            {
                                CustomRuntimeResult removalResult = await getChannelResult.ResultValue.RemoveLayoutMap(Server, new ChannelLayoutMap(map.Index, map.Value, layoutTarget));

                                if (!removalResult.IsSuccess)
                                {
                                    issues.AppendLine($"{map}: {removalResult}");
                                }
                            }


                            string body = $"{ReplyDictionary.MAP_REMOVED_FROM_CHANNEL}: {(await Launcher.Instance.GetChannel<IChannel>(parsedChannelId))?.Name}";

                            if (issues.Length > 0)
                            {
                                body += $" \nIssues: {issues}";
                            }


                            await Server.GetModule<LayoutTemplateHandler>().SaveTemplatesToFile();


                            ChannelMessage msg = new ChannelMessage(Context)
                                                 .SetTemplate(ChannelMessage.MessageTemplateOption.Templates)
                                                 .AddContent(new ChannelMessageContent()
                                                             .SetTitle(getResult.ResultValue.TemplateName, EmojiDictionary.PENCIL)
                                                             .SetDescription(body)
                                                 );

                            await msg.SendAsync();

                            return CustomRuntimeResult.FromSuccess();
                        }

                        return getChannelResult;
                    }

                    return getResult;
                }
                catch (Exception e)
                {
                    return CustomRuntimeResult.FromError(e.ToString());
                }
            }
        }
    }
}