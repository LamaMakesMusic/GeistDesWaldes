using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Users;

public class PendingUser
{
    public enum OptTypeOption
    {
        OptIn = 0,
        OptOut = 1,
        Connect = 2
    }

    public DateTime CreatedAt;

    public ulong DiscordUserId;
    public Guid ForestUserId;

    public string OptCode;
    public OptTypeOption OptType;
    public string TwitchUserId;


    public PendingUser(Guid forestUserId, OptTypeOption optType, ulong discordId = default, string twitchId = default)
    {
        ForestUserId = forestUserId;

        OptType = optType;

        DiscordUserId = discordId;
        TwitchUserId = twitchId;

        RefreshOptCode();
    }


    public Task<bool> OptCodeValid(string providedCode)
    {
        return Task.Run(() => { return OptCode.Equals(providedCode, StringComparison.Ordinal); });
    }

    public void RefreshOptCode()
    {
        OptCode = Launcher.Random.Next(10000, 99999).ToString();

        CreatedAt = DateTime.UtcNow;
    }
}