using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord;

namespace GeistDesWaldes.Polls;

[Serializable]
public class Poll
{
    private readonly object _locker = new();
    public ulong ChannelId;

    public string ChannelName;

    public string Description;
    public string Name;
    public int NameHash;

    public List<PollOption> PollOptions = new();
    public List<ulong> UsersVoted = new();

    public Poll()
    {
    }

    public Poll(string name, string description, IChannel channel, PollOption[] pollOptions)
    {
        SetName(name);

        Description = description;

        ChannelName = channel.Name;
        ChannelId = channel.Id;

        PollOptions.AddRange(pollOptions);
    }

    public void SetName(string name)
    {
        Name = name.ToLower();
        NameHash = Name.GetHashCode();
    }

    public PollHandler.VoteEvaluationResult TryVote(string message, ulong userId)
    {
        lock (_locker)
        {
            int pollHash = message.Trim().ToLower().GetHashCode();

            if (PollOptions.FirstOrDefault(p => p.IdentifierHash == pollHash) is PollOption option && option != default)
            {
                if (!UsersVoted.Contains(userId))
                {
                    option.Votes++;
                    UsersVoted.Add(userId);

                    return PollHandler.VoteEvaluationResult.Valid;
                }

                return PollHandler.VoteEvaluationResult.AlreadyVoted;
            }

            return PollHandler.VoteEvaluationResult.Invalid;
        }
    }

    public override string ToString()
    {
        return $"{HeaderToString()}\n{BodyToString()}";
    }

    public string HeaderToString()
    {
        return $"{Name} [{ChannelName}]";
    }

    public string BodyToString()
    {
        StringBuilder builder = new();

        if (!string.IsNullOrWhiteSpace($"'{Description}'"))
        {
            builder.AppendLine(Description);
        }

        lock (_locker)
        {
            PollOptions.Sort((p1, p2) => p2.Votes.CompareTo(p1.Votes));

            //builder.AppendLine($"{ReplyDictionary.ID,-3} | {ReplyDictionary.VOTES,-8} | {ReplyDictionary.DESCRIPTION,-24}");

            for (int i = 0; i < PollOptions?.Count; i++)
            {
                builder.AppendLine(PollOptions[i].ToString());
            }
        }

        return builder.ToString();
    }
}