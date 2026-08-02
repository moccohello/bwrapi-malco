using Malco.Application.Contracts.Input;
using Malco.Application.Contracts.Projection;
using Malco.Data;

namespace Malco.Application.Projection
{
    internal readonly struct ProjectionPresentationState
    {
        public ProjectionPresentationState(
            bool isUsable,
            bool isAuthoritativeClear,
            ProjectionClearReason clearReason,
            string sessionEpoch,
            long sessionGeneration,
            long demandEpoch,
            long presentationRevision,
            bool hasGameFrame,
            int gameFrame,
            int viewportMapX,
            int viewportMapY)
        {
            IsUsable = isUsable;
            IsAuthoritativeClear = isAuthoritativeClear;
            ClearReason = clearReason;
            SessionEpoch = sessionEpoch ?? string.Empty;
            SessionGeneration = sessionGeneration;
            DemandEpoch = demandEpoch;
            PresentationRevision = presentationRevision;
            HasGameFrame = hasGameFrame;
            GameFrame = gameFrame;
            ViewportMapX = viewportMapX;
            ViewportMapY = viewportMapY;
        }

        public bool IsUsable { get; }
        public bool IsAuthoritativeClear { get; }
        public ProjectionClearReason ClearReason { get; }
        public string SessionEpoch { get; }
        public long SessionGeneration { get; }
        public long DemandEpoch { get; }
        public long PresentationRevision { get; }
        public bool HasGameFrame { get; }
        public int GameFrame { get; }
        public int ViewportMapX { get; }
        public int ViewportMapY { get; }
    }

    internal sealed class ProjectionPresentationAdapter
    {
        private readonly IProjectionMailboxReader _reader;
        private ProjectionControlState _control;
        private ProjectionPresentationState _retained;
        private bool _hasControl;
        private bool _hasRetained;

        public ProjectionPresentationAdapter(IProjectionMailboxReader reader)
        {
            _reader = reader;
        }

        public void UpdateControl(in ProjectionControlState control)
        {
            if (!_hasControl ||
                _control.SessionGeneration != control.SessionGeneration ||
                _control.DemandEpoch != control.DemandEpoch ||
                !string.Equals(_control.SessionEpoch, control.SessionEpoch, System.StringComparison.Ordinal))
            {
                _hasRetained = false;
            }

            _control = control;
            _hasControl = true;
            if (control.IsAuthoritativeClear || !control.IsDemanded)
            {
                _hasRetained = false;
            }
        }

        public ProjectionPresentationState ResolveLatest()
        {
            if (!_hasControl)
            {
                return default;
            }

            if (_control.IsAuthoritativeClear || !_control.IsDemanded)
            {
                return new ProjectionPresentationState(
                    false,
                    _control.IsAuthoritativeClear,
                    _control.ClearReason,
                    _control.SessionEpoch,
                    _control.SessionGeneration,
                    _control.DemandEpoch,
                    _control.ProjectionRevision.Value,
                    false,
                    0,
                    0,
                    0);
            }

            ProjectionSample sample;
            if (_reader != null && _reader.TryReadLatest(out sample) &&
                string.Equals(sample.SessionEpoch, _control.SessionEpoch, System.StringComparison.Ordinal) &&
                sample.SessionGeneration == _control.SessionGeneration &&
                sample.DemandEpoch == _control.DemandEpoch)
            {
                if (sample.IsAuthoritativeClear)
                {
                    _hasRetained = false;
                    return new ProjectionPresentationState(
                        false,
                        true,
                        sample.ClearReason,
                        sample.SessionEpoch,
                        sample.SessionGeneration,
                        sample.DemandEpoch,
                        sample.ProjectionPresentationRevision.Value,
                        false,
                        0,
                        0,
                        0);
                }

                if (sample.IsUsable &&
                    sample.Status == ProviderStatus.Ready)
                {
                    // The mailbox owns presentation readiness for this exact
                    // generation/epoch. Control delivery can lag the ready
                    // sample and must not collapse all retained spatial HUD.
                    _retained = new ProjectionPresentationState(
                        true,
                        false,
                        ProjectionClearReason.None,
                        sample.SessionEpoch,
                        sample.SessionGeneration,
                        sample.DemandEpoch,
                        sample.ProjectionPresentationRevision.Value,
                        sample.HasGameFrame,
                        sample.GameFrame,
                        sample.ViewportMapX,
                        sample.ViewportMapY);
                    _hasRetained = true;
                }
                else
                {
                    if (_hasRetained &&
                        _retained.SessionGeneration == sample.SessionGeneration &&
                        _retained.DemandEpoch == sample.DemandEpoch &&
                        string.Equals(
                            _retained.SessionEpoch,
                            sample.SessionEpoch,
                            System.StringComparison.Ordinal))
                    {
                        return _retained;
                    }
                    return new ProjectionPresentationState(
                        false,
                        false,
                        ProjectionClearReason.None,
                        sample.SessionEpoch,
                        sample.SessionGeneration,
                        sample.DemandEpoch,
                        sample.ProjectionPresentationRevision.Value,
                        sample.HasGameFrame,
                        sample.GameFrame,
                        0,
                        0);
                }
            }

            if (_hasRetained &&
                _retained.SessionGeneration == _control.SessionGeneration &&
                _retained.DemandEpoch == _control.DemandEpoch &&
                string.Equals(_retained.SessionEpoch, _control.SessionEpoch, System.StringComparison.Ordinal))
            {
                return _retained;
            }

            return new ProjectionPresentationState(
                false,
                false,
                ProjectionClearReason.None,
                _control.SessionEpoch,
                _control.SessionGeneration,
                _control.DemandEpoch,
                _control.ProjectionRevision.Value,
                false,
                0,
                0,
                0);
        }

    }
}
