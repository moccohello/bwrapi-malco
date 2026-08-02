using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace Malco.Settings.Views
{
    internal sealed class SettingsEditorChrome : ISettingsEditorChrome
    {
        private readonly SettingsViewPalette _palette;

        public SettingsEditorChrome(SettingsViewPalette palette)
        {
            _palette = palette ?? throw new System.ArgumentNullException(nameof(palette));
        }

        public Button ActionButton(string label)
        {
            var button = new Button
            {
                Content = label,
                MinWidth = 62d,
                Height = 44d,
                Margin = new Thickness(6d, 0d, 0d, 0d),
                Padding = new Thickness(14d, 0d, 14d, 0d),
                Foreground = _palette.TextBrush,
                FontSize = 12d,
                FontWeight = FontWeights.SemiBold,
                Background = _palette.RaisedSurfaceBrush,
                BorderBrush = _palette.BorderBrush,
                BorderThickness = new Thickness(1d),
                Style = ButtonStyle()
            };
            AutomationProperties.SetName(button, label);
            return button;
        }

        public void ConfigureScrollViewer(ScrollViewer scroll)
        {
            if (scroll == null)
            {
                return;
            }
            scroll.PanningMode = PanningMode.VerticalOnly;
            scroll.Resources[typeof(ScrollBar)] = CreateScrollBarStyle();
            scroll.PreviewMouseWheel += (sender, args) =>
            {
                var viewer = sender as ScrollViewer;
                if (viewer == null)
                {
                    return;
                }
                viewer.ScrollToVerticalOffset(viewer.VerticalOffset - (args.Delta / 120d * 64d));
                args.Handled = true;
            };
        }

        public Style ButtonStyle()
        {
            var xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type Button}'>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type Button}'>
        <Border x:Name='Frame' Background='{TemplateBinding Background}'
                BorderBrush='{TemplateBinding BorderBrush}'
                BorderThickness='{TemplateBinding BorderThickness}'
                CornerRadius='3' Padding='{TemplateBinding Padding}'>
          <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                            VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='Frame' Property='Background' Value='$HOVER_SURFACE'/>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$ACCENT_HOVER'/>
          </Trigger>
          <Trigger Property='IsPressed' Value='True'>
            <Setter TargetName='Frame' Property='Background' Value='$PANEL'/>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$ACCENT_PRESSED'/>
          </Trigger>
          <MultiTrigger>
            <MultiTrigger.Conditions>
              <Condition Property='Tag' Value='Primary'/>
              <Condition Property='IsMouseOver' Value='True'/>
            </MultiTrigger.Conditions>
            <Setter TargetName='Frame' Property='Background' Value='$ACCENT_HOVER'/>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$ACCENT_HOVER'/>
          </MultiTrigger>
          <MultiTrigger>
            <MultiTrigger.Conditions>
              <Condition Property='Tag' Value='Primary'/>
              <Condition Property='IsPressed' Value='True'/>
            </MultiTrigger.Conditions>
            <Setter TargetName='Frame' Property='Background' Value='$ACCENT_PRESSED'/>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$ACCENT_PRESSED'/>
          </MultiTrigger>
          <MultiTrigger>
            <MultiTrigger.Conditions>
              <Condition Property='Tag' Value='Warning'/>
              <Condition Property='IsMouseOver' Value='True'/>
            </MultiTrigger.Conditions>
            <Setter TargetName='Frame' Property='Background' Value='$WARNING_HOVER'/>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$WARNING_HOVER'/>
          </MultiTrigger>
          <MultiTrigger>
            <MultiTrigger.Conditions>
              <Condition Property='Tag' Value='Warning'/>
              <Condition Property='IsPressed' Value='True'/>
            </MultiTrigger.Conditions>
            <Setter TargetName='Frame' Property='Background' Value='$WARNING_PRESSED'/>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$WARNING_PRESSED'/>
          </MultiTrigger>
          <Trigger Property='IsKeyboardFocused' Value='True'>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$FOCUS'/>
            <Setter TargetName='Frame' Property='BorderThickness' Value='2'/>
          </Trigger>
          <Trigger Property='IsEnabled' Value='False'>
            <Setter TargetName='Frame' Property='Opacity' Value='.45'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            xaml = xaml
                .Replace("$HOVER_SURFACE", SettingsVisualTokens.HoverSurface)
                .Replace("$ACCENT_HOVER", SettingsVisualTokens.AccentHover)
                .Replace("$ACCENT_PRESSED", SettingsVisualTokens.AccentPressed)
                .Replace("$PANEL", SettingsVisualTokens.Panel)
                .Replace("$WARNING_HOVER", SettingsVisualTokens.WarningHover)
                .Replace("$WARNING_PRESSED", SettingsVisualTokens.WarningPressed)
                .Replace("$FOCUS", SettingsVisualTokens.FocusRing);
            return (Style)XamlReader.Parse(xaml);
        }

        private static Style CreateScrollBarStyle()
        {
            var xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type ScrollBar}'>
  <Setter Property='Width' Value='18'/>
  <Setter Property='MinWidth' Value='18'/>
  <Setter Property='Background' Value='$PANEL'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type ScrollBar}'>
        <Border Background='{TemplateBinding Background}'>
          <Track x:Name='PART_Track' IsDirectionReversed='True' Margin='3'>
            <Track.DecreaseRepeatButton>
              <RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Opacity='0' Focusable='False'/>
            </Track.DecreaseRepeatButton>
            <Track.Thumb>
              <Thumb MinHeight='44'>
                <Thumb.Template>
                  <ControlTemplate TargetType='{x:Type Thumb}'>
                    <Border x:Name='Grip' Background='$CONTROL_BORDER' CornerRadius='3'/>
                    <ControlTemplate.Triggers>
                      <Trigger Property='IsMouseOver' Value='True'>
                        <Setter TargetName='Grip' Property='Background' Value='$TEXT_SECONDARY'/>
                      </Trigger>
                      <Trigger Property='IsDragging' Value='True'>
                        <Setter TargetName='Grip' Property='Background' Value='$ACCENT'/>
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Opacity='0' Focusable='False'/>
            </Track.IncreaseRepeatButton>
          </Track>
        </Border>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            xaml = xaml
                .Replace("$PANEL", SettingsVisualTokens.Panel)
                .Replace("$CONTROL_BORDER", SettingsVisualTokens.ControlBorder)
                .Replace("$TEXT_SECONDARY", SettingsVisualTokens.TextSecondary)
                .Replace("$ACCENT", SettingsVisualTokens.Accent);
            return (Style)XamlReader.Parse(xaml);
        }
    }
}
