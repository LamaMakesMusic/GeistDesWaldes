using Discord;
using GeistDesWaldes.TwitchIntegration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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


        public Task<IUserMessage> SendMessageAsync(string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None)
            => SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds, flags, null);
        public Task<IUserMessage> SendMessageAsync(string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(text) && embed != null)
                    text = $"[Embed not supported] <{embed.Title}> {embed.Description}";


                Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SendMessageAsync), text), (int)ConsoleColor.Blue);


                IUserMessage msg = null;
                return msg;
            });
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
                var response = await TwitchIntegrationHandler.ValidatedAPICall(TwitchIntegrationHandler.Instance.API.Helix.Users.GetUsersAsync(logins: await TwitchIntegrationHandler.GetChattersForChannel(Name)));

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
        public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetUsersAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
        {
            Task<ReadOnlyCollection<IUser>> task = Task.Run(async () =>
            {
                #region Discord Users
                var discordUsers = new List<IUser>();

                foreach (var guild in Launcher.Instance.DiscordClient.Guilds)
                {
                    discordUsers.AddRange(guild.Users);
                }
                #endregion

                #region Twitch Users
                var twitchUsers = new List<TwitchUser>();
                var response = await TwitchIntegrationHandler.ValidatedAPICall(TwitchIntegrationHandler.Instance.API.Helix.Users.GetUsersAsync(logins: await TwitchIntegrationHandler.GetChattersForChannel(Name)));

                foreach (var user in response.Users)
                {
                    twitchUsers.Add(new TwitchUser(user.Id, user.Login));
                }
                #endregion

                var result = twitchUsers.ToList<IUser>();
                result.AddRange(discordUsers);

                return new ReadOnlyCollection<IUser>(result);
            });

            return task.ToAsyncEnumerable();
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
        public Task<IUserMessage> SendFileAsync(string filePath, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None)
            => throw new NotImplementedException();
        public Task<IUserMessage> SendFileAsync(Stream stream, string filename, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None)
            => throw new NotImplementedException();
        public Task<IUserMessage> SendFileAsync(Stream stream, string filename, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
            => throw new NotImplementedException();
        public Task<IUserMessage> SendFileAsync(FileAttachment attachment, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
            => throw new NotImplementedException();
        public Task<IUserMessage> SendFileAsync(FileAttachment attachment, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None)
            => throw new NotImplementedException();
        public Task<IUserMessage> SendFilesAsync(IEnumerable<FileAttachment> attachments, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
            => throw new NotImplementedException();
        public Task<IUserMessage> SendFilesAsync(IEnumerable<FileAttachment> attachments, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None)
            => throw new NotImplementedException();


        public override string ToString()
        {
            return Name;
        }
    }
}
