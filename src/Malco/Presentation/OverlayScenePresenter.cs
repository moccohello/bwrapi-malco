using Malco.Data;

namespace Malco.Presentation
{
    internal readonly struct OverlaySceneRoutingDecision
    {
        public OverlaySceneRoutingDecision(
            string sessionEpoch,
            long sessionGeneration,
            bool generationChanged,
            bool semanticChanged,
            bool commandsChanged)
        {
            SessionEpoch = sessionEpoch ?? string.Empty;
            SessionGeneration = sessionGeneration;
            GenerationChanged = generationChanged;
            SemanticChanged = semanticChanged;
            CommandsChanged = commandsChanged;
        }

        public string SessionEpoch { get; }
        public long SessionGeneration { get; }
        public bool GenerationChanged { get; }
        public bool SemanticChanged { get; }
        public bool CommandsChanged { get; }
    }

    internal sealed class OverlayScenePresenter
    {
        private long _sessionGeneration = long.MinValue;
        private string _sessionEpoch = string.Empty;
        private SemanticSnapshotState _semantic;
        private CommandProjectionState _commands;

        public long SessionGeneration => _sessionGeneration == long.MinValue ? 0 : _sessionGeneration;
        public string SessionEpoch => _sessionEpoch;

        public OverlaySceneRoutingDecision Evaluate(
            SemanticSnapshotState semantic,
            CommandProjectionState commands)
        {
            var generation = semantic != null ? semantic.SessionGeneration : 0;
            var sessionEpoch = semantic != null ? semantic.SessionEpoch : string.Empty;
            var generationChanged = _sessionGeneration != generation ||
                                    !string.Equals(_sessionEpoch, sessionEpoch, System.StringComparison.Ordinal);
            if (generationChanged)
            {
                _sessionGeneration = generation;
                _sessionEpoch = sessionEpoch;
                _semantic = null;
                _commands = null;
            }

            return new OverlaySceneRoutingDecision(
                sessionEpoch,
                generation,
                generationChanged,
                !object.ReferenceEquals(_semantic, semantic),
                !object.ReferenceEquals(_commands, commands));
        }

        public void Accept(
            SemanticSnapshotState semantic,
            CommandProjectionState commands)
        {
            _semantic = semantic;
            _commands = commands;
        }
    }
}
