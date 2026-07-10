using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for TipeeeStreamServiceControl.xaml
    /// </summary>
    public partial class TipeeeStreamServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.TipeeeStream; } }

        private TipeeeStreamServiceControlViewModel viewModel;

        public TipeeeStreamServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new TipeeeStreamServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
