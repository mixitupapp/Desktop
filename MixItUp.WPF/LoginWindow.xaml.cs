using MixItUp.Base;
using MixItUp.Base.Model.API;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.WPF.Windows;
using MixItUp.WPF.Windows.Wizard;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Media;

namespace MixItUp.WPF
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : LoadingWindowBase
    {
        private MixItUpUpdateModel currentUpdate;
        private bool updateFound = false;
        private OutageModel currentOutage;

        private ThreadSafeObservableCollection<SettingsV3Model> streamerSettings = new ThreadSafeObservableCollection<SettingsV3Model>();
        private bool isBackupOnlyMode = false;

        public LoginWindow()
        {
            InitializeComponent();

            this.Initialize(this.StatusBar);
        }

        protected override async Task OnLoaded()
        {
            ChannelSession.OnRestartRequested += ChannelSession_OnRestartRequested;

            Version entryVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            string versionString = "v" + VersionHelper.NormalizeSemVerString(entryVersion);
            
            if (Util.BuildExpirationHelper.IsEnabled())
            {
                versionString += " -- TEST BUILD --";
            }
            
            this.Title += " - " + versionString;

            this.VersionTextBlock.Text = versionString;

            if (ServiceManager.Get<IProcessService>().GetProcessesByName("MixItUp").Count() > 1)
            {
                if (!await DialogHelper.ShowConfirmation(MixItUp.Base.Resources.MixItUpIsAlreadyRunning))
                {
                    this.Close();
                }
            }

            this.ExistingStreamerComboBox.ItemsSource = streamerSettings;

            await CheckForOutages();

            await this.CheckForUpdates();

            bool isBuildExpired = await Util.BuildExpirationHelper.CheckBuildExpiration();
            if (isBuildExpired)
            {
                isBackupOnlyMode = true;
                await DialogHelper.ShowMessage(
                    $"This build expired on {Util.BuildExpirationHelper.GetExpirationDate().ToShortDateString()}.\n\n" +
                    "You can use the Backup Settings button to backup your profile.");
            }

            foreach (SettingsV3Model setting in (await ServiceManager.Get<SettingsService>().GetAllSettings()).OrderBy(s => s.Name))
            {
                this.streamerSettings.Add(setting);
            }

            if (this.streamerSettings.Count > 0)
            {
                this.ExistingStreamerComboBox.Visibility = Visibility.Visible;
                this.StreamerLoginButton.IsEnabled = true;
                if (this.streamerSettings.Count == 1)
                {
                    this.ExistingStreamerComboBox.SelectedIndex = 0;
                }
            }

            if (isBackupOnlyMode)
            {
                this.StreamerLoginButton.Content = MixItUp.Base.Resources.BackupSettings;
                this.NewStreamerLoginButton.IsEnabled = false;
                this.NewStreamerLoginButton.Visibility = Visibility.Collapsed;
                this.RestoreBackupButton.IsEnabled = false;
            }

            if (ChannelSession.AppSettings.AutoLogInID != Guid.Empty)
            {
                var allSettings = this.streamerSettings.ToList();
                SettingsV3Model autoLogInSettings = allSettings.FirstOrDefault(s => s.ID == ChannelSession.AppSettings.AutoLogInID);
                if (autoLogInSettings != null)
                {
                    await Task.Delay(5000);

                    if (!updateFound)
                    {
                        if (await this.ExistingSettingLogin(autoLogInSettings))
                        {
                            LoadingWindowBase newWindow = null;
                            if (ChannelSession.Settings.ReRunWizard)
                            {
                                newWindow = new NewUserWizardWindow();
                            }
                            else
                            {
                                newWindow = new MainWindow();
                            }
                            ShowMainWindow(newWindow);
                            this.Hide();
                            this.Close();
                            return;
                        }
                    }
                }
            }

            await base.OnLoaded();
        }

        private async void StreamerLoginButton_Click(object sender, RoutedEventArgs e)
        {
            await this.RunAsyncOperation(async () =>
            {
                if (this.ExistingStreamerComboBox.SelectedIndex >= 0)
                {
                    SettingsV3Model setting = (SettingsV3Model)this.ExistingStreamerComboBox.SelectedItem;
                    if (setting.ID != Guid.Empty)
                    {
                        if (isBackupOnlyMode)
                        {
                            await this.BackupProfileSettings(setting);
                        }
                        else
                        {
                            if (await this.ExistingSettingLogin(setting))
                            {
                                LoadingWindowBase newWindow = null;
                                if (ChannelSession.Settings.ReRunWizard)
                                {
                                    newWindow = new NewUserWizardWindow();
                                }
                                else
                                {
                                    newWindow = new MainWindow();
                                }

                                ChannelSession.OnRestartRequested -= ChannelSession_OnRestartRequested;

                                ShowMainWindow(newWindow);
                                this.Hide();
                                this.Close();
                                return;
                            }
                        }
                    }
                }
                else
                {
                    await DialogHelper.ShowMessage(MixItUp.Base.Resources.LoginErrorNoStreamerAccount);
                }
            });
        }

        private async Task CheckForUpdates()
        {
            this.currentUpdate = await ServiceManager.Get<MixItUpService>().GetLatestUpdate();
            if (this.currentUpdate != null)
            {
                Version currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0, 0);
                Version updateVersion = this.currentUpdate.GetNormalizedVersion();

                bool hasNewerVersion = updateVersion > currentVersion;
                bool semverMatches = VersionHelper.SemVerEquals(currentVersion, this.currentUpdate.Version);

                if (hasNewerVersion || (this.currentUpdate.Mandatory && !semverMatches))
                {
                    updateFound = true;

                    if (this.currentUpdate.Mandatory)
                    {
                        bool launched = await UpdateWindow.DownloadAndInstallUpdate(this.currentUpdate);
                        if (launched)
                        {
                            return;
                        }
                    }

                    UpdateWindow window = new UpdateWindow(this.currentUpdate);
                    window.Show();
                }
            }
        }

        private async Task CheckForOutages()
        {
            try
            {
                currentOutage = await ServiceManager.Get<MixItUpService>().CheckOutageStatus();

                if (currentOutage != null && currentOutage.Enabled && !string.IsNullOrEmpty(currentOutage.Message))
                {
                    OutageBanner.Visibility = Visibility.Visible;
                    OutageMessage.Text = currentOutage.Message;

                    switch (currentOutage.Severity?.ToLower())
                    {
                        case "info":
                            OutageBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2"));
                            OutageIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Information;
                            break;

                        case "warning":
                            OutageBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F57C00"));
                            OutageIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Warning;
                            break;

                        case "critical":
                            OutageBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F"));
                            OutageIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"Failed to check for outages: {ex.Message}");
            }
        }

        private async Task<bool> ExistingSettingLogin(SettingsV3Model setting)
        {
            Result result = await ChannelSession.Connect(setting);
            if (result.Success)
            {
                result = await ChannelSession.InitializeSession();
                if (result.Success)
                {
                    return true;
                }
            }
            await DialogHelper.ShowMessage(result.Message);
            return false;
        }

        private async Task BackupProfileSettings(SettingsV3Model setting)
        {
            string filePath = ServiceManager.Get<IFileService>().ShowSaveFileDialog(
                setting.Name + "." + SettingsV3Model.SettingsBackupFileExtension, 
                MixItUp.Base.Resources.MixItUpBackupFileFormatFilter);
            
            if (!string.IsNullOrEmpty(filePath))
            {
                await ServiceManager.Get<SettingsService>().SavePackagedBackup(setting, filePath);
                await DialogHelper.ShowMessage(
                    $"Backup saved to:\n{filePath}\n\nYou may now close the program.");
            }
        }


        private void NewStreamerLoginButton_Click(object sender, RoutedEventArgs e)
        {
            ChannelSession.OnRestartRequested -= ChannelSession_OnRestartRequested;

            ShowMainWindow(new NewUserWizardWindow());
            this.Hide();
            this.Close();
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            await SettingsV3Model.RestoreSettingsBackup();
        }

        private async void ChannelSession_OnRestartRequested(object sender, EventArgs e)
        {
            await ChannelSession.AppSettings.Save();

            this.Close();

            ServiceManager.Get<IProcessService>().LaunchProgram(Environment.ProcessPath);
        }

        public void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ServiceManager.Get<IProcessService>().LaunchLink(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void OpenInstallFolder_Click(object sender, RoutedEventArgs e)
        {
            ServiceManager.Get<IProcessService>().LaunchFolder(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
        }

        private void OpenDiscord_Click(object sender, RoutedEventArgs e)
        {
            ServiceManager.Get<IProcessService>().LaunchLink("https://mixitupapp.com/discord");
        }

        private async void ResetWindowPosition_Click(object sender, RoutedEventArgs e)
        {
            ChannelSession.AppSettings.Top = 0;
            ChannelSession.AppSettings.Left = 0;
            ChannelSession.AppSettings.Width = 0;
            ChannelSession.AppSettings.Height = 0;
            ChannelSession.AppSettings.IsMaximized = false;
            ChannelSession.AppSettings.DashboardTop = 0;
            ChannelSession.AppSettings.DashboardLeft = 0;
            ChannelSession.AppSettings.DashboardWidth = 0;
            ChannelSession.AppSettings.DashboardHeight = 0;
            ChannelSession.AppSettings.IsDashboardMaximized = false;
            
            await ChannelSession.AppSettings.Save();
            
            await DialogHelper.ShowMessage(MixItUp.Base.Resources.ResetWindowPositionDialog);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
        private void CloseOutageBannerButton_Click(object sender, RoutedEventArgs e)
        {
            OutageBanner.Visibility = Visibility.Collapsed;
        }

    }
}