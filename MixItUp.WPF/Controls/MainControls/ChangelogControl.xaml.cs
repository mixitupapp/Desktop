using MixItUp.Base.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for ChangelogControl.xaml
    /// </summary>
    public partial class ChangelogControl : MainControlBase
    {
        public ChangelogControl()
        {
            InitializeComponent();

            MainMenuControl.OnMainMenuStateChanged += MainMenuControl_OnMainMenuStateChanged;
            this.AttachHyperlinkHandler();
        }

        protected override async Task InitializeInternal()
        {
            try
            {
                string changelogFilePath = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
                if (File.Exists(changelogFilePath))
                {
                    this.ChangelogViewer.Markdown = await File.ReadAllTextAsync(changelogFilePath);
                }
                else
                {
                    Logger.Log(LogLevel.Warning, $"Bundled changelog not found at {changelogFilePath}");
                    this.ChangelogViewer.Markdown = "No changelog available.";
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                this.ChangelogViewer.Markdown = "Unable to load changelog.";
            }
            await base.InitializeInternal();
        }

        private void MainMenuControl_OnMainMenuStateChanged(object sender, bool state)
        {
            this.ChangelogViewer.Visibility = state ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AttachHyperlinkHandler()
        {
            this.ChangelogViewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(this.OnMarkdownLinkClicked));
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
    }
}
