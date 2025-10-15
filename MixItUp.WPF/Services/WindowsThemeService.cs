using MixItUp.Base.Services;

namespace MixItUp.WPF.Services
{
    public class WindowsThemeService : IThemeService
    {
        public void ApplyTheme(string colorScheme, string backgroundColor, string fullThemeName)
        {
            ((App)System.Windows.Application.Current).SwitchTheme(colorScheme, backgroundColor, fullThemeName);
        }
    }
}