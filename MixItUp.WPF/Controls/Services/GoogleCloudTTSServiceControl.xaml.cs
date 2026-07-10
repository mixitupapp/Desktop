using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for GoogleCloudTTSServiceControl.xaml
    /// </summary>
    public partial class GoogleCloudTTSServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.GoogleCloud; } }

        private GoogleCloudTTSServiceControlViewModel viewModel;

        public GoogleCloudTTSServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new GoogleCloudTTSServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}