using System;
using System.IO;
using System.Security.Cryptography;

namespace Malco.Launcher
{
    internal static partial class Program
    {
        private static string FindAcceptedManifestHash(InstallState state)
        {
            foreach (var reference in new[]
            {
                state.Current,
                state.LastKnownGood,
                state.Pending?.Candidate,
                state.Pending?.PreviousCurrent,
                state.LastRollback?.From,
                state.LastRollback?.To
            })
            {
                if (reference != null && reference.Sequence == state.HighestAcceptedSequence)
                {
                    return reference.ManifestSha256;
                }
            }
            return null;
        }

        private static bool IsLaunchFailure(Exception exception) =>
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is InvalidDataException ||
            exception is CryptographicException ||
            exception is System.ComponentModel.Win32Exception ||
            exception is InvalidOperationException;

        private static bool IsUpdateFailure(Exception exception) =>
            IsLaunchFailure(exception) ||
            exception is System.Net.Http.HttpRequestException ||
            exception is System.Threading.Tasks.TaskCanceledException;

        private static bool IsDeferredUpdateCheckFailure(Exception exception) =>
            exception is System.Net.Http.HttpRequestException ||
            exception is System.Threading.Tasks.TaskCanceledException;

        private static void ShowFailure(ExitCode result)
        {
            var key = result switch
            {
                ExitCode.PolicyInvalid => "policy",
                ExitCode.StateInvalid => "state",
                ExitCode.NoValidRelease => "release",
                ExitCode.StartupFailed => "startup",
                ExitCode.RequiredUpdateFailed => "required",
                ExitCode.UnexpectedFailure => "unexpected",
                _ => null
            };
            var language = _language ?? LauncherLanguage.Resolve(Path.GetFullPath(AppContext.BaseDirectory));
            var message = key == null ? null : language.FailureMessage(key);
            if (message != null)
            {
                LauncherDialog.ShowError("Malco Launcher", message, language);
            }
        }
    }
}
