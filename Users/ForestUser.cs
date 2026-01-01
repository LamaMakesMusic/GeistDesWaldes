using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Configuration;
using System;
using System.Xml.Serialization;
using DiscordUser = Discord.Rest.RestUser;
using TwitchUser = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace GeistDesWaldes.Users
{
    [Serializable]
    public class ForestUser
    {
        private Guid _forestUserId;
        public Guid ForestUserId {
            get {
                return _forestUserId;
            }
            set {
                lock (_lock)
                {
                    _forestUserId = value;
                }
            }
        }

        private string _discordName;
        public string DiscordName {
            get {
                return _discordName;
            }
            set {
                lock (_lock)
                {
                    _discordName = value;
                }
            }
        }

        private ulong _discordUserId;
        public ulong DiscordUserId {
            get {
                return _discordUserId;
            }
            set {
                lock (_lock)
                {
                    _discordUserId = value;
                }
            }
        }


        private string _twitchName;
        public string TwitchName {
            get {
                return _twitchName;
            }
            set {
                lock (_lock)
                {
                    _twitchName = value;
                }
            }
        }

        private string _twitchUserId;
        public string TwitchUserId {
            get {
                return _twitchUserId;
            }
            set {
                lock (_lock)
                {
                    _twitchUserId = value;
                }
            }
        }

        private TimeSpan _twitchViewTime = TimeSpan.FromMinutes(1);
        public TimeSpan TwitchViewTime 
        {
            get => _twitchViewTime;
            set => _twitchViewTime = value;
        }

        private int _wallet = 0;
        public int Wallet {
            get {
                return _wallet;
            }
            set {
                _wallet = value;
            }
        }

        [XmlIgnore] public bool RequestUpdate = false;
        [XmlIgnore]
        public string Name {
            get {
                if (!string.IsNullOrEmpty(_discordName))
                    return _discordName;
                else if (!string.IsNullOrEmpty(_twitchName))
                    return _twitchName;
                else
                    return "empty";
            }
        }
        [XmlIgnore] public DateTime LastUpdate;

        private bool _isDirty;
        [XmlIgnore] public bool IsDirty 
        {
            get 
            {
                lock(_lock)
                    return _isDirty; 
            }
            set 
            {
                lock(_lock)
                    _isDirty = value;
            }
        }

        private readonly object _lock = new object();


        public ForestUser()
        {

        }
        public ForestUser(Guid forestUserId, ulong discordId = default, string twitchId = default)
        {
            ForestUserId = forestUserId;

            DiscordUserId = discordId;
            TwitchUserId = twitchId;
        }
        public ForestUser(ForestUser clone)
        {
            ForestUserId = clone.ForestUserId;

            DiscordName = clone.DiscordName;
            DiscordUserId = clone.DiscordUserId;

            TwitchName = clone.TwitchName;
            TwitchUserId = clone.TwitchUserId;

            Wallet = clone.Wallet;
        }


        public void UpdateUserData(bool force = false)
        {
            lock (_lock)
            {
                if (!force && (DateTime.UtcNow - LastUpdate).TotalMinutes < ConfigurationHandler.Shared.UpdateUserIntervalInMinutes)
                    return;

                LastUpdate = DateTime.UtcNow;
            }

            RequestUpdate = true;
        }
        public RuntimeResult ApplyUserData(DiscordUser discordUser, TwitchUser twitchUser)
        {
            string result = "";
            RequestUpdate = false;

            if (discordUser != default)
            {
                if (discordUser.Id == DiscordUserId)
                {
                    if (DiscordName != discordUser.Username)
                    {
                        DiscordName = discordUser.Username;
                        IsDirty = true;
                    }
                }
                else
                    result = $"Can not update Discord Info of {nameof(ForestUser)} '{Name}'! Mismatching Discord Id!";
            }

            if (twitchUser != default)
            {
                if (twitchUser.Id == TwitchUserId)
                {
                    if (TwitchName != twitchUser.Login)
                    {
                        TwitchName = twitchUser.Login;
                        IsDirty = true;
                    }
                }
                else
                    result = $"{(result.Length > 0 ? "\n" : "")}Can not update Twitch Info of {nameof(ForestUser)} '{Name}'! Mismatching Twitch Id!";
            }

            return CustomRuntimeResult.FromSuccess(result);
        }


        public void MergeWith(ForestUser user)
        {
            lock (_lock)
            {
                LastUpdate = LastUpdate > user.LastUpdate ? LastUpdate : user.LastUpdate;

                if (DiscordUserId == default && user.DiscordUserId != default)
                {
                    DiscordUserId = user.DiscordUserId;
                    DiscordName = user.DiscordName;
                }   

                if (TwitchUserId == default && user.TwitchUserId != default)
                {
                    TwitchUserId = user.TwitchUserId;
                    TwitchName = user.TwitchName;
                }

                Wallet += user.Wallet;
            }
        }

        public bool CanAfford(int price)
        {
            lock (_lock)
                return price <= Wallet;
        }

        public void AddToWallet(int amount)
        {
            lock (_lock)
                Wallet += amount;

            IsDirty = true;
        }

        public override string ToString()
        {
            lock (_lock)
                return $"Discord: {DiscordUserId} | Twitch: {TwitchUserId}";
        }
    }
}
