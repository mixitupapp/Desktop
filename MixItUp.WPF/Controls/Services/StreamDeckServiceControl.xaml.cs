using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    public partial class StreamDeckServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.Elgato; } }

        private StreamDeckServiceControlViewModel viewModel;

        public StreamDeckServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new StreamDeckServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
