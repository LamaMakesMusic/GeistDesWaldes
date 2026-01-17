using System;

namespace GeistDesWaldes.Calendar;

[Serializable]
public class Birthday
{
    public DateTime BirthDate;
    public Guid UserId;

    public Birthday()
    {
    }

    public Birthday(Guid id, DateTime date)
    {
        UserId = id;
        BirthDate = date;
    }
}