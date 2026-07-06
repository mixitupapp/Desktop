using System;

namespace MixItUp.Base.ViewModel.Settings.Generic
{
    public class GenericTextSettingsOptionControlViewModel : GenericSettingsOptionControlViewModelBase
    {
        public string Value
        {
            get { return this.value; }
            set
            {
                if (this.allowEmpty || !string.IsNullOrEmpty(value))
                {
                    this.value = value;
                    this.valueSetter(value);
                }
                this.NotifyPropertyChanged();
            }
        }
        private string value;
        private Action<string> valueSetter;
        private bool allowEmpty;

        public GenericTextSettingsOptionControlViewModel(string name, string initialValue, Action<string> valueSetter, string tooltip = null, bool allowEmpty = false)
            : base(name, tooltip)
        {
            this.value = initialValue;
            this.valueSetter = valueSetter;
            this.allowEmpty = allowEmpty;
        }
    }
}
