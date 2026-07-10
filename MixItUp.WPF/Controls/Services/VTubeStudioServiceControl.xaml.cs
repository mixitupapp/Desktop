using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for VTubeStudioServiceControl.xaml
    /// </summary>
    public partial class VTubeStudioServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.VTubeStudio; } }

        private VTubeStudioServiceControlViewModel viewModel;

        public VTubeStudioServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new VTubeStudioServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
