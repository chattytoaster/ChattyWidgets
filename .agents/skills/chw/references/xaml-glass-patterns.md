# ChattyWidgets XAML & Glassmorphic Design Patterns

All ChattyWidgets must adhere to the high-aesthetic glassmorphic design system.

## 1. Root Container Pattern

Every `Widget.xaml` starts with a rounded glass container:

```xaml
<Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:ChattyWidgets.UI.Controls;assembly=ChattyWidgets"
        Background="#D8121622"
        BorderBrush="#25FFFFFF"
        BorderThickness="1"
        CornerRadius="20"
        ClipToBounds="True">

    <!-- Inner Content Grid -->
    <Grid Background="Transparent" Margin="14">
        <!-- Widget UI elements here -->
    </Grid>
</Border>
```

## 2. Dynamic Dark & Light Theme Contrast

Widgets automatically receive theme notifications. Never hardcode pure white text on transparent backgrounds without light-theme adaptation.

### Pattern A: Binding to ViewModel Brushes

```xaml
<TextBlock Text="{Binding MainTitle}"
           Foreground="{Binding PrimaryTextBrush, FallbackValue='#F8FAFC'}"
           FontSize="14"
           FontWeight="Bold"/>

<TextBlock Text="{Binding Subtitle}"
           Foreground="{Binding SecondaryTextBrush, FallbackValue='#94A3B8'}"
           FontSize="11"/>
```

In `NativeViewModelBase`:
- Dark theme: Primary `#F8FAFC`, Secondary `#94A3B8`, Accent `#38BDF8`.
- Light theme: Primary `#0F172A`, Secondary `#475569`, Accent `#0284C7`.

## 3. Running Marquee Text on Overflow

When text (such as news headlines, track names, or custom statuses) exceeds the widget container width, use `MarqueeBehavior`:

```xaml
<Grid ClipToBounds="True">
    <TextBlock Text="{Binding LongStatusText}"
               FontSize="13"
               FontWeight="SemiBold"
               Foreground="{Binding PrimaryTextBrush}"
               controls:MarqueeBehavior.EnableMarquee="True"/>
</Grid>
```

> [!IMPORTANT]
> The direct parent container (e.g. `Grid` or `Border`) MUST have `ClipToBounds="True"` so that the running text smoothly scrolls across the visible viewport window without spilling outside the widget.

## 4. Modern Flat Button Template

Never leave buttons with the default Windows 3D / Aero chrome gray background. Use clean flat vector styling:

```xaml
<Button Command="{Binding ActionCommand}"
        Height="30"
        Padding="10,0"
        Cursor="Hand">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Border x:Name="btnBorder"
                    Background="#1838BDF8"
                    BorderBrush="#3038BDF8"
                    BorderThickness="1"
                    CornerRadius="8">
                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter TargetName="btnBorder" Property="Background" Value="#3538BDF8"/>
                    <Setter TargetName="btnBorder" Property="BorderBrush" Value="#8038BDF8"/>
                </Trigger>
                <Trigger Property="IsPressed" Value="True">
                    <Setter TargetName="btnBorder" Property="Background" Value="#5038BDF8"/>
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>
    </Button.Template>
    <TextBlock Text="Action" FontSize="11" FontWeight="Bold" Foreground="#38BDF8"/>
</Button>
```

## 5. Image & Photo Opacity Mask (Rounded Corners)

WPF `Border.ClipToBounds="True"` does not natively clip child bitmaps to corner radii. Use an `OpacityMask` on the image container to guarantee smooth rounded corners:

```xaml
<Grid Background="Transparent">
    <Grid.OpacityMask>
        <VisualBrush>
            <VisualBrush.Visual>
                <Border Background="Black" CornerRadius="20"
                        Width="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Border}}"
                        Height="{Binding ActualHeight, RelativeSource={RelativeSource AncestorType=Border}}"/>
            </VisualBrush.Visual>
        </VisualBrush>
    </Grid.OpacityMask>

    <Image Source="{Binding DisplayImage}"
           Stretch="UniformToFill"
           Opacity="{Binding PhotoOpacity, FallbackValue=1.0}"/>
</Grid>
```
