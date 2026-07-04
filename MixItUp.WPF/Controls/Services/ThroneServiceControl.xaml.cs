using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Util;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for ThroneServiceControl.xaml
    /// </summary>
    public partial class ThroneServiceControl : ServiceControlBase
    {
        private ThroneServiceControlViewModel viewModel;

        public ThroneServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new ThroneServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }

        private async void CopyURLButton_Click(object sender, RoutedEventArgs e)
        {
            await UIHelpers.CopyToClipboard(this.viewModel.WebhookURL);
        }
    }
}
