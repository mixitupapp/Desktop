using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for StreamlabsServiceControl.xaml
    /// </summary>
    public partial class StreamlabsServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.Streamlabs; } }

        private StreamlabsServiceControlViewModel viewModel;

        public StreamlabsServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new StreamlabsServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
