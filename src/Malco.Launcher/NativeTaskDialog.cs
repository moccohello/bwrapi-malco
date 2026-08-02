using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Malco.Launcher
{
    internal enum NativeTaskDialogNotification
    {
        Created = 0,
        Navigated = 1,
        ButtonClicked = 2,
        Destroyed = 5
    }

    internal readonly struct NativeTaskDialogButton
    {
        public NativeTaskDialogButton(int id, string text)
        {
            Id = id;
            Text = text ?? string.Empty;
        }

        public int Id { get; }
        public string Text { get; }
    }

    internal sealed class NativeTaskDialog : IDisposable
    {
        public const int CancelButtonId = 2;
        public const int CloseDialog = 0;
        public const int KeepDialogOpen = 1;

        private const uint WmClose = 0x0010;
        private const uint WmUser = 0x0400;
        private const uint TdmNavigatePage = WmUser + 101;
        private const uint TdmSetMarqueeProgressBar = WmUser + 103;
        private const uint TdmSetProgressBarState = WmUser + 104;
        private const uint TdmSetProgressBarRange = WmUser + 105;
        private const uint TdmSetProgressBarPosition = WmUser + 106;
        private const uint TdmSetProgressBarMarquee = WmUser + 107;
        private const uint TdmSetElementText = WmUser + 108;
        private const uint TdmEnableButton = WmUser + 111;
        private const int ContentElement = 0;
        private const int ProgressStateNormal = 1;
        private readonly Func<NativeTaskDialogNotification, int, int> _notification;
        private readonly TaskDialogCallback _callback;
        private readonly IntPtr _callbackPointer;
        private readonly List<NativeTaskDialogPage> _pages = new List<NativeTaskDialogPage>();
        private IntPtr _window;
        private IntPtr _icon;
        private bool _disposed;

        public NativeTaskDialog(Func<NativeTaskDialogNotification, int, int> notification)
        {
            _notification = notification ?? throw new ArgumentNullException(nameof(notification));
            _callback = OnCallback;
            _callbackPointer = Marshal.GetFunctionPointerForDelegate(_callback);
            var executable = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(executable) && File.Exists(executable))
            {
                ExtractIconEx(executable, 0, out var largeIcon, out var smallIcon, 1);
                _icon = largeIcon != IntPtr.Zero ? largeIcon : smallIcon;
                var unusedIcon = _icon == largeIcon ? smallIcon : largeIcon;
                if (unusedIcon != IntPtr.Zero)
                {
                    DestroyIcon(unusedIcon);
                }
            }
        }

        public NativeTaskDialogPage CreatePage(
            string instruction,
            string content,
            IReadOnlyList<NativeTaskDialogButton> buttons,
            int defaultButton,
            bool showProgress)
        {
            ThrowIfDisposed();
            var page = new NativeTaskDialogPage(
                "Malco",
                instruction,
                content,
                buttons,
                defaultButton,
                showProgress,
                _callbackPointer,
                _icon);
            _pages.Add(page);
            return page;
        }

        public void Show(NativeTaskDialogPage page)
        {
            ThrowIfDisposed();
            if (page == null) throw new ArgumentNullException(nameof(page));
            var result = TaskDialogIndirect(page.Configuration, out _, out _, out _);
            if (result < 0)
            {
                throw new ExternalException(
                    "Windows could not display the Malco dialog.",
                    result);
            }
        }

        public void Navigate(NativeTaskDialogPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            Send(TdmNavigatePage, IntPtr.Zero, page.Configuration);
        }

        public void EnableButton(int buttonId, bool enabled)
        {
            Send(TdmEnableButton, new IntPtr(buttonId), enabled ? new IntPtr(1) : IntPtr.Zero);
        }

        public void SetContent(string text)
        {
            WithNativeString(text, pointer => Send(TdmSetElementText, new IntPtr(ContentElement), pointer));
        }

        public void ShowMarquee()
        {
            Send(TdmSetMarqueeProgressBar, new IntPtr(1), IntPtr.Zero);
            Send(TdmSetProgressBarMarquee, new IntPtr(1), new IntPtr(28));
        }

        public void ShowPercentage(int percentage)
        {
            var value = Math.Max(0, Math.Min(100, percentage));
            Send(TdmSetProgressBarMarquee, IntPtr.Zero, IntPtr.Zero);
            Send(TdmSetMarqueeProgressBar, IntPtr.Zero, IntPtr.Zero);
            Send(TdmSetProgressBarState, new IntPtr(ProgressStateNormal), IntPtr.Zero);
            Send(TdmSetProgressBarRange, IntPtr.Zero, new IntPtr((100 << 16) | 0));
            Send(TdmSetProgressBarPosition, new IntPtr(value), IntPtr.Zero);
        }

        public void Close()
        {
            var window = _window;
            if (window != IntPtr.Zero)
            {
                PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var page in _pages)
            {
                page.Dispose();
            }
            _pages.Clear();
            if (_icon != IntPtr.Zero)
            {
                DestroyIcon(_icon);
                _icon = IntPtr.Zero;
            }
            GC.KeepAlive(_callback);
        }

        private int OnCallback(IntPtr window, uint notification, IntPtr wParam, IntPtr lParam, IntPtr data)
        {
            try
            {
                var value = (NativeTaskDialogNotification)notification;
                if (value == NativeTaskDialogNotification.Created ||
                    value == NativeTaskDialogNotification.Navigated)
                {
                    _window = window;
                }
                else if (value == NativeTaskDialogNotification.Destroyed)
                {
                    _window = IntPtr.Zero;
                }
                return _notification(value, wParam.ToInt32());
            }
            catch
            {
                return KeepDialogOpen;
            }
        }

        private void Send(uint message, IntPtr wParam, IntPtr lParam)
        {
            var window = _window;
            if (window != IntPtr.Zero)
            {
                SendMessage(window, message, wParam, lParam);
            }
        }

        private static void WithNativeString(string value, Action<IntPtr> action)
        {
            var pointer = Marshal.StringToHGlobalUni(value ?? string.Empty);
            try
            {
                action(pointer);
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeTaskDialog));
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int TaskDialogCallback(
            IntPtr window,
            uint notification,
            IntPtr wParam,
            IntPtr lParam,
            IntPtr callbackData);

        [DllImport("comctl32.dll", PreserveSig = true)]
        private static extern int TaskDialogIndirect(
            IntPtr taskConfig,
            out int button,
            out int radioButton,
            [MarshalAs(UnmanagedType.Bool)] out bool verificationFlagChecked);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(
            string file,
            int iconIndex,
            out IntPtr largeIcon,
            out IntPtr smallIcon,
            uint iconCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        internal sealed class NativeTaskDialogPage : IDisposable
        {
            [Flags]
            private enum TaskDialogFlags : uint
            {
                UseMainIconHandle = 0x0002,
                AllowCancellation = 0x0008,
                ShowMarqueeProgressBar = 0x0400,
                SizeToContent = 0x01000000
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct TaskDialogButton
            {
                public int Id;
                public IntPtr Text;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct TaskDialogConfig
            {
                public uint Size;
                public IntPtr Parent;
                public IntPtr Instance;
                public TaskDialogFlags Flags;
                public uint CommonButtons;
                public IntPtr WindowTitle;
                public IntPtr MainIcon;
                public IntPtr MainInstruction;
                public IntPtr Content;
                public uint ButtonCount;
                public IntPtr Buttons;
                public int DefaultButton;
                public uint RadioButtonCount;
                public IntPtr RadioButtons;
                public int DefaultRadioButton;
                public IntPtr VerificationText;
                public IntPtr ExpandedInformation;
                public IntPtr ExpandedControlText;
                public IntPtr CollapsedControlText;
                public IntPtr FooterIcon;
                public IntPtr Footer;
                public IntPtr Callback;
                public IntPtr CallbackData;
                public uint Width;
            }

            private readonly List<IntPtr> _strings = new List<IntPtr>();
            private IntPtr _buttons;
            private IntPtr _configuration;

            public NativeTaskDialogPage(
                string windowTitle,
                string instruction,
                string content,
                IReadOnlyList<NativeTaskDialogButton> buttons,
                int defaultButton,
                bool showProgress,
                IntPtr callback,
                IntPtr icon)
            {
                var flags = TaskDialogFlags.AllowCancellation | TaskDialogFlags.SizeToContent;
                if (showProgress) flags |= TaskDialogFlags.ShowMarqueeProgressBar;
                if (icon != IntPtr.Zero) flags |= TaskDialogFlags.UseMainIconHandle;

                var config = new TaskDialogConfig
                {
                    Size = (uint)Marshal.SizeOf<TaskDialogConfig>(),
                    Flags = flags,
                    WindowTitle = AddString(windowTitle),
                    MainIcon = icon,
                    MainInstruction = AddString(instruction),
                    Content = AddString(content),
                    DefaultButton = defaultButton,
                    Callback = callback
                };
                if (buttons != null && buttons.Count > 0)
                {
                    var size = Marshal.SizeOf<TaskDialogButton>();
                    _buttons = Marshal.AllocHGlobal(size * buttons.Count);
                    for (var index = 0; index < buttons.Count; index++)
                    {
                        var button = new TaskDialogButton
                        {
                            Id = buttons[index].Id,
                            Text = AddString(buttons[index].Text)
                        };
                        Marshal.StructureToPtr(button, IntPtr.Add(_buttons, index * size), false);
                    }
                    config.ButtonCount = (uint)buttons.Count;
                    config.Buttons = _buttons;
                }

                _configuration = Marshal.AllocHGlobal(Marshal.SizeOf<TaskDialogConfig>());
                Marshal.StructureToPtr(config, _configuration, false);
            }

            public IntPtr Configuration => _configuration;

            public void Dispose()
            {
                if (_configuration != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_configuration);
                    _configuration = IntPtr.Zero;
                }
                if (_buttons != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_buttons);
                    _buttons = IntPtr.Zero;
                }
                foreach (var pointer in _strings)
                {
                    Marshal.FreeHGlobal(pointer);
                }
                _strings.Clear();
            }

            private IntPtr AddString(string value)
            {
                var pointer = Marshal.StringToHGlobalUni(value ?? string.Empty);
                _strings.Add(pointer);
                return pointer;
            }
        }
    }

    internal static class NativeMessageDialog
    {
        private const uint ErrorIcon = 0x00000010;
        private const uint OkButton = 0x00000000;

        public static void ShowError(string title, string message, string closeText)
        {
            try
            {
                using (var dialog = new NativeTaskDialog((notification, button) => NativeTaskDialog.CloseDialog))
                using (var page = dialog.CreatePage(
                    title,
                    message,
                    new[] { new NativeTaskDialogButton(1001, closeText) },
                    1001,
                    showProgress: false))
                {
                    dialog.Show(page);
                }
            }
            catch
            {
                MessageBox(IntPtr.Zero, message, title, OkButton | ErrorIcon);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
    }
}
