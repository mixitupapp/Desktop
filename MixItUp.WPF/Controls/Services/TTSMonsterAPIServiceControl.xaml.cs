using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Services
{
    /// <summary>
    /// Interaction logic for TTSMonsterAPIServiceControl.xaml
    /// </summary>
    public partial class TTSMonsterAPIServiceControl : ServiceControlBase
    {
        public override Brand Brand { get { return Brands.TTSMonster; } }

        private TTSMonsterAPIServiceControlViewModel viewModel;

        public TTSMonsterAPIServiceControl()
        {
            this.DataContext = this.ViewModel = this.viewModel = new TTSMonsterAPIServiceControlViewModel();

            InitializeComponent();
        }

        protected override async Task OnLoaded()
        {
            await this.viewModel.OnOpen();
        }
    }
}
