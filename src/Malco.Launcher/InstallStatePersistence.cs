using System;
using System.IO;

namespace Malco.Launcher
{
    internal sealed class InstallStatePersistence
    {
        private readonly string _statePath;
        private LauncherPolicy _policy;
        private long? _lastSavedGeneration;

        public InstallStatePersistence(string statePath)
        {
            _statePath = statePath;
        }

        public void ConfigurePolicy(LauncherPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public InstallState Load()
        {
            RequirePolicy();
            if (!File.Exists(_statePath))
            {
                throw new InvalidDataException("The installed launcher state is missing; repair or reinstall Malco.");
            }
            return ContractCodec.ParseState(
                ReleaseVerifier.ReadBoundedFile(_statePath, _policy.MaximumStateBytes),
                _policy);
        }

        public void Save(InstallState state)
        {
            RequirePolicy();
            if (state == null) throw new ArgumentNullException(nameof(state));
            var generation = state.Generation;
            if (_lastSavedGeneration.HasValue && _lastSavedGeneration.Value > generation)
            {
                generation = _lastSavedGeneration.Value;
            }
            if (generation == long.MaxValue)
            {
                throw new InvalidDataException("The install-state generation is exhausted.");
            }
            var persistedState = new InstallState(
                generation + 1,
                state.HighestAcceptedSequence,
                state.Current,
                state.LastKnownGood,
                state.Pending,
                state.LastRollback);
            var bytes = ContractCodec.SerializeState(persistedState);
            var temporaryPath = _statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                ContractCodec.ParseState(
                    ReleaseVerifier.ReadBoundedFile(temporaryPath, _policy.MaximumStateBytes),
                    _policy);
                if (File.Exists(_statePath))
                {
                    File.Replace(temporaryPath, _statePath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, _statePath, false);
                }
                _lastSavedGeneration = persistedState.Generation;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private void RequirePolicy()
        {
            if (_policy == null)
            {
                throw new InvalidOperationException("The launcher policy must be configured before state access.");
            }
        }
    }
}
