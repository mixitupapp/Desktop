using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for CrowdControlServiceControl.xaml
    /// </summary>
    public partial class CrowdControlServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.CrowdControl; } }

        private CrowdControlServiceControlViewModel viewModel;

        public CrowdControlServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new CrowdControlServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}