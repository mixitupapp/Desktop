using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for VConnectServiceControl.xaml
    /// </summary>
    public partial class VConnectServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.VConnect; } }

        private VConnectServiceControlViewModel viewModel;

        public VConnectServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new VConnectServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
