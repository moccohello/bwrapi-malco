using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Malco.Telemetry
{
    internal sealed class AtomicTelemetryQueue
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private readonly string _path;
        private readonly TelemetryPolicy _policy;

        public AtomicTelemetryQueue(string path, TelemetryPolicy policy)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public void Append(IEnumerable<TelemetryEvent> incoming)
        {
            WithMutex(() =>
            {
                var events = ReadUnsafe();
                foreach (var item in incoming ?? Enumerable.Empty<TelemetryEvent>())
                {
                    if (item == null) continue;
                    var eventBytes = JsonSerializer.SerializeToUtf8Bytes(item);
                    if (eventBytes.Length <= _policy.MaxEventBytes) events.Add(item);
                }
                Trim(events);
                WriteUnsafe(events);
            });
        }

        public IReadOnlyList<TelemetryEvent> PeekBatch()
        {
            return PeekBatch(_policy.MaxBatchEvents);
        }

        public IReadOnlyList<TelemetryEvent> PeekBatch(int maximumEvents)
        {
            if (maximumEvents <= 0) return Array.Empty<TelemetryEvent>();
            var result = Array.Empty<TelemetryEvent>();
            WithMutex(() => result = ReadUnsafe().Take(Math.Min(maximumEvents, _policy.MaxBatchEvents)).ToArray());
            return result;
        }

        public void RemoveById(IEnumerable<string> ids)
        {
            var set = new HashSet<string>(ids ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (set.Count == 0) return;
            WithMutex(() =>
            {
                var events = ReadUnsafe();
                events.RemoveAll(item => item != null && set.Contains(item.EventId));
                WriteUnsafe(events);
            });
        }

        private List<TelemetryEvent> ReadUnsafe()
        {
            try
            {
                if (!File.Exists(_path)) return new List<TelemetryEvent>();
                if (new FileInfo(_path).Length > _policy.MaxQueueBytes) return new List<TelemetryEvent>();
                return JsonSerializer.Deserialize<List<TelemetryEvent>>(File.ReadAllText(_path), JsonOptions) ?? new List<TelemetryEvent>();
            }
            catch
            {
                return new List<TelemetryEvent>();
            }
        }

        private void Trim(List<TelemetryEvent> events)
        {
            while (events.Count > _policy.MaxPendingEvents) events.RemoveAt(0);
            while (events.Count > 0 && JsonSerializer.SerializeToUtf8Bytes(events).Length > _policy.MaxQueueBytes) events.RemoveAt(0);
        }

        private void WriteUnsafe(List<TelemetryEvent> events)
        {
            if (events.Count == 0)
            {
                TryDelete(_path);
                return;
            }
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(events);
            var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, _path, true);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        private bool WithMutex(Action action)
        {
            try
            {
                using (var mutex = new Mutex(false, _policy.QueueMutexName))
                {
                    var acquired = false;
                    try
                    {
                        try { acquired = mutex.WaitOne(_policy.MutexWaitMilliseconds); }
                        catch (AbandonedMutexException) { acquired = true; }
                        if (acquired) action();
                        return acquired;
                    }
                    finally
                    {
                        if (acquired) mutex.ReleaseMutex();
                    }
                }
            }
            catch
            {
                // Telemetry storage must never affect the host application.
                return false;
            }
        }

        private static bool TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return !File.Exists(path);
            }
            catch { return false; }
        }
    }
}
