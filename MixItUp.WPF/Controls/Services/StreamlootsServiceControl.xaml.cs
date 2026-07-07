using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for StreamlootsServiceControl.xaml
    /// </summary>
    public partial class StreamlootsServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.Streamloots; } }

        private StreamlootsServiceControlViewModel viewModel;

        public StreamlootsServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new StreamlootsServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
