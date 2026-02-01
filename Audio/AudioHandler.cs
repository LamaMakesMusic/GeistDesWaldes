using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
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

    private const string AUDIO_DIRECTORY_NAME = "audio";
    private static readonly string[] _supportedAudioFileExtensions = [".mp3", ".ogg", ".wav"];

    private readonly ConcurrentQueue<AudioQueueEntry> _audioQueue = new();

    public readonly string AudioDirectoryPath;

    private IAudioClient _audioClient;

    private Task _audioQueueProcessor;
    private CancellationTokenSource _cancelQueueProcessorSource;
    private AudioOutStream _discordOutStream;

    private IGuildUser BotGuildUser => Server.Guild.GetUser(Launcher.Instance.DiscordClient.CurrentUser.Id);

    
    public AudioHandler(Server server) : base(server)
    {
        AudioDirectoryPath = Path.GetFullPath(Path.Combine(Server.ServerFilesDirectoryPath, AUDIO_DIRECTORY_NAME));
    }

    
    public override async Task OnServerStartUp()
    {
        await base.OnServerStartUp();

        await InitializeAudioHandler();
        EnsureAudioQueueProcessor();
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

    private void EnsureAudioQueueProcessor()
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
            const int INTERVAL_IN_SECONDS = 1;
            int exitInSeconds = ConfigurationHandler.Shared.AudioVoiceChannelIdleLeaveInSeconds;
            
            while (_audioQueueProcessor != null && !_cancelQueueProcessorSource.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(INTERVAL_IN_SECONDS), _cancelQueueProcessorSource.Token);

                // auto-leave timer
                if (_audioQueue.IsEmpty)
                {
                    if (HasActiveListeners())
                    {
                        exitInSeconds -= INTERVAL_IN_SECONDS;

                        if (exitInSeconds > 0)
                            continue;
                    }
                    
                    await LeaveVoiceChannel();
                    break;
                }
                
                // play all in queue
                while (_audioQueue.TryDequeue(out AudioQueueEntry nextEntry))
                {
                    if (nextEntry.CancellationSource.IsCancellationRequested)
                        continue;
                    
                    RuntimeResult result = await SayAndLogAudio(nextEntry);
                    await PostPlaybackResult(nextEntry, result);
                }
                
                // reset idle timer
                exitInSeconds = ConfigurationHandler.Shared.AudioVoiceChannelIdleLeaveInSeconds;
            }
        }
        catch (Exception ex)
        {
            if (ex is not TaskCanceledException)
                await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(ProcessAudioQueue), string.Empty, ex));
        }
        finally
        {
            _audioQueueProcessor = null;
            _cancelQueueProcessorSource = null;

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(ProcessAudioQueue), "Stopped."));
        }
    }

    private bool HasActiveListeners()
    {
        if (_audioClient == null)
            return false;
        
        if (BotGuildUser?.VoiceChannel is not { } activeChannel)
            return false;

        if (activeChannel is not SocketVoiceChannel activeSocketChannel)
            return false;

        return activeSocketChannel.ConnectedUsers.Count > 2;
    }

    private async Task PostPlaybackResult(AudioQueueEntry entry, RuntimeResult result)
    {
        if (entry.Context == null || result.IsSuccess)
            return;

        await Server.HandleFailedCommand(entry.Context, result);
    }

    
    private async Task<RuntimeResult> SayAndLogAudio(AudioQueueEntry entry)
    {
        try
        {
            IGuildUser botUser = BotGuildUser;
            
            if (botUser == null)
                throw new Exception("Could not get Bot User from Guild!");

            if (!TryGetVoiceChannelContext(botUser, entry, out IVoiceChannel channel))
                throw new Exception("Could not get VoiceChannel from Guild!");

            await EnsureVoiceChannelConnection(botUser, channel);
            
            if (_audioClient == null)
                throw new Exception("Could not get AudioClient!");

            await Server.LogHandler.Log(new LogMessage(LogSeverity.Info, nameof(SayAndLogAudio), $"In '{channel.Name}' ({channel.Guild.Name}): '{entry.Path}'"));

            _discordOutStream ??= _audioClient.CreatePCMStream(AudioApplication.Mixed, channel.Bitrate);

            using Process ffmpeg = CreateProcess(entry.Path);
            await ffmpeg.StandardOutput.BaseStream.CopyToAsync(_discordOutStream);
            await _discordOutStream.FlushAsync();
            
            return CustomRuntimeResult.FromSuccess();
        }
        catch (Exception e)
        {
            await Server.LogHandler.Log(new LogMessage(LogSeverity.Error, nameof(SayAndLogAudio), "Failed playing Audio File!", e));

            if (_discordOutStream != null)
            {
                await _discordOutStream.DisposeAsync();
                _discordOutStream = null;
            }
            
            if (_audioClient != null)
            {
                await _audioClient.StopAsync();
            }

            return CustomRuntimeResult.FromError("Failed Playing Audio File!");
        }
    }

    private bool TryGetVoiceChannelContext(IGuildUser botUser, AudioQueueEntry entry, out IVoiceChannel result)
    {
        result = entry.Context.User switch
        {
            MetaUser metaUser => (metaUser.OriginalUser as IGuildUser)?.VoiceChannel,
            IGuildUser guildUser => guildUser.VoiceChannel,
            _ => null
        };

        if (result != null)
            return true;  // found the channel in which the user of the command is in

        result = botUser?.VoiceChannel ?? Server.RuntimeConfig.DefaultBotVoiceChannel;

        if (result != null)
            return true; // found the channel the bot is currently in OR the default voice channel of the bot
        
        return TryFindActiveVoiceChannel(botUser, out result);
    }

    private bool TryFindActiveVoiceChannel(IGuildUser botUser, out IVoiceChannel result)
    {
        if (botUser != null)
        {
            foreach (SocketVoiceChannel svc in Server.Guild.VoiceChannels)
            {
                foreach (SocketGuildUser user in svc.ConnectedUsers)
                {
                    if (user.Id != botUser.Id)
                        continue;

                    result = svc;
                    return true;
                }
            }
        }

        result = null;
        return false;
    }

    private async Task EnsureVoiceChannelConnection(IGuildUser user, IVoiceChannel channel)
    {
        if (_audioClient != null && user.VoiceChannel != null && user.VoiceChannel.Id == channel.Id)
            return;
        
        if (_discordOutStream != null)
        {
            await _discordOutStream.DisposeAsync();
            _discordOutStream = null;
        }

        _audioClient = await channel.ConnectAsync();
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
                return CustomRuntimeResult.FromError(ReplyDictionary.PATH_MUST_NOT_END_ABOVE_START_DIRECTORY);

            if (!audioFile.Exists)
                return CustomRuntimeResult.FromError($"{ReplyDictionary.FILE_DOES_NOT_EXIST} ('{localPathOrUrl}')");
                
            localPathOrUrl = audioFile.FullName;
        }

        if (!_supportedAudioFileExtensions.Contains(Path.GetExtension(localPathOrUrl)))
            return CustomRuntimeResult.FromError(ReplyDictionary.FILE_TYPE_NOT_SUPPORTED);

        // Enqueue
        AudioQueueEntry entry = new(localPathOrUrl, source, context);
        _audioQueue.Enqueue(entry);

        if (_audioQueueProcessor == null)
            EnsureAudioQueueProcessor();
        
        await Server.LogHandler.Log(new LogMessage(LogSeverity.Verbose, nameof(QueueAudioFileAtPath), $"Enqueued audio '{entry}'. (Position {_audioQueue.Count})"));
        return CustomRuntimeResult.FromSuccess();
    }


    public async Task<RuntimeResult> LeaveVoiceChannel()
    {
        try
        {
            if (_discordOutStream != null)
            {
                await _discordOutStream.DisposeAsync();
                _discordOutStream = null;
            }
            
            if (BotGuildUser?.VoiceChannel is  { } activeVoiceChannel)
            {
                await activeVoiceChannel.DisconnectAsync();
            }
            
            if (_audioClient != null)
            {
                await _audioClient.StopAsync();
                _audioClient = null;
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
                        files = Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories).Where(s => _supportedAudioFileExtensions.Contains(Path.GetExtension(s), StringComparer.OrdinalIgnoreCase)).ToArray();
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


    private static Process CreateProcess(string path)
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