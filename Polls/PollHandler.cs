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
        private List<ChannelPoll> _currentPolls = new List<ChannelPoll>();
        private readonly object _pollsLock = new object();

        private string _pollList = "-";
        private readonly object _pollListLock = new object();

        private const string POLLS_FILE_NAME = "Polls";

        
        public PollHandler(Server server) : base(server)
        {

        }

        internal override void OnServerStart(object source, EventArgs e)
        {
            base.OnServerStart(source, e);

            Task.Run(InitializePollHandler).GetAwaiter().GetResult();
        }
        internal override void OnCheckIntegrity(object source, EventArgs e)
        {
            base.OnCheckIntegrity(source, e);

            Task.Run(CheckIntegrity).GetAwaiter().GetResult();
        }

        private async Task InitializePollHandler()
        {
            await GenericXmlSerializer.EnsurePathExistance(_Server.LogHandler, _Server.ServerFilesDirectoryPath, POLLS_FILE_NAME, _currentPolls);
            await LoadPollsFromFile();
        }
        private async Task CheckIntegrity()
        {
            var builder = new StringBuilder("Polls ERROR:\n");
            int startLength = builder.Length;

            foreach (var channelPoll in _currentPolls)
            {
                var subBuilder = new StringBuilder($"...[{channelPoll.ChannelId}]");
                int subStartLength = subBuilder.Length;

                if (channelPoll.Polls?.Count > 0)
                {
                    if (channelPoll.Polls?.Count > _Server.Config.GeneralSettings.MaxPollsPerChannel)
                        subBuilder.Append(" | Poll Limit exceeded!");
                    else
                    {
                        for (int i = 0; i < channelPoll.Polls.Count; i++)
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
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(CheckIntegrity), builder.ToString()));
            else
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Polls OK."), (int)ConsoleColor.DarkGreen);
        }


        public async Task<CustomRuntimeResult<Poll>> StartPoll(string name, string description, IChannel channel, string[] options)
        {
            try
            {
                int pollCount = await GetChannelPollCount(channel.Id);

                if (pollCount >= _Server.Config.GeneralSettings.MaxPollsPerChannel)
                    return CustomRuntimeResult<Poll>.FromError(await ReplyDictionary.ReplaceStringInvariantCase(ReplyDictionary.POLL_MAX_POLLS_X_PER_CHANNEL_REACHED, "{x}", _Server.Config.GeneralSettings.MaxPollsPerChannel.ToString()));


                var getPollResult = await GetPoll(name, channel.Id);
                if (getPollResult.IsSuccess)
                    return CustomRuntimeResult<Poll>.FromError(ReplyDictionary.POLL_WITH_NAME_ALREADY_EXISTS);


                List<PollOption> voteOptions = new List<PollOption>();
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
                    if (_currentPolls.FirstOrDefault(c => c.ChannelId == poll.ChannelId) is ChannelPoll cp && cp != null)
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

        public async Task<CustomRuntimeResult<Poll>> GetPoll(string pollName, ulong channelId)
        {
            try
            {
                int nameHash = pollName.ToLower().GetHashCode();

                lock (_pollsLock)
                {
                    if (_currentPolls.FirstOrDefault(c => c.ChannelId == channelId) is ChannelPoll channelPoll)
                    {
                        if (channelPoll.Polls?.Count > 0)
                        {
                            Poll poll = channelPoll.Polls.FirstOrDefault(p => p.NameHash == nameHash);

                            if (poll != default)
                                return CustomRuntimeResult<Poll>.FromSuccess(value: poll);
                        }
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

                if (_currentPolls.FirstOrDefault(c => c.ChannelId == channelId) is ChannelPoll channelPoll
                && channelPoll.Polls.FirstOrDefault(p => p.NameHash == nameHash) is Poll poll)
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
                    if (_currentPolls.FirstOrDefault(c => c.ChannelId == channelId) is ChannelPoll channelPoll)
                        pollCount = channelPoll.Polls.Count;
                }

                return pollCount;
            });
        }

        public async Task<VoteEvaluationResult> TryVote(IUserMessage message, int prefixPosition)
        {
            try
            {
                var preconResult = await _Server.CommandService.Search("polls vote").Commands[0].CheckPreconditionsAsync(new CommandContext(Launcher.Instance.DiscordClient, message), _Server.Services);

                lock (_pollsLock)
                {
                    if (_currentPolls.FirstOrDefault(c => c.ChannelId == message.Channel.Id) is ChannelPoll channelPoll)
                    {
                        for (int i = 0; i < channelPoll.Polls?.Count; i++)
                        {
                            var result = channelPoll.Polls[i].TryVote(message.Content.Substring(prefixPosition), message.Author.Id);

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
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(TryVote), "", e));
                return VoteEvaluationResult.Error;
            }

        }


        public string GetPollList()
        {
            lock (_pollListLock)
                return _pollList;
        }

        public Task UpdatePollList()
        {
            return Task.Run(() =>
            {
                StringBuilder builder = new StringBuilder();

                lock (_pollsLock)
                {
                    for (int i = 0; i < _currentPolls?.Count; i++)
                    {
                        if (_currentPolls[i].Polls?.Count > 0)
                        {
                            for (int j = 0; j < _currentPolls[i].Polls.Count; j++)
                                builder.AppendLine(_currentPolls[i].Polls[j].HeaderToString());
                        }
                    }
                }

                lock (_pollListLock)
                    _pollList = builder.Length > 0 ? builder.ToString() : "-";
            });
        }


        public Task SavePollsToFile()
        {
            return GenericXmlSerializer.SaveAsync<List<ChannelPoll>>(_Server.LogHandler, _currentPolls, POLLS_FILE_NAME, _Server.ServerFilesDirectoryPath);
        }
        public async Task LoadPollsFromFile()
        {
            List<ChannelPoll> loadedPolls = await GenericXmlSerializer.LoadAsync<List<ChannelPoll>>(_Server.LogHandler, POLLS_FILE_NAME, _Server.ServerFilesDirectoryPath);

            if (loadedPolls == default)
                await _Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(loadedPolls), "Loaded Polls == DEFAULT"));
            else
            {
                //Ensure Hash for externally added Polls
                for (int i = 0; i < loadedPolls?.Count; i++)
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
