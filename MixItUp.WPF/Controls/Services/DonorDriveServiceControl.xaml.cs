using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for DonorDriveServiceControl.xaml
    /// </summary>
    public partial class DonorDriveServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.DonorDrive; } }

        private DonorDriveServiceControlViewModel viewModel;

        public DonorDriveServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new DonorDriveServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
