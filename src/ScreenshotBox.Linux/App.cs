using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using Avalonia.Media;

namespace ScreenshotBox.Linux;

public sealed class App : Application
{
    public static string[] Arguments { get; set; } = [];
    public static int TestExitCode { get; set; }
    public static AppSettings Settings { get; private set; } = new();
    public static string? SettingsFile { get; private set; }
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        var index = Array.IndexOf(Arguments, "--settings");
        if (index >= 0 && index + 1 < Arguments.Length) SettingsFile = Path.GetFullPath(Arguments[index + 1]);
        Settings = AppSettings.Load(SettingsFile);
        L.Apply(Settings.Language);
        ApplyTheme();
    }
    public static void ApplyTheme()
    {
        if (Current is not null) Current.RequestedThemeVariant = Settings.Theme switch { "Light" => ThemeVariant.Light, "Dark" => ThemeVariant.Dark, _ => ThemeVariant.Default };
    }
    public static void StyleWindow(Window window)
    {
        window.FontSize = 14;
        void Apply()
        {
            bool dark = window.ActualThemeVariant == ThemeVariant.Dark;
            IBrush Brush(string light, string night) => new SolidColorBrush(Color.Parse(dark ? night : light));
            window.Background = Brush("#F8F9FB", "#202124");
            window.Styles.Clear();
            window.Styles.Add(new Style(s => s.OfType<Button>().Class("flat")) { Setters = {
                new Setter(Button.BackgroundProperty, Brush("#FFFFFF", "#2B2D31")),
                new Setter(Button.BorderBrushProperty, Brush("#D9DEE5", "#45484E")),
                new Setter(Button.BorderThicknessProperty, new Thickness(1)),
                new Setter(Button.CornerRadiusProperty, new CornerRadius(4)) } });
            window.Styles.Add(new Style(s => s.OfType<Button>().Class("flat").Class(":pointerover")) { Setters = {
                new Setter(Button.BackgroundProperty, Brush("#EDF1F6", "#353A42")) } });
            window.Styles.Add(new Style(s => s.OfType<Button>().Class("navigation")) { Setters = {
                new Setter(Button.BackgroundProperty, Brushes.Transparent), new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                new Setter(Button.HorizontalAlignmentProperty, Avalonia.Layout.HorizontalAlignment.Stretch), new Setter(Button.PaddingProperty, new Thickness(10, 8)) } });
            window.Styles.Add(new Style(s => s.OfType<Button>().Class("selected")) { Setters = {
                new Setter(Button.BackgroundProperty, Brush("#E2ECFA", "#183B60")), new Setter(Button.BorderBrushProperty, new SolidColorBrush(Color.Parse("#1769C2"))) } });
        }
        Apply(); window.ActualThemeVariantChanged += (_, _) => Apply();
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            lifetime.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
