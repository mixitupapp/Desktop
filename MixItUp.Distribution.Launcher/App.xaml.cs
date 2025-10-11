using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using MaterialDesignThemes.Wpf;

namespace MixItUp.Distribution.Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SystemParameters.StaticPropertyChanged += this.OnSystemParametersChanged;
            this.ApplySystemPalette();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemParameters.StaticPropertyChanged -= this.OnSystemParametersChanged;
            base.OnExit(e);
        }

        private void OnSystemParametersChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(SystemParameters.HighContrast), StringComparison.Ordinal))
            {
                this.ApplySystemPalette();
            }
        }

        private void ApplySystemPalette()
        {
            BundledTheme theme = this.Resources.MergedDictionaries
                .OfType<BundledTheme>()
                .FirstOrDefault();

            if (theme == null)
            {
                return;
            }

            if (SystemParameters.HighContrast)
            {
                theme.BaseTheme = BaseTheme.Light;
                theme.ColorAdjustment = new ColorAdjustment
                {
                    Contrast = Contrast.High,
                    DesiredContrastRatio = 1.1f,
                    Colors = ColorSelection.All,
                };
            }
            else
            {
                theme.BaseTheme = BaseTheme.Inherit;
                theme.ColorAdjustment = new ColorAdjustment
                {
                    Contrast = Contrast.Medium,
                    DesiredContrastRatio = 0.15f,
                    Colors = ColorSelection.All,
                };
            }
        }
    }
}
