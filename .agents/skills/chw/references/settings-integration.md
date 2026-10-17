# ChattyWidgets Settings & ViewModel Integration Guide

When building widgets that require native C# business logic, timers, system APIs, or customized settings dialogues, follow this architecture.

## 1. Creating the ViewModel in `NativeWidgetContextProvider.cs`

All native widget ViewModels inherit from `NativeViewModelBase`:

```csharp
public class MyWidgetViewModel : NativeViewModelBase
{
    private string _customText = "Hello";
    public string CustomText
    {
        get => _customText;
        set { if (_customText != value) { _customText = value; OnPropertyChanged(); } }
    }

    public MyWidgetViewModel(string instanceId) : base(instanceId)
    {
        var win = WidgetManager.Instance.GetActiveWindow(instanceId);
        if (win?.Settings?.CustomSettings != null)
        {
            if (win.Settings.CustomSettings.TryGetValue("customText", out var savedText))
                _customText = savedText;
        }
    }

    protected override void OnThemeChanged()
    {
        // React to Dark / Light theme change
        OnPropertyChanged(nameof(PrimaryTextBrush));
        OnPropertyChanged(nameof(SecondaryTextBrush));
    }

    protected override void OnOpacityChanged()
    {
        // React to window opacity change
    }
}
```

### Factory Registration

In `NativeWidgetContextProvider.CreateContext(string widgetId, string instanceId, ...)`:

```csharp
"com.chatty.my-widget" => new MyWidgetViewModel(instanceId),
```

---

## 2. Registering Localized Strings in `LocalizationService.cs`

In `Core/Services/LocalizationService.cs`, register both Russian and English translations:

```csharp
// My Widget
Add("MyWidget.Title", "Настройки Моего Виджета", "My Widget Settings");
Add("MyWidget.Header", "⚙️ Параметры отображения", "⚙️ Display Settings");
Add("MyWidget.HeaderSub", "Настройка текста, частоты обновлений и цветов", "Configure text, refresh rate, and colors");
Add("MyWidget.CustomTextLabel", "Пользовательский текст:", "Custom text:");
Add("MyWidget.SettingsSaved", "Настройки виджета сохранены", "Widget settings saved");
```

---

## 3. Building Settings Cards in `WidgetSettingsWindow.xaml.cs`

In `UI/WidgetSettingsWindow.xaml.cs`:

### 1. Route in `BuildSettingsContent()`

```csharp
case "com.chatty.my-widget":
    BuildMyWidgetSettings();
    break;
```

### 2. Implement `BuildMyWidgetSettings()`

```csharp
private void BuildMyWidgetSettings()
{
    var loc = LocalizationService.Instance;
    TxtWindowTitle.Text = loc.T("MyWidget.Title", "Настройки Моего Виджета");
    AddSectionHeader(
        loc.T("MyWidget.Header", "⚙️ Параметры отображения"),
        loc.T("MyWidget.HeaderSub", "Настройка текста и параметров виджета"));

    var win = WidgetManager.Instance.GetActiveWindow(_instanceId);
    var vm = _viewModel as MyWidgetViewModel;

    // Card 1: Text Input Card
    var card = CreateCard();
    var sp = new StackPanel();

    sp.Children.Add(new TextBlock
    {
        Text = loc.T("MyWidget.CustomTextLabel", "Пользовательский текст:"),
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White,
        Margin = new Thickness(0, 0, 0, 6)
    });

    var txtBox = new TextBox
    {
        Height = 32,
        Background = new SolidColorBrush(Color.FromRgb(22, 25, 40)),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
        Text = vm?.CustomText ?? "",
        Margin = new Thickness(0, 0, 0, 8)
    };

    txtBox.TextChanged += (s, e) =>
    {
        string val = txtBox.Text.Trim();
        if (vm != null) vm.CustomText = val;
        if (win != null)
        {
            win.Settings.CustomSettings["customText"] = val;
            WidgetManager.Instance.SaveSettings();
        }
        TxtStatus.Text = loc.T("MyWidget.SettingsSaved", "Настройки сохранены");
        _onSettingsChanged?.Invoke();
    };

    sp.Children.Add(txtBox);
    card.Child = sp;
    SettingsPanel.Children.Add(card);
}
```
