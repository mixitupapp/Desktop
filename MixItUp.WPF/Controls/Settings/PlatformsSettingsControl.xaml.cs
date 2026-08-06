using MixItUp.Base.ViewModel.Settings;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Settings
{
    /// <summary>
    /// Interaction logic for PlatformsSettingsControl.xaml
    /// </summary>
    public partial class PlatformsSettingsControl : SettingsControlBase
    {
        private PlatformsSettingsControlViewModel viewModel;

        public PlatformsSettingsControl()
        {
            InitializeComponent();

            this.DataContext = this.viewModel = new PlatformsSettingsControlViewModel();
        }

        protected override async Task InitializeInternal()
        {
            await this.viewModel.OnOpen();
            await base.InitializeInternal();
        }

        protected override async Task OnVisibilityChanged()
        {
            await this.InitializeInternal();
        }
    }
}
