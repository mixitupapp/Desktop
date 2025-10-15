using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MaterialDesignThemes.Wpf;
using MaterialDesignColors;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MixItUp.WPF.Services
{
    public class WindowsThemeService : IThemeService
    {
        public void ApplyTheme(string colorScheme, string backgroundColor, string fullThemeName)
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

                // Color scheme
                colorScheme = (colorScheme ?? "Indigo").Replace(" ", "");
                if (Enum.TryParse<MaterialDesignColor>(colorScheme, out var materialColor))
                {
                    Color primaryColor = SwatchHelper.Lookup[materialColor];
                    theme.SetPrimaryColor(primaryColor);
                    theme.SetSecondaryColor(primaryColor);
                }

                paletteHelper.SetTheme(theme);

                // Mix It Up background color theme
                var backgroundDict = new ResourceDictionary
                {
                    Source = new Uri($"Themes/MixItUpBackgroundColor.{backgroundColor}.xaml", UriKind.Relative)
                };
                ReplaceResourceDictionary("MixItUpBackgroundColor.", backgroundDict);

                // LiveCharts theme
                LiveCharts.Configure(config =>
                {
                    config.AddSkiaSharp().AddDefaultMappers();
                    if (backgroundColor == "Light")
                    {
                        config.AddLightTheme();
                    }
                    else
                    {
                        config.AddDarkTheme();
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log($"Failed to switch theme. ColorScheme: {colorScheme}, Background: {backgroundColor}, FullTheme: {fullThemeName}");
            }
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