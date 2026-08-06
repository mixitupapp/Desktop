using MixItUp.WPF.Controls.MainControls;

namespace MixItUp.WPF.Controls.Settings
{
    public abstract class SettingsControlBase : MainControlBase
    {
        /// <summary>
        /// Whether the settings container gives this control the full height of the settings area instead of
        /// hosting it in a scroll viewer. Pages that opt in have to scroll their own content.
        /// </summary>
        public virtual bool FillsAvailableHeight { get { return false; } }
    }
}
