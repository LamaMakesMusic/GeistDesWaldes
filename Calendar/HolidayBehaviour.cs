using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.UserCommands;
using System;
using System.Xml.Serialization;

namespace GeistDesWaldes.Calendar
{
    [Serializable]
    public class HolidayBehaviour
    {
        [XmlIgnore] public int HolidayNameHash;
        public string HolidayName;

        public DateTime HolidayDate;

        public CustomCommand StartCallback;
        public CustomCommand EndCallback;


        public HolidayBehaviour()
        {

        }
        public HolidayBehaviour(Server server, string holidayName, DateTime date)
        {
            SetHolidayName(holidayName);

            StartCallback = new CustomCommand(server, nameof(BehaviourAction.StartCallback), null, 0, 0);
            EndCallback = new CustomCommand(server, nameof(BehaviourAction.EndCallback), null, 0, 0);

            HolidayDate = date;
        }
        public HolidayBehaviour(HolidayBehaviour copy)
        {
            HolidayName = copy.HolidayName;
            HolidayNameHash = copy.HolidayNameHash;
            HolidayDate = copy.HolidayDate;
            StartCallback = copy.StartCallback;
            EndCallback = copy.EndCallback;
        }

        public void SetHolidayName(string name)
        {
            HolidayName = name;
            HolidayNameHash = name.ToLower().GetHashCode();
        }

        public ChannelMessage ToMessage()
        {
            return new ChannelMessage(null)
                .SetTemplate(ChannelMessage.MessageTemplateOption.Calendar)
                .AddContent(new ChannelMessageContent()
                    .SetTitle(HolidayName)
                    .SetDescription("Callbacks"))
                .AddContent(StartCallback.ActionsToMessageContent()
                    .SetTitle($"{StartCallback.Name} {StartCallback.TargetChannelToString()}", EmojiDictionary.INFO))
                .AddContent(EndCallback.ActionsToMessageContent()
                    .SetTitle($"{EndCallback.Name} {EndCallback.TargetChannelToString()}", EmojiDictionary.INFO));
        }

        public enum BehaviourAction
        {
            StartCallback = 0,
            EndCallback = 1
        }
    }
}
