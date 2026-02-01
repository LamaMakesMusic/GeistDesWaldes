using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using GeistDesWaldes.Attributes;
using GeistDesWaldes.Configuration;
using GeistDesWaldes.Dictionaries;
using GeistDesWaldes.Misc;
using GeistDesWaldes.Users;

namespace GeistDesWaldes.Audio;

public class AudioHandler : BaseHandler
{
    public override int Priority => -13;
    
    public const string AUDIO_DIRECTORY_NAME = "audio";
    public static readonly string[] SupportedAudioFileExtensions = [".mp3", ".ogg", ".wav"];

    private readonly ConcurrentQueue<AudioQueueEntry> _audioQueue = new();

    public readonly string AudioDirectoryPath;

    private IAudioClient _audioClient;

    private Task _audioQueueProcessor;
    private CancellationTokenSource _cancelQueueProcessorSource;
    private AudioOutStream _discordOutStream;

    public AudioHandler(Server server) : base(server)
    {
        AudioDirectoryPath = Path.GetFullPath(Path.Combine(Server.ServerFilesDirectoryPath, AUDIO_DIRECTORY_NAME));
    }

    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();

        await InitializeAudioHandler();
        StartAudioQueueProcessing();
    }

    public override async Task OnServerShutdown()
    {
        await base.OnServerShutdown();

        _cancelQueueProcessorSource?.Cancel();

        _audioQueue.Clear();

        await LeaveVoiceChannel();
    }

    public override async Task OnCheckIntegrity()
    {
        await base.OnCheckIntegrity();

        await CheckIntegrity();
    }

    private async Task CheckIntegrity()
    {
        if (_audioQueueProcessor == null)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(CheckIntegrity), "Audio Handler ERROR: Queue Processor not running!"));
        }

        await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(CheckIntegrity), "Audio Handler OK."), (int)ConsoleColor.DarkGreen);
    }

    private async Task InitializeAudioHandler()
    {
        await GenericXmlSerializer.EnsurePathExistence<object>(Server.LogHandler, AudioDirectoryPath);
    }

    private void StartAudioQueueProcessing()
    {
        if (_audioQueueProcessor == null && _cancelQueueProcessorSource == null)
        {
            _audioQueueProcessor = Task.Run(ProcessAudioQueue);
        }
    }

    private async Task ProcessAudioQueue()
    {
        _cancelQueueProcessorSource = new CancellationTokenSource();

        await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(ProcessAudioQueue), "Started."));

        try
        {
            int loopDelayInMs = 1000;
            int exitTimerInMs = -1;

            while (_audioQueueProcessor != null && !_cancelQueueProcessorSource.IsCancellationRequested)
            {
                if (!_audioQueue.IsEmpty)
                {
                    exitTimerInMs = ConfigurationHandler.Shared.VoiceChannelNoUsersExitInSeconds * 1000;

                    if (_audioQueue.TryDequeue(out AudioQueueEntry nextEntry))
                    {
                        if (!nextEntry.CancellationSource.IsCancellationRequested)
                        {
                            nextEntry.PlaybackStarted = true;
                            RuntimeResult playResult = await SayAndLogAudio(nextEntry);
                            nextEntry.PlayResult = playResult;
                            nextEntry.PlaybackStarted = false;
                        }
                    }
                    else
                    {
                        await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(ProcessAudioQueue), "Could not dequeue next entry!"));
                    }
                }
                else if (exitTimerInMs > 0)
                {
                    exitTimerInMs -= loopDelayInMs;

                    if (exitTimerInMs < 1)
                    {
                        await LeaveVoiceChannel();
                    }
                }

                await Task.Delay(loopDelayInMs, _cancelQueueProcessorSource.Token);
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _audioQueueProcessor = null;
            _cancelQueueProcessorSource = null;

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(ProcessAudioQueue), "Stopped."));
        }
    }


    private async Task<RuntimeResult> SayAndLogAudio(AudioQueueEntry entry)
    {
        try
        {
            IGuildUser botUser = Server.Guild.GetUser(Launcher.Instance.DiscordClient.CurrentUser.Id);
            if (botUser == null)
            {
                throw new Exception("Could not get bot User from Guild!");
            }

            IVoiceChannel channel;

            if (entry.Context.User is MetaUser metaUser)
            {
                channel = (metaUser.OriginalUser as IGuildUser)?.VoiceChannel;
            }
            else
            {
                channel = (entry.Context.User as IGuildUser)?.VoiceChannel;
            }

            if (channel == null)
            {
                ulong id = (botUser.VoiceChannel ?? Server.RuntimeConfig.DefaultBotVoiceChannel)?.Id ?? 0;
                channel = id == 0 ? null : Server.Guild.GetVoiceChannel(id);

                if (channel == null || !(channel as SocketVoiceChannel).ConnectedUsers.Any(u => u.Id != botUser.Id))
                {
                    foreach (SocketVoiceChannel svc in Server.Guild.VoiceChannels)
                    {
                        if (!svc.ConnectedUsers.Any(u => u.Id != botUser.Id))
                        {
                            continue;
                        }

                        channel = svc;
                        break;
                    }

                    if (channel == null)
                    {
                        return CustomRuntimeResult.FromError(ReplyDictionary.NO_VOICE_CHANNEL_FOUND);
                    }
                }
            }

            if (_audioClient == null || botUser.VoiceChannel == null || botUser.VoiceChannel.Id != channel.Id)
            {
                if (_discordOutStream != null)
                {
                    _discordOutStream.Dispose();
                    _discordOutStream = null;
                }

                float connectTimeoutInMs = 8000;
                Task<IAudioClient> connectTask = channel.ConnectAsync();

                while (!connectTask.IsCompleted)
                {
                    if (connectTimeoutInMs < 1)
                    {
                        throw new TimeoutException($"Timed out connecting to voice channel '{channel.Name}' for audio '{entry.Path}'...!");
                    }

                    await Task.Delay(500);
                    connectTimeoutInMs -= 500;
                }

                if (connectTask.IsFaulted)
                {
                    throw connectTask.Exception;
                }

                _audioClient = await connectTask;
            }


            if (_audioClient == null)
            {
                return CustomRuntimeResult.FromError($"AudioClient not found! => '{channel.Name}' ({channel.Guild.Name}): '{entry.Path}'");
            }

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SayAndLogAudio), $"In '{channel.Name}' ({channel.Guild.Name}): '{entry.Path}'"));

            if (_discordOutStream == null)
            {
                _discordOutStream = _audioClient.CreatePCMStream(AudioApplication.Mixed, channel.Bitrate);
            }


            using Process ffmpeg = CreateProcess(entry.Path);
            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(_discordOutStream);
            await _discordOutStream.FlushAsync();


            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(SayAndLogAudio), "Failed playing Audio File!", e));

            if (_audioClient != null)
            {
                await _audioClient.StopAsync();
            }

            if (_discordOutStream != null)
            {
                _discordOutStream.Dispose();
                _discordOutStream = null;
            }

            return CustomRuntimeResult.FromError("Failed Playing Audio File!");
        }
    }

    public async Task<RuntimeResult> QueueAudioFileAtPath(string localPathOrUrl, ICommandContext context)
    {
        AudioQueueEntry.SourceOption source = AudioQueueEntry.SourceOption.Local;
        localPathOrUrl = localPathOrUrl.Trim().Replace("http://", "").Replace("https://", "").Replace("www.", "");

        if (localPathOrUrl.StartsWith("cdn.discordapp.com/attachments/"))
        {
            localPathOrUrl = $"https://{localPathOrUrl}";
            source = AudioQueueEntry.SourceOption.Web;
        }
        else
        {
            FileInfo audioFile = new(Path.GetFullPath(localPathOrUrl, AudioDirectoryPath));
            localPathOrUrl = audioFile.FullName;

            if (!localPathOrUrl.StartsWith(AudioDirectoryPath))
            {
                return CustomRuntimeResult.FromError(ReplyDictionary.PATH_MUST_NOT_END_ABOVE_START_DIRECTORY);
            }

            if (audioFile.Exists)
            {
                localPathOrUrl = audioFile.FullName;
            }
            else
            {
                return CustomRuntimeResult.FromError($"{ReplyDictionary.FILE_DOES_NOT_EXIST} ('{localPathOrUrl}')");
            }
        }

        if (!SupportedAudioFileExtensions.Contains(Path.GetExtension(localPathOrUrl)))
        {
            return CustomRuntimeResult.FromError(ReplyDictionary.FILE_TYPE_NOT_SUPPORTED);
        }

        return await QueueAndAwaitAudio(new AudioQueueEntry(localPathOrUrl, source, context));
    }

    private async Task<RuntimeResult> QueueAndAwaitAudio(AudioQueueEntry entry)
    {
        try
        {
            _audioQueue.Enqueue(entry);

            // Extra 1000ms since we include the time the SayAndLogAudio() waits in between some operations
            int totalTimeoutMs = 1000 + ConfigurationHandler.Shared.AudioCommandTimeOutInSeconds * 1000 * Math.Max(1, _audioQueue.Count);
            int timeOutInMs = totalTimeoutMs;

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(QueueAndAwaitAudio), $"Enqueued audio '{entry}'. (Position {_audioQueue.Count})"));

            if (_audioQueueProcessor == null)
            {
                StartAudioQueueProcessing();
            }

            while (entry.PlayResult == null)
            {
                await Task.Delay(500);

                if (!entry.PlaybackStarted)
                {
                    timeOutInMs -= 500;

                    if (timeOutInMs < 1)
                    {
                        entry.CancellationSource.Cancel();
                        throw new TimeoutException($"Timed out ({totalTimeoutMs * 0.001} seconds) waiting for '{entry}'.");
                    }
                }
            }

            return entry.PlayResult;
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }


    public async Task<RuntimeResult> LeaveVoiceChannel()
    {
        try
        {
            if (_audioClient != null)
            {
                await _audioClient.StopAsync();
                _audioClient = null;
            }

            if (_discordOutStream != null)
            {
                _discordOutStream.Dispose();
                _discordOutStream = null;
            }

            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            return CustomRuntimeResult.FromError(e.ToString());
        }
    }

    public async Task<string[]> GetAllFilesInPaths(string[] relativeAudioFileOrDirectoryPaths, string parentDirectoryFullPath)
    {
        var result = new List<string>();

        foreach (string path in relativeAudioFileOrDirectoryPaths)
        {
            try
            {
                if (path.IndexOf("http", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    result.Add(path);
                    continue;
                }

                string fullPath = string.IsNullOrWhiteSpace(path) ? parentDirectoryFullPath : Path.Combine(parentDirectoryFullPath, path);
                string[] files = [fullPath];


                // i.a. get all files in the directory
                try
                {
                    FileAttributes attr = File.GetAttributes(fullPath);

                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        files = Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories).Where(s => SupportedAudioFileExtensions.Contains(Path.GetExtension(s), StringComparer.OrdinalIgnoreCase)).ToArray();
                    }
                }
                catch (FileNotFoundException)
                {
                    await Server.LogHandler.Log(new LogMessage(LogSeverity.Warning, nameof(GetAllFilesInPaths), $"{ReplyDictionary.FILE_DOES_NOT_EXIST}: '{fullPath}'!"));
                    continue;
                }


                if (files.Length > 0)
                {
                    result.AddRange(files);
                }
            }
            catch (Exception e)
            {
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(GetAllFilesInPaths), "", e));
            }
        }

        return result.ToArray();
    }


    private Process CreateProcess(string path)
    {
        string logLevel;
        if (Launcher.LogLevel == 0 || Launcher.LogLevel == 1)
        {
            logLevel = "fatal";
        }
        else if (Launcher.LogLevel == 2)
        {
            logLevel = "warning";
        }
        else if (Launcher.LogLevel == 3)
        {
            logLevel = "info";
        }
        else if (Launcher.LogLevel == 4 || Launcher.LogLevel == 5)
        {
            logLevel = "verbose";
        }
        else
        {
            logLevel = "info";
        }

        Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel {logLevel} -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true
        });

        return process;
    }
}