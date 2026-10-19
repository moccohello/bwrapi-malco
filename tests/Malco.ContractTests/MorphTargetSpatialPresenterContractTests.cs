using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Malco.Application.Contracts;
using Malco.Application.Contracts.Input;
using Malco.Application.Projection;
using Malco.Configuration.Models;
using Malco.Data;
using Malco.Models;
using Malco.Presentation.Spatial;
using Malco.Presentation.Scheduling;
using Xunit;

namespace Malco.ContractTests
{
    public sealed class MorphTargetSpatialPresenterContractTests
    {
        [Theory]
        [InlineData(36, 38)]
        [InlineData(59, 44)]
        [InlineData(59, 62)]
        public void CorrectColoredTargetAssetRendersAtContainerCenter(int type, int target)
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                fixture.Apply(State(type, target));
                var visual = fixture.Visual();
                var image = Assert.IsType<StackPanel>(visual.Badge.Child).Children.OfType<Image>().Single();
                var info = BwapiBroodWarTables.GetUnitTypeInfo(target);
                var expected = fixture.Icons.GetUnitIcon(new UnitCount { UnitId = target, Name = info.Name, IconKey = info.IconKey });
                Assert.NotNull(expected);
                Assert.Same(expected, image.Source);
                Assert.Equal(28d, image.Width);
                Assert.Equal(28d, image.Height);
                Assert.Equal((byte)220, Assert.IsType<SolidColorBrush>(visual.Badge.Background).Color.A);
                Assert.Equal(Color.FromRgb(208, 208, 208), Assert.IsType<SolidColorBrush>(visual.Badge.BorderBrush).Color);
                Assert.Equal(new Thickness(1.5d), visual.Badge.BorderThickness);
                Assert.Equal(new Thickness(2d), visual.Badge.Padding);
                Assert.Equal(new CornerRadius(4d), visual.Badge.CornerRadius);
                var shadow = Assert.IsType<DropShadowEffect>(visual.Badge.Effect);
                Assert.Equal(Colors.Black, shadow.Color);
                Assert.True(shadow.IsFrozen);
                Assert.Equal(Visibility.Visible, visual.Badge.Visibility);
                Assert.Equal(240d, Canvas.GetLeft(visual.Badge) + visual.LayoutWidth / 2d, 5);
                Assert.Equal(150d, Canvas.GetTop(visual.Badge) + visual.LayoutHeight / 2d, 5);
                if (type == 59 && target == 44) fixture.SavePreviewIfRequested();
            });
        }

        [Fact]
        public void PausedToggleAndChangedTargetUpdateWithoutStaleIcon()
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                fixture.Apply(State(59, 44));
                fixture.Reapply(false);
                Assert.Equal(0, fixture.Tree.UnitOverlayCount);
                fixture.Reapply(true);
                var badge = fixture.Visual().Badge;
                fixture.Apply(State(59, 62));
                Assert.Same(badge, fixture.Visual().Badge);
                Assert.Equal(";cargo=;morph=62", fixture.Visual().ContentKey.Split(";language=")[0]);
                foreach (var invalid in new int?[] { null, 228, 0, 43 })
                {
                    fixture.Apply(State(59, invalid));
                    Assert.Equal(0, fixture.Tree.UnitOverlayCount);
                    Assert.Empty(fixture.Host.Children.Cast<UIElement>());
                }
                fixture.Apply(State(36, 38));
                fixture.Apply(State(38, 38));
                Assert.Equal(0, fixture.Tree.UnitOverlayCount);
                fixture.Apply(State(59, 44));
                fixture.Presenter.ResetSession("next", 2);
                Assert.Empty(fixture.Host.Children.Cast<UIElement>());
            });
        }

        [Fact]
        public void ResizeScrollAndAbsentIconDoNotLeaveWrongVisual()
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                fixture.Apply(State(36, 43));
                var visual = fixture.Visual();
                var initialWidth = visual.LayoutWidth;
                fixture.Resize();
                Assert.True(visual.LayoutWidth > initialWidth);
                Assert.Equal(225d, Canvas.GetTop(visual.Badge) + visual.LayoutHeight / 2d, 5);
                var clip = Assert.IsType<RectangleGeometry>(fixture.Host.Clip).Rect;
                Assert.True(clip.Contains(new Rect(Canvas.GetLeft(visual.Badge), Canvas.GetTop(visual.Badge),
                    visual.LayoutWidth, visual.LayoutHeight)));
                fixture.Compose(5000);
                Assert.Equal(Visibility.Collapsed, visual.Badge.Visibility);
                fixture.Compose(0);
                Assert.Equal(Visibility.Visible, visual.Badge.Visibility);
                var absentIcons = new Fixture(false);
                absentIcons.Apply(State(59, 44));
                Assert.Equal(0, absentIcons.Tree.UnitOverlayCount);
            });
        }

        [Theory]
        [InlineData(36, 43)]
        [InlineData(59, 44)]
        public void CameraOnlyChangesImmediatelyMoveCenteredIconAndNeverPinItToAnEdge(int type, int target)
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                fixture.Apply(State(type, target));
                var visual = fixture.Visual();
                var left = Canvas.GetLeft(visual.Badge);
                var top = Canvas.GetTop(visual.Badge);
                // No new semantic sample, unit movement, frame or presentation revision is required.
                fixture.Compose(16, 8, false);
                Assert.Equal(left - 24d, Canvas.GetLeft(visual.Badge), 5);
                Assert.Equal(top - 12d, Canvas.GetTop(visual.Badge), 5);
                Assert.False(visual.HasActiveMotion);
                Assert.Equal(Visibility.Visible, visual.Badge.Visibility);
                fixture.Compose(0, 0, false);
                Assert.Equal(left, Canvas.GetLeft(visual.Badge), 5);
                Assert.Equal(top, Canvas.GetTop(visual.Badge), 5);

                // Center at the left edge and just outside it: clamping would attach the icon to another location.
                fixture.Compose(160, 0, false);
                Assert.Equal(Visibility.Collapsed, visual.Badge.Visibility);
                fixture.Compose(170, 0, false);
                Assert.Equal(Visibility.Collapsed, visual.Badge.Visibility);
                fixture.Compose(0, 100, false);
                Assert.Equal(Visibility.Collapsed, visual.Badge.Visibility);
                fixture.Compose(0, 0, false);
                Assert.Equal(Visibility.Visible, visual.Badge.Visibility);
                Assert.Equal(left, Canvas.GetLeft(visual.Badge), 5);
                Assert.Equal(top, Canvas.GetTop(visual.Badge), 5);
            });
        }

        private static UnitSpatialState State(int type, int? target) => new UnitSpatialState
        {
            UnitTag = "container", UnitId = type, HitPointsRaw = 25600, MapX = 160, MapY = 100,
            IsCompleted = false, MorphTargetUnitId = target
        };

        [Theory]
        [InlineData(36, 43)]
        [InlineData(59, 44)]
        public void CentralCameraCommitReachesBadgeThroughPumpWithoutNewSemanticOrGameFrame(int type, int target)
        {
            RunSta(() =>
            {
                var fixture = new Fixture();
                fixture.Apply(State(type, target));
                var mailbox = new ProjectionMailboxPublisher();
                var adapter = new ProjectionPresentationAdapter(mailbox.ProjectionMailboxReader);
                adapter.UpdateControl(new ProjectionControlState(ProviderStatus.Ready, "match", 1, 1,
                    true, false, ProjectionClearReason.None, new ContentRevision(1), string.Empty));
                var applies = 0;
                using var pump = new CompositionFramePump(Dispatcher.CurrentDispatcher, () =>
                {
                    var latest = adapter.ResolveLatest();
                    Assert.Equal(7, latest.GameFrame);
                    fixture.ComposeProjection(latest);
                    applies++;
                });
                mailbox.RegisterProjectionPresentationCommitSink(pump);
                pump.SetProjectionEnabled(true);

                void Camera(int x, int y, long revision)
                {
                    mailbox.Commit(ViewportProjectionState.Ready(x, y, 100, 7, string.Empty,
                        1, revision, 1, "match"), out var sink);
                    Assert.Same(pump, sink);
                    sink.MarkProjectionPresentationCommitted();
                    var dispatchFrame = new DispatcherFrame();
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() => dispatchFrame.Continue = false));
                    Dispatcher.PushFrame(dispatchFrame);
                }

                Camera(0, 0, 1);
                var visual = fixture.Visual();
                var badge = visual.Badge;
                var content = badge.Child;
                var left = Canvas.GetLeft(badge);
                var top = Canvas.GetTop(badge);
                Camera(16, 8, 2);
                Assert.Equal(2, applies);
                Assert.Same(badge, fixture.Visual().Badge);
                Assert.Same(content, badge.Child);
                Assert.Equal(left - 24d, Canvas.GetLeft(badge), 5);
                Assert.Equal(top - 12d, Canvas.GetTop(badge), 5);
                Assert.Equal(Visibility.Visible, badge.Visibility);
                Camera(0, 0, 3);
                Assert.Equal(3, applies);
                Assert.Equal(left, Canvas.GetLeft(badge), 5);
                Assert.Equal(top, Canvas.GetTop(badge), 5);
                mailbox.UnregisterProjectionPresentationCommitSink(pump);
            });
        }

        private static void RunSta(Action action)
        {
            Exception error = null;
            var thread = new Thread(() => { try { action(); } catch (Exception exception) { error = exception; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
        }

        private sealed class Fixture
        {
            private SpatialSurfaceState _surface = new SpatialSurfaceState(true, false, true, 1280d, 720d, false);
            private FrozenSemanticSnapshot _snapshot;
            private long _revision;
            public Canvas Host { get; } = new Canvas { Width = 1280d, Height = 720d, Background = Brushes.Black };
            public IconLocator Icons { get; } = new IconLocator();
            public SpatialVisualTree Tree { get; }
            public SpatialPresenter Presenter { get; }
            public Fixture(bool withIcons = true)
            {
                Tree = new SpatialVisualTree(Host);
                Presenter = new SpatialPresenter(Tree,
                    new SpatialVisualStyle(Brushes.Black, Brushes.Gray, Brushes.White, Brushes.Coral), withIcons ? Icons : null);
            }
            public void Apply(UnitSpatialState state)
            {
                _snapshot = FrozenSemanticSnapshot.Freeze(new GameSnapshot(DateTime.UtcNow, true, Race.Zerg, 0,
                    0, 0, 0, 0, string.Empty, Array.Empty<UnitCount>(), Array.Empty<UnitCount>(),
                    Array.Empty<GasWorkerGroup>(), Array.Empty<MineralWorkerGroup>(), Array.Empty<UpgradeState>(),
                    Array.Empty<UpgradeState>(), new[] { state }));
                ApplySnapshot(true, true);
            }
            public void Reapply(bool enabled) => ApplySnapshot(enabled, false);
            private void ApplySnapshot(bool enabled, bool dirty)
            {
                var preferences = HudDisplayPreferences.FromLayout(new HudLayoutConfig
                { ShowMorphTargetIcons = enabled, ShowLarvaCountdown = false, ShowTransportCargo = false });
                Presenter.ApplySlowState(new SpatialSlowState("match", 1, _snapshot, null,
                    new SpatialFeaturePreferences(false, false, false, false, preferences),
                    _surface, dirty, false, Stopwatch.GetTimestamp()));
                Compose(0);
            }
            public void Compose(int x, int y = 0, bool advanceRevision = true) =>
                Presenter.ApplyCompositionFrame(new SpatialCompositionFrame(
                    true, false, "match", 1, advanceRevision ? ++_revision : _revision, x, y, _surface));
            public void ComposeProjection(ProjectionPresentationState state) =>
                Presenter.ApplyCompositionFrame(new SpatialCompositionFrame(state.IsUsable,
                    state.IsAuthoritativeClear, state.SessionEpoch, state.SessionGeneration,
                    state.PresentationRevision, state.ViewportMapX, state.ViewportMapY, _surface));
            public void Resize()
            {
                Host.Width = 1920d;
                Host.Height = 1080d;
                _surface = new SpatialSurfaceState(true, false, true, 1920d, 1080d, false);
                Compose(0);
            }
            public UnitOverlaySpatialVisual Visual()
            {
                Assert.True(Tree.TryGetUnitOverlay("container", out var visual));
                return visual;
            }
            public void SavePreviewIfRequested()
            {
                var path = Environment.GetEnvironmentVariable("MALCO_MORPH_HUD_PREVIEW_PATH");
                if (string.IsNullOrWhiteSpace(path)) return;
                Host.Measure(new Size(1280d, 720d));
                Host.Arrange(new Rect(0d, 0d, 1280d, 720d));
                Host.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1280, 720, 96d, 96d, PixelFormats.Pbgra32);
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
