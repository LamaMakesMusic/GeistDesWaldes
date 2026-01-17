using Discord;
using Discord.Commands;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeistDesWaldes.Polls
{
    public class PollHandler : BaseHandler
    {
        private List<ChannelPoll> _currentPolls = [];
        private readonly object _pollsLock = new object();

        private string _pollList = "-";
        private readonly object _pollListLock = new object();

        private const string POLLS_FILE_NAME = "Polls";

        
        public PollHandler(Server server) : base(server)
        {

        }

        public override async Task OnServerStartUp()
        {
            await base.OnServerStartUp();
            await InitializePollHandler();
        }
        
        public override async Task OnCheckIntegrity()
        {
            await base.OnCheckIntegrity();
            await CheckIntegrity();
        }

        private async Task InitializePollHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance(Server.LogHandler, Server.ServerFilesDirectoryPath, POLLS_FILE_NAME, _currentPolls);
            await LoadPollsFromFile();
        }
        private async Task CheckIntegrity()
        {
            StringBuilder builder = new("Polls ERROR:\n");
            int startLength = builder.Length;

            foreach (ChannelPoll channelPoll in _currentPolls)
            {
                StringBuilder subBuilder = new($"...[{channelPoll.ChannelId}]");
                int subStartLength = subBuilder.Length;

                if (channelPoll.Polls?.Count > 0)
                {
                    if (channelPoll.Polls?.Count > Server.Config.GeneralSettings.MaxPollsPerChannel)
                        subBuilder.Append(" | Poll Limit exceeded!");
                    else
                    {
                        for (int i = 0; i < channelPoll.Polls?.Count; i++)
                        {
                            if (channelPoll.Polls[i].PollOptions?.Count > 0)
                            {
                                for (int j = 0; j < channelPoll.Polls[i].PollOptions.Count; j++)
                                {
                                    if (string.IsNullOrEmpty(channelPoll.Polls[i].PollOptions[j].Identifier))
                                        subBuilder.Append($" | Poll Option ID missing! [{j}]");
                                }
                            }
                            else
                                subBuilder.Append(" | No Poll Options found!");
                        }
                    }
                }
                else
                    subBuilder.Append(" | Poll List is empty!");


                if (subBuilder.Length > subStartLength)
                    builder.AppendLine(subBuilder.ToString());
            }


            if (builder.Length > startLength)
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
            else
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Polls OK."), (int)ConsoleColor.DarkGreen);
        }


        public async Task<CustomRuntimeResult<Poll>> StartPoll(string name, string description, IChannel channel, string[] options)
        {
            try
            {
                int pollCount = await GetChannelPollCount(channel.Id);

                if (pollCount >= Server.Config.GeneralSettings.MaxPollsPerChannel)
                    return CustomRuntimeResult<Poll>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.POLL_MAX_POLLS_X_PER_CHANNEL_REACHED, "{x}", Server.Config.GeneralSettings.MaxPollsPerChannel.ToString()));


                CustomRuntimeResult<Poll> getPollResult = GetPoll(name, channel.Id);
                if (getPollResult.IsSuccess)
                    return CustomRuntimeResult<Poll>.FromError(ReplyDictionary.POLL_WITH_NAME_ALREADY_EXISTS);


                List<PollOption> voteOptions = [];
                for (int i = 0; i < options?.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(options[i]))
                        voteOptions.Add(new PollOption(options[i], $"{await Utility.IndexToLetter(i)}{pollCount + 1}"));
                }

                if (voteOptions.Count < 1)
                    return CustomRuntimeResult<Poll>.FromError(ReplyDictionary.POLL_HAS_NO_OPTIONS);


                Poll poll = new Poll(name, description, channel, voteOptions.ToArray());

                lock (_pollsLock)
                {
                    if (_currentPolls.FirstOrDefault(c => c.ChannelId == poll.ChannelId) is { } cp)
                        cp.Polls.Add(poll);
                    else
                        _currentPolls.Add(new ChannelPoll(poll.ChannelId, poll));
                }

                await SavePollsToFile();

                await UpdatePollList();

                return CustomRuntimeResult<Poll>.FromSuccess(value: poll);
            }
            catch (Exception e)
            {
                return CustomRuntimeResult<Poll>.FromError(e.ToString());
            }
        }

        public CustomRuntimeResult<Poll> GetPoll(string pollName, ulong channelId)
        {
            try
            {
                int nameHash = pollName.ToLower().GetHashCode();

                lock (_pollsLock)
                {
                    if (_currentPolls.FirstOrDefault(c => c.ChannelId == channelId) is { Polls.Count: > 0 } channelPoll)
                    {
                        Poll poll = channelPoll.Polls.FirstOrDefault(p => p.NameHash == nameHash);

                        if (poll != null)
                            return CustomRuntimeResult<Poll>.FromSuccess(value: poll);
                    }
                }

                return CustomRuntimeResult<Poll>.FromError(ReplyDictionary.POLL_NOT_FOUND);
            }
            catch (Exception e)
            {
                return CustomRuntimeResult<Poll>.FromError(e.ToString());
            }
        }

        public async Task<CustomRuntimeResult<Poll>> StopPoll(string pollName, ulong channelId)
        {
            try
            {
                int nameHash = pollName.ToLower().GetHashCode();

                if (_currentPolls.FirstOrDefault(c => c.ChannelId == channelId) is { } channelPoll
                && channelPoll.Polls.FirstOrDefault(p => p.NameHash == nameHash) is { } poll)
                {
                    lock (_pollsLock)
                    {
                        channelPoll.Polls.Remove(poll);

                        if (channelPoll.Polls.Count < 1)
                            _currentPolls.Remove(channelPoll);
                    }

                    await SavePollsToFile();

                    await UpdatePollList();

                    return CustomRuntimeResult<Poll>.FromSuccess(value: poll);
                }
                else
                    return CustomRuntimeResult<Poll>.FromError(ReplyDictionary.POLL_NOT_FOUND);
            }
            catch (Exception e)
            {
                return CustomRuntimeResult<Poll>.FromError(e.ToString());
            }
        }

        public Task<int> GetChannelPollCount(ulong channelId)
        {
            return Task.Run(() =>
            {
                int pollCount = 0;

                lock (_pollsLock)
                {
                    if (_currentPolls.FirstOrDefault(c => c.ChannelId == channelId) is { } channelPoll)
                        pollCount = channelPoll.Polls.Count;
                }

                return pollCount;
            });
        }

        public async Task<VoteEvaluationResult> TryVote(IUserMessage message, int prefixPosition)
        {
            try
            {
                PreconditionResult preconResult = await Server.CommandService.Search("polls vote").Commands[0].CheckPreconditionsAsync(new CommandContext(Launcher.Instance.DiscordClient, message), Server.Services);

                lock (_pollsLock)
                {
                    if (_currentPolls.FirstOrDefault(c => c.ChannelId == message.Channel.Id) is { } channelPoll)
                    {
                        for (int i = 0; i < channelPoll.Polls?.Count; i++)
                        {
                            VoteEvaluationResult result = channelPoll.Polls[i].TryVote(message.Content.Substring(prefixPosition), message.Author.Id);

                            if (result != VoteEvaluationResult.Invalid)
                            {
                                if (preconResult.IsSuccess)
                                    return result;
                                else
                                    return VoteEvaluationResult.NoPermission;
                            }
                        }
                    }
                }

                return VoteEvaluationResult.Invalid;
            }
            catch (Exception e)
            {
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(TryVote), "", e));
                return VoteEvaluationResult.Error;
            }

        }


        public string GetPollList()
        {
            lock (_pollListLock)
                return _pollList;
        }

        private Task UpdatePollList()
        {
            StringBuilder builder = new();

            lock (_pollsLock)
            {
                foreach (ChannelPoll poll in _currentPolls)
                {
                    if (poll?.Polls == null || poll.Polls.Count == 0)
                        continue;
                    
                    foreach (Poll p in poll.Polls)
                    {
                        builder.AppendLine(p.HeaderToString());
                    }
                }
            }

            lock (_pollListLock)
            {
                _pollList = builder.Length > 0 ? builder.ToString() : "-";
            }

            return Task.CompletedTask;
        }


        private Task SavePollsToFile()
        {
            return GenericXmlSerializer.SaveAsync<List<ChannelPoll>>(Server.LogHandler, _currentPolls, POLLS_FILE_NAME, Server.ServerFilesDirectoryPath);
        }

        private async Task LoadPollsFromFile()
        {
            List<ChannelPoll> loadedPolls = await GenericXmlSerializer.LoadAsync<List<ChannelPoll>>(Server.LogHandler, POLLS_FILE_NAME, Server.ServerFilesDirectoryPath);

            if (loadedPolls == null)
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(loadedPolls), "Loaded Polls == DEFAULT"));
            else
            {
                //Ensure Hash for externally added Polls
                for (int i = 0; i < loadedPolls.Count; i++)
                {
                    for (int j = 0; j < loadedPolls[i]?.Polls?.Count; j++)
                    {
                        loadedPolls[i].Polls[j].SetName(loadedPolls[i].Polls[j].Name);

                        for (int k = 0; k < loadedPolls[i].Polls[j].PollOptions?.Count; k++)
                            loadedPolls[i].Polls[j].PollOptions[k].SetIdentifier(loadedPolls[i].Polls[j].PollOptions[k].Identifier);
                    }
                }

                lock (_pollsLock)
                    _currentPolls = loadedPolls;
            }

            await UpdatePollList();
        }

        public enum VoteEvaluationResult
        {
            Error = -2,
            Invalid = -1,
            Valid = 0,
            AlreadyVoted = 1,
            NoPermission = 2
        }
    }
}
