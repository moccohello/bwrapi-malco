using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Malco.Presentation.Spatial
{
    internal readonly struct SpatialVisualTreeClearResult
    {
        public SpatialVisualTreeClearResult(bool changed, int removedVisualCount)
        {
            Changed = changed;
            RemovedVisualCount = removedVisualCount;
        }

        public bool Changed { get; }

        public int RemovedVisualCount { get; }
    }

    internal sealed class SpatialVisualTree
    {
        private readonly Canvas _host;
        private readonly List<GasSpatialVisual> _gas = new List<GasSpatialVisual>();
        private readonly List<MineralSpatialVisual> _minerals = new List<MineralSpatialVisual>();
        private readonly List<RallySpatialVisual> _rallies = new List<RallySpatialVisual>();
        private readonly List<UnitOverlaySpatialVisual> _unitOverlays = new List<UnitOverlaySpatialVisual>();
        private readonly Dictionary<string, RallySpatialVisual> _ralliesById =
            new Dictionary<string, RallySpatialVisual>(StringComparer.Ordinal);
        private readonly Dictionary<string, GasSpatialVisual> _gasById =
            new Dictionary<string, GasSpatialVisual>(StringComparer.Ordinal);
        private readonly Dictionary<string, MineralSpatialVisual> _mineralsById =
            new Dictionary<string, MineralSpatialVisual>(StringComparer.Ordinal);
        private readonly Dictionary<string, UnitOverlaySpatialVisual> _unitOverlaysById =
            new Dictionary<string, UnitOverlaySpatialVisual>(StringComparer.Ordinal);
        private Rect _clipStamp = Rect.Empty;

        public SpatialVisualTree(Canvas host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public int RallyCount => _rallies.Count;

        public int GasCount => _gas.Count;

        public int MineralCount => _minerals.Count;
        public int UnitOverlayCount => _unitOverlays.Count;

        public int VisualCount => RallyCount + GasCount + MineralCount + UnitOverlayCount;

        public bool HasVisuals => VisualCount != 0;
        public bool HasActiveUnitOverlayMotion
        {
            get
            {
                for (var index = 0; index < _unitOverlays.Count; index++)
                {
                    if (_unitOverlays[index].HasActiveMotion) return true;
                }
                return false;
            }
        }

        public RallySpatialVisual GetRallyAt(int index) => _rallies[index];

        public GasSpatialVisual GetGasAt(int index) => _gas[index];

        public MineralSpatialVisual GetMineralAt(int index) => _minerals[index];
        public UnitOverlaySpatialVisual GetUnitOverlayAt(int index) => _unitOverlays[index];

        public bool TryGetRally(string id, out RallySpatialVisual visual) =>
            _ralliesById.TryGetValue(id, out visual);

        public bool TryGetGas(string id, out GasSpatialVisual visual) =>
            _gasById.TryGetValue(id, out visual);

        public bool TryGetMineral(string id, out MineralSpatialVisual visual) =>
            _mineralsById.TryGetValue(id, out visual);

        public bool TryGetUnitOverlay(string id, out UnitOverlaySpatialVisual visual) =>
            _unitOverlaysById.TryGetValue(id, out visual);

        public IReadOnlyCollection<string> UnitOverlayIds => _unitOverlaysById.Keys;

        public void AddRally(string id, RallySpatialVisual visual)
        {
            if (visual == null) throw new ArgumentNullException(nameof(visual));
            _host.Children.Add(visual.Line);
            if (visual.SourceRing != null) _host.Children.Add(visual.SourceRing);
            _host.Children.Add(visual.Ring);
            _rallies.Add(visual);
            _ralliesById.Add(id, visual);
        }

        public bool RemoveRally(string id)
        {
            RallySpatialVisual visual;
            if (!_ralliesById.TryGetValue(id, out visual)) return false;
            _host.Children.Remove(visual.Line);
            _host.Children.Remove(visual.Ring);
            if (visual.SourceRing != null) _host.Children.Remove(visual.SourceRing);
            _rallies.Remove(visual);
            _ralliesById.Remove(id);
            return true;
        }

        public void AddGas(string id, GasSpatialVisual visual)
        {
            if (visual == null) throw new ArgumentNullException(nameof(visual));
            _host.Children.Add(visual.Badge);
            _gas.Add(visual);
            _gasById.Add(id, visual);
        }

        public bool RemoveGas(string id)
        {
            GasSpatialVisual visual;
            if (!_gasById.TryGetValue(id, out visual)) return false;
            _host.Children.Remove(visual.Badge);
            _gas.Remove(visual);
            _gasById.Remove(id);
            return true;
        }

        public void AddMineral(string id, MineralSpatialVisual visual)
        {
            if (visual == null) throw new ArgumentNullException(nameof(visual));
            _host.Children.Add(visual.Badge);
            _minerals.Add(visual);
            _mineralsById.Add(id, visual);
        }

        public bool RemoveMineral(string id)
        {
            MineralSpatialVisual visual;
            if (!_mineralsById.TryGetValue(id, out visual)) return false;
            _host.Children.Remove(visual.Badge);
            _minerals.Remove(visual);
            _mineralsById.Remove(id);
            return true;
        }

        public void AddUnitOverlay(string id, UnitOverlaySpatialVisual visual)
        {
            if (visual == null) throw new ArgumentNullException(nameof(visual));
            _host.Children.Add(visual.Badge);
            _unitOverlays.Add(visual);
            _unitOverlaysById.Add(id, visual);
        }

        public bool RemoveUnitOverlay(string id)
        {
            UnitOverlaySpatialVisual visual;
            if (!_unitOverlaysById.TryGetValue(id, out visual)) return false;
            _host.Children.Remove(visual.Badge);
            _unitOverlays.Remove(visual);
            _unitOverlaysById.Remove(id);
            return true;
        }

        public bool RestoreCanonicalOrder(
            IReadOnlyList<string> rallyIds,
            IReadOnlyList<string> gasIds,
            IReadOnlyList<string> mineralIds)
        {
            var changed = RestoreListOrder(_ralliesById, rallyIds, _rallies);
            changed |= RestoreListOrder(_gasById, gasIds, _gas);
            changed |= RestoreListOrder(_mineralsById, mineralIds, _minerals);
            if (!changed) return false;

            for (var index = 0; index < _rallies.Count; index++)
            {
                var rally = _rallies[index];
                MoveToEnd(rally.Line);
                if (rally.SourceRing != null) MoveToEnd(rally.SourceRing);
                MoveToEnd(rally.Ring);
            }
            for (var index = 0; index < _gas.Count; index++) MoveToEnd(_gas[index].Badge);
            for (var index = 0; index < _minerals.Count; index++) MoveToEnd(_minerals[index].Badge);
            for (var index = 0; index < _unitOverlays.Count; index++) MoveToEnd(_unitOverlays[index].Badge);
            return true;
        }

        public SpatialVisualTreeClearResult Clear()
        {
            var removed = VisualCount;
            var changed = _host.Children.Count != 0 || removed != 0 ||
                          _ralliesById.Count != 0 || _gasById.Count != 0 || _mineralsById.Count != 0 ||
                          _unitOverlaysById.Count != 0;
            if (!changed) return new SpatialVisualTreeClearResult(false, 0);

            _host.Children.Clear();
            _gas.Clear();
            _minerals.Clear();
            _rallies.Clear();
            _ralliesById.Clear();
            _unitOverlays.Clear();
            _unitOverlaysById.Clear();
            _gasById.Clear();
            _mineralsById.Clear();
            ClearClip();
            return new SpatialVisualTreeClearResult(true, removed);
        }

        public int SetVisualsVisibility(Visibility visibility)
        {
            var writes = 0;
            for (var index = 0; index < _rallies.Count; index++)
            {
                var rally = _rallies[index];
                writes += SetVisibility(rally.Line, visibility);
                writes += SetVisibility(rally.Ring, visibility);
                if (rally.SourceRing != null) writes += SetVisibility(rally.SourceRing, visibility);
            }
            for (var index = 0; index < _gas.Count; index++) writes += SetVisibility(_gas[index].Badge, visibility);
            for (var index = 0; index < _minerals.Count; index++) writes += SetVisibility(_minerals[index].Badge, visibility);
            for (var index = 0; index < _unitOverlays.Count; index++) writes += SetVisibility(_unitOverlays[index].Badge, visibility);
            return writes;
        }

        public void SetHostVisibility(Visibility visibility)
        {
            _host.Visibility = visibility;
        }

        public void SnapUnitOverlayMotions()
        {
            for (var index = 0; index < _unitOverlays.Count; index++)
                _unitOverlays[index].SnapMotion();
        }

        public void ClearClip()
        {
            if (_host.Clip == null && _clipStamp.IsEmpty) return;
            _host.Clip = null;
            _clipStamp = Rect.Empty;
        }

        public void UpdateClip(Rect clipRect)
        {
            if (_host.Clip != null && !_clipStamp.IsEmpty && AreClose(_clipStamp, clipRect)) return;
            _host.Clip = new RectangleGeometry(clipRect);
            _clipStamp = clipRect;
        }

        private static bool RestoreListOrder<TVisual>(
            Dictionary<string, TVisual> indexed,
            IReadOnlyList<string> orderedIds,
            List<TVisual> ordered)
        {
            var desired = new List<TVisual>(orderedIds.Count);
            for (var index = 0; index < orderedIds.Count; index++)
            {
                TVisual visual;
                if (indexed.TryGetValue(orderedIds[index], out visual)) desired.Add(visual);
            }
            if (desired.Count == ordered.Count)
            {
                var unchanged = true;
                for (var index = 0; index < desired.Count; index++)
                    unchanged &= ReferenceEquals(desired[index], ordered[index]);
                if (unchanged) return false;
            }
            ordered.Clear();
            ordered.AddRange(desired);
            return true;
        }

        private void MoveToEnd(UIElement element)
        {
            _host.Children.Remove(element);
            _host.Children.Add(element);
        }

        private static int SetVisibility(UIElement element, Visibility visibility)
        {
            if (element.Visibility == visibility) return 0;
            element.Visibility = visibility;
            return 1;
        }

        private static bool AreClose(Rect left, Rect right)
        {
            return Math.Abs(left.X - right.X) <= .5d &&
                   Math.Abs(left.Y - right.Y) <= .5d &&
                   Math.Abs(left.Width - right.Width) <= .5d &&
                   Math.Abs(left.Height - right.Height) <= .5d;
        }
    }
}
