using MixItUp.Base;
using MixItUp.Base.Model.API;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
                List<PatreonMemberV2Model> members = await ServiceManager.Get<MixItUpService>().GetAllPatreonMembersV2();
                this.PatreonMembersWrapPanel.Children.Clear();

                foreach (PatreonMemberV2Model member in members)
                {
                    bool hasLink = !string.IsNullOrWhiteSpace(member.SocialMediaLink);

                    Border chip = new Border
                    {
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(10, 5, 10, 5),
                        Margin = new Thickness(3),
                        Background = (Brush)this.FindResource("MaterialDesign.Brush.Card.Background"),
                        BorderThickness = new Thickness(1),
                        BorderBrush = (Brush)this.FindResource("MaterialDesignDivider"),
                        Cursor = hasLink ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                    };

                    StackPanel inner = new StackPanel { Orientation = Orientation.Horizontal };

                    inner.Children.Add(new TextBlock
                    {
                        Text = member.DisplayName,
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                    });

                    if (!string.IsNullOrWhiteSpace(member.Platform) && !string.IsNullOrWhiteSpace(member.PlatformUsername))
                    {
                        string imagePath = member.Platform switch
                        {
                            "twitch" => "/Assets/Images/twitch-color_sm.png",
                            "kick" => "/Assets/Images/kick-color_sm.png",
                            "velora" => "/Assets/Images/velora-color_sm.png",
                            "youtube" => "/Assets/Images/youtube-color_sm.png",
                            _ => null,
                        };

                        if (imagePath != null)
                        {
                            Border bracket = new Border
                            {
                                CornerRadius = new CornerRadius(8),
                                Padding = new Thickness(5, 2, 6, 2),
                                Margin = new Thickness(6, 0, 0, 0),
                                Background = (Brush)this.FindResource("MaterialDesignDivider"),
                            };

                            StackPanel bracketInner = new StackPanel { Orientation = Orientation.Horizontal };

                            bracketInner.Children.Add(new Image
                            {
                                Source = new BitmapImage(new Uri(imagePath, UriKind.Relative)),
                                Width = 12,
                                Height = 12,
                                Margin = new Thickness(0, 0, 4, 0),
                                VerticalAlignment = VerticalAlignment.Center,
                            });

                            bracketInner.Children.Add(new TextBlock
                            {
                                Text = member.PlatformUsername,
                                FontSize = 11,
                                VerticalAlignment = VerticalAlignment.Center,
                            });

                            bracket.Child = bracketInner;
                            inner.Children.Add(bracket);
                        }
                    }

                    chip.Child = inner;

                    if (hasLink)
                    {
                        string link = member.SocialMediaLink;
                        chip.MouseLeftButtonUp += (s, e) =>
                        {
                            try { Process.Start(new ProcessStartInfo(link) { UseShellExecute = true }); } catch { }
                        };
                    }

                    this.PatreonMembersWrapPanel.Children.Add(chip);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void IssueReportHyperlink_Click(object sender, RoutedEventArgs e)
        {
            ServiceManager.Get<IProcessService>().LaunchProgram("MixItUp.Reporter.exe", $"{FileLoggerHandler.CurrentLogFilePath} {ChannelSession.Settings?.Name ?? "NONE"}");
        }

        private void TwitterButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://x.com/MixItUpBot"); }

        private void DiscordButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://mixitup.bot/discord"); }

        private void YouTubeButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://www.youtube.com/c/MixItUpApp"); }

        private void Patreon_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://www.patreon.com/mixitupbot"); }

        private void WikiButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://wiki.mixitup.bot/"); }

        private void GithubButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://github.com/mixitupbot/Desktop"); }

        private void SaviorXTanrenButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://twitch.tv/SaviorXTanren"); }

        private void VerbatimTButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://twitch.tv/Verbatim_T"); }

        private void TyrenDesButton_Click(object sender, RoutedEventArgs e) { ServiceManager.Get<IProcessService>().LaunchLink("https://twitch.tv/TyrenDes"); }
    }
}
