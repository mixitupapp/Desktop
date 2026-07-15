using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for ResponsiveVoiceServiceControl.xaml
    /// </summary>
    public partial class ResponsiveVoiceServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.ResponsiveVoice; } }

        private ResponsiveVoiceServiceControlViewModel viewModel;

        public ResponsiveVoiceServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new ResponsiveVoiceServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}