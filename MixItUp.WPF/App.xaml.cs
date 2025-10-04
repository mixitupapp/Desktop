using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MaterialDesignThemes.Wpf;
using MaterialDesignColors;
using MixItUp.Base;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using MixItUp.WPF.Services;
using MixItUp.WPF.Services.DeveloperAPI;
using MixItUp.WPF.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MixItUp.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool crashObtained = false;

        public App()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));

            try
            {
                // We need to load the language setting VERY early, so this is the minimal code necessary to get this value
                ServiceManager.Add<IDatabaseService>(new WindowsDatabaseService());
                ServiceManager.Add<IFileService>(new WindowsFileService());
                ServiceManager.Add<IInputService>(new WindowsInputService());
                ServiceManager.Add<IImageService>(new WindowsImageService());
                ServiceManager.Add<IAudioService>(new WindowsAudioService());
                ServiceManager.Add<IDeveloperAPIService>(new WindowsDeveloperAPIService());
                ServiceManager.Add<ITelemetryService>(new WindowsTelemetryService());
                ServiceManager.Add<IMusicPlayerService>(new WindowsMusicPlayerService());
                ServiceManager.Add<IProcessService>(new WindowsProcessService());
                ServiceManager.Add<IScriptRunnerService>(new WindowsScriptRunnerService());
                ServiceManager.Add(new WindowsMicrosoftAzureSpeechService());
                ServiceManager.Add(new StreamlabsService(new WindowsSocketIOConnection()));
                ServiceManager.Add(new RainmakerService(new WindowsSocketIOConnection()));
                ServiceManager.Add(new StreamElementsService(new WindowsSocketIOConnection()));
                ServiceManager.Add(new TipeeeStreamService(new WindowsSocketIOConnection()));
                ServiceManager.Add(new TreatStreamService(new WindowsSocketIOConnection()));
                ServiceManager.Add<IOvrStreamService>(new WindowsOvrStreamService());
                ServiceManager.Add<IOBSStudioService>(new WindowsOBSService());
                ServiceManager.Add(new WindowsSpeechService());
                ServiceManager.Add(new WindowsAmazonPollyService());

                ChannelSession.Initialize().Wait();

                System.Threading.Thread.CurrentThread.CurrentUICulture = Languages.GetLanguageLocaleCultureInfo();
            }
            catch { }
        }

        public void SwitchTheme(string colorScheme, string backgroundColorName, string fullThemeName)
        {
            try
            {
                var paletteHelper = new PaletteHelper();
                Theme theme = paletteHelper.GetTheme();

                // Handle custom full themes
                if (!string.IsNullOrEmpty(fullThemeName))
                {
                    var customThemeDict = new ResourceDictionary();
                    customThemeDict.Source = new Uri($"Themes/MixItUpTheme.{fullThemeName}.xaml", UriKind.Relative);

                    if (customThemeDict.Contains("MainApplicationBackground"))
                    {
                        SolidColorBrush mainApplicationBackground = (SolidColorBrush)customThemeDict["MainApplicationBackground"];
                        backgroundColorName = (mainApplicationBackground.ToString().Equals("#FFFFFFFF")) ? "Light" : "Dark";
                    }

                    if (customThemeDict.Contains("BaseTheme"))
                    {
                        string customBaseTheme = (string)customThemeDict["BaseTheme"];
                        var baseThemeDict = new ResourceDictionary()
                        {
                            Source = new Uri($"Themes/MixItUpBaseTheme.{customBaseTheme}.xaml", UriKind.Relative)
                        };

                        var existingBaseTheme = Application.Current.Resources.MergedDictionaries
                            .FirstOrDefault(rd => rd.Source != null && rd.Source.OriginalString.Contains("MixItUpBaseTheme"));
                        if (existingBaseTheme != null)
                        {
                            Application.Current.Resources.MergedDictionaries.Remove(existingBaseTheme);
                        }
                        Application.Current.Resources.MergedDictionaries.Add(baseThemeDict);
                    }

                    var existingCustomTheme = Application.Current.Resources.MergedDictionaries
                        .FirstOrDefault(rd => rd.Source != null && rd.Source.OriginalString.Contains("MixItUpTheme."));
                    if (existingCustomTheme != null)
                    {
                        Application.Current.Resources.MergedDictionaries.Remove(existingCustomTheme);
                    }
                    Application.Current.Resources.MergedDictionaries.Add(customThemeDict);
                }

                BaseTheme baseThemeEnum = backgroundColorName == "Light" ? BaseTheme.Light : BaseTheme.Dark;
                theme.SetBaseTheme(baseThemeEnum);

                if (!string.IsNullOrEmpty(colorScheme))
                {
                    colorScheme = colorScheme.Replace(" ", "");

                    if (Enum.TryParse<MaterialDesignColor>(colorScheme, out var materialColor))
                    {
                        Color primaryColor = SwatchHelper.Lookup[materialColor];
                        theme.SetPrimaryColor(primaryColor);
                        theme.SetSecondaryColor(primaryColor);
                    }
                }

                paletteHelper.SetTheme(theme);

                // Change Mix It Up Light/Dark Theme
                var existingMIUResourceDictionary = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(rd => rd.Source != null && Regex.Match(rd.Source.OriginalString, @"(MixItUpBackgroundColor\.)").Success);
                if (existingMIUResourceDictionary != null)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(existingMIUResourceDictionary);
                }

                var newMIUResourceDictionary = new ResourceDictionary()
                {
                    Source = new Uri($"Themes/MixItUpBackgroundColor.{backgroundColorName}.xaml", UriKind.Relative)
                };
                Application.Current.Resources.MergedDictionaries.Add(newMIUResourceDictionary);

                LiveCharts.Configure(config =>
                {
                    config.AddSkiaSharp().AddDefaultMappers();
                    if (backgroundColorName == "Light")
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
                Logger.Log($"Failed to switch theme. ColorScheme: {colorScheme}, Background: {backgroundColorName}, FullTheme: {fullThemeName}");
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            ActivationProtocolHandler.Initialize();

            RegistryHelpers.RegisterFileAssociation();
            RegistryHelpers.RegisterURIActivationProtocol();
            // Disabled for now until we can figure out why anti-virus hates it
            // RegistryHelpers.RegisterUninstaller();

            FileLoggerHandler.Initialize();

            DispatcherHelper.RegisterDispatcher(new WindowsDispatcher(this.Dispatcher));

            DialogHelper.Initialize(new WPFDialogShower());

            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                ChannelSession.IsElevated = id.Owner != id.User;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            Logger.ForceLog(LogLevel.Information, "Application Version: " + ServiceManager.Get<IFileService>().GetApplicationVersion());
            Logger.AlwaysLogFullStackTraceWithExceptions = true;
            if (ChannelSession.IsDebug() || (ChannelSession.AppSettings != null && ChannelSession.AppSettings.DiagnosticLogging))
            {
                Logger.SetLogLevel(LogLevel.Debug);
            }
            else
            {
                Logger.SetLogLevel(LogLevel.Error);
            }

            try
            {
                this.SwitchTheme(
                    ChannelSession.AppSettings.ColorScheme ?? "Indigo",
                    ChannelSession.AppSettings.BackgroundColor ?? "Light",
                    ChannelSession.AppSettings.FullThemeName
                );
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                this.SwitchTheme("Indigo", "Light", null);
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ActivationProtocolHandler.Close();

            base.OnExit(e);
        }

        private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) { this.HandleCrash(e.Exception); }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) { this.HandleCrash((Exception)e.ExceptionObject); }

        private void HandleCrash(Exception ex)
        {
            if (!this.crashObtained)
            {
#if DEBUG
                Debugger.Break();
#endif

                this.crashObtained = true;

                if (ServiceManager.Has<ITelemetryService>())
                {
                    ServiceManager.Get<ITelemetryService>().TrackException(ex);
                }

                try
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (Exception e in ex.UnwrapException())
                    {
                        sb.AppendLine(e.ToString());
                        sb.AppendLine();
                    }

                    using (StreamWriter writer = File.AppendText(FileLoggerHandler.CurrentLogFilePath))
                    {
                        writer.WriteLine("CRASHING EXCEPTION: " + Environment.NewLine + sb.ToString() + Environment.NewLine + Environment.StackTrace);
                    }
                }
                catch (Exception) { }

                ServiceManager.Get<IProcessService>().LaunchProgram("MixItUp.Reporter.exe", $"{FileLoggerHandler.CurrentLogFilePath} {ChannelSession.Settings?.Name ?? "NONE"}");

                Task.Delay(3000).Wait();
            }
        }
    }
}