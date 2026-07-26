using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for MCPServiceControl.xaml
    /// </summary>
    public partial class MCPServiceControl : ServiceControlBase
    {
        public override Feature Feature { get { return Features.MCPServer; } }

        private MCPServiceControlViewModel viewModel;

        public MCPServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new MCPServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
