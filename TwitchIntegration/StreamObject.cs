using System;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace GeistDesWaldes.TwitchIntegration
{
    public class StreamObject
    {
        public bool IsOnline
        {
            get;
            private set;
        }

        public string Title;
        public string Category;
        public DateTimeOffset LastChange { get; private set; }
        public TimeSpan TimeSinceLastChange => DateTimeOffset.Now - LastChange;
        
        public bool HasInvalidEntries => string.IsNullOrEmpty(Title) || string.IsNullOrEmpty(Category);
        

        public void SetOnline(DateTimeOffset startedAt)
        {
            IsOnline = true;
            LastChange = startedAt;
        }

        public void SetOffline()
        {
            IsOnline = false;
            LastChange = DateTimeOffset.Now;
        }

        public void Update(Stream stream)
        {
            if (stream == null)
            {
                Title = "null";
                Category = "null";
            }
            else
            {
                Title = stream.Title;
                Category = stream.GameId;
            }
        }
        
        
        public override string ToString()
        {
            return $"{nameof(Title)}: '{Title}' | {nameof(Category)}: '{Category}' | {nameof(LastChange)} {LastChange}";
        }
    }
}
