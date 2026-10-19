using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Localization;
using Malco.Models;
using Malco.Presentation.Spatial;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class LarvaSpatialPresenterContractTests
    {
        [Fact]
        public void TwoBuildingsRenderBelowTheirCentersAndFollowViewportScroll()
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                fixture.Apply(true, Building("hatch", 131, 160, 100, 192, 1),
                    Building("lair", 132, 400, 180, 48, 2));
                Assert.Equal(2, fixture.Tree.UnitOverlayCount);
                var hatch = fixture.Visual("hatch");
                var lair = fixture.Visual("lair");
                Assert.Equal(LarvaText(8), Text(hatch));
                Assert.Equal(LarvaText(2), Text(lair));
                Assert.Equal(160d, Canvas.GetLeft(hatch.Badge) + hatch.LayoutWidth / 2d, 5);
                Assert.Equal(132d, Canvas.GetTop(hatch.Badge) + hatch.LayoutHeight / 2d, 5);
                Assert.Equal(212d, Canvas.GetTop(lair.Badge) + lair.LayoutHeight / 2d, 5);
                Assert.True(Canvas.GetTop(hatch.Badge) + hatch.LayoutHeight <= 148d);
                Assert.True(Canvas.GetTop(lair.Badge) + lair.LayoutHeight <= 228d);
                Assert.Equal(Visibility.Visible, hatch.Badge.Visibility);
                fixture.SavePreviewIfRequested();

                var previousLeft = Canvas.GetLeft(hatch.Badge);
                var previousTop = Canvas.GetTop(hatch.Badge);
                fixture.Compose(32, 24);
                Assert.Equal(previousLeft - 32d, Canvas.GetLeft(hatch.Badge), 5);
                Assert.Equal(previousTop - 24d, Canvas.GetTop(hatch.Badge), 5);
                Assert.Equal(LarvaText(8), Text(hatch));
            });
        }

        [Fact]
        public void ObservedCountdownUpdatesInPlaceAndThreeLarvaeRemoveOnlyTheirBuilding()
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                fixture.Apply(true, Building("hatch", 131, 160, 100, 192, 1),
                    Building("hive", 133, 400, 180, 48, 2));
                var badge = fixture.Visual("hatch").Badge;
                fixture.Apply(true, Building("hatch", 131, 160, 100, 168, 1),
                    Building("hive", 133, 400, 180, 48, 3));
                Assert.Equal(1, fixture.Tree.UnitOverlayCount);
                Assert.Same(badge, fixture.Visual("hatch").Badge);
                Assert.Equal(LarvaText(7), Text(fixture.Visual("hatch")));
                Assert.False(fixture.Tree.TryGetUnitOverlay("hive", out _));
                Assert.Single(fixture.Host.Children.Cast<UIElement>());
            });
        }

        [Fact]
        public void PreferenceOffAndSessionResetRemoveRealVisuals()
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                var state = Building("hatch", 131, 160, 100, 192, 1);
                fixture.Apply(true, state);
                fixture.Apply(false, state);
                Assert.Equal(0, fixture.Tree.UnitOverlayCount);
                Assert.Empty(fixture.Host.Children.Cast<UIElement>());
                fixture.Apply(true, state);
                Assert.Equal(1, fixture.Tree.UnitOverlayCount);
                fixture.Presenter.ResetSession("next-match", 2);
                Assert.Equal(0, fixture.Tree.UnitOverlayCount);
                Assert.Empty(fixture.Host.Children.Cast<UIElement>());
            });
        }

        [Fact]
        public void UnchangedSnapshotImmediatelyAppliesToggleAndLanguageChanges()
        {
            RunSta(() =>
            {
                var originalLanguage = UiText.CurrentLanguage;
                var originalCulture = Thread.CurrentThread.CurrentUICulture;
                try
                {
                    UiText.Initialize(MalcoPreferenceValues.Korean);
                    var fixture = new Fixture();
                    fixture.Apply(true, Building("hatch", 131, 160, 100, 192, 1));
                    Assert.Equal("8s", Text(fixture.Visual("hatch")));

                    // Reuse the same frozen sample without semantic or command changes, as when paused.
                    fixture.Reapply(false);
                    Assert.Equal(0, fixture.Tree.UnitOverlayCount);
                    Assert.Empty(fixture.Host.Children.Cast<UIElement>());
                    fixture.Reapply(true);
                    var visual = fixture.Visual("hatch");
                    var previousContent = visual.Badge.Child;
                    Assert.Equal("8s", Text(visual));
                    UiText.Initialize(MalcoPreferenceValues.English);
                    fixture.Reapply(true);
                    Assert.Same(visual.Badge, fixture.Visual("hatch").Badge);
                    Assert.NotSame(previousContent, visual.Badge.Child);
                    Assert.Equal("8s", Text(visual));
                }
                finally
                {
                    UiText.Initialize(originalLanguage);
                    Thread.CurrentThread.CurrentUICulture = originalCulture;
                }
            });
        }

        [Fact]
        public void DestroyedBuildingDoesNotRetainItsCountdownBadge()
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                var building = Building("hatch", 131, 160, 100, 192, 1);
                fixture.Apply(true, building);
                Assert.Equal(1, fixture.Tree.UnitOverlayCount);
                building.HitPointsRaw = 0;
                fixture.Apply(true, building);
                Assert.Equal(0, fixture.Tree.UnitOverlayCount);
                Assert.Empty(fixture.Host.Children.Cast<UIElement>());
            });
        }

        [Fact]
        public void ViewportEdgesClampBadgeAboveCommandPanelAndHideDistantBuildings()
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                fixture.Apply(true, Building("edge", 131, 10, 345, 192, 1),
                    Building("distant", 132, 1000, 100, 192, 1));
                var edge = fixture.Visual("edge");
                Assert.Equal(Visibility.Visible, edge.Badge.Visibility);
                Assert.True(Canvas.GetLeft(edge.Badge) >= 0d);
                Assert.True(Canvas.GetTop(edge.Badge) + edge.LayoutHeight <= 352d + 0.00001d);
                Assert.Equal(Visibility.Collapsed, fixture.Visual("distant").Badge.Visibility);
                var clip = Assert.IsType<RectangleGeometry>(fixture.Host.Clip);
                Assert.Equal(new Rect(0d, 0d, 640d, 352d), clip.Rect);
                fixture.Compose(900, 0);
                Assert.Equal(Visibility.Collapsed, edge.Badge.Visibility);
                Assert.Equal(Visibility.Visible, fixture.Visual("distant").Badge.Visibility);
            });
        }

        [Theory]
        [InlineData(1280d, 720d)]
        [InlineData(1920d, 1080d)]
        public void SurfaceResizeAndAspectSwitchRepositionAndScaleExistingBadges(double width, double height)
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                fixture.Apply(true, Building("hatch", 131, 160, 100, 192, 1),
                    Building("hive", 133, 400, 180, 48, 2));
                var hatch = fixture.Visual("hatch");
                var hive = fixture.Visual("hive");
                var initialWidth = hatch.LayoutWidth;
                var initialHeight = hatch.LayoutHeight;
                var initialTop = Canvas.GetTop(hatch.Badge);
                var initialSeparation = CenterX(hive) - CenterX(hatch);

                fixture.Resize(width, height, true);
                Assert.Same(hatch.Badge, fixture.Visual("hatch").Badge);
                Assert.True(hatch.LayoutWidth > initialWidth);
                Assert.True(hatch.LayoutHeight >= initialHeight);
                Assert.True(Canvas.GetTop(hatch.Badge) > initialTop);
                Assert.Equal(initialSeparation * height / 480d, CenterX(hive) - CenterX(hatch), 5);
                // Both labels stay below their map centers and inside the actual applied gameplay clip.
                Assert.True(Canvas.GetTop(hatch.Badge) > 100d * height / 480d);
                Assert.True(Canvas.GetTop(hive.Badge) > 180d * height / 480d);
                Assert.True(Canvas.GetTop(hatch.Badge) + hatch.LayoutHeight <= 148d * height / 480d);
                Assert.True(Canvas.GetTop(hive.Badge) + hive.LayoutHeight <= 228d * height / 480d);
                AssertContained(fixture, hatch);
                AssertContained(fixture, hive);
                var narrowHatchCenter = CenterX(hatch);
                var narrowHiveCenter = CenterX(hive);
                var narrowTop = Canvas.GetTop(hatch.Badge);
                var resizedWidth = hatch.LayoutWidth;

                fixture.Resize(width, height, false);
                var hatchShift = narrowHatchCenter - CenterX(hatch);
                Assert.True(hatchShift > 0d);
                Assert.Equal(hatchShift, narrowHiveCenter - CenterX(hive), 5);
                Assert.Equal(narrowTop, Canvas.GetTop(hatch.Badge), 5);
                Assert.Equal(resizedWidth, hatch.LayoutWidth, 5);
                AssertContained(fixture, hatch);
                AssertContained(fixture, hive);

                fixture.Compose(5000, 5000);
                Assert.Equal(Visibility.Collapsed, hatch.Badge.Visibility);
                Assert.Equal(Visibility.Collapsed, hive.Badge.Visibility);
                fixture.Compose(0, 0);
                Assert.Equal(Visibility.Visible, hatch.Badge.Visibility);
                Assert.Equal(Visibility.Visible, hive.Badge.Visibility);
                Assert.Equal(LarvaText(8), Text(hatch));
            });
        }

        private static double CenterX(UnitOverlaySpatialVisual visual) =>
            Canvas.GetLeft(visual.Badge) + visual.LayoutWidth / 2d;

        private static void AssertContained(Fixture fixture, UnitOverlaySpatialVisual visual)
        {
            Assert.Equal(Visibility.Visible, visual.Badge.Visibility);
            var clip = Assert.IsType<RectangleGeometry>(fixture.Host.Clip).Rect;
            var bounds = new Rect(Canvas.GetLeft(visual.Badge), Canvas.GetTop(visual.Badge),
                visual.LayoutWidth, visual.LayoutHeight);
            Assert.True(clip.Contains(bounds));
        }

        private static string Text(UnitOverlaySpatialVisual visual) =>
            Assert.IsType<StackPanel>(visual.Badge.Child).Children.OfType<TextBlock>().Single().Text;

        private static string LarvaText(int seconds) =>
            seconds.ToString(CultureInfo.InvariantCulture) + "s";

        private static UnitSpatialState Building(string tag, int id, int x, int y, uint frames, int count) =>
            new UnitSpatialState
            {
                UnitTag = tag, UnitId = id, Name = tag, MapX = x, MapY = y,
                IsBuilding = true, IsCompleted = true, HitPointsRaw = 25600,
                LarvaRemainingFrames = frames, ConnectedLarvaCount = count
            };

        private static void RunSta(Action action)
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception exception) { error = exception; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
        }

        private sealed class Fixture
        {
            private SpatialSurfaceState _surface =
                new SpatialSurfaceState(true, false, true, 640d, 480d, true);
            private long _revision;
            private FrozenSemanticSnapshot _snapshot;
            public Canvas Host { get; } = new Canvas { Width = 640d, Height = 480d, Background = Brushes.Black };
            public SpatialVisualTree Tree { get; }
            public SpatialPresenter Presenter { get; }

            public Fixture()
            {
                Tree = new SpatialVisualTree(Host);
                Presenter = new SpatialPresenter(Tree,
                    new SpatialVisualStyle(Brushes.Black, Brushes.Gray, Brushes.White, Brushes.Coral), null);
            }

            public void Apply(bool enabled, params UnitSpatialState[] states)
            {
                _snapshot = FrozenSemanticSnapshot.Freeze(new GameSnapshot(
                    DateTime.UtcNow, true, Race.Zerg, 0, 0, 0, 0, 0, string.Empty,
                    Array.Empty<UnitCount>(), Array.Empty<UnitCount>(), Array.Empty<GasWorkerGroup>(),
                    Array.Empty<MineralWorkerGroup>(), Array.Empty<UpgradeState>(), Array.Empty<UpgradeState>(), states));
                ApplySnapshot(enabled, true);
            }

            public void Reapply(bool enabled) => ApplySnapshot(enabled, false);

            private void ApplySnapshot(bool enabled, bool semanticDirty)
            {
                var preferences = HudDisplayPreferences.FromLayout(new HudLayoutConfig
                {
                    ShowLarvaCountdown = enabled, ShowTransportCargo = false
                });
                Presenter.ApplySlowState(new SpatialSlowState("match", 1, _snapshot, null,
                    new SpatialFeaturePreferences(false, false, false, false, preferences),
                    _surface, semanticDirty, false, Stopwatch.GetTimestamp()));
                Compose(0, 0);
            }

            public void Compose(int x, int y) =>
                Presenter.ApplyCompositionFrame(new SpatialCompositionFrame(
                    true, false, "match", 1, ++_revision, x, y, _surface));

            public void Resize(double width, double height, bool originalAspectRatio)
            {
                Host.Width = width;
                Host.Height = height;
                _surface = new SpatialSurfaceState(true, false, true, width, height, originalAspectRatio);
                Compose(0, 0);
            }

            public UnitOverlaySpatialVisual Visual(string id)
            {
                Assert.True(Tree.TryGetUnitOverlay(id, out var visual));
                return visual;
            }

            public void SavePreviewIfRequested()
            {
                var path = Environment.GetEnvironmentVariable("MALCO_LARVA_HUD_PREVIEW_PATH");
                if (string.IsNullOrWhiteSpace(path)) return;
                Host.Measure(new Size(640d, 480d));
                Host.Arrange(new Rect(0d, 0d, 640d, 480d));
                Host.UpdateLayout();
                var bitmap = new RenderTargetBitmap(640, 480, 96d, 96d, PixelFormats.Pbgra32);
                bitmap.Render(Host);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var fullPath = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                using (var stream = File.Create(fullPath)) encoder.Save(stream);
            }
        }
    }
}
