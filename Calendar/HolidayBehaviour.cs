using System;
using System.Xml.Serialization;
using GeistDesWaldes.Communication;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.UserCommands;

namespace GeistDesWaldes.Calendar;

[Serializable]
public class HolidayBehaviour
{
    public enum BehaviourAction
    {
        StartCallback = 0,
        EndCallback = 1
    }

    public CustomCommand EndCallback;

    public DateTime HolidayDate;
    public string HolidayName;
    [XmlIgnore] public int HolidayNameHash;

    public CustomCommand StartCallback;


    public HolidayBehaviour()
    {
    }

    public HolidayBehaviour(Server server, string holidayName, DateTime date)
    {
        SetHolidayName(holidayName);

        StartCallback = new CustomCommand(server, nameof(BehaviourAction.StartCallback), null, 0);
        EndCallback = new CustomCommand(server, nameof(BehaviourAction.EndCallback), null, 0);

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
}