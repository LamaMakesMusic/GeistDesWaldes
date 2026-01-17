using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace GeistDesWaldes.Users;

public class MetaUser : IUser
{
    public readonly IUser FrontUser;
    public readonly IUser OriginalUser;

    public MetaUser(IUser frontUser, IUser originalUser)
    {
        FrontUser = frontUser;
        OriginalUser = originalUser;
    }

    public string AvatarId => FrontUser.AvatarId;
    public string Discriminator => FrontUser.Discriminator;
    public ushort DiscriminatorValue => FrontUser.DiscriminatorValue;
    public bool IsBot => FrontUser.IsBot;
    public bool IsWebhook => FrontUser.IsWebhook;
    public string Username => FrontUser.Username;
    public DateTimeOffset CreatedAt => FrontUser.CreatedAt;
    public ulong Id => FrontUser.Id;
    public string Mention => FrontUser.Mention;
    public UserStatus Status => FrontUser.Status;
    public UserProperties? PublicFlags => FrontUser.PublicFlags;
    public string GlobalName => FrontUser.GlobalName;

    public string AvatarDecorationHash => throw new NotImplementedException();

    public ulong? AvatarDecorationSkuId => throw new NotImplementedException();

    public PrimaryGuild? PrimaryGuild => throw new NotImplementedException();

    IReadOnlyCollection<ClientType> IPresence.ActiveClients => FrontUser.ActiveClients;
    IReadOnlyCollection<IActivity> IPresence.Activities => FrontUser.Activities;

    public Task<IDMChannel> CreateDMChannelAsync(RequestOptions options = null)
    {
        return FrontUser.CreateDMChannelAsync(options);
    }

    public string GetAvatarDecorationUrl()
    {
        throw new NotImplementedException();
    }

    public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
    {
        return FrontUser.GetAvatarUrl();
    }

    public string GetDefaultAvatarUrl()
    {
        return FrontUser.GetDefaultAvatarUrl();
    }

    public string GetDisplayAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
    {
        throw new NotImplementedException();
    }
}