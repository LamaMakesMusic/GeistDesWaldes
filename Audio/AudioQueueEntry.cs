using System.Threading;
using Discord.Commands;

namespace GeistDesWaldes.Audio;

public class AudioQueueEntry
{
    public enum SourceOption
    {
        Local = 0,
        Web = 1
    }

    private readonly object _locker = new();
    public readonly CancellationTokenSource CancellationSource;
    private RuntimeResult _playResult;
    public ICommandContext Context;
    public string Path;

    public bool PlaybackStarted = false;

    public SourceOption Source;


    public AudioQueueEntry(string path, SourceOption sourceType, ICommandContext context)
    {
        Path = path;
        Source = sourceType;

        Context = context;
        CancellationSource = new CancellationTokenSource();

        _playResult = null;
    }

    public RuntimeResult PlayResult
    {
        get
        {
            lock (_locker)
            {
                return _playResult;
            }
        }
        set
        {
            lock (_locker)
            {
                _playResult = value;
            }
        }
    }
}