using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Util;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for KoFiServiceControl.xaml
    /// </summary>
    public partial class KoFiServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.KoFi; } }

        private KoFiServiceControlViewModel viewModel;

        public KoFiServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new KoFiServiceControlViewModel();

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
