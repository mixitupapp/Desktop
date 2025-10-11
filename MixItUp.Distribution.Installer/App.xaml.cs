using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Linq;
using MaterialDesignThemes.Wpf;

namespace MixItUp.Distribution.Installer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls | SecurityProtocolType.Tls12;
        }

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

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            AssemblyName assemblyName = new AssemblyName(args.Name);

            var path = assemblyName.Name + ".dll";
            if (assemblyName.CultureInfo.Equals(CultureInfo.InvariantCulture) == false)
                path = String.Format(@"{0}\{1}", assemblyName.CultureInfo, path);

            using (Stream stream = executingAssembly.GetManifestResourceStream(path))
            {
                if (stream == null)
                {
                    return null;
                }

                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    return Assembly.Load(memoryStream.ToArray());
                }
            }
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
