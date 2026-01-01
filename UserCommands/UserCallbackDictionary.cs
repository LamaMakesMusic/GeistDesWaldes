using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace GeistDesWaldes.UserCommands
{
    [Serializable]
    public class UserCallbackDictionary
    {
        public List<CustomCommand> Callbacks;

        public const string DiscordPrefix = "Discord_";
        public const string TwitchPrefix = "Twitch_";

        [XmlIgnore] [NonSerialized] public Server _Server;

        public enum DiscordCallbackTypes
        {
            OnClientReady = 0,
            OnUserJoinedGuild = 1
        }
        public enum TwitchCallbackTypes
        {
            OnStreamStart = 0,
            OnStreamUpdate = 1,
            OnStreamEnd = 2,
            OnFollow = 3,
            OnUserIntro = 4,
            OnStreamStartOneShot = 5,
            OnStreamEndOneShot = 6,
            OnRaid = 7
        }

        public UserCallbackDictionary()
        {

        }
        public UserCallbackDictionary(Server server)
        {
            _Server = server;
            Callbacks = new List<CustomCommand>();
        }


        public void AddMissingEntries()
        {
            string nameCombi;
            int nameHash;

            foreach (string t in Enum.GetNames(typeof(DiscordCallbackTypes)))
            {
                nameCombi = $"{DiscordPrefix}{t}";
                nameHash = nameCombi.ToLower().GetHashCode();

                if (Callbacks.Any(c => c.NameHash == nameHash))
                    continue;

                Callbacks.Add(new CustomCommand(_Server, nameCombi, null, default));
            }

            foreach (string t in Enum.GetNames(typeof(TwitchCallbackTypes)))
            {
                nameCombi = $"{TwitchPrefix}{t}";
                nameHash = nameCombi.ToLower().GetHashCode();

                if (Callbacks.Any(c => c.NameHash == nameHash))
                    continue;

                Callbacks.Add(new CustomCommand(_Server, $"{TwitchPrefix}{t}", null, default));
            }

        }
    }
}
