using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using Malco.Application.Contracts.Output;
using Malco.Data;

namespace Malco.Updates
{
    internal sealed class RequiredUpdateSessionMonitor : IOverlayStateCommitSink, IDisposable
    {
        private const int RequiredUpdateAvailableExitCode = 30;

        private readonly IOverlayStateCommitSource _source;
        private readonly string _launcherPath;
        private readonly string _currentManifestSha256;
        private readonly int _launchingProcessId;
        private readonly long _launchingProcessStartTimeUtcTicks;
        private readonly Dispatcher _dispatcher;
        private readonly Action _requestShutdown;
        private readonly object _sync = new object();
        private string _lastSessionEpoch = string.Empty;
        private long _lastSessionGeneration = long.MinValue;
        private bool _checkRunning;
        private long _pendingChecks;
        private bool _shutdownRequested;
        private bool _started;
        private bool _disposed;

        public RequiredUpdateSessionMonitor(
            IOverlayStateCommitSource source,
            string launcherPath,
            string currentManifestSha256,
            int launchingProcessId,
            long launchingProcessStartTimeUtcTicks,
            Dispatcher dispatcher,
            Action requestShutdown)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _launcherPath = Path.GetFullPath(
                launcherPath ?? throw new ArgumentNullException(nameof(launcherPath)));
            _currentManifestSha256 = currentManifestSha256 ??
                throw new ArgumentNullException(nameof(currentManifestSha256));
            _launchingProcessId = launchingProcessId;
            _launchingProcessStartTimeUtcTicks = launchingProcessStartTimeUtcTicks;
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _requestShutdown = requestShutdown ?? throw new ArgumentNullException(nameof(requestShutdown));
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(RequiredUpdateSessionMonitor));
                if (_started) return;
                _started = true;
            }
            try
            {
                _source.RegisterStateCommitSink(this);
            }
            catch
            {
                lock (_sync) _started = false;
                throw;
            }
        }

        public void MarkOverlayStateCommitted(OverlayReadModel state)
        {
            var semantic = state?.Semantic;
            if (semantic?.Snapshot == null || !semantic.Snapshot.IsInMatch)
            {
                return;
            }

            lock (_sync)
            {
                if (_disposed || _shutdownRequested ||
                    (semantic.SessionGeneration == _lastSessionGeneration &&
                     string.Equals(
                         semantic.SessionEpoch,
                         _lastSessionEpoch,
                         StringComparison.Ordinal)))
                {
                    return;
                }

                _lastSessionEpoch = semantic.SessionEpoch ?? string.Empty;
                _lastSessionGeneration = semantic.SessionGeneration;
                if (_checkRunning)
                {
                    _pendingChecks++;
                    return;
                }
                _checkRunning = true;
            }

            _ = Task.Run(CheckUntilCurrentAsync);
        }

        private async Task CheckUntilCurrentAsync()
        {
            await AwaitLaunchingProcessExitAsync().ConfigureAwait(false);
            while (true)
            {
                lock (_sync)
                {
                    if (_disposed)
                    {
                        _checkRunning = false;
                        return;
                    }
                }
                var required = await CheckRequiredUpdateAsync().ConfigureAwait(false);
                lock (_sync)
                {
                    if (_disposed)
                    {
                        _checkRunning = false;
                        return;
                    }
                    if (required)
                    {
                        _shutdownRequested = true;
                        _checkRunning = false;
                    }
                    else if (_pendingChecks > 0)
                    {
                        _pendingChecks--;
                        continue;
                    }
                    else
                    {
                        _checkRunning = false;
                        return;
                    }
                }

                if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
                {
                    _ = _dispatcher.BeginInvoke(_requestShutdown);
                }
                return;
            }
        }

        private async Task AwaitLaunchingProcessExitAsync()
        {
            if (_launchingProcessId <= 0 || _launchingProcessStartTimeUtcTicks <= 0)
            {
                return;
            }
            try
            {
                using (var process = Process.GetProcessById(_launchingProcessId))
                {
                    if (process.HasExited ||
                        process.StartTime.ToUniversalTime().Ticks != _launchingProcessStartTimeUtcTicks)
                    {
                        return;
                    }
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is System.ComponentModel.Win32Exception)
            {
            }
        }

        private async Task<bool> CheckRequiredUpdateAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _launcherPath,
                    WorkingDirectory = Path.GetDirectoryName(_launcherPath) ?? AppContext.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("--check-required-update");
                startInfo.ArgumentList.Add(_currentManifestSha256);
                using (var process = Process.Start(startInfo))
                {
                    if (process == null) return false;
                    await process.WaitForExitAsync().ConfigureAwait(false);
                    return process.ExitCode == RequiredUpdateAvailableExitCode;
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException ||
                exception is System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        public void Dispose()
        {
            var unregister = false;
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _pendingChecks = 0;
                unregister = _started;
                _started = false;
            }
            if (unregister) _source.UnregisterStateCommitSink(this);
        }
    }
}
