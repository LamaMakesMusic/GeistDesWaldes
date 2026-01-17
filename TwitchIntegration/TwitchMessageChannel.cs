using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord;
using GeistDesWaldes.Configuration;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Client;

namespace GeistDesWaldes.TwitchIntegration;

public class TwitchMessageChannel : IMessageChannel
{
    public TwitchClient Client;


    public TwitchMessageChannel(TwitchClient client, ulong guildId, ulong twitchChatId, string channelName, bool isWhisper = false) : this(guildId, twitchChatId, channelName, isWhisper)
    {
        Client = client;
    }

    public TwitchMessageChannel(ulong guildId, ulong twitchChatId, string channelName, bool isWhisper = false)
    {
        GuildId = guildId;
        Id = twitchChatId;
        Name = channelName;
        IsWhisper = isWhisper;
        CreatedAt = DateTimeOffset.Now;

        Client = TwitchIntegrationHandler.Instance.GetClient(channelName);
    }

    public ulong GuildId { get; }

    public bool IsWhisper { get; }

    public ulong Id { get; }
    public string Name { get; }
    public DateTimeOffset CreatedAt { get; }

    public ChannelType ChannelType => ChannelType.Text;

    public async Task<IUserMessage> SendMessageAsync(string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
    {
        if (text == null)
        {
            text = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(text) && embed != null)
        {
            text = $"[Embed not supported] <{embed.Title}> {embed.Description}";
        }

        if (text.Length > 2000)
        {
            text = text.Substring(0, 1997);
            text = $"{text}...";
        }

        string overflow = null;

        if (text.Length > 500)
        {
            overflow = text.Substring(500);
            text = text.Substring(0, 500);
        }

        if (IsWhisper)
        {
            Client.SendWhisper(Name, text);
        }
        else
        {
            Client.SendMessage(Name, text);
        }

        if (overflow != null)
        {
            await Task.Delay(250);
            await SendMessageAsync(overflow, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds, flags, poll);
        }

        return null;
    }

    public Task<IUser> GetUserAsync(ulong twitchIdHash, CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
    {
        return Task.Run(async () =>
        {
            RuntimeConfiguration runConfig = ConfigurationHandler.RuntimeConfig[GuildId];

            // If we are looking for the bot via the discord-bot's ID, return the twitch-bot
            if (twitchIdHash == Launcher.Instance.DiscordClient.CurrentUser.Id)
            {
                return TwitchIntegrationHandler.Instance.BotUser;
            }

            // If we are looking for the streamer
            if (twitchIdHash == (ulong)runConfig.ChannelOwner.Id.GetHashCode())
            {
                return new TwitchUser(runConfig.ChannelOwner.Id, runConfig.ChannelOwner.Login);
            }

            GetUsersResponse response = await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.Users.GetUsersAsync(logins: await TwitchIntegrationHandler.GetChattersForChannel(Name)));

            foreach (User user in response.Users)
            {
                if (twitchIdHash == (ulong)user.Id.GetHashCode())
                {
                    return (IUser)new TwitchUser(user.Id, user.Login);
                }
            }

            return null;
        });
    }

    public async IAsyncEnumerable<IReadOnlyCollection<IUser>> GetUsersAsync(CacheMode mode = CacheMode.AllowDownload, RequestOptions options = null)
    {
        RuntimeConfiguration runConfig = ConfigurationHandler.RuntimeConfig[GuildId];

        List<TwitchUser> twitchUsers =
        [
            new(runConfig.ChannelOwner.Id, runConfig.ChannelOwner.Login)
        ];

        GetUsersResponse response = await TwitchIntegrationHandler.ValidatedApiCall(TwitchIntegrationHandler.Instance.Api.Helix.Users.GetUsersAsync(logins: await TwitchIntegrationHandler.GetChattersForChannel(Name)));

        foreach (User user in response.Users)
        {
            twitchUsers.Add(new TwitchUser(user.Id, user.Login));
        }

        yield return twitchUsers;
    }

    public IDisposable EnterTypingState(RequestOptions options = null)
    {
        return null;
    }

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

    public Task<IUserMessage> SendFileAsync(string filePath, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
    {
        throw new NotImplementedException();
    }

    public Task<IUserMessage> SendFileAsync(Stream stream, string filename, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool isSpoiler = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
    {
        throw new NotImplementedException();
    }

    public Task<IUserMessage> SendFileAsync(FileAttachment attachment, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
    {
        throw new NotImplementedException();
    }

    public Task<IUserMessage> SendFilesAsync(IEnumerable<FileAttachment> attachments, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null, MessageComponent components = null, ISticker[] stickers = null, Embed[] embeds = null, MessageFlags flags = MessageFlags.None, PollProperties poll = null)
    {
        throw new NotImplementedException();
    }

    public Task TriggerTypingAsync(RequestOptions options = null)
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return Name;
    }
}