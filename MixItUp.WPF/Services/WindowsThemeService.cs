using MaterialDesignThemes.Wpf;
using MaterialDesignColors;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MixItUp.WPF.Services
{
    public class WindowsThemeService : IThemeService
    {
        public bool IsDarkTheme { get; private set; } = false;

        public event EventHandler ThemeChanged = delegate { };

        public void ApplyTheme(string colorScheme, string backgroundColor, string foregroundColor, string fullThemeName)
        {
            try
            {
                var paletteHelper = new PaletteHelper();
                Theme theme = paletteHelper.GetTheme();

                bool hasFullTheme = !string.IsNullOrEmpty(fullThemeName);

                // Custom theme
                if (hasFullTheme)
                {
                    var customThemeDict = new ResourceDictionary
                    {
                        Source = new Uri($"Themes/MixItUpTheme.{fullThemeName}.xaml", UriKind.Relative)
                    };

                    if (customThemeDict.Contains("MainApplicationBackground"))
                    {
                        SolidColorBrush mainApplicationBackground = (SolidColorBrush)customThemeDict["MainApplicationBackground"];
                        backgroundColor = (mainApplicationBackground.ToString().Equals("#FFFFFFFF")) ? "Light" : "Dark";
                    }

                    if (customThemeDict.Contains("BaseTheme"))
                    {
                        string customBaseTheme = (string)customThemeDict["BaseTheme"];
                        var baseThemeDict = new ResourceDictionary
                        {
                            Source = new Uri($"Themes/MixItUpBaseTheme.{customBaseTheme}.xaml", UriKind.Relative)
                        };

                        ReplaceResourceDictionary("MixItUpBaseTheme", baseThemeDict);
                    }

                    ReplaceResourceDictionary("MixItUpTheme.", customThemeDict);
                }
                else
                {
                    RemoveResourceDictionary("MixItUpTheme.");
                    RemoveResourceDictionary("MixItUpBaseTheme");
                }

                // Material Design base theme
                BaseTheme baseThemeEnum = backgroundColor == "Light" ? BaseTheme.Light : BaseTheme.Dark;
                theme.SetBaseTheme(baseThemeEnum);
                this.IsDarkTheme = baseThemeEnum == BaseTheme.Dark;

                // Color scheme
                colorScheme = (colorScheme ?? "Indigo").Replace(" ", "");
                if (Enum.TryParse<MaterialDesignColor>(colorScheme, out var materialColor))
                {
                    Color primaryColor = SwatchHelper.Lookup[materialColor];
                    theme.SetPrimaryColor(primaryColor);
                    theme.SetSecondaryColor(primaryColor);
                }

                paletteHelper.SetTheme(theme);

                Application.Current.Resources.Remove("MaterialDesign.Brush.Primary.Foreground");
                Application.Current.Resources.Remove("MaterialDesign.Brush.Primary.Light.Foreground");
                Application.Current.Resources.Remove("MaterialDesign.Brush.Primary.Dark.Foreground");

                // Foreground color override
                if (!hasFullTheme && !string.IsNullOrEmpty(foregroundColor) && foregroundColor != "Default")
                {
                    Color fgColor = foregroundColor == "Black" ? Colors.Black : Colors.White;
                    var fgBrush = new SolidColorBrush(fgColor);
                    fgBrush.Freeze();
                    Application.Current.Resources["MaterialDesign.Brush.Primary.Foreground"] = fgBrush;
                }

                // Mix It Up background color theme
                var backgroundDict = new ResourceDictionary
                {
                    Source = new Uri($"Themes/MixItUpBackgroundColor.{backgroundColor}.xaml", UriKind.Relative)
                };
                ReplaceResourceDictionary("MixItUpBackgroundColor.", backgroundDict);

                this.ThemeChanged(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log($"Failed to switch theme. ColorScheme: {colorScheme}, Background: {backgroundColor}, FullTheme: {fullThemeName}");
            }
        }

        public void ApplyFont(string fontName)
        {
            try
            {
                // Only the body font keys are swapped. The Material Symbols icon font lives under its own
                // resource key (see MixItUp.WPF/Branding/MaterialSymbols.cs) and must never be included here,
                // otherwise every icon in the application turns into fallback glyphs. The BebasNeue display
                // font is intentionally left alone as well so headlines keep the Mix It Up branding.
                FontFamily family = ResolveFontFamily(fontName);

                Application.Current.Resources["NotoSans"] = family;
                Application.Current.Resources["MaterialDesignFont"] = family;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log($"Failed to switch UI font. Font: {fontName}");
            }
        }

        public void ApplyFontScale(double scale)
        {
            try
            {
                // Captured once, before anything has been scaled. Every apply recomputes from these
                // originals rather than from the live resource value, otherwise repeated calls would
                // compound on each other (1.5x applied twice would render at 2.25x).
                if (this.baseFontSizes == null)
                {
                    this.baseFontSizes = CaptureBaseFontSizes();
                }

                double min = ApplicationSettingsV2Model.MinimumUIScale / 100.0;
                double max = ApplicationSettingsV2Model.MaximumUIScale / 100.0;
                scale = Math.Min(Math.Max(scale, min), max);

                // Only the text scale moves. Icon sizes (MIU.IconSize.*) are deliberately excluded:
                // too many icons sit inside fixed-size controls for scaling them to be safe, so they
                // are left at their designed sizes. Keeping the two token sets separate is what makes
                // that a one-line distinction here.
                foreach (KeyValuePair<string, double> baseSize in this.baseFontSizes)
                {
                    Application.Current.Resources[baseSize.Key] = baseSize.Value * scale;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log($"Failed to apply UI font scale. Scale: {scale}");
            }
        }

        private Dictionary<string, double> baseFontSizes;

        private const string FontSizeTokenPrefix = "MIU.FontSize.";

        private static Dictionary<string, double> CaptureBaseFontSizes()
        {
            Dictionary<string, double> baseSizes = new Dictionary<string, double>();
            CaptureBaseFontSizes(Application.Current.Resources, baseSizes);
            return baseSizes;
        }

        private static void CaptureBaseFontSizes(ResourceDictionary dictionary, Dictionary<string, double> baseSizes)
        {
            // A dictionary's own entries win over its merged ones, matching how WPF resolves a lookup,
            // so the first value found for a key is the effective one.
            foreach (object key in dictionary.Keys)
            {
                if (key is string name && name.StartsWith(FontSizeTokenPrefix) && !baseSizes.ContainsKey(name))
                {
                    if (dictionary[key] is double size)
                    {
                        baseSizes[name] = size;
                    }
                }
            }

            foreach (ResourceDictionary merged in dictionary.MergedDictionaries)
            {
                CaptureBaseFontSizes(merged, baseSizes);
            }
        }

        public IEnumerable<string> GetAvailableFonts()
        {
            try
            {
                // Enumerated through WPF rather than IFileService.GetInstalledFonts(), which is GDI+ based.
                // The two disagree on family names for a lot of fonts - GDI+ reports "8BIT WONDER" where WPF
                // needs "8BIT WONDER Nominal" - and a family name WPF can not resolve renders as a silent
                // fallback face instead of the font the user picked. Only WPF's own names are safe here.
                return Fonts.SystemFontFamilies
                    .Select(f => f.Source)
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new List<string>();
            }
        }

        private FontFamily ResolveFontFamily(string fontName)
        {
            if (!string.IsNullOrWhiteSpace(fontName) && !string.Equals(fontName, ApplicationSettingsV2Model.DefaultUIFontFamily, StringComparison.OrdinalIgnoreCase))
            {
                // A font that is no longer installed would silently render as an arbitrary WPF fallback,
                // so anything that can not be found resolves back to the packaged font instead.
                if (Fonts.SystemFontFamilies.Any(f => string.Equals(f.Source, fontName, StringComparison.OrdinalIgnoreCase) ||
                    f.FamilyNames.Values.Any(name => string.Equals(name, fontName, StringComparison.OrdinalIgnoreCase))))
                {
                    return new FontFamily(fontName);
                }

                Logger.Log($"UI font '{fontName}' is not installed, falling back to {ApplicationSettingsV2Model.DefaultUIFontFamily}.");
            }

            return new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Noto Sans");
        }

        private void ReplaceResourceDictionary(string searchPattern, ResourceDictionary newDict)
        {
            var existing = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(rd => rd.Source != null && rd.Source.OriginalString.Contains(searchPattern));

            if (existing != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existing);
            }

            Application.Current.Resources.MergedDictionaries.Add(newDict);
        }

        private void RemoveResourceDictionary(string searchPattern)
        {
            var existing = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(rd => rd.Source != null && rd.Source.OriginalString.Contains(searchPattern));

            if (existing != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existing);
            }
        }
    }
}