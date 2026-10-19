using System;
using System.IO;
using System.Linq;
using System.Threading;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Diagnostics;
using Malco.Game.Services;
using Malco.Models;
using Malco.Presentation.Scheduling;
using Malco.Presentation.Spatial;
using Malco.Shell;

namespace Malco
{
    internal sealed partial class OverlayHudMetrics : IDisposable
    {
        private const int MaxSamples = 600;
        private static readonly TimeSpan WriteInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan CallbackGapThreshold = TimeSpan.FromMilliseconds(20);

        private readonly bool _enabled;
        private readonly object _sync;
        private readonly MetricRingBuffer _tickDurationsUs;
        private readonly MetricRingBuffer _renderDurationsUs;
        private readonly MetricRingBuffer _tickAllocatedBytes;
        private readonly MetricRingBuffer _renderAllocatedBytes;
        private readonly Timer _writeTimer;
        private readonly string _outputPath;
        private readonly string _scenario;
        private readonly DateTime _startedAtUtc;
        private readonly IProviderOptimizationMetricsSource _providerMetrics;
        private readonly CompositionFramePump _framePump;
        private long _lastRenderingTimestamp;
        private int _writeActive;
        private int _disposed;
        private long _tickCount;
        private long _renderCount;
        private long _activeRenderCount;
        private long _spatialSemanticRebuilds;
        private long _renderCallbackGapsOverThreshold;
        private long _activeRenderCallbackGapsOverThreshold;
        private long _managedAllocatedBytes;
        private long _renderManagedAllocatedBytes;
        private long _gen0Collections;
        private long _renderGen0Collections;
        private long _reportedWpfPositionWrites;
        private long _reportedVisualCreates;
        private long _reportedVisualUpdates;
        private long _reportedVisualRemoves;
        private CoordinatorMetricsSnapshot _coordinator;
        private HudPresentationDiagnostic _lastHudPresentation;
        private CompositionRenderDiagnostic _lastCompositionRender;
        private object _lastDisplayState;
        private DateTime _previousProcessSampleUtc;
        private TimeSpan _previousProcessCpu;
        private long _previousProcessTickCount;

        private OverlayHudMetrics(
            bool enabled,
            IProviderOptimizationMetricsSource providerMetrics,
            CompositionFramePump framePump)
        {
            _enabled = enabled;
            _providerMetrics = providerMetrics;
            _framePump = framePump;
            if (!enabled)
            {
                return;
            }

            _sync = new object();
            _tickDurationsUs = new MetricRingBuffer(MaxSamples);
            _renderDurationsUs = new MetricRingBuffer(MaxSamples);
            _tickAllocatedBytes = new MetricRingBuffer(MaxSamples);
            _renderAllocatedBytes = new MetricRingBuffer(MaxSamples);
            _scenario = Environment.GetEnvironmentVariable("MALCO_DIAGNOSTICS_SCENARIO") ?? string.Empty;
            _startedAtUtc = DateTime.UtcNow;
            Directory.CreateDirectory(AppPaths.UserDataDirectory);
            _outputPath = Path.Combine(AppPaths.UserDataDirectory, "overlay-hud-metrics.json");
            _writeTimer = new Timer(WriteSnapshot, null, WriteInterval, WriteInterval);
        }

        public void RecordDisplayState(
            OverlayRuntimeMode mode,
            bool hasUsableTarget,
            SemanticSnapshotState semantic)
        {
            if (!_enabled) return;
            var snapshot = semantic?.Snapshot;
            var state = new
            {
                observed_at_utc = DateTime.UtcNow,
                runtime_mode = mode.ToString(),
                target_window_usable = hasUsableTarget,
                provider_status = semantic?.Status.ToString(),
                provider_message = semantic?.Message,
                snapshot_in_match = snapshot?.IsInMatch,
                local_player_id = snapshot?.LocalPlayerId,
                race = snapshot?.Race.ToString(),
                process_id = semantic?.BoundProcessId,
                session_epoch = semantic?.SessionEpoch,
                session_generation = semantic?.SessionGeneration,
                game_frame = semantic?.Frame
            };
            lock (_sync) _lastDisplayState = state;
        }

        public static OverlayHudMetrics CreateFromEnvironment(
            IProviderOptimizationMetricsSource providerMetrics,
            CompositionFramePump framePump)
        {
            try
            {
                var enabled = string.Equals(
                    Environment.GetEnvironmentVariable("MALCO_DIAGNOSTICS"),
                    "1",
                    StringComparison.Ordinal);
                return new OverlayHudMetrics(enabled, providerMetrics, framePump);
            }
            catch
            {
                // Opt-in diagnostics must never prevent the overlay from starting.
                return new OverlayHudMetrics(false, providerMetrics, framePump);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _writeTimer?.Dispose();
        }

        public void RecordHudPresentation(
            SemanticSnapshotState semantic,
            DateTime? semanticPublishedAtUtc,
            FrozenSemanticSnapshot snapshot,
            HudDisplayPreferences preferences)
        {
            if (!_enabled)
            {
                return;
            }

            var badgeCapture = CaptureAbilityBadges(snapshot, preferences);
            var sessionEpoch = semantic != null &&
                               !string.IsNullOrWhiteSpace(semantic.SessionEpoch)
                ? semantic.SessionEpoch
                : null;
            var presentation = new HudPresentationDiagnostic(
                DateTime.UtcNow,
                semanticPublishedAtUtc,
                semantic != null ? semantic.BoundProcessId : null,
                semantic != null ? semantic.BoundProcessStartedAtUtc : null,
                sessionEpoch,
                semantic != null && sessionEpoch != null
                    ? semantic.SessionGeneration
                    : (long?)null,
                semantic != null ? semantic.Sequence : null,
                semantic != null ? semantic.Frame : null,
                badgeCapture);

            lock (_sync)
            {
                _lastHudPresentation = presentation;
            }
        }

        private static AbilityBadgeCapture CaptureAbilityBadges(
            FrozenSemanticSnapshot snapshot,
            HudDisplayPreferences preferences)
        {
            if (snapshot == null || preferences == null)
            {
                return AbilityBadgeCapture.Unavailable();
            }

            var candidates = (snapshot.UnitSpatialStates ?? Array.Empty<UnitSpatialState>())
                .Where(unit => unit != null && !string.IsNullOrWhiteSpace(unit.UnitTag))
                .Select(unit => new
                {
                    Unit = unit,
                    Mode = preferences.AbilityDisplayMode(unit.UnitId),
                    Definition = AbilityCatalog.Find(unit.UnitId)
                })
                .Where(item => item.Mode != null &&
                               item.Mode.StartsWith("tech:", StringComparison.Ordinal) &&
                               item.Definition?.Abilities.Any(ability =>
                                   string.Equals(
                                       ability.Mode,
                                       item.Mode,
                                       StringComparison.Ordinal)) == true)
                .ToArray();
            var evaluated = candidates
                .Select((item, candidateOrder) =>
                {
                    var definition = item.Definition?.Abilities.FirstOrDefault(ability => string.Equals(
                            ability.Mode,
                            item.Mode,
                            StringComparison.Ordinal));
                    var token = SpatialPresenter.BuildAbilityToken(
                        snapshot,
                        item.Unit,
                        preferences);
                    var researchRow = (snapshot.Upgrades ?? Array.Empty<UpgradeState>())
                        .FirstOrDefault(upgrade => upgrade != null && string.Equals(
                            upgrade.StateKey,
                            item.Mode,
                            StringComparison.OrdinalIgnoreCase));

                    return new AbilityBadgeDiagnostic(
                        item.Unit.UnitTag,
                        item.Unit.UnitId,
                        item.Mode,
                        definition?.Name,
                        token.StartsWith("ready=", StringComparison.Ordinal),
                        token.StartsWith("ready=", StringComparison.Ordinal)
                            ? token
                            : null,
                        candidateOrder,
                        new
                        {
                            hit_points_raw = item.Unit.HitPointsRaw,
                            is_completed = item.Unit.IsCompleted,
                            is_loaded = item.Unit.IsLoaded,
                            is_building = item.Unit.IsBuilding,
                            is_powered = item.Unit.IsPowered,
                            is_interruptible = item.Unit.IsInterruptible,
                            order_id = item.Unit.OrderId,
                            secondary_order_id = item.Unit.SecondaryOrderId,
                            lockdown_timer = item.Unit.LockdownTimer,
                            maelstrom_timer = item.Unit.MaelstromTimer,
                            stasis_timer = item.Unit.StasisTimer,
                            energy = item.Unit.Energy,
                            required_energy = definition?.EnergyCost,
                            research_row_present = researchRow != null,
                            research_complete = researchRow != null
                                ? (bool?)researchRow.IsComplete
                                : null,
                            spider_mine_count = item.Unit.SpiderMineCount,
                            completed_nuclear_missile_count =
                                item.Unit.CompletedNuclearMissileCount
                        });
                })
                .OrderByDescending(item => item.ReadyBadgeComputed)
                .ThenBy(item => item.CandidateOrder)
                .ToArray();
            var captured = evaluated.Take(8).ToArray();
            return new AbilityBadgeCapture(
                candidates.Length,
                captured.Length,
                candidates.Length > captured.Length,
                "computed_ready_first_then_source_order",
                captured);
        }

        private void WriteSnapshot(object state)
        {
            if (!_enabled || Volatile.Read(ref _disposed) != 0 ||
                Interlocked.Exchange(ref _writeActive, 1) != 0)
            {
                return;
            }

            try
            {
                MetricsSnapshot snapshot;
                lock (_sync)
                {
                    snapshot = CaptureSnapshot(
                        DateTime.UtcNow,
                        _lastHudPresentation,
                        _lastCompositionRender);
                }

                File.WriteAllText(_outputPath, snapshot.BuildJson());
            }
            catch
            {
                // Diagnostics must never alter overlay behavior.
            }
            finally
            {
                Volatile.Write(ref _writeActive, 0);
            }
        }
    }
}
