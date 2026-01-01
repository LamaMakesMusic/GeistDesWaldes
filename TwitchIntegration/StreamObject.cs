using System;

namespace GeistDesWaldes.TwitchIntegration
{
    public class StreamObject
    {
        public bool IsOnline;

        public string Title;
        public string Game;
        public DateTime StartedAt;
        public DateTime LastSeenAt;

        public StreamObject()
        {

        }

        public void UpdateContent(string title, string game, DateTime startTime)
        {
            Title = title;
            Game = game;
            StartedAt = startTime;
            LastSeenAt = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"{nameof(StartedAt)} {StartedAt} | {nameof(Title)}: '{Title}' | {nameof(Game)}: '{Game}' | {nameof(LastSeenAt)} {LastSeenAt}";
        }
    }
}
