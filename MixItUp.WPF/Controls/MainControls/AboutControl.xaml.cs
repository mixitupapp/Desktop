using MixItUp.Base;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for AboutControl.xaml
    /// </summary>
    public partial class AboutControl : MainControlBase
    {
        public AboutControl()
        {
            InitializeComponent();
        }

        protected override Task InitializeInternal()
        {
            this.VersionTextBlock.Text = VersionHelper.GetFullVersionString() + BuildChannelHelper.BUILD_CHANNEL_SUFFIX;
            _ = this.LoadPatreonMembers();

            return base.InitializeInternal();
        }

        private async Task LoadPatreonMembers()
        {
            try
            {
                List<string> names = await ServiceManager.Get<MixItUpService>().GetAllPatreonMemberNames();
                this.PatreonMembersTextBlock.Text = string.Join(" • ", names);
            }
            catch (System.Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void IssueReportHyperlink_Click(object sender, RoutedEventArgs e)
        {
            ServiceManager.Get<IProcessService>().LaunchProgram("MixItUp.Reporter.exe", $"{FileLoggerHandler.CurrentLogFilePath} {ChannelSession.Settings?.Name ?? "NONE"}");
        }

        private void TwitterButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://x.com/MixItUpBot"); }

        private void DiscordButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://mixitupapp.com/discord"); }

        private void YouTubeButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://www.youtube.com/c/MixItUpApp"); }

        private void Patreon_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://www.patreon.com/mixitupbot"); }

        private void WikiButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://wiki.mixitupapp.com/"); }

        private void GithubButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://github.com/mixitupapp/Desktop"); }

        private void SaviorXTanrenButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://twitch.tv/SaviorXTanren"); }

        private void VerbatimTButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://twitch.tv/Verbatim_T"); }

        private void TyrenDesButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://twitch.tv/TyrenDes"); }
    }
}
