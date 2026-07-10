using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for MtionStudioServiceControl.xaml
    /// </summary>
    public partial class MtionStudioServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.MtionStudio; } }

        private MtionStudioServiceControlViewModel viewModel;

        public MtionStudioServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new MtionStudioServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
