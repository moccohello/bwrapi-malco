using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Malco.Configuration;
using Malco.Interop;
using Malco.Overlay;
using Malco.Shell.Input;
using Malco.Presentation.Scheduling;
using Malco.Data;

namespace Malco.Shell
{
    internal sealed partial class OverlayShellController : IDisposable
    {
        private const int WmShellWake = 0x8000 + 0x5A;
        private const int WsExAppWindow = 0x40000;
        private const int WsExTransparent = 0x20;
        private const int WsExNoActivate = 0x08000000;
        private const uint SwpFrameChanged = 0x0020;
        private const int ShellDirtyGeometry = 1;
        private const int ShellDirtyValidity = 2;
        private const int ShellDirtyForeground = 4;

        private readonly IOverlayShellViewPort _view;
        private readonly ISettingsShellPort _settings;
        private readonly IShellPresentationPort _presentation;
        private readonly HotkeyController _hotkey;
        private readonly IOverlayFramePump _framePump;
        private readonly ITrackedGameProcessSink _trackedGameProcessSink;
        private readonly StarCraftWindowTracker _windowTracker;
        private readonly GameDisplaySettingsReader _displaySettings;
        private readonly DispatcherTimer _watchdog;
        private readonly NativeMethods.WinEventDelegate _winEventCallback;

        private HwndSource _source;
        private IntPtr _handle;
        private GameWindowInfo _targetWindow;
        private bool _hasTrackedProcess;
        private int _lastTrackedProcessId;
        private string _lastTrackedProcessName;
        private DateTime? _lastTrackedProcessStartedAtUtc;
        private Process _trackedProcessLifetime;
        private int _trackedProcessLifetimeId;
        private DateTime? _trackedProcessLifetimeStartedAtUtc;
        private bool _trackedProcessExitShutdownRequested;
        private long _nextTargetAcquisitionTimestamp;
        private IntPtr _lastForegroundRoot;
        private IntPtr _systemWinEventHook;
        private IntPtr _objectWinEventHook;
        private IntPtr _eventTargetHandle;
        private int _eventTargetProcessId;
        private int _dirtyMask;
        private int _wakeQueued;
        private int _wakeFallbackPending;
        private int _geometryDirty;
        private int _validityDirty;
        private int _foregroundDirty;
        private int _appliedExtendedStyle = int.MinValue;
        private IntPtr _failedOwnerHandle;
        private long _nextOwnerRetryTimestamp;
        private bool _frameChangePending;
        private bool _hasAppliedTargetBounds;
        private Rectangle _lastAppliedTargetDeviceBounds;
        private uint _lastAppliedTargetDpi;
        private IntPtr _lastAppliedTargetMonitor;
        private OverlayRuntimeMode _lastAppliedBoundsMode;
        private bool _lastWatchdogTargetUsable;
        private bool _hasAppliedRuntimeMode;
        private bool _runtimeHooksDetached;
        private bool _hasLastKnownWorkArea;
        private Rect _lastKnownWorkAreaDip;
        private bool _disposed;

        public OverlayShellController(
            OverlayConfig config,
            IOverlayShellViewPort view,
            ISettingsShellPort settings,
            IShellPresentationPort presentation,
            HotkeyController hotkey,
            IOverlayFramePump framePump,
            ITrackedGameProcessSink trackedGameProcessSink)
        {
            _view = view;
            _settings = settings;
            _presentation = presentation;
            _hotkey = hotkey;
            _framePump = framePump ?? throw new ArgumentNullException(nameof(framePump));
            _trackedGameProcessSink = trackedGameProcessSink ?? throw new ArgumentNullException(nameof(trackedGameProcessSink));
            _windowTracker = new StarCraftWindowTracker(config);
            _displaySettings = new GameDisplaySettingsReader(OnDisplaySettingsChanged);
            _winEventCallback = OnWinEvent;
            _watchdog = new DispatcherTimer(DispatcherPriority.Background, view.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100d)
            };
            _watchdog.Tick += OnWatchdog;
            _watchdog.Start();
        }

        public OverlayRuntimeMode CurrentMode { get; private set; }
        public bool OriginalAspectRatio { get { return _displaySettings.OriginalAspectRatio; } }
        public bool HasUsableTarget { get { return HasUsableTargetWindow(); } }
        public void RefreshRuntimeMode()
        {
            ApplyRuntimeMode(_presentation.DesiredRuntimeMode);
        }

        public void NotifyDpiChanged()
        {
            Interlocked.Exchange(ref _geometryDirty, 1);
            _framePump.RequestFrame();
        }

        public void ApplyPendingTargetGeometry()
        {
            if (Interlocked.Exchange(ref _geometryDirty, 0) == 0 || _targetWindow == null) return;
            GameWindowInfo refreshed;
            if (!_windowTracker.TryRefreshGeometry(out refreshed)) return;
            if (!SetTrackedTarget(refreshed) || _settings.ShutdownRequested) return;
            ApplyRuntimeWindowBounds(CurrentMode);
        }

        public void ActivateTargetIfAvailable()
        {
            if (HasUsableTargetWindow()) NativeMethods.SetForegroundWindow(_targetWindow.Handle);
        }

        private void ApplyRuntimeMode(OverlayRuntimeMode mode)
        {
            if (_hasAppliedRuntimeMode && CurrentMode == mode) return;
            CurrentMode = mode;
            _hasAppliedRuntimeMode = true;
            ApplyRuntimeWindowBounds(mode);
            _presentation.ApplyRuntimeVisualState(mode, OriginalAspectRatio);
            ApplyWindowStacking();
        }
    }
}
