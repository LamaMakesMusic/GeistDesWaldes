using Discord;
using GeistDesWaldes.UserCommands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeistDesWaldes.Misc
{
    public class MetaCommandMessage : IUserMessage
    {
        /*
            This Class only exists to trigger commands via the CommandSerivce that requires a valid command context,
            which in turn does not exist for "meta"-commands
        */

        public MetaCommandMessage(string messageContent, IMessageChannel contextChannel, IUser contextUser, CommandBundleEntry bundleCallback = null)
        {
            Content = messageContent;

            Channel = contextChannel;
            Author = contextUser;

            Type = MessageType.Default;
            Source = MessageSource.Bot;
            Timestamp = new DateTimeOffset(DateTime.Now);

            BundleCallback = bundleCallback;
        }

        public CommandBundleEntry BundleCallback { get; }

        public IReadOnlyDictionary<IEmote, ReactionMetadata> Reactions { get; }

        public MessageType Type { get; }

        public MessageSource Source { get; }

        public bool IsTTS { get; }

        public bool IsPinned { get; }

        public string Content { get; }

        public DateTimeOffset Timestamp { get; }

        public DateTimeOffset? EditedTimestamp { get; }

        public IMessageChannel Channel { get; }

        public IUser Author { get; }

        public IReadOnlyCollection<IAttachment> Attachments { get; }

        public IReadOnlyCollection<IEmbed> Embeds { get; }

        public IReadOnlyCollection<ITag> Tags { get; }

        public IReadOnlyCollection<ulong> MentionedChannelIds { get; }

        public IReadOnlyCollection<ulong> MentionedRoleIds { get; }

        public IReadOnlyCollection<ulong> MentionedUserIds { get; }

        public MessageActivity Activity { get; }

        public MessageApplication Application { get; }

        public DateTimeOffset CreatedAt { get; }

        public ulong Id { get; }


        public bool IsSuppressed => throw new NotImplementedException();

        public MessageReference Reference => throw new NotImplementedException();

        public IUserMessage ReferencedMessage => throw new NotImplementedException();

        public bool MentionedEveryone => throw new NotImplementedException();

        public MessageFlags? Flags => throw new NotImplementedException();

        public IReadOnlyCollection<ISticker> Stickers => throw new NotImplementedException();

        public string CleanContent => throw new NotImplementedException();

        public IReadOnlyCollection<IMessageComponent> Components => throw new NotImplementedException();

        IReadOnlyCollection<IStickerItem> IMessage.Stickers => throw new NotImplementedException();

        public IMessageInteraction Interaction => throw new NotImplementedException();

        public IThreadChannel Thread => throw new NotImplementedException();

        public MessageRoleSubscriptionData RoleSubscriptionData => throw new NotImplementedException();

        public MessageResolvedData ResolvedData => throw new NotImplementedException();

        public IMessageInteractionMetadata InteractionMetadata => throw new NotImplementedException();

        public Poll? Poll => throw new NotImplementedException();

        public PurchaseNotification PurchaseNotification => throw new NotImplementedException();

        public MessageCallData? CallData => throw new NotImplementedException();

        public IReadOnlyCollection<MessageSnapshot> ForwardedMessages => throw new NotImplementedException();

        public Task AddReactionAsync(IEmote emote, RequestOptions options = null) => throw new NotImplementedException();
        
        public Task CrosspostAsync(RequestOptions options = null) => throw new NotImplementedException();

        public Task DeleteAsync(RequestOptions options = null) => throw new NotImplementedException();

        public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit, RequestOptions options = null) => throw new NotImplementedException();

        public Task ModifyAsync(Action<MessageProperties> func, RequestOptions options = null) => throw new NotImplementedException();

        public Task ModifySuppressionAsync(bool suppressEmbeds, RequestOptions options = null) => throw new NotImplementedException();

        public Task PinAsync(RequestOptions options = null) => throw new NotImplementedException();
        
        public Task UnpinAsync(RequestOptions options = null) => throw new NotImplementedException();

        public Task RemoveAllReactionsAsync(RequestOptions options = null) => throw new NotImplementedException();

        public Task RemoveAllReactionsForEmoteAsync(IEmote emote, RequestOptions options = null) => throw new NotImplementedException();

        public Task RemoveReactionAsync(IEmote emote, IUser user, RequestOptions options = null) => throw new NotImplementedException();

        public Task RemoveReactionAsync(IEmote emote, ulong userId, RequestOptions options = null) => throw new NotImplementedException();

        public string Resolve(TagHandling userHandling = TagHandling.Name, TagHandling channelHandling = TagHandling.Name, TagHandling roleHandling = TagHandling.Name, TagHandling everyoneHandling = TagHandling.Ignore, TagHandling emojiHandling = TagHandling.Name) => throw new NotImplementedException();

        public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetReactionUsersAsync(IEmote emoji, int limit, RequestOptions options = null, ReactionType type = ReactionType.Normal)
        {
            throw new NotImplementedException();
        }

        public Task EndPollAsync(RequestOptions options)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<IReadOnlyCollection<IUser>> GetPollAnswerVotersAsync(uint answerId, int? limit = null, ulong? afterId = null, RequestOptions options = null)
        {
            throw new NotImplementedException();
        }
    }
}
