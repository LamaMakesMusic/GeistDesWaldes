using Discord.Commands;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using System;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GeistDesWaldes.Counters
{
    [Serializable]
    public class Counter
    {
        [XmlIgnore] public int NameHash;
        public string Name;
        public string Description;
        public int Value;

        public Counter()
        {

        }
        public Counter(string name, string description)
        {
            SetName(name);
            Description = string.IsNullOrWhiteSpace(description) ? "" : description;
            Value = 0;
        }

        public void SetName(string name)
        {
            Name = name;
            NameHash = name.ToLower().GetHashCode();
        }

        public Task<string> ReturnValueText()
        {
            return ReplyDictionary.ReplaceStringInvariantCase(Description, "{x}", Value.ToString());
        }

        public void IncreaseCounterValue(int amount = 1)
        {
            Value += amount;
        }

        public async Task ExecuteCallback(ICommandContext arg1, object[] arg2, IServiceProvider arg3, CommandInfo arg4)
        {
            string header = Name;
            string headerEmoji = null;
            string body = await ReturnValueText();

            if (arg2.Length > 0 && arg2[0]?.ToString() is string parameter)
            {
                int amount = 1;
                if (arg2.Length > 1 && arg2[1] is int a && a != 0)
                    amount = a;

                if (parameter == "++")
                {
                    IncreaseCounterValue(Math.Abs(amount));

                    header = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COUNTER_X_INCREASED, "{x}", Name);
                    headerEmoji = EmojiDictionary.ARROW_DOUBLE_UP;
                    body = await ReturnValueText();
                }
                else if (parameter == "--")
                {
                    IncreaseCounterValue(Math.Abs(amount) * -1);

                    header = await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COUNTER_X_DECREASED, "{x}", Name);
                    headerEmoji = EmojiDictionary.ARROW_DOUBLE_DOWN;
                    body = await ReturnValueText();
                }
                else
                    throw new Exception(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.COUNTER_UNDEFINED_PARAMETER_X, "{x}", parameter));

                Server server = (Server)arg3.GetService(typeof(Server));

                await server.CounterHandler.SaveCounterCollectionToFile();
            }

            ChannelMessage message = new ChannelMessage(arg1)
                .SetTemplate(ChannelMessage.MessageTemplateOption.Counter)
                .AddContent(new ChannelMessageContent()
                    .SetTitle(header, headerEmoji)
                    .SetDescription(body)
                );

            await message.SendAsync();
        }
    }
}
