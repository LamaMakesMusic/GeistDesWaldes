using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeistDesWaldes.Statistics
{
    [Serializable]
    public class CommandStatistic
    {
        public string Id;

        public DateTime Start;
        public DateTime End;

        public bool Active 
        {
            get 
            {
                DateTime now = DateTime.Now;
                return now >= Start && now < End;
            }
        }

        public List<CommandStatisticInfo> CommandInfos = new();

        private readonly Dictionary<string, CommandStatisticInfo> _runtimeDictionary = new();


        public CommandStatistic()
        {
        }

        public CommandStatistic(string name, DateTime start, DateTime end)
        {
            Id = name;
            Start = start;
            End = end;
        }
                

        public void CreateRuntimeDictionary()
        {
            _runtimeDictionary.Clear();

            foreach (CommandStatisticInfo info in CommandInfos)
            {
                _runtimeDictionary.Add(info.Command, info);
            }
        }


        public void Add(string command)
        {
            if (_runtimeDictionary.TryGetValue(command, out CommandStatisticInfo info))
            {
                info.CallCount++;
            }
            else
            {
                info = new CommandStatisticInfo(command);

                CommandInfos.Add(info);
                _runtimeDictionary.Add(command, info);
            }

            CommandInfos.Sort((i1, i2) => i2.CallCount.CompareTo(i1.CallCount));
        }


        public override string ToString()
        {
            return $"{Id} ({Start:dd.MM.yyyy} - {End:dd.MM.yyyy}) [{(Active ? "Aktiv" : "Inaktiv")}]";
        }

        public ChannelMessage ToMessage()
        {
            StringBuilder builder = new();

            foreach (CommandStatisticInfo info in CommandInfos)
            {
                builder.AppendLine(info.ToString());
            }

            return new ChannelMessage(null)
                .SetTemplate(ChannelMessage.MessageTemplateOption.Statistics)
                .AddContent(new ChannelMessageContent()
                    .SetTitle(ToString(), EmojiDictionary.CHART)
                    .SetDescription(builder.Length == 0 ? "-" : builder.ToString()));
        }
    }

    [Serializable]
    public class CommandStatisticInfo
    {
        public string Command;
        public int CallCount;


        public CommandStatisticInfo()
        {
        }

        public CommandStatisticInfo(string command)
        {
            Command = command;
            CallCount = 1;
        }


        public override string ToString()
        {
            return $"{CallCount,4}x {Command}";
        }
    }
}
