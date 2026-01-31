using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Discord.Commands;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using Microsoft.Extensions.DependencyInjection;

namespace GeistDesWaldes.Counters;

[Serializable]
public class Counter
{
    public string Description;
    public string Name;
    [XmlIgnore] public int NameHash;
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

    public string ReturnValueText()
    {
        return Description.ReplaceStringInvariantCase("{x}", Value.ToString());
    }

    public void IncreaseCounterValue(int amount = 1)
    {
        Value += amount;
    }

    public async Task ExecuteCallback(ICommandContext arg1, object[] arg2, IServiceProvider services, CommandInfo arg4)
    {
        string header = Name;
        string headerEmoji = null;
        string body = ReturnValueText();

        if (arg2.Length > 0 && arg2[0]?.ToString() is { } parameter)
        {
            int amount = 1;
            if (arg2.Length > 1 && arg2[1] is int a && a != 0)
            {
                amount = a;
            }

            if (parameter == "++")
            {
                IncreaseCounterValue(Math.Abs(amount));

                header = ReplyDictionary.COUNTER_X_INCREASED.ReplaceStringInvariantCase("{x}", Name);
                headerEmoji = EmojiDictionary.ARROW_DOUBLE_UP;
                body = ReturnValueText();
            }
            else if (parameter == "--")
            {
                IncreaseCounterValue(Math.Abs(amount) * -1);

                header = ReplyDictionary.COUNTER_X_DECREASED.ReplaceStringInvariantCase("{x}", Name);
                headerEmoji = EmojiDictionary.ARROW_DOUBLE_DOWN;
                body = ReturnValueText();
            }
            else
            {
                throw new Exception(ReplyDictionary.COUNTER_UNDEFINED_PARAMETER_X.ReplaceStringInvariantCase("{x}", parameter));
            }

            CounterHandler handler = services.GetService<CounterHandler>();
            await handler.SaveCounterCollectionToFile();
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