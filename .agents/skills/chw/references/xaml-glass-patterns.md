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

## 5. Image & Photo Opacity Mask (Rounded Corners on Dynamic Resize)

WPF `Border.ClipToBounds="True"` only clips child elements to rectangular bounding boxes and does not clip bitmaps to `CornerRadius`. Use an `OpacityMask` with `VisualBrush` on the inner container:

```xaml
<Grid x:Name="MediaGrid" Background="Transparent">
    <Grid.OpacityMask>
        <VisualBrush Stretch="None" AlignmentX="Left" AlignmentY="Top">
            <VisualBrush.Visual>
                <Border Background="Black" CornerRadius="19"
                        Width="{Binding ActualWidth, ElementName=MediaGrid}"
                        Height="{Binding ActualHeight, ElementName=MediaGrid}"/>
            </VisualBrush.Visual>
        </VisualBrush>
    </Grid.OpacityMask>

    <Image Source="{Binding DisplayImage}"
           Stretch="UniformToFill"
           Opacity="{Binding PhotoOpacity, FallbackValue=1.0}"/>

    <!-- If any overlay bar is docked to the bottom, give it bottom corner radii -->
    <Border VerticalAlignment="Bottom" Background="#80000000" CornerRadius="0,0,19,19" Padding="8,4">
        <TextBlock Text="{Binding Caption}" Foreground="#CBD5E1"/>
    </Border>
</Grid>
```

> [!IMPORTANT]
> - Always set `Stretch="None"` and `AlignmentX="Left" AlignmentY="Top"` on `VisualBrush`. This prevents WPF composition from distorting or flattening the corner curves into squares when the widget window is shrunk.
> - Bind `Width` and `Height` using `ElementName` rather than `RelativeSource AncestorType=Border`, ensuring instant re-evaluation during dynamic window resizing.
> - Inner clipping radius should match outer radius minus border thickness (e.g., `20 - 1 = 19`).

## 6. Proportional 4:3 Layout & Responsive Scaling (320×240 Standard)

All ChattyWidgets must adhere to the unified **4:3 aspect ratio** to maintain a cohesive row layout on the desktop:

- **Default Viewport**: `320×240` (100% scale).
- **Size Presets**: `80×60` (25%), `160×120` (50%), `240×180` (75%), `320×240` (100%), `400×300` (125%).

### Best Practices for Responsive Layouts:
1. **Dynamic Grid Sizing**:
   - Do NOT use hardcoded pixel heights (e.g. `Height="240"`) on inner containers.
   - Use `Grid` with `*` and `Auto` rows/columns, `UniformGrid`, or `DockPanel`.
2. **Vector Graphics & Gauges in `<Viewbox>`**:
   - Wrap circular dials, meters, or graphics inside `<Viewbox>` containers:
     ```xaml
     <Viewbox Stretch="Uniform" HorizontalAlignment="Center" VerticalAlignment="Center">
         <controls:CircularGauge Width="120" Height="120" Value="{Binding CpuUsage}"/>
     </Viewbox>
     ```
   - This ensures vector elements scale smoothly down to `240×180` and up to `480×360` without pixelation or clipping.
3. **Text Overflow Handling**:
   - Always protect text from overflowing or pushing other elements out of the viewport.
   - Use `TextTrimming="CharacterEllipsis"` for concise labels, or `controls:MarqueeBehavior.EnableMarquee="True"` inside clipped containers for long running titles.
