using System;
using System.Threading;
using System.Windows;
using Malco.Bootstrap;
using Malco.Configuration.Models;
using Malco.Localization;
using Malco.Shell.Control;

namespace Malco
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = @"Local\Malco.Desktop.SingleInstance.v1";

        [STAThread]
        private static int Main(string[] args)
        {
            InstalledLaunchAuthorization installedLayout = null;
            var installed = InstalledLaunchAuthorization.IsInstalledVersionLayout();
            StartupHandshakeReporter startupHandshake = null;
            if (args.Length == 1 && string.Equals(args[0], "--quit", StringComparison.Ordinal))
            {
                return (int)MalcoControlProtocol.RequestQuit(SingleInstanceMutexName);
            }
            if (installed)
            {
                if ((args.Length != 2 && args.Length != 4) ||
                    !string.Equals(args[0], "--launch-token", StringComparison.Ordinal))
                {
                    return (int)MalcoControlExitCode.Usage;
                }
                if (args.Length == 4 &&
                    (!string.Equals(args[2], "--startup-token", StringComparison.Ordinal) ||
                     !StartupHandshakeReporter.TryCreate(args[3], out startupHandshake)))
                {
                    return (int)MalcoControlExitCode.Usage;
                }
                if (!InstalledLaunchAuthorization.TryConsume(args[1], out installedLayout))
                {
                    return (int)MalcoControlExitCode.Usage;
                }
            }
            else if (args.Length != 0)
            {
                return (int)MalcoControlExitCode.Usage;
            }

            bool releaseCreated = true;
            using (var releaseMutex = installed
                ? new Mutex(true, installedLayout.ReleaseMutexName, out releaseCreated)
                : null)
            {
                if (!releaseCreated) return (int)MalcoControlExitCode.Refused;
                bool createdNew;
                using (var mutex = new Mutex(true, SingleInstanceMutexName, out createdNew))
                {
                    if (!createdNew)
                    {
                        // A mutex collision alone does not prove that a healthy Malco
                        // primary or control server owns it. Do not report a verified
                        // successful launch when no primary handshake occurred.
                        return (int)MalcoControlExitCode.Refused;
                    }

                    var app = new System.Windows.Application
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown
                    };
                    using (var bootstrapper = new OverlayBootstrapper(installedLayout))
                    {
                        var window = bootstrapper.CreateWindow();
                        if (startupHandshake != null)
                        {
                            startupHandshake.Attach(window);
                        }
                        app.MainWindow = window;
                        app.Run(window);
                    }
                }
            }
            return (int)MalcoControlExitCode.Success;
        }

    }

}
