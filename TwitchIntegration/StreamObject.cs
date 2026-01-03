using System;

namespace GeistDesWaldes.TwitchIntegration
{
    public class StreamObject
    {
        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline == value)
                    return;
                
                _isOnline = value;
                
                if (_isOnline)
                    StartedAt = DateTime.Now;
                else
                    LastSeenAt = DateTime.Now;
            }
        }
        private bool _isOnline;

        public string Title;
        public string Category;
        public DateTime StartedAt { get; private set; }
        public DateTime LastSeenAt{ get; private set; }

        public StreamObject()
        {
        }

        public override string ToString()
        {
            return $"{nameof(StartedAt)} {StartedAt} | {nameof(Title)}: '{Title}' | {nameof(Category)}: '{Category}' | {nameof(LastSeenAt)} {LastSeenAt}";
        }
    }
}
