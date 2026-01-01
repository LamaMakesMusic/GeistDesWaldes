using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeistDesWaldes.Attributes;
using TwitchLib.EventSub.Core.Models.Chat;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;

namespace GeistDesWaldes.TwitchIntegration
{
    public class TwitchUser : IUser
    {
        public TwitchUser(string twitchId, string username, bool isBot = false)
        {
            Id = (ulong)twitchId.GetHashCode();
            TwitchId = twitchId;
            Username = username;

            IsBot = isBot;
        }

        public TwitchUser(ChannelChatMessage eventSubMessage) : this(eventSubMessage.ChatterUserId, eventSubMessage.ChatterUserName)
        {
            Badges = BadgeTypeOption.None;
            
            foreach (ChatBadge badge in eventSubMessage.Badges)
            {
                if (Enum.TryParse(badge.SetId, true, out BadgeTypeOption parsedBadge))
                    Badges |= parsedBadge;
            }
        }

        public ulong Id { get; } // Hacky: Id in a discord-environment (IUser) context is our hashed "Twitch Api User" Id (which is a string)
        public string TwitchId { get; } // Twitch Id MUST be used when interacting with the Twitch-API, or if we want a unique ID
        public string Username { get; }
        public readonly BadgeTypeOption Badges;
        public bool IsBot { get; }
        public string GlobalName => Username;

        public string AvatarId => throw new NotImplementedException();
        public string Discriminator => throw new NotImplementedException();
        public ushort DiscriminatorValue => throw new NotImplementedException();

        public bool IsWebhook => throw new NotImplementedException();

        public DateTimeOffset CreatedAt => throw new NotImplementedException();

        public string Mention => throw new NotImplementedException();

        public UserStatus Status => throw new NotImplementedException();
        
        IReadOnlyCollection<IActivity> IPresence.Activities => throw new NotImplementedException();
        IReadOnlyCollection<ClientType> IPresence.ActiveClients => throw new NotImplementedException();

        public UserProperties? PublicFlags => throw new NotImplementedException();

        public string AvatarDecorationHash => throw new NotImplementedException();

        public ulong? AvatarDecorationSkuId => throw new NotImplementedException();

        PrimaryGuild? IUser.PrimaryGuild => throw new NotImplementedException();

        public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128) => throw new NotImplementedException();
        
        public string GetDefaultAvatarUrl() => throw new NotImplementedException();
        
        public Task<IDMChannel> CreateDMChannelAsync(RequestOptions options = null) => throw new NotImplementedException();

        public override string ToString()
        {
            return Username;
        }

        public string GetDisplayAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            throw new NotImplementedException();
        }

        public string GetAvatarDecorationUrl()
        {
            throw new NotImplementedException();
        }
    }
}
