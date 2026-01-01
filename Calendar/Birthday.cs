using System;

namespace GeistDesWaldes.Calendar
{
    [Serializable]
    public class Birthday
    {
        public Guid UserId;
        public DateTime BirthDate;

        public Birthday()
        {

        }
        public Birthday(Guid id, DateTime date)
        {
            UserId = id;
            BirthDate = date;
        }
    }
}
