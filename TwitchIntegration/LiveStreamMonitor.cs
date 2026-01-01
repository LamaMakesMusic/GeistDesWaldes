using Discord;
using GeistDesWaldes.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;

namespace GeistDesWaldes.TwitchIntegration
{
    public class LivestreamMonitor
    {
        private Task _updateLoopTask;
        private CancellationTokenSource _cancelUpdateLoopSource;

        private readonly Dictionary<string, StreamObject> _streamCache = new Dictionary<string, StreamObject>();


        public LivestreamMonitor()
        {

        }

        public void Start()
        {
            if (_updateLoopTask == null && _cancelUpdateLoopSource == null)
                _updateLoopTask = Task.Run(StreamUpdateLoop);
        }
        public void Stop()
        {
            _streamCache.Clear();
            _cancelUpdateLoopSource?.Cancel();
        }
        
        private async Task StreamUpdateLoop()
        {
            _cancelUpdateLoopSource = new CancellationTokenSource();

            TwitchIntegrationHandler.LogToMain(nameof(StreamUpdateLoop), "Started.", LogSeverity.Verbose);

            try
            {
                while (!_cancelUpdateLoopSource.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(ConfigurationHandler.Shared.LivestreamMonitorIntervalInMinutes), _cancelUpdateLoopSource.Token);

                        await UpdateStreams();
                    }
                    catch (Exception e)
                    {
                        if (e is TaskCanceledException t)
                            throw t;
                        else
                            TwitchIntegrationHandler.LogToMain(nameof(StreamUpdateLoop), string.Empty, LogSeverity.Error, exception: e);
                    }
                }
            }
            catch (TaskCanceledException)
            {

            }
            finally
            {
                _updateLoopTask = null;
                _cancelUpdateLoopSource = null;

                TwitchIntegrationHandler.LogToMain(nameof(StreamUpdateLoop), "Stopped.", LogSeverity.Warning);
            }
        }
        
        private async Task UpdateStreams()
        {
            List<string> userLogins = TwitchIntegrationHandler.Instance?.Clients.Keys.ToList() ?? new();

            if (userLogins.Count == 0)
                return;

            List<Stream> streamResponse = (await TwitchIntegrationHandler.ValidatedAPICall(TwitchIntegrationHandler.Instance.API.Helix.Streams.GetStreamsAsync(first: userLogins.Count, userLogins: userLogins)))?.Streams?.ToList() ?? new();

            foreach (string login in userLogins)
            {
                if (!_streamCache.TryGetValue(login, out StreamObject cachedStream))
                {
                    await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(UpdateStreams), $"Could not find {nameof(StreamObject)} for user '{login}'"));
                    continue;
                }
                if (!TwitchIntegrationHandler.Instance.Clients.TryGetValue(login, out TwitchIntegrationClient client))
                {
                    await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(UpdateStreams), $"Could not find {nameof(TwitchIntegrationClient)} for user '{login}'"));
                    continue;
                }

                Stream stream = null;
                for (int i = streamResponse.Count - 1; i >= 0; i--)
                {
                    if (streamResponse[i] == null || !streamResponse[i].UserLogin.Equals(login, StringComparison.OrdinalIgnoreCase))
                        continue;

                    stream = streamResponse[i];
                    streamResponse.RemoveAt(i);
                    break;
                }

                if (stream != null)
                {
                    cachedStream.UpdateContent(stream.Title, stream.GameId, stream.StartedAt);

                    if (cachedStream.IsOnline)
                    {
                        await client.OnStreamUpdate(cachedStream);
                    }
                    else
                    {
                        cachedStream.IsOnline = true;
                        await client.OnStreamOnline(cachedStream);
                    }
                }
                else if (cachedStream.IsOnline)
                {
                    await Launcher.Instance.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(UpdateStreams), "Could not get stream!"));

                    cachedStream.IsOnline = false;
                    await client.OnStreamOffline(cachedStream);
                }
            }
        }

        public StreamObject GetStream(string channelName)
        {
            if (_streamCache.TryGetValue(channelName, out StreamObject value))
                return value;

            return null;
        }
    
        public void AddCache(string channel)
        {
            if (_streamCache.ContainsKey(channel))
                return;

            _streamCache.Add(channel, new());
        }
        public void RemoveCache(string channel)
        {
            if (!_streamCache.ContainsKey(channel))
                return;

            _streamCache.Remove(channel);
        }
    }
}
