using System;
using System.Collections.Generic;

namespace GeistDesWaldes.Calendar
{
    [Serializable]
    public class HolidayBehaviourDictionary
    {
        public string ActiveHoliday = null;

        public List<HolidayBehaviour> Behaviours;


        public HolidayBehaviourDictionary()
        {
            Behaviours = new List<HolidayBehaviour>();
        }
    }
}
