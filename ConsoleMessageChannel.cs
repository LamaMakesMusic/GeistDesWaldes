using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using GeistDesWaldes.TwitchIntegration;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace GeistDesWaldes
{
    public class ConsoleMessageChannel : IMessageChannel
    {
        public ConsoleMessageChannel()
        {
            Name = "Console";
            _createdAt = DateTimeOffset.Now;
        }

        public ulong Id { get; }
        public string Name { get; }

        
        public Task<IUserMessage> SendMessageAsync(string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
        {
            if (string.IsNullOrWhiteSpace(text) && embed != null)
                text = $"[Embed not supported] <{embed.Title}> {embed.Description}";

            Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SendMessageAsync), text), (int)ConsoleColor.Blue);

            return Task.FromResult(default(IUserMessage));
        }
        
        public Task<IUser> GetUserAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            return Task.Run(async () =>
            {
                if (id == Launcher.Instance.DiscordClient.CurrentUser.Id)
                    return Launcher.Instance.DiscordClient.CurrentUser;

                #region Discord Users
                foreach (var guild in Launcher.Instance.DiscordClient.Guilds)
                {
                    var user = guild.GetUser(id);

                    if (user != null)
                        return user;
                }
                #endregion

                #region Twitch Users
                var response = await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.Users.GetUsersAsync(logins: await TwitchIntegrationHandler.GetChattersForChannel(Name)));

                foreach (var user in response.Users)
                {
                    if (id == (ulong)user.Id.GetHashCode())
                    {
                        IUser u = new TwitchUser(user.Id, user.Login);
                        return u;
                    }
                }
                #endregion

                await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(GetUserAsync), $"Could not find User with ID: {id}"));
                return null;
            });
        }
        public async IAsyncEnumerable<IReadOnlyCollection<IUser>> GetUsersAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            // Discord Users
            List<IUser> result = new();

            foreach (SocketGuild guild in Launcher.Instance.DiscordClient.Guilds)
            {
                result.AddRange(guild.Users);
            }

            // Twitch Users
            GetUsersResponse response = await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.Users.GetUsersAsync(logins: await TwitchIntegrationHandler.GetChattersForChannel(Name)));

            foreach (User user in response.Users)
            {
                result.Add(new TwitchUser(user.Id, user.Login));
            }

            yield return result;
        }

        private readonly DateTimeOffset _createdAt;
        public DateTimeOffset CreatedAt => _createdAt;

        public ChannelType ChannelType => ChannelType.Text;

        public Task<IUserMessage> ModifyMessageAsync(ulong messageId, Action<MessageProperties> func, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task DeleteMessageAsync(ulong messageId, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task DeleteMessageAsync(IMessage message, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public IDisposable EnterTypingState(RequestOptions options = null)
        {
            return null;
        }

        public Task<IMessage> GetMessageAsync(ulong id, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(int limit = 100, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(ulong fromMessageId, Direction dir, int limit = 100, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetMessagesAsync(IMessage fromMessage, Direction dir, int limit = 100, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyCollection<IMessage>> GetPinnedMessagesAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task TriggerTypingAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }


        public Task<IUserMessage> SendFileAsync(string filePath, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
            => throw new NotImplementedException();
        public Task<IUserMessage> SendFileAsync(Stream stream, string filename, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
            => throw new NotImplementedException();
        public Task<IUserMessage> SendFileAsync(FileAttachment attachment, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
            => throw new NotImplementedException();
        public Task<IUserMessage> SendFilesAsync(IEnumerable<FileAttachment> attachments, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
            => throw new NotImplementedException();


        public override string ToString()
        {
            return Name;
        }
    }
}
