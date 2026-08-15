using System;
using System.IO;
using System.Security.Cryptography;

namespace Malco.Launcher
{
    internal sealed class InstallStateStore
    {
        private readonly string _installRoot;
        private readonly string _stateRoot;
        private readonly string _versionsRoot;
        private readonly string _stagingRoot;
        private readonly string _startupRoot;
        private readonly InstallStatePersistence _statePersistence;
        private readonly StartupArtifactStore _startupArtifacts;
        private readonly ReleaseStorageCleaner _releaseStorage;

        public InstallStateStore(string installRoot)
        {
            _installRoot = Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar);
            _stateRoot = LauncherPathGuard.Child(_installRoot, "state");
            _versionsRoot = LauncherPathGuard.Child(_installRoot, "versions");
            _stagingRoot = LauncherPathGuard.Child(_installRoot, "staging");
            _startupRoot = LauncherPathGuard.Child(_stateRoot, "startup");
            _statePersistence = new InstallStatePersistence(
                LauncherPathGuard.Child(_stateRoot, "install-state.json"));
            _releaseStorage = new ReleaseStorageCleaner(_versionsRoot, _stagingRoot);
            _startupArtifacts = new StartupArtifactStore(
                _startupRoot,
                _releaseStorage.VersionDirectory);
        }

        public string PolicyPath => LauncherPathGuard.Child(_installRoot, "launcher-policy.json");
        public string StagingRoot => _stagingRoot;
        public string StagedUpdatePath => LauncherPathGuard.Child(_stateRoot, "staged-update.json");

        public void ConfigurePolicy(LauncherPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            _statePersistence.ConfigurePolicy(policy);
            _startupArtifacts.ConfigurePolicy(policy);
            _releaseStorage.ConfigurePolicy(policy);
        }

        public void EnsureRoots()
        {
            LauncherPathGuard.RequireOrdinaryDirectory(_installRoot, "install root");
            var markerPath = LauncherPathGuard.Child(_installRoot, ".malco-install-root");
            LauncherPathGuard.RequireOrdinaryFile(markerPath, "install-root marker");
            var marker = File.ReadAllBytes(markerPath);
            var expectedMarker = System.Text.Encoding.ASCII.GetBytes("malco-install-root=1\r\n");
            if (!CryptographicOperations.FixedTimeEquals(marker, expectedMarker))
            {
                throw new InvalidDataException("The launcher is not running from an owned Malco install root.");
            }
            Directory.CreateDirectory(_stateRoot);
            Directory.CreateDirectory(_versionsRoot);
            Directory.CreateDirectory(_stagingRoot);
            Directory.CreateDirectory(_startupRoot);
            LauncherPathGuard.RequireOrdinaryDirectory(_stateRoot, "state root");
            LauncherPathGuard.RequireOrdinaryDirectory(_versionsRoot, "versions root");
            LauncherPathGuard.RequireOrdinaryDirectory(_stagingRoot, "staging root");
            LauncherPathGuard.RequireOrdinaryDirectory(_startupRoot, "startup-marker root");
            _startupArtifacts.CleanupLaunchAuthorizations();
        }

        public InstallState Load() => _statePersistence.Load();

        public void Save(InstallState state) => _statePersistence.Save(state);

        public string VersionDirectory(ReleaseReference reference) =>
            _releaseStorage.VersionDirectory(reference);

        public string MarkerPath(string activationId) => _startupArtifacts.MarkerPath(activationId);

        public string LaunchAuthorizationPath(string launchToken) =>
            _startupArtifacts.LaunchAuthorizationPath(launchToken);

        public void WriteLaunchAuthorization(
            string launchToken,
            ReleaseReference reference,
            bool requiredUpdateRecheck) =>
            _startupArtifacts.WriteLaunchAuthorization(launchToken, reference, requiredUpdateRecheck);

        public void DeleteLaunchAuthorization(string launchToken) =>
            _startupArtifacts.DeleteLaunchAuthorization(launchToken);

        public bool LaunchAuthorizationExists(string launchToken) =>
            _startupArtifacts.LaunchAuthorizationExists(launchToken);

        public StartupMarker TryReadMarker(string activationId) =>
            _startupArtifacts.TryReadMarker(activationId);

        public void DeleteMarker(string activationId) => _startupArtifacts.DeleteMarker(activationId);

        public string NewActivationId() => _startupArtifacts.NewActivationId();

        public void CleanupStaging(ReleaseVerifier verifier) => _releaseStorage.CleanupStaging(verifier);

        public void CleanupUnreferencedVersions(
            InstallState state,
            ReleaseVerifier verifier,
            Func<ReleaseReference, bool> isVersionInUse,
            ReleaseReference stagedUpdate = null) =>
            _releaseStorage.CleanupUnreferencedVersions(
                state,
                verifier,
                isVersionInUse,
                stagedUpdate);
    }
}
