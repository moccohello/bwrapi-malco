using System;
using System.IO;
using System.Security.Cryptography;

namespace Malco.Launcher
{
    internal static partial class Program
    {
        private static int RunRequiredUpdateCheck(string currentManifestSha256)
        {
            var installRoot = Path.GetFullPath(AppContext.BaseDirectory);
            var stateStore = new InstallStateStore(installRoot);
            stateStore.EnsureRoots();

            LauncherPolicy policy;
            try
            {
                policy = ContractCodec.ParsePolicy(ReleaseVerifier.ReadBoundedFile(
                    stateStore.PolicyPath,
                    LauncherPolicy.BootstrapMaximumPolicyBytes));
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException ||
                exception is FormatException)
            {
                return (int)ExitCode.PolicyInvalid;
            }

            stateStore.ConfigurePolicy(policy);
            var verifier = new ReleaseVerifier(policy);
            InstallState state;
            try
            {
                state = stateStore.Load();
                if (state.Current == null ||
                    !string.Equals(
                        state.Current.ManifestSha256,
                        currentManifestSha256,
                        StringComparison.Ordinal))
                {
                    return (int)ExitCode.RequiredUpdateCheckUnavailable;
                }
                verifier.VerifyInstalledRelease(
                    state.Current,
                    stateStore.VersionDirectory(state.Current));
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException ||
                exception is CryptographicException)
            {
                return (int)ExitCode.RequiredUpdateCheckUnavailable;
            }

            VerifiedEnvelope latest;
            try
            {
                using (var installer = new ReleaseInstaller(policy, verifier, stateStore))
                {
                    latest = installer.FetchLatest();
                }
            }
            catch (Exception exception) when (IsUpdateFailure(exception))
            {
                return (int)ExitCode.RequiredUpdateCheckUnavailable;
            }

            if (latest.Manifest.Sequence < state.Current.Sequence)
            {
                return (int)ExitCode.Success;
            }
            if (latest.Manifest.Sequence == state.Current.Sequence)
            {
                return string.Equals(
                    latest.ManifestSha256,
                    state.Current.ManifestSha256,
                    StringComparison.Ordinal)
                    ? (int)ExitCode.Success
                    : (int)ExitCode.RequiredUpdateCheckUnavailable;
            }

            return string.Equals(
                latest.Manifest.UpdatePolicy,
                "required",
                StringComparison.Ordinal)
                ? (int)ExitCode.RequiredUpdateAvailable
                : (int)ExitCode.Success;
        }
    }
}
