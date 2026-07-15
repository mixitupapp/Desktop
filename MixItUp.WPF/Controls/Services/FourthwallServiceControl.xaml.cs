using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Util;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for FourthwallServiceControl.xaml
    /// </summary>
    public partial class FourthwallServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.Fourthwall; } }

        private FourthwallServiceControlViewModel viewModel;

        public FourthwallServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new FourthwallServiceControlViewModel();

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
