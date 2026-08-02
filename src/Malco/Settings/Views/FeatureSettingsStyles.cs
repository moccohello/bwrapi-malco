using System.Windows;
using System.Windows.Markup;

namespace Malco.Settings.Views
{
    internal static class FeatureSettingsStyles
    {
        public static Style CreateTextBox()
        {
            var xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type TextBox}'>
  <Setter Property='CaretBrush' Value='$ACCENT'/>
  <Setter Property='SelectionBrush' Value='$ACCENT_PRESSED'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type TextBox}'>
        <Border x:Name='Frame' Background='{TemplateBinding Background}'
                BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'
                CornerRadius='3'>
          <ScrollViewer x:Name='PART_ContentHost' Padding='{TemplateBinding Padding}'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$ACCENT_HOVER'/>
          </Trigger>
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
                .Replace("$ACCENT_HOVER", SettingsVisualTokens.AccentHover)
                .Replace("$ACCENT_PRESSED", SettingsVisualTokens.AccentPressed)
                .Replace("$ACCENT", SettingsVisualTokens.Accent)
                .Replace("$FOCUS", SettingsVisualTokens.FocusRing);
            return (Style)XamlReader.Parse(xaml);
        }

        public static Style CreateSwitch()
        {
            var xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type ToggleButton}'>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type ToggleButton}'>
        <Grid Width='44' Height='24' VerticalAlignment='Center'>
          <Border x:Name='Track' Background='$TOGGLE_OFF' BorderBrush='$CONTROL_BORDER'
                  BorderThickness='1' CornerRadius='12'/>
          <Ellipse x:Name='Thumb' Width='18' Height='18' Margin='3'
                   HorizontalAlignment='Left' Fill='$TEXT_SECONDARY'/>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property='IsChecked' Value='True'>
            <Setter TargetName='Track' Property='Background' Value='$SELECTED_SURFACE'/>
            <Setter TargetName='Track' Property='BorderBrush' Value='$ACCENT'/>
            <Setter TargetName='Thumb' Property='Fill' Value='$ACCENT'/>
            <Setter TargetName='Thumb' Property='HorizontalAlignment' Value='Right'/>
          </Trigger>
          <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='Track' Property='BorderBrush' Value='$ACCENT_HOVER'/>
          </Trigger>
          <Trigger Property='IsKeyboardFocused' Value='True'>
            <Setter TargetName='Track' Property='BorderBrush' Value='$FOCUS'/>
            <Setter TargetName='Track' Property='BorderThickness' Value='2'/>
          </Trigger>
          <Trigger Property='IsEnabled' Value='False'>
            <Setter Property='Opacity' Value='.45'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            xaml = xaml
                .Replace("$TOGGLE_OFF", SettingsVisualTokens.ToggleOff)
                .Replace("$CONTROL_BORDER", SettingsVisualTokens.ControlBorder)
                .Replace("$TEXT_SECONDARY", SettingsVisualTokens.TextSecondary)
                .Replace("$SELECTED_SURFACE", SettingsVisualTokens.SelectedSurface)
                .Replace("$ACCENT_HOVER", SettingsVisualTokens.AccentHover)
                .Replace("$ACCENT", SettingsVisualTokens.Accent)
                .Replace("$FOCUS", SettingsVisualTokens.FocusRing);
            return (Style)XamlReader.Parse(xaml);
        }

        public static Style CreateSegment()
        {
            var xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='{x:Type RadioButton}'>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='{x:Type RadioButton}'>
        <Border x:Name='Frame' Background='{TemplateBinding Background}'
                BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'
                CornerRadius='3' Padding='{TemplateBinding Padding}'>
          <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsChecked' Value='True'>
            <Setter TargetName='Frame' Property='Background' Value='$SELECTED_SURFACE'/>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$ACCENT'/>
          </Trigger>
          <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='Frame' Property='Background' Value='$HOVER_SURFACE'/>
          </Trigger>
          <Trigger Property='IsKeyboardFocused' Value='True'>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$FOCUS'/>
            <Setter TargetName='Frame' Property='BorderThickness' Value='2'/>
          </Trigger>
          <MultiTrigger>
            <MultiTrigger.Conditions>
              <Condition Property='IsChecked' Value='True'/>
              <Condition Property='IsMouseOver' Value='True'/>
            </MultiTrigger.Conditions>
            <Setter TargetName='Frame' Property='Background' Value='$SELECTED_SURFACE'/>
            <Setter TargetName='Frame' Property='BorderBrush' Value='$ACCENT_HOVER'/>
          </MultiTrigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            xaml = xaml
                .Replace("$SELECTED_SURFACE", SettingsVisualTokens.SelectedSurface)
                .Replace("$HOVER_SURFACE", SettingsVisualTokens.HoverSurface)
                .Replace("$ACCENT_HOVER", SettingsVisualTokens.AccentHover)
                .Replace("$ACCENT", SettingsVisualTokens.Accent)
                .Replace("$FOCUS", SettingsVisualTokens.FocusRing);
            return (Style)XamlReader.Parse(xaml);
        }
    }
}
