using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for LumiaStreamServiceControl.xaml
    /// </summary>
    public partial class LumiaStreamServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.LumiaStream; } }

        private LumiaStreamServiceControlViewModel viewModel;

        public LumiaStreamServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new LumiaStreamServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
