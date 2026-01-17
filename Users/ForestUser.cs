using System;
using System.Xml.Serialization;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Configuration;
using DiscordUser = Discord.Rest.RestUser;
using TwitchUser = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace GeistDesWaldes.Users;

[Serializable]
public class ForestUser
{
    private readonly object _lock = new();

    private string _discordName;

    private ulong _discordUserId;
    private Guid _forestUserId;

    private bool _isDirty;


    private string _twitchName;

    private string _twitchUserId;

    private TimeSpan _twitchViewTime = TimeSpan.FromMinutes(1);

    private int _wallet;
    [XmlIgnore] public DateTime LastUpdate;

    [XmlIgnore] public bool RequestUpdate;


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

    public Guid ForestUserId
    {
        get => _forestUserId;
        set
        {
            lock (_lock)
            {
                _forestUserId = value;
            }
        }
    }

    public string DiscordName
    {
        get => _discordName;
        set
        {
            lock (_lock)
            {
                _discordName = value;
            }
        }
    }

    public ulong DiscordUserId
    {
        get => _discordUserId;
        set
        {
            lock (_lock)
            {
                _discordUserId = value;
            }
        }
    }

    public string TwitchName
    {
        get => _twitchName;
        set
        {
            lock (_lock)
            {
                _twitchName = value;
            }
        }
    }

    public string TwitchUserId
    {
        get => _twitchUserId;
        set
        {
            lock (_lock)
            {
                _twitchUserId = value;
            }
        }
    }

    public TimeSpan TwitchViewTime
    {
        get => _twitchViewTime;
        set => _twitchViewTime = value;
    }

    public int Wallet
    {
        get => _wallet;
        set => _wallet = value;
    }

    [XmlIgnore]
    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(_discordName))
            {
                return _discordName;
            }

            if (!string.IsNullOrEmpty(_twitchName))
            {
                return _twitchName;
            }

            return "empty";
        }
    }

    [XmlIgnore]
    public bool IsDirty
    {
        get
        {
            lock (_lock)
            {
                return _isDirty;
            }
        }
        set
        {
            lock (_lock)
            {
                _isDirty = value;
            }
        }
    }


    public void UpdateUserData(bool force = false)
    {
        lock (_lock)
        {
            if (!force && (DateTime.UtcNow - LastUpdate).TotalMinutes < ConfigurationHandler.Shared.UpdateUserIntervalInMinutes)
            {
                return;
            }

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
            {
                result = $"Can not update Discord Info of {nameof(ForestUser)} '{Name}'! Mismatching Discord Id!";
            }
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
            {
                result = $"{(result.Length > 0 ? "\n" : "")}Can not update Twitch Info of {nameof(ForestUser)} '{Name}'! Mismatching Twitch Id!";
            }
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
        {
            return price <= Wallet;
        }
    }

    public void AddToWallet(int amount)
    {
        lock (_lock)
        {
            Wallet += amount;
        }

        IsDirty = true;
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return $"Discord: {DiscordUserId} | Twitch: {TwitchUserId}";
        }
    }
}