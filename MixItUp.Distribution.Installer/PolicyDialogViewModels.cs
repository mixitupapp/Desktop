using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MixItUp.Distribution.Installer
{
    internal sealed class PolicyDocumentViewModel : INotifyPropertyChanged
    {
        private bool isAccepted;

        public PolicyDocumentViewModel(string policy, string title, string version, string markdown)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Title = title ?? string.Empty;
            Version = version ?? string.Empty;
            Markdown = markdown ?? string.Empty;
        }

        public string Policy { get; }

        public string Title { get; }

        public string Version { get; }

        public string Markdown { get; }

        public bool IsAccepted
        {
            get => this.isAccepted;
            set
            {
                if (this.isAccepted != value)
                {
                    this.isAccepted = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAccepted)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    internal sealed class PolicyDialogViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<PolicyDocumentViewModel> policies;

        public PolicyDialogViewModel(IEnumerable<PolicyDocumentViewModel> documents)
        {
            if (documents == null)
            {
                throw new ArgumentNullException(nameof(documents));
            }

            this.policies = new ObservableCollection<PolicyDocumentViewModel>(documents);
            foreach (PolicyDocumentViewModel policy in this.policies)
            {
                policy.PropertyChanged += this.Policy_PropertyChanged;
            }
        }

        public ObservableCollection<PolicyDocumentViewModel> Policies => this.policies;

        public bool CanAccept => this.policies.Count > 0 && this.policies.All(p => p.IsAccepted);

        public event PropertyChangedEventHandler PropertyChanged;

        private void Policy_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(PolicyDocumentViewModel.IsAccepted), StringComparison.Ordinal))
            {
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanAccept)));
            }
        }
    }
}
