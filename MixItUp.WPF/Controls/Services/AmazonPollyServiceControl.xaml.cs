using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for AmazonPollyServiceControl.xaml
    /// </summary>
    public partial class AmazonPollyServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.AWS; } }

        private AmazonPollyServiceControlViewModel viewModel;

        public AmazonPollyServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new AmazonPollyServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
