using MixItUp.Base.Model.API;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.WPF.Windows;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace MixItUp.WPF
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : LoadingWindowBase
    {
        private MixItUpUpdateModel update;

        public UpdateWindow(MixItUpUpdateModel update)
        {
            this.update = update;

            InitializeComponent();

            this.Initialize(this.StatusBar);
            this.AttachHyperlinkHandler();
        }

        protected override async Task OnLoaded()
        {
            this.NewVersionTextBlock.Text = this.update.Version;
            Version entryVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            this.CurrentVersionTextBlock.Text = VersionHelper.NormalizeSemVerString(entryVersion);

            if (this.update.IsPreview)
            {
                this.PreviewUpdateGrid.Visibility = Visibility.Visible;
            }

            this.SkipUpdateButton.Visibility = this.update.Mandatory ? Visibility.Collapsed : Visibility.Visible;
            this.SkipUpdateButton.IsEnabled = !this.update.Mandatory;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(this.update.ChangelogLink);
                    if (response.IsSuccessStatusCode)
                    {
                        string markdown = await response.Content.ReadAsStringAsync();
                        this.UpdateChangelogViewer.Markdown = markdown;
                    }
                    else
                    {
                        Logger.Log(LogLevel.Warning, $"Failed to retrieve changelog from {this.update.ChangelogLink}: {(int)response.StatusCode} {response.ReasonPhrase}");
                        this.UpdateChangelogViewer.Markdown = "Unable to load changelog.";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                this.UpdateChangelogViewer.Markdown = "Unable to load changelog.";
            }

            await base.OnLoaded();
        }

        private async void DownloadUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await this.RunAsyncOperation(async () =>
            {
                await DownloadAndInstallUpdate(this.update);
            });
        }

        internal static async Task<bool> DownloadAndInstallUpdate(MixItUpUpdateModel update)
        {
            if (update == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(update.InstallerLink))
            {
                await DialogHelper.ShowMessage("Installer URL missing from the update manifest.");
                return false;
            }

            string setupFilePath = Path.Combine(Path.GetTempPath(), $"MixItUp-Setup-{Guid.NewGuid():N}.exe");

            try
            {
                using (HttpClient client = new HttpClient())
                using (HttpResponseMessage response = await client.GetAsync(update.InstallerLink, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.Log(LogLevel.Warning, $"Failed to download installer from {update.InstallerLink}: {(int)response.StatusCode} {response.ReasonPhrase}");
                        await DialogHelper.ShowMessage("Unable to download the installer. Please try again later.");
                        return false;
                    }

                    using (Stream sourceStream = await response.Content.ReadAsStreamAsync())
                    using (FileStream destinationStream = new FileStream(setupFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await sourceStream.CopyToAsync(destinationStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await DialogHelper.ShowMessage("Unable to download the installer. Please try again later.");
                return false;
            }

            if (!File.Exists(setupFilePath))
            {
                await DialogHelper.ShowMessage("Unable to download the installer. Please try again later.");
                return false;
            }

            string installDirectory = Path.GetFullPath(AppContext.BaseDirectory);
            ServiceManager.Get<IProcessService>().LaunchProgram(setupFilePath, QuoteArgument(installDirectory));
            Application.Current.Shutdown();
            return true;
        }

        private void SkipUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AttachHyperlinkHandler()
        {
            this.UpdateChangelogViewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(this.OnMarkdownLinkClicked));
        }

        private void OnMarkdownLinkClicked(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                string target = e.Uri != null ? e.Uri.AbsoluteUri : e.OriginalSource?.ToString();
                if (!string.IsNullOrWhiteSpace(target))
                {
                    Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                    e.Handled = true;
                }
            }
            catch
            {
                // Ignore navigation failures.
            }
        }

        internal static string QuoteArgument(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "\"\"";
            }

            value = value.Trim();
            value = value.Replace("\"", "\\\"");

            int trailingSlashes = 0;
            for (int i = value.Length - 1; i >= 0 && value[i] == '\\'; i--)
            {
                trailingSlashes++;
            }

            if (trailingSlashes > 0)
            {
                value = value + new string('\\', trailingSlashes);
            }

            return $"\"{value}\"";
        }
    }
}
