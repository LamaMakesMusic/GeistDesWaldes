using System;
using System.Threading.Tasks;

namespace GeistDesWaldes.Users
{
    public class PendingUser
    {
        public Guid ForestUserId;

        public ulong DiscordUserId;
        public string TwitchUserId;

        public string OptCode;
        public OptTypeOption OptType;

        public DateTime CreatedAt;


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
            return Task.Run(() =>
            {
                return OptCode.Equals(providedCode, StringComparison.Ordinal);
            });
        }



        public enum OptTypeOption
        {
            OptIn = 0,
            OptOut = 1,
            Connect = 2
        }

        public void RefreshOptCode()
        {
            OptCode = Launcher.Random.Next(10000, 99999).ToString();

            CreatedAt = DateTime.UtcNow;
        }
    }
}
