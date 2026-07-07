using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for VoicemodServiceControl.xaml
    /// </summary>
    public partial class VoicemodServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.Voicemod; } }

        private VoicemodServiceControlViewModel viewModel;

        public VoicemodServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new VoicemodServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
