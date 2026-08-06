using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for VeadotubeServiceControl.xaml
    /// </summary>
    public partial class VeadotubeServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.Veadotube; } }

        private VeadotubeServiceControlViewModel viewModel;

        public VeadotubeServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new VeadotubeServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
