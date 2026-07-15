using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for XSplitServiceControl.xaml
    /// </summary>
    public partial class XSplitServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.XSplit; } }

        private XSplitServiceControlViewModel viewModel;

        public XSplitServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new XSplitServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
