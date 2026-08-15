using System;
using System.IO;

namespace Malco.Launcher
{
    internal sealed partial class StartupSupervisor
    {
        private const string MalcoMutexName = @"Local\Malco.Desktop.SingleInstance.v1";
        private readonly InstallStateStore _stateStore;
        private readonly ReleaseVerifier _verifier;
        private readonly LauncherPolicy _policy;
        private readonly string _uiLanguage;

        public StartupSupervisor(
            InstallStateStore stateStore,
            ReleaseVerifier verifier,
            LauncherPolicy policy,
            string uiLanguage)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _uiLanguage = uiLanguage ?? throw new ArgumentNullException(nameof(uiLanguage));
        }

        public void LaunchStable(
            ReleaseReference reference,
            bool requiredUpdateRecheck = false)
        {
            var activationId = _stateStore.NewActivationId();
            try
            {
                if (!LaunchAndAwaitHandshake(
                        reference,
                        activationId,
                        requiredUpdateRecheck: requiredUpdateRecheck))
                {
                    throw new InvalidDataException(
                        "Malco did not complete its startup handshake.");
                }
                UpdateInstalledProductVersion(reference);
            }
            finally
            {
                _stateStore.DeleteMarker(activationId);
            }
        }

        public void UpdateInstalledProductVersion(ReleaseReference reference)
        {
            var versionDirectory = _stateStore.VersionDirectory(reference);
            var envelope = _verifier.VerifyInstalledRelease(reference, versionDirectory);
            InstalledProductRegistration.TrySetVersion(envelope.Manifest.Version);
        }
    }
}
