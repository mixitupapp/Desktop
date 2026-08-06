using System;
using System.Collections.Generic;

namespace MixItUp.Base.ViewModel.Settings.Generic
{
    public class GenericComboBoxSettingsOptionControlViewModel<T> : GenericSettingsOptionControlViewModelBase
    {
        public List<T> Values { get; set; }

        public T Value
        {
            get { return this.value; }
            set
            {
                if (!object.Equals(this.value, value))
                {
                    this.value = value;
                    this.valueSetter(value);
                    this.NotifyPropertyChanged();
                }
            }
        }
        private T value;
        private Action<T> valueSetter;

        public int Width { get; set; } = 200;

        /// <summary>
        /// Whether list entries are run through the localization table for display. Set to false for
        /// values that are proper nouns, such as font family names, otherwise an entry that happens to
        /// match a resource key (the "Symbol" font, for example) renders as the translated string.
        /// </summary>
        public bool LocalizeItems { get; set; } = true;

        /// <summary>
        /// Whether each entry renders in the font family it names, so the list previews the choice
        /// rather than showing every option in the font that is currently applied. Only meaningful
        /// for a list of font family names. Symbol fonts (Wingdings and the like) will render their
        /// own names as glyphs, which is the same trade-off every font picker makes.
        /// </summary>
        public bool RenderItemsInOwnFont { get; set; } = false;

        public bool Enabled
        {
            get { return this.enabled; }
            set
            {
                this.enabled = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool enabled = true;

        public GenericComboBoxSettingsOptionControlViewModel(string name, IEnumerable<T> values, T initialValue, Action<T> valueSetter, string tooltip = null)
            : base(name, tooltip)
        {
            this.Values = new List<T>(values);
            this.value = initialValue;
            this.valueSetter = valueSetter;
        }
    }
}
