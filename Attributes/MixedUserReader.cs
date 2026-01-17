using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GeistDesWaldes.TwitchIntegration;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.Attributes
{
    public class MixedUserReader<T> : TypeReader where T : class, IUser
    {
        public const string TWITCH_KEYWORD = "twitch:";
        public const string DISCORD_KEYWORD = "discord:";

        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            input = input?.TrimStart();
            if (string.IsNullOrWhiteSpace(input))
                return TypeReaderResult.FromSuccess((object)null);

            bool twitchKeyword = input.StartsWith(TWITCH_KEYWORD, StringComparison.OrdinalIgnoreCase);
            bool discordKeyword = input.StartsWith(DISCORD_KEYWORD, StringComparison.OrdinalIgnoreCase);
            bool isTwitchMessageChannel = context.Channel is TwitchMessageChannel;

            bool lookingForTwitchUser = twitchKeyword || typeof(T) == typeof(TwitchUser) || (isTwitchMessageChannel && !discordKeyword);

            if (twitchKeyword)
                input = input.Remove(0, TWITCH_KEYWORD.Length);
            if (discordKeyword)
                input = input.Remove(0, DISCORD_KEYWORD.Length);

            if (lookingForTwitchUser)
                return await FindTwitchUser(input);


            Server server = (Server)services.GetService(typeof(Server));
            if (server == null)
                return TypeReaderResult.FromError(CommandError.ObjectNotFound, $"Could not get Server from {nameof(IServiceProvider)}!");

            SocketGuild serverGuild = Launcher.Instance.DiscordClient.GetGuild(server.GuildId);
            if (serverGuild == null)
                return TypeReaderResult.FromError(CommandError.ObjectNotFound, $"Could not get {nameof(SocketGuild)} from Server with {nameof(Server.GuildId)} '{server.GuildId}'!");

            return await FindDiscordUser(input, serverGuild);
        }    
    
        private async Task<TypeReaderResult> FindTwitchUser(string input)
        {
            input = input.ToLower().Trim().TrimStart('@');

            TwitchUser user = null;

            var userResponse = await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.Users.GetUsersAsync(logins: new List<string>() { input }));

            StringBuilder errorReason = new StringBuilder($"Could not find matching twitch user. '{userResponse?.Users?.Length}' results: ");
            
            if (userResponse != null)
            {
                foreach (var u in userResponse.Users)
                {
                    if (u.Login.Equals(input, StringComparison.OrdinalIgnoreCase))
                    {
                        user = new TwitchUser(u.Id, u.Login);
                        break;
                    }

                    errorReason.Append($"'{u.Login}' | ");
                }
            }

            if (user == null)
            {
                errorReason.Remove(errorReason.Length - 2, 2);

                return TypeReaderResult.FromError(CommandError.ObjectNotFound, errorReason.ToString());
            }

            if (user as T is T tUser && tUser != null)
                return TypeReaderResult.FromSuccess(tUser);
            else
                return TypeReaderResult.FromError(CommandError.ParseFailed, $"Could not parse {nameof(TwitchUser)} to {nameof(T)}");
        }
    
        private async Task<TypeReaderResult> FindDiscordUser(string input, SocketGuild guild)
        {
            //By Mention (1.0)
            if (MentionUtils.TryParseUser(input, out var id) && guild.GetUser(id) is T tUserMentioned && tUserMentioned != null)
                return TypeReaderResult.FromSuccess(tUserMentioned);

            //By Id (0.9)
            if (ulong.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out id) && guild.GetUser(id) is T tUserId && tUserId != null)
                return TypeReaderResult.FromSuccess(tUserId);


            T tUserSearch = (await guild.SearchUsersAsync(input, 1)).FirstOrDefault() as T;

            if (tUserSearch != null)
                return TypeReaderResult.FromSuccess(tUserSearch);

            return TypeReaderResult.FromError(CommandError.ObjectNotFound, "User not found.");
        }
    }
}
