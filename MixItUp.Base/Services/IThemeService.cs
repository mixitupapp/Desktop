namespace MixItUp.Base.Services
{
    public interface IThemeService
    {
        bool IsDarkTheme { get; }

        event System.EventHandler ThemeChanged;

        void ApplyTheme(string colorScheme, string backgroundColor, string foregroundColor, string fullThemeName);
    }
}