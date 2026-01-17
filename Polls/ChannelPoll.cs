using System;
using System.Collections.Generic;

namespace GeistDesWaldes.Polls;

[Serializable]
public class ChannelPoll
{
    public ulong ChannelId;
    public List<Poll> Polls = new();

    public ChannelPoll()
    {
    }

    public ChannelPoll(ulong channelId, Poll poll = null)
    {
        ChannelId = channelId;

        if (poll != null)
        {
            Polls.Add(poll);
        }
    }
}