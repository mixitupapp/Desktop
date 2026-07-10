using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for JustGivingServiceControl.xaml
    /// </summary>
    public partial class JustGivingServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.JustGiving; } }

        private JustGivingServiceControlViewModel viewModel;

        public JustGivingServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new JustGivingServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
