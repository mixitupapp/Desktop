using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.Services;
using MixItUp.WPF.Branding;
using System.Windows.Navigation;

namespace MixItUp.WPF.Controls.Services
{
    public class ServiceControlBase : LoadingControlBase
    {
        /// <summary>The brand mark shown in the service's container header for third-party services.</summary>
        public virtual Brand Brand { get { return null; } }

        /// <summary>The Material Design 3 feature icon shown in the container header when the service has no brand.</summary>
        public virtual Feature Feature { get { return null; } }

        public ServiceControlViewModelBase ViewModel { get; protected set; }

        protected ServiceContainerControl containerControl { get; private set; }

        public void Initialize(ServiceContainerControl containerControl) { this.containerControl = containerControl; }

        protected virtual void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri.IsAbsoluteUri)
            {
                ServiceManager.Get<IProcessService>().LaunchLink(e.Uri.AbsoluteUri);
            }
            else
            {
                ServiceManager.Get<IProcessService>().LaunchFolder(e.Uri.OriginalString);
            }
        }
    }
}
