using System;
using System.Collections.Generic;
using GeistDesWaldes.UserCommands;

namespace GeistDesWaldes.Calendar;

[Serializable]
public class BirthdayDictionary
{
    public List<Birthday> ActiveBirthdays;
    public List<Birthday> Birthdays;
    public CustomCommand EndCallback;

    public CustomCommand StartCallback;

    public BirthdayDictionary()
    {
    }

    public BirthdayDictionary(Server server)
    {
        ActiveBirthdays = new List<Birthday>();
        Birthdays = new List<Birthday>();

        StartCallback = new CustomCommand(server, nameof(StartCallback), null, default) { IsBirthday = true };
        EndCallback = new CustomCommand(server, nameof(EndCallback), null, default) { IsBirthday = true };
    }
}