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
        // No brand mark has been staged for VConnect yet, so the header falls back to an icon.
        private static readonly Feature VConnectFeature = new Feature("vconnect", "hub");

        public override Feature Feature { get { return VConnectFeature; } }

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
