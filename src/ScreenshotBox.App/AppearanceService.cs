using Microsoft.Win32;
namespace ScreenshotBox.App;
public static class AppearanceService
{
    public static void Apply(string preference)
    {
        bool dark=preference=="Dark";
        if(preference=="System") {
            using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark=key?.GetValue("AppsUseLightTheme") is int light && light==0;
        }
        var theme=dark?Wpf.Ui.Appearance.ApplicationTheme.Dark:Wpf.Ui.Appearance.ApplicationTheme.Light;
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme,Wpf.Ui.Controls.WindowBackdropType.None,false);
        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(Color.FromRgb(23,105,194),theme,false,false);
        var resources=Application.Current.Resources;
        Set("SbBackground",dark?"#202020":"#F6F6F6");Set("SbSurface",dark?"#2C2C2C":"#FFFFFF");
        Set("SbText",dark?"#F1F1F1":"#202020");Set("SbMuted",dark?"#B0B0B0":"#666666");
        Set("SbStroke",dark?"#484848":"#D8D8D8");Set("SbAccent","#1769C2");Set("SbAccentText",dark?"#75B5FF":"#1769C2");Set("SbSelection",dark?"#173653":"#E5F0FB");
        foreach(Window window in Application.Current.Windows) {
            if(window.GetType().Namespace?.Contains("Native")==true)continue;
            window.SetResourceReference(Window.BackgroundProperty,"SbBackground");window.SetResourceReference(Window.ForegroundProperty,"SbText");
        }
        void Set(string name,string color)=>resources[name]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
