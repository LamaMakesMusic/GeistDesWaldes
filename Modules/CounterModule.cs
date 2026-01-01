using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Counters;
using GeistDesWaldes.Dictionaries;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GeistDesWaldes.Modules
{
    [RequireTimeJoined("0", "0", "1", Group = "CounterPermissions")]
    [RequireIsFollower(Group = "CounterPermissions")]
    [RequireIsBot(Group = "CounterPermissions")]
    [Group("counter")]
    [Alias("counters")]
    public class CounterModule : ModuleBase<CommandContext>, IServerModule
    {
        public Server _Server { get; set; }
        
        [Priority(-1)]
        [Command]
        [Summary("Lists existing counters.")]
        public async Task ListCounters()
        {
            string body = await _Server.CounterHandler.ListCounters();

            ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Counter)
                            .AddContent(new ChannelMessageContent()
                                .SetDescription(body)
                            );

            await msg.SendAsync();
        }


        [RequireUserPermission(GuildPermission.Administrator, Group = "CounterModPermissions")] [RequireUserPermission(GuildPermission.ManageChannels, Group = "CounterModPermissions")]
        [RequireTwitchBadge(BadgeTypeOption.Broadcaster | BadgeTypeOption.Moderator, Group = "CounterModPermissions")]
        public class CounterModuleModPermissionSubModule : ModuleBase<CommandContext>, IServerModule
        {
            public Server _Server { get; set; }

            [Command("add")]
            [Summary("Creates a new counter.")]
            public async Task<RuntimeResult> AddCounter([Summary("The name of the counter")] string counterName, [Summary("Text to embedd the counter into. e.g. \"I ate {x} fish, today!\"")][Optional] string counterText)
            {
                var result = await _Server.CounterHandler.AddCounterAsync(new Counter(counterName, counterText));

                if (result.IsSuccess)
                {
                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COUNTER_X_CREATED, "{X}", counterName);

                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Counter)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                    await msg.SendAsync();
                }

                return result;
            }

            [Command("remove")]
            [Summary("Removes an existing counter.")]
            public async Task<RuntimeResult> RemoveCounter([Summary("The name of the counter")] string counterName)
            {
                var result = await _Server.CounterHandler.RemoveCounterAsync(counterName);
                if (result.IsSuccess)
                {
                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COUNTER_X_REMOVED, "{X}", counterName);

                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Counter)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(ReplyDictionary.AFFIRMATIVE, EmojiDictionary.FLOPPY_DISC)
                                .SetDescription(body)
                            );

                    await msg.SendAsync();
                }

                return result;
            }

            [Command("set")]
            [Summary("Sets existing counter to value.")]
            public async Task<RuntimeResult> SetCounter([Summary("The name of the counter")] string counterName, [Summary("The value the counter will be set to.")] int counterValue)
            {
                var result = _Server.CounterHandler.GetCounter(counterName);
                if (result.ResultValue is Counter counter)
                {
                    counter.Value = counterValue;
                    await _Server.CounterHandler.SaveCounterCollectionToFile();


                    string body = await counter.ReturnValueText();

                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Counter)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(counter.Name, EmojiDictionary.PENCIL)
                                .SetDescription(body)
                            );

                    await msg.SendAsync();
                }

                return result;
            }

            [Command("edit")]
            [Summary("Edits existing counter description.")]
            public async Task<RuntimeResult> EditCounter([Summary("The name of the counter")] string counterName, [Summary("The new description.")] string newDescription)
            {
                var result = _Server.CounterHandler.GetCounter(counterName);
                if (result.ResultValue is Counter counter)
                {
                    counter.Description = newDescription;

                    await _Server.CounterHandler.SaveCounterCollectionToFile();

                    string body = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COUNTER_X_DESCRIPTION_CHANGED, "{x}", counterName);

                    ChannelMessage msg = new ChannelMessage(Context)
                            .SetTemplate(ChannelMessage.MessageTemplateOption.Counter)
                            .AddContent(new ChannelMessageContent()
                                .SetTitle(counter.Name, EmojiDictionary.PENCIL)
                                .SetDescription(body)
                            );

                    await msg.SendAsync();
                }

                return result;
            }
        }

    }
}
