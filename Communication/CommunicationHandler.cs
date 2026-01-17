using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;

namespace GeistDesWaldes.Communication;

public static class CommunicationHandler
{
    public static async Task<RuntimeResult> SetChannelName(ulong channelId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return CustomRuntimeResult.FromError($"{ReplyDictionary.CHANNEL_ID_MUST_NOT_BE_EMPTY}");
        }

        try
        {
            if (Launcher.Instance.DiscordClient.GetChannel(channelId) is SocketChannel channel)
            {
                if (channel is SocketTextChannel textchannel)
                {
                    await textchannel.ModifyAsync(c => { c.Name = newName; });

                    return CustomRuntimeResult.FromSuccess();
                }

                if (channel is SocketVoiceChannel voicechannel)
                {
                    await voicechannel.ModifyAsync(c => { c.Name = newName; });

                    return CustomRuntimeResult.FromSuccess();
                }

                return CustomRuntimeResult.FromError($"{ReplyDictionary.CAN_ONLY_SET_NAME_OF_TEXT_VOICE_CHANNELS}. ({channelId})");
            }
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }


        return CustomRuntimeResult.FromError($"{ReplyDictionary.COULD_NOT_FIND_CHANNEL_WITH_ID}: {channelId}");
    }

    public static async Task<RuntimeResult> SetChannelTopic(ulong channelId, string newTopic)
    {
        try
        {
            if (Launcher.Instance.DiscordClient.GetChannel(channelId) is SocketChannel channel)
            {
                if (channel is SocketTextChannel textchannel)
                {
                    await textchannel.ModifyAsync(c => { c.Topic = new Optional<string>(newTopic); });

                    return CustomRuntimeResult.FromSuccess();
                }

                return CustomRuntimeResult.FromError($"{ReplyDictionary.CAN_ONLY_SET_TOPIC_OF_TEXT_CHANNELS} ({channelId})");
            }
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }


        return CustomRuntimeResult.FromError($"{ReplyDictionary.COULD_NOT_FIND_CHANNEL_WITH_ID} {channelId}");
    }
}