using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for PallyServiceControl.xaml
    /// </summary>
    public partial class PallyServiceControl : ServiceControlBase
    {
        public override Feature Feature { get { return Features.Tips; } }

        private PallyServiceControlViewModel viewModel;

        public PallyServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new PallyServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
