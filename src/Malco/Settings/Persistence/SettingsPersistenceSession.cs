using System;
using System.Threading;
using Malco.Configuration;
using Malco.Configuration.Models;
using Malco.Settings.Contracts;
using Malco.Settings.Controller;

namespace Malco.Settings.Persistence
{
    internal sealed class SettingsPersistenceSession : IDisposable
    {
        private const int MaxAutosaveRetryAttempts = 3;
        private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(750d);
        private static readonly TimeSpan AutosaveRetryDelay = TimeSpan.FromSeconds(2d);

        private readonly object _stateSync = new object();
        private readonly object _operationGate = new object();
        private readonly object _flushGate = new object();
        private readonly SettingsController _controller;
        private readonly HudLayoutFileStore _store;
        private readonly TimeSpan _debounce;
        private Timer _debounceTimer;
        private long _savedRevision;
        private SettingsFlushResult? _lastFlushResult;
        private int _autosaveRetryAttempts;
        private bool _autosaveCallbackActive;
        private bool _editorActive;
        private bool _disposed;

        public SettingsPersistenceSession(SettingsController controller, HudLayoutFileStore store)
            : this(controller, store, DefaultDebounce)
        {
        }

        internal SettingsPersistenceSession(
            SettingsController controller,
            HudLayoutFileStore store,
            TimeSpan debounce)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _debounce = debounce > TimeSpan.Zero ? debounce : DefaultDebounce;
        }

        public long SavedRevision
        {
            get
            {
                lock (_stateSync)
                {
                    return _savedRevision;
                }
            }
        }

        public bool IsDirty
        {
            get { return _controller.EditRevision > SavedRevision; }
        }

        public bool HasPendingAutosave
        {
            get
            {
                lock (_stateSync)
                {
                    return _autosaveCallbackActive || _debounceTimer != null;
                }
            }
        }

        public SettingsFlushResult? LastFlushResult
        {
            get
            {
                lock (_stateSync)
                {
                    return _lastFlushResult;
                }
            }
        }

        public SettingsEditResult ApplyEdit(SettingsEdit edit)
        {
            lock (_operationGate)
            {
                ThrowIfDisposed();
                var result = _controller.ApplyEdit(edit);
                if (result.Changed)
                {
                    lock (_stateSync)
                    {
                        _lastFlushResult = null;
                        if (_editorActive && result.Revision > _savedRevision)
                        {
                            _autosaveRetryAttempts = 0;
                            ArmDebounceLocked(_debounce);
                        }
                    }
                }

                return result;
            }
        }

        public void SetEditorActive(bool active)
        {
            lock (_operationGate)
            {
                ThrowIfDisposed();
                var currentRevision = _controller.EditRevision;
                lock (_stateSync)
                {
                    _editorActive = active;
                    if (!active)
                    {
                        CancelDebounceLocked();
                        _autosaveRetryAttempts = 0;
                    }
                    else if (currentRevision > _savedRevision)
                    {
                        _autosaveRetryAttempts = 0;
                        ArmDebounceLocked(_debounce);
                    }
                }
            }
        }

        public SettingsFlushResult TryFlush(SettingsFlushReason reason)
        {
            ThrowIfDisposed();
            SettingsFlushResult result;
            lock (_flushGate)
            {
                ThrowIfDisposed();
                result = TryFlushLocked(reason);
                lock (_stateSync)
                {
                    _lastFlushResult = result;
                    if (result.Succeeded)
                    {
                        _autosaveRetryAttempts = 0;
                    }
                }
            }

            return result;
        }

        private SettingsFlushResult TryFlushLocked(SettingsFlushReason reason)
        {
            var capture = _controller.Capture();
            LayoutSaveResult? recoveredSave = null;
            if (_store.IsWriteBlocked)
            {
                var recovery = _store.RecoverAndSave(capture.Snapshot);
                if (!recovery.Succeeded)
                {
                    return new SettingsFlushResult(
                        recovery.Status == LayoutSaveStatus.WriteBlocked
                            ? SettingsFlushStatus.RecoveryRequired
                            : SettingsFlushStatus.Failed,
                        reason,
                        SavedRevision,
                        capture.Revision,
                        recovery.Message);
                }
                recoveredSave = recovery;
            }
            lock (_stateSync)
            {
                if (!recoveredSave.HasValue && capture.Revision <= _savedRevision)
                {
                    return new SettingsFlushResult(
                        SettingsFlushStatus.NoChanges,
                        reason,
                        _savedRevision,
                        capture.Revision,
                        "Settings are already saved.");
                }
            }

            var save = recoveredSave ?? _store.Save(capture.Snapshot);
            if (!save.Succeeded)
            {
                return new SettingsFlushResult(
                    save.Status == LayoutSaveStatus.WriteBlocked
                        ? SettingsFlushStatus.RecoveryRequired
                        : SettingsFlushStatus.Failed,
                    reason,
                    SavedRevision,
                    capture.Revision,
                    save.Message);
            }

            var latestRevision = _controller.EditRevision;
            lock (_stateSync)
            {
                if (capture.Revision > _savedRevision)
                {
                    _savedRevision = capture.Revision;
                }

                if (_editorActive && latestRevision > _savedRevision)
                {
                    _autosaveRetryAttempts = 0;
                    ArmDebounceLocked(_debounce);
                }
            }

            return new SettingsFlushResult(
                SettingsFlushStatus.Saved,
                reason,
                capture.Revision,
                capture.Revision,
                save.Message);
        }

        public void Dispose()
        {
            lock (_operationGate)
            {
                lock (_stateSync)
                {
                    if (!_disposed)
                    {
                        _disposed = true;
                        _editorActive = false;
                        CancelDebounceLocked();
                    }
                }
            }

            lock (_flushGate)
            {
                // Wait for an already-started save to finish. TryFlush checks the
                // disposed flag again after entering this gate, so a queued flush
                // cannot begin a file replacement after this point.
            }
        }

        private void OnDebounceElapsed(object state)
        {
            lock (_stateSync)
            {
                if (_disposed || !_editorActive)
                {
                    CancelDebounceLocked();
                    return;
                }

                CancelDebounceLocked();
                _autosaveCallbackActive = true;
            }

            try
            {
                var result = TryFlush(SettingsFlushReason.Autosave);
                if (!result.Succeeded)
                {
                    ScheduleAutosaveRetry();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                lock (_stateSync)
                {
                    _autosaveCallbackActive = false;
                }
            }
        }

        private void ScheduleAutosaveRetry()
        {
            var editRevision = _controller.EditRevision;
            lock (_stateSync)
            {
                if (_disposed || !_editorActive || editRevision <= _savedRevision ||
                    _autosaveRetryAttempts >= MaxAutosaveRetryAttempts)
                {
                    return;
                }

                _autosaveRetryAttempts++;
                ArmDebounceLocked(AutosaveRetryDelay);
            }
        }

        private void ArmDebounceLocked(TimeSpan dueTime)
        {
            CancelDebounceLocked();
            _debounceTimer = new Timer(OnDebounceElapsed, null, dueTime, Timeout.InfiniteTimeSpan);
        }

        private void CancelDebounceLocked()
        {
            var timer = _debounceTimer;
            _debounceTimer = null;
            if (timer != null)
            {
                timer.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            lock (_stateSync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(SettingsPersistenceSession));
                }
            }
        }
    }
}
