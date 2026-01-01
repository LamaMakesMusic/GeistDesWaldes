using Discord.Commands;
using System.Threading;

namespace GeistDesWaldes.Audio
{
    public class AudioQueueEntry
    {
        public readonly CancellationTokenSource CancellationSource;
        public ICommandContext Context;

        public bool PlaybackStarted = false;
        public string Path;

        public SourceOption Source;

        public RuntimeResult PlayResult {
            get {
                lock (_locker)
                    return _playResult;
            }
            set {
                lock (_locker)
                    _playResult = value;
            }
        }
        private RuntimeResult _playResult;
        
        private readonly object _locker = new object();



        public AudioQueueEntry(string path, SourceOption sourceType, ICommandContext context)
        {
            Path = path;
            Source = sourceType;

            Context = context;
            CancellationSource = new CancellationTokenSource();
            
            _playResult = null;
        }

        public enum SourceOption
        {
            Local = 0,
            Web = 1
        }
    }
}
