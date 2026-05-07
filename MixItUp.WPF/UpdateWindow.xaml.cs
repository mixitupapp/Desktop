using MixItUp.Base.Model.API;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.WPF.Util;
using MixItUp.WPF.Windows;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;

namespace MixItUp.WPF
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : LoadingWindowBase
    {
        private MixItUpUpdateModel update;
        private readonly bool isMandatory;

        public UpdateWindow(MixItUpUpdateModel update, bool isMandatory = false)
        {
            this.update = update;
            this.isMandatory = isMandatory;

            InitializeComponent();

            this.Initialize(this.StatusBar);
            this.AttachHyperlinkHandler();
            this.Closing += UpdateWindow_Closing;
        }

        protected override async Task OnLoaded()
        {
            _ = this.TryLoadPatreonMemberShoutout();

            this.NewVersionTextBlock.Text = this.update.Version;
            Version entryVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            this.CurrentVersionTextBlock.Text = VersionHelper.NormalizeSemVerString(entryVersion);

            if (this.update.IsPreview)
            {
                this.PreviewUpdateGrid.Visibility = Visibility.Visible;
            }

            this.SkipUpdateButton.Visibility = this.isMandatory ? Visibility.Collapsed : Visibility.Visible;
            this.SkipUpdateButton.IsEnabled = !this.isMandatory;

            if (this.isMandatory)
            {
                this.MandatoryBanner.Visibility = Visibility.Visible;
            }

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
            TaskbarFlashHelper.Flash(this);

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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.isMandatory)
            {
                Application.Current.Shutdown();
            }
            else
            {
                this.Close();
            }
        }

        private void UpdateWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            TaskbarFlashHelper.Flash(this, stop: true);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
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

        private async Task TryLoadPatreonMemberShoutout()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    using HttpResponseMessage response = await client.GetAsync("https://util.mixitupapp.com/api/services/external/patreon/members/random");
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.Log(LogLevel.Warning, $"Failed to retrieve Patreon shoutout member: {(int)response.StatusCode} {response.ReasonPhrase}");
                        return;
                    }

                    string json = await response.Content.ReadAsStringAsync();
                    PatreonRandomMemberResponseModel result = JsonConvert.DeserializeObject<PatreonRandomMemberResponseModel>(json);
                    if (result?.Success != true || string.IsNullOrWhiteSpace(result.Member?.DisplayName))
                    {
                        return;
                    }

                    string[] shoutoutMessages = new string[]
                    {
                        "This update is brought to you by Patreon supporter {0}.",
                        "This update is made possible in part by Patreon supporter {0}.",
                        "Special thanks to Patreon supporter {0} for helping power this update.",
                        "This release gets a boost from Patreon supporter {0}.",
                        "Shoutout to Patreon supporter {0} for supporting Mix It Up.",
                        "Thank you to Patreon supporter {0} for backing Mix It Up.",
                    };
                    this.PatreonMemberNameTextBlock.Text = string.Format(shoutoutMessages[RandomHelper.GenerateRandomNumber(shoutoutMessages.Length)], result.Member.DisplayName);
                    this.PatreonShoutoutBorder.Visibility = Visibility.Visible;

                    if (!string.IsNullOrWhiteSpace(result.Member.AvatarUrl))
                    {
                        ImageHelper.SetImageSource(this.PatreonMemberAvatarImage, result.Member.AvatarUrl, 28, 28, result.Member.DisplayName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private class PatreonRandomMemberResponseModel
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("member")]
            public PatreonRandomMemberModel Member { get; set; }
        }

        private class PatreonRandomMemberModel
        {
            [JsonProperty("display_name")]
            public string DisplayName { get; set; }

            [JsonProperty("avatar_url")]
            public string AvatarUrl { get; set; }
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
