using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for RahiTuberServiceControl.xaml
    /// </summary>
    public partial class RahiTuberServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.RahiTuber; } }

        private RahiTuberServiceControlViewModel viewModel;

        public RahiTuberServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new RahiTuberServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
