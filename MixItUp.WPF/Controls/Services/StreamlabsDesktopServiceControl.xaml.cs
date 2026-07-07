using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for StreamlabsDesktopServiceControl.xaml
    /// </summary>
    public partial class StreamlabsDesktopServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.Streamlabs; } }

        private StreamlabsDesktopServiceControlViewModel viewModel;

        public StreamlabsDesktopServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new StreamlabsDesktopServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
