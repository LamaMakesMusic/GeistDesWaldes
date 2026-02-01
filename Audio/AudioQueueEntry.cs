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

    public readonly SourceOption Source;
    public readonly ICommandContext Context;
    public readonly string Path;
    
    public readonly CancellationTokenSource CancellationSource;


    public AudioQueueEntry(string path, SourceOption sourceType, ICommandContext context)
    {
        Path = path;
        Source = sourceType;

        Context = context;
        CancellationSource = new CancellationTokenSource();
    }
}