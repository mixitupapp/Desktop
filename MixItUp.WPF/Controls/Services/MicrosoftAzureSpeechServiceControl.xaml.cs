using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for MicrosoftAzureSpeechServiceControl.xaml
    /// </summary>
    public partial class MicrosoftAzureSpeechServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.Azure; } }

        private MicrosoftAzureSpeechServiceControlViewModel viewModel;

        public MicrosoftAzureSpeechServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new MicrosoftAzureSpeechServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
