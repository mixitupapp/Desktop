using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for TiltifyServiceControl.xaml
    /// </summary>
    public partial class TiltifyServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.Tiltify; } }

        private TiltifyServiceControlViewModel viewModel;

        public TiltifyServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new TiltifyServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
