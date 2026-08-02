using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace Malco.Shell
{
    internal sealed class GameDisplaySettingsReader : IDisposable
    {
        private static readonly Regex OriginalAspectRatioSetting = new Regex(
            "\\\"OriginalAspectRatio\\\"\\s*:\\s*(true|false)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500d);

        private readonly string _path;
        private readonly Action _changed;
        private readonly FileSystemWatcher _watcher;
        private Timer _debounceTimer;
        private int _originalAspectRatio;
        private int _readQueued;
        private int _dirtyRevision;
        private int _disposed;

        public GameDisplaySettingsReader(Action changed)
        {
            _changed = changed ?? throw new ArgumentNullException(nameof(changed));
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "StarCraft");
            _path = Path.Combine(directory, "CSettings.json");
            if (Directory.Exists(directory))
            {
                _watcher = new FileSystemWatcher(directory, "CSettings.json")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += OnChanged;
                _watcher.Created += OnChanged;
                _watcher.Renamed += OnRenamed;
            }
            QueueRead(TimeSpan.Zero);
        }

        public bool OriginalAspectRatio => Volatile.Read(ref _originalAspectRatio) != 0;

        private void OnChanged(object sender, FileSystemEventArgs args) => QueueRead(Debounce);
        private void OnRenamed(object sender, RenamedEventArgs args) => QueueRead(Debounce);

        private void QueueRead(TimeSpan dueTime)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            Interlocked.Increment(ref _dirtyRevision);
            ScheduleRead(dueTime);
        }

        private void ScheduleRead(TimeSpan dueTime)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            var timer = new Timer(OnDebounceElapsed, null, dueTime, Timeout.InfiniteTimeSpan);
            var previous = Interlocked.Exchange(ref _debounceTimer, timer);
            previous?.Dispose();
        }

        private void OnDebounceElapsed(object state)
        {
            if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _readQueued, 1) != 0) return;
            ThreadPool.QueueUserWorkItem(_ => ReadWorker());
        }

        private void ReadWorker()
        {
            var drainedRevision = Volatile.Read(ref _dirtyRevision);
            try
            {
                if (Volatile.Read(ref _disposed) != 0 || !File.Exists(_path)) return;
                var match = OriginalAspectRatioSetting.Match(File.ReadAllText(_path));
                if (!match.Success) return;
                var next = string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                var previous = Interlocked.Exchange(ref _originalAspectRatio, next);
                if (previous != next && Volatile.Read(ref _disposed) == 0) _changed();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally
            {
                Interlocked.Exchange(ref _readQueued, 0);
                if (Volatile.Read(ref _disposed) == 0 &&
                    Volatile.Read(ref _dirtyRevision) != drainedRevision)
                {
                    ScheduleRead(Debounce);
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            var timer = Interlocked.Exchange(ref _debounceTimer, null);
            timer?.Dispose();
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnChanged;
                _watcher.Created -= OnChanged;
                _watcher.Renamed -= OnRenamed;
                _watcher.Dispose();
            }
        }
    }
}
