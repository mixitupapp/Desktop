using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace MixItUp.Installer
{
    public partial class EulaDialog : Window
    {
        private readonly string eulaUrl;

        public EulaDialog(string markdownContent, string eulaUrl)
        {
            this.eulaUrl = eulaUrl;

            InitializeComponent();

            this.Loaded += this.EulaDialog_Loaded;
            this.AttachHyperlinkHandler();

            if (!string.IsNullOrWhiteSpace(markdownContent))
            {
                try
                {
                    this.EulaViewer.Markdown = markdownContent;
                }
                catch (Exception ex)
                {
                    this.EnableFallback(ex);
                }
            }
            else
            {
                this.EnableFallback(null);
            }
        }

        private void EulaDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.EulaViewer.Visibility == Visibility.Visible)
            {
                this.EulaViewer.Focus();
            }
            else
            {
                this.AcceptButton.Focus();
            }
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OpenInBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            this.TryOpenInBrowser();
        }

        private void EnableFallback(Exception error)
        {
            this.EulaViewer.Visibility = Visibility.Collapsed;
            this.FallbackBorder.Visibility = Visibility.Visible;
            this.OpenInBrowserButton.Visibility = string.IsNullOrEmpty(this.eulaUrl) ? Visibility.Collapsed : Visibility.Visible;

            string message = "We were unable to display the EULA inside the installer. The agreement has been opened in your default browser.";
            if (error != null)
            {
                message += Environment.NewLine + Environment.NewLine + error.Message;
            }
            this.FallbackText.Text = message;

            this.TryOpenInBrowser();
        }

        private void TryOpenInBrowser()
        {
            if (string.IsNullOrEmpty(this.eulaUrl))
            {
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(this.eulaUrl)
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
                // Ignore failures; fallback text already informs the user.
            }
        }

        private void AttachHyperlinkHandler()
        {
            this.EulaViewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(this.OnMarkdownLinkClicked));
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
                // Ignore navigation failures; users can still use the fallback button.
            }
        }
    }
}
