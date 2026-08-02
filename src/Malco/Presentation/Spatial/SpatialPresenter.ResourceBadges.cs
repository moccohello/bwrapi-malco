using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Malco.Data;
using Malco.Models;

namespace Malco.Presentation.Spatial
{
    internal sealed partial class SpatialPresenter
    {
        private void AddGas(string id, GasWorkerGroup group)
        {
            var count = NormalizeGasWorkerCount(group.WorkerCount);
            var text = new TextBlock
            {
                Text = count.ToString(CultureInfo.InvariantCulture) + "/3",
                FontFamily = new FontFamily("Segoe UI"), FontSize = GasWorkerFontSize, FontWeight = FontWeights.Bold,
                Foreground = _style.GetGasBadgeBrush(count), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = ResourceTextShadow()
            };
            var badge = new Border
            {
                Width = 54d, Height = 30d, Child = text, ToolTip = "Gas workers",
                Background = _style.ChipBackground, BorderBrush = _style.ChipBorder,
                BorderThickness = new Thickness(1d), CornerRadius = new CornerRadius(5d)
            };
            _tree.AddGas(id, new GasSpatialVisual(group, badge, text));
        }

        private void AddMineral(string id, MineralWorkerGroup group)
        {
            var text = new TextBlock
            {
                Text = FormatMineralBadgeText(group), FontFamily = new FontFamily("Segoe UI"), FontSize = MineralWorkerFontSize,
                FontWeight = FontWeights.Bold, Foreground = _style.Text, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = ResourceTextShadow()
            };
            var badge = new Border
            {
                Width = 56d, Height = 26d, Child = text,
                ToolTip = "Mineral workers / patches",
                Background = _style.ChipBackground, BorderBrush = _style.ChipBorder,
                BorderThickness = new Thickness(0.75d), CornerRadius = new CornerRadius(4d),
                Padding = new Thickness(0d)
            };
            _tree.AddMineral(id, new MineralSpatialVisual(group, badge, text));
        }

        private int RefreshBadgeCounts(FrozenSemanticSnapshot snapshot, bool isEditor)
        {
            if (isEditor || snapshot == null || !snapshot.IsInMatch) return 0;
            var mineralsByBase = (snapshot.MineralWorkerGroups ?? new List<MineralWorkerGroup>())
                .Where(group => group != null).GroupBy(group => group.BaseIdentity)
                .ToDictionary(group => group.Key, group => group.First());
            var writes = 0;
            for (var index = 0; index < _tree.MineralCount; index++)
            {
                var mineral = _tree.GetMineralAt(index);
                MineralWorkerGroup current;
                if (!mineralsByBase.TryGetValue(mineral.Group.BaseIdentity, out current))
                {
                    writes += SetVisibility(mineral.Badge, Visibility.Collapsed);
                    continue;
                }
                mineral.Group = current;
                var mineralText = FormatMineralBadgeText(current);
                if (!string.Equals(mineral.Text.Text, mineralText, StringComparison.Ordinal))
                {
                    mineral.Text.Text = mineralText;
                    writes++;
                }
            }
            var gasByBuilding = (snapshot.GasWorkerGroups ?? new List<GasWorkerGroup>())
                .Where(group => group != null).GroupBy(group => group.GasIdentity)
                .ToDictionary(group => group.Key, group => group.First());
            for (var index = 0; index < _tree.GasCount; index++)
            {
                var gas = _tree.GetGasAt(index);
                GasWorkerGroup current;
                if (!gasByBuilding.TryGetValue(gas.Group.GasIdentity, out current))
                {
                    writes += SetVisibility(gas.Badge, Visibility.Collapsed);
                    continue;
                }
                gas.Group = current;
                var display = NormalizeGasWorkerCount(current.WorkerCount);
                var gasText = display.ToString(CultureInfo.InvariantCulture) + "/3";
                if (!string.Equals(gas.Text.Text, gasText, StringComparison.Ordinal))
                {
                    gas.Text.Text = gasText;
                    writes++;
                }
                var foreground = _style.GetGasBadgeBrush(display);
                if (!ReferenceEquals(gas.Text.Foreground, foreground))
                {
                    gas.Text.Foreground = foreground;
                    writes++;
                }
            }
            return writes;
        }

        private static DropShadowEffect ResourceTextShadow() => new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 4d,
            ShadowDepth = 0d,
            Opacity = 1d
        };

        private static SolidColorBrush FrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static string GasSpatialId(GasWorkerGroup group) =>
            "g:" + group.GasIdentity.Value;
        private static string MineralSpatialId(MineralWorkerGroup group) =>
            "m:" + group.BaseIdentity.Value;
        private static int NormalizeGasWorkerCount(int count) => Math.Max(0, count);
        private static string FormatMineralBadgeText(MineralWorkerGroup group) => group == null
            ? string.Empty
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}",
                Math.Max(0, group.WorkerCount),
                Math.Max(0, group.MineralPatchCount));
    }
}
