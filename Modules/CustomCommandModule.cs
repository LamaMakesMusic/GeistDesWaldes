using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.CommandMeta;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.UserCommands;
using System;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.Modules
{
    [RequireTimeJoined("0", "0", "1", Group = "CustomCommandFree")]
    [RequireIsFollower(Group = "CustomCommandFree")]
    [RequireIsBot(Group = "CustomCommandFree")]
    [Group("command")]
    [Alias("commands")]
    public class CustomCommandModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Priority(-1)]
        [Command]
        [Summary("Lists existing custom commands.")]
        public async Task<RuntimeResult> ListCommands()
        {
            try
            {
                ChannelMessage msg = Server.GetModule<CommandInfoHandler>().CustomCommandHelpMessage;
                
                if (!string.IsNullOrWhiteSpace(Server.Config.GeneralSettings.CommandPageLink))
                {
                    msg = Server.GetModule<CommandInfoHandler>().CommandPageLinkMessage;
                }
                
                msg.Channel = Context.Channel;
                await msg.SendAsync();

                return CustomRuntimeResult.FromSuccess();
            }
            catch (Exception e)
            {
                return CustomRuntimeResult.FromError(e.ToString());
            }

        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "CustomCommandAdmin")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "CustomCommandAdmin")]
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "CustomCommandAdmin")]
        public class CustomCommandModPermissionSubModule : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }


            [Command("add")]
            [Summary("Creates a new command.")]
            public async Task<RuntimeResult> AddCommand(string commandName, string[] commands, [Summary("Category")] string category = "null", [Summary("Embed on execution?")] bool embed = false, [Summary("Cooldown in Seconds")] float cooldown = 0, [Summary("Price")] int fee = 0, IChannel channel = null)
            {
                commandName = commandName.ToLower();
                CustomRuntimeResult<CommandMetaInfo[]> parseResult = await Server.GetModule<CommandInfoHandler>().ParseToSerializableCommandInfo(commands, Context);

                if (parseResult.IsSuccess)
                {
                    CustomCommand newCommand = new CustomCommand(Server, commandName, parseResult.ResultValue, channel != null ? channel.Id : 0, cooldown, fee, embed);
                    if (!string.IsNullOrWhiteSpace(category) && !category.Equals("null", StringComparison.OrdinalIgnoreCase))
                        await newCommand.SetCategory(Server, (await Server.GetModule<CustomCommandHandler>().GetOrCreateCategory(category)).ResultValue);

                    CustomRuntimeResult addResult = await Server.GetModule<CustomCommandHandler>().AddCommandAsync(newCommand);
                    if (addResult.IsSuccess)
                    {
                        string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COMMAND_X_CREATED, "{x}", commandName);

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

            [Command("remove")]
            [Summary("Removes an existing command.")]
            public async Task<RuntimeResult> RemoveCommand(string commandName)
            {
                commandName = commandName.ToLower();
                CustomRuntimeResult result = await Server.GetModule<CustomCommandHandler>().RemoveCommandAsync(commandName);

                if (result.IsSuccess)
                {
                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COMMAND_X_REMOVED, "{x}", commandName);

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

            [Command("SetCooldown")]
            [Summary("Edits an existing cooldown of custom command.")]
            public async Task<RuntimeResult> SetCommandCooldown(string commandName, [Summary("New Cooldown in Seconds")] float cooldown)
                => await EditCommand(commandName, cooldown: cooldown);

            [Command("SetFee")]
            [Summary("Edits an existing cooldown of custom command.")]
            public async Task<RuntimeResult> SetCommandFee(string commandName, [Summary("New Fee")] int fee)
                => await EditCommand(commandName, fee: fee);

            [Command("SetActions")]
            [Summary("Edits actions of custom command.")]
            public async Task<RuntimeResult> SetCommandActions(string commandName, [Summary("New actions")] string[] commands = null, IChannel channel = null)
                => await EditCommand(commandName, commands: commands, channel: channel);

            [Command("SetCategory")]
            [Summary("Adds or removes category to/from custom command.")]
            public async Task<RuntimeResult> SetCommandCategory(string commandName, [Summary("Category")] string categoryName)
                => await EditCommand(commandName, category: categoryName);

            [Command("SetEmbed")]
            [Summary("Whether this command should be embedded on execution.")]
            public async Task<RuntimeResult> SetCommandEmbed(string commandName, [Summary("Use embedding?")] bool embed)
                => await EditCommand(commandName, embed: embed);

            [Command("SetShowInfo")]
            [Summary("Whether this command should be embedded on execution.")]
            public async Task<RuntimeResult> SetShowCommandInfo(string commandName, [Summary("Show Command info?")] bool showInfo)
                => await EditCommand(commandName, showInfo: showInfo);


            public async Task<RuntimeResult> EditCommand(string commandName, float? cooldown = null, int? fee = null, string[] commands = null, string category = "null", IChannel channel = null, bool? embed = null, bool? showInfo = null)
            {
                CustomRuntimeResult<CustomCommand> result = await Server.GetModule<CustomCommandHandler>().GetCommandAsync(commandName);
                if (result.IsSuccess && result.ResultValue is { } command)
                {
                    CustomRuntimeResult<CommandMetaInfo[]> parseResult;
                    if (commands == null)
                        parseResult = CustomRuntimeResult<CommandMetaInfo[]>.FromSuccess(value: null);
                    else
                        parseResult = await Server.GetModule<CommandInfoHandler>().ParseToSerializableCommandInfo(commands, Context);

                    if (parseResult.IsSuccess)
                    {
                        if (channel != null)
                            command.TextChannelContextId = channel.Id;

                        if (parseResult.ResultValue != null)
                            command.CommandsToExecute = parseResult.ResultValue;

                        if (cooldown.HasValue)
                            command.CooldownInSeconds = cooldown.Value;

                        if (fee.HasValue)
                            command.PriceTag = fee.Value;

                        if (embed.HasValue)
                            command.Embed = embed.Value;

                        if (showInfo.HasValue)
                            command.ShowCommandInfo = showInfo.Value;

                        if (!category.Equals("null", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrWhiteSpace(category))
                                await command.SetCategory(Server, null);
                            else
                                await command.SetCategory(Server, (await Server.GetModule<CustomCommandHandler>().GetOrCreateCategory(category)).ResultValue);
                        }

                        command.EnsureParameterQuotes();

                        await Server.GetModule<CustomCommandHandler>().UpdateCommandService();
                        await Server.GetModule<CustomCommandHandler>().SaveCustomCommandsToFile();

                        string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COMMAND_X_EDITED, "{x}", commandName);

                        ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Modified)
                            .AddContent(new ChannelMessageContent()
                                .SetDescription(body)
                            );

                        await msg.SendAsync();


                        return CustomRuntimeResult.FromSuccess();
                    }

                    return parseResult;
                }

                return result;
            }

        }
    }

    [RequireTimeJoined("0", "0", "1", Group = "CustomCommandFree")]
    [RequireIsFollower(Group = "CustomCommandFree")]
    [Group("category")]
    [Alias("categories")]
    public class CustomCommandCategoryModule : ModuleBase<CommandContext>, ICommandModule
    {
        public Server Server { get; set; }

        [Priority(-1)]
        [Command]
        [Summary("Lists existing categories.")]
        public async Task<RuntimeResult> ListCategories([Summary("Category Name")] string categoryName = "")
        {
            string header;
            StringBuilder body = new StringBuilder();

            CustomRuntimeResult<CustomCommandCategory> categorySearchResult = await Server.GetModule<CustomCommandHandler>().GetCategory(categoryName);
            if (categorySearchResult.IsSuccess)
            {
                header = categorySearchResult.ResultValue.Name;

                if (categorySearchResult.ResultValue.Commands.Count == 0)
                {
                    body.AppendLine("-");
                }
                else
                {
                    foreach (string command in categorySearchResult.ResultValue.Commands)
                    {
                        CustomRuntimeResult<CommandMetaInfo> cmd = await Server.GetModule<CommandInfoHandler>().GetCommandInfoAsync(command, true);

                        if (cmd.IsSuccess)
                            body.AppendLine(await Server.GetModule<CommandInfoHandler>().GetCommandInfoStringAsync(cmd.ResultValue, cmd.ResultValue.Groups.Count + 2));
                        else
                            body.AppendLine($"{command}");
                    }
                }
            }
            else
            {
                header = ReplyDictionary.CATEGORIES;

                if (Server.GetModule<CustomCommandHandler>().CustomCommands.Categories.Count == 0)
                {
                    body.AppendLine("-");
                }
                else
                {
                    foreach (CustomCommandCategory category in Server.GetModule<CustomCommandHandler>().CustomCommands.Categories)
                        body.AppendLine($"{category} | cmds:{category.Commands.Count}");
                }
            }


            ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(header, EmojiDictionary.OPEN_FOLDER)
                                .SetDescription(body.ToString()))
                            .SetFooter(categorySearchResult.ResultValue?.GetCostsString());

            await msg.SendAsync();

            return CustomRuntimeResult.FromSuccess();
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "CustomCommandAdmin")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "CustomCommandAdmin")]
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "CustomCommandAdmin")]
        public class CustomCommandCategoryModPermissionSubModule : ModuleBase<CommandContext>, ICommandModule
        {
            public Server Server { get; set; }


            [Command("add")]
            [Summary("Creates a new category.")]
            public async Task<RuntimeResult> CreateCategory([Summary("The name of the category")] string categoryName, [Summary("Cooldown in Seconds")] float cooldown = -1f, int fee = 0)
            {
                CustomRuntimeResult<CustomCommandCategory> result = await Server.GetModule<CustomCommandHandler>().CreateCategory(categoryName);

                if (result.IsSuccess)
                {
                    if (cooldown > -1f)
                        result.ResultValue.CategoryCooldownInSeconds = cooldown;

                    result.ResultValue.PriceTag = fee;

                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CATEGORY_X_CREATED, "{x}", categoryName);

                    ChannelMessage msg = new ChannelMessage(Context)
                                .SetTemplate(ChannelMessage.MessageTemplateOption.Positive)
                                .AddContent(new ChannelMessageContent()
                                    .SetTitle(null, EmojiDictionary.FLOPPY_DISC)
                                    .SetDescription(body)
                                );

                    await msg.SendAsync();


                    await Server.GetModule<CustomCommandHandler>().UpdateCommandService();
                    await Server.GetModule<CustomCommandHandler>().SaveCustomCommandsToFile();

                    return CustomRuntimeResult.FromSuccess();
                }
                else
                    return CustomRuntimeResult.FromError(result.Reason);
            }

            [Command("SetCooldown")]
            [Summary("Edits an existing category.")]
            public async Task<RuntimeResult> SetCategoryCooldown([Summary("The name of the category")] string categoryName, [Summary("New Cooldown in Seconds")] float cooldown)
            {
                CustomRuntimeResult<CustomCommandCategory> result = await Server.GetModule<CustomCommandHandler>().GetCategory(categoryName);

                if (result.IsSuccess)
                {
                    result.ResultValue.CategoryCooldownInSeconds = cooldown;

                    await Server.GetModule<CustomCommandHandler>().UpdateCommandService();
                    await Server.GetModule<CustomCommandHandler>().SaveCustomCommandsToFile();


                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CATEGORY_X_EDITED, "{x}", categoryName);
                    body = $"{body} | Cooldown: {result.ResultValue.CategoryCooldownInSeconds} => {cooldown}";


                    ChannelMessage msg = new ChannelMessage(Context)
                                .SetTemplate(ChannelMessage.MessageTemplateOption.Modified)
                                .AddContent(new ChannelMessageContent()
                                    .SetDescription(body)
                                );

                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }

                return CustomRuntimeResult.FromError(result.Reason);
            }

            [Command("SetFee")]
            [Summary("Edits an existing category.")]
            public async Task<RuntimeResult> SetCategoryFee([Summary("The name of the category")] string categoryName, [Summary("New Fee")] int fee)
            {
                CustomRuntimeResult<CustomCommandCategory> result = await Server.GetModule<CustomCommandHandler>().GetCategory(categoryName);

                if (result.IsSuccess)
                {
                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CATEGORY_X_EDITED, "{x}", categoryName);
                    body = $"{body} | Fee: {result.ResultValue.PriceTag} => {fee}";
                

                    result.ResultValue.PriceTag = fee;

                    await Server.GetModule<CustomCommandHandler>().UpdateCommandService();
                    await Server.GetModule<CustomCommandHandler>().SaveCustomCommandsToFile();


                    ChannelMessage msg = new ChannelMessage(Context)
                                .SetTemplate(ChannelMessage.MessageTemplateOption.Modified)
                                .AddContent(new ChannelMessageContent()
                                    .SetDescription(body)
                                );

                    await msg.SendAsync();

                    return CustomRuntimeResult.FromSuccess();
                }

                return CustomRuntimeResult.FromError(result.Reason);
            }

            [Command("lock")]
            [Summary("(Un-/locks a category, blocking it from execution.")]
            public async Task<RuntimeResult> LockCategory([Summary("The name of the category")] string categoryName, bool categoryLocked)
            {
                CustomRuntimeResult<CustomCommandCategory> result = await Server.GetModule<CustomCommandHandler>().GetCategory(categoryName);

                if (result.IsSuccess)
                {
                    result.ResultValue.Locked = categoryLocked;
                    await Server.GetModule<CustomCommandHandler>().SaveCustomCommandsToFile();


                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CATEGORY_X_EDITED, "{x}", categoryName);
                    body = $"{body} | Locked: {result.ResultValue.Locked} => {categoryLocked}";


                    ChannelMessage msg = new ChannelMessage(Context)
                                .SetTemplate(ChannelMessage.MessageTemplateOption.Modified)
                                .AddContent(new ChannelMessageContent()
                                    .SetDescription(body)
                                );

                    await msg.SendAsync();


                    return CustomRuntimeResult.FromSuccess();
                }

                return CustomRuntimeResult.FromError(result.Reason);
            }

            [Command("remove")]
            [Summary("Deletes an existing category.")]
            public async Task<RuntimeResult> DeleteCategory([Summary("The name of the category")] string categoryName)
            {
                CustomRuntimeResult result = await Server.GetModule<CustomCommandHandler>().DeleteCategory(categoryName);

                if (result.IsSuccess)
                {
                    await Server.GetModule<CustomCommandHandler>().UpdateCommandService();
                    await Server.GetModule<CustomCommandHandler>().SaveCustomCommandsToFile();


                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.CATEGORY_X_REMOVED, "{x}", categoryName);


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
    
        }
    }
}
