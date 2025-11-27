using MixItUp.Base.ViewModel.Services;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for GoogleCloudTTSServiceControl.xaml
    /// </summary>
    public partial class GoogleCloudTTSServiceControl : ServiceControlBase
    {
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