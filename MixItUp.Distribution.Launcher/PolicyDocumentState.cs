using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MixItUp.Distribution.Core;

namespace MixItUp.Distribution.Launcher
{
    public sealed class PolicyDocumentState : INotifyPropertyChanged
    {
        private readonly PolicyInfo policyInfo;
        private readonly string requestedPolicySlug;
        private bool isAccepted;
        private DateTime? acceptedAtUtc;
        private string lastAcceptedVersion;

        public PolicyDocumentState(string requestedPolicy, PolicyInfo info)
        {
            this.policyInfo = info ?? throw new ArgumentNullException(nameof(info));
            this.requestedPolicySlug = string.IsNullOrWhiteSpace(requestedPolicy)
                ? throw new ArgumentException("Policy identifier is required.", nameof(requestedPolicy))
                : requestedPolicy.Trim();

            string resolvedPolicy = string.IsNullOrWhiteSpace(info.Policy) ? this.requestedPolicySlug : info.Policy.Trim();

            this.Policy = resolvedPolicy;
            this.Title = string.IsNullOrWhiteSpace(info.Title) ? FormatPolicyTitle(resolvedPolicy) : info.Title.Trim();
            this.Version = info.Version ?? string.Empty;
            this.Description = info.Description ?? string.Empty;
            this.PublishedAt = info.PublishedAt;
            this.ContentUri = info.ContentUri;
            this.ContentPath = info.ContentPath ?? string.Empty;
            this.ContentType = info.ContentType ?? string.Empty;
            this.ContentSha256 = info.ContentSha256 ?? string.Empty;
            this.ContentSize = info.ContentSize;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Policy { get; }

        public string Title { get; }

        public string Version { get; }

        public string Description { get; }

        public DateTime? PublishedAt { get; }

        public Uri ContentUri { get; }

        public string ContentPath { get; }

        public string ContentType { get; }

        public string ContentSha256 { get; }

        public long? ContentSize { get; }

        public bool IsAccepted
        {
            get { return this.isAccepted; }
            private set { this.SetProperty(ref this.isAccepted, value); }
        }

        public DateTime? AcceptedAtUtc
        {
            get { return this.acceptedAtUtc; }
            private set { this.SetProperty(ref this.acceptedAtUtc, value); }
        }

        public string LastAcceptedVersion
        {
            get { return this.lastAcceptedVersion ?? string.Empty; }
            private set { this.SetProperty(ref this.lastAcceptedVersion, value); }
        }

        public PolicyInfo Info => this.policyInfo;

        public void ApplyAcceptanceRecord(string version, DateTime? acceptedAtUtc, bool matchesCurrentVersion)
        {
            this.LastAcceptedVersion = version ?? string.Empty;
            this.AcceptedAtUtc = acceptedAtUtc;
            this.IsAccepted = matchesCurrentVersion;
        }

        public void MarkPending()
        {
            this.IsAccepted = false;
        }

        private static string FormatPolicyTitle(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy))
            {
                return "Policy";
            }

            string normalized = policy.Replace('-', ' ').Replace('_', ' ');
            string[] pieces = normalized
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < pieces.Length; i++)
            {
                string piece = pieces[i];
                if (piece.Length == 0)
                {
                    continue;
                }

                if (piece.Length == 1)
                {
                    pieces[i] = piece.ToUpperInvariant();
                }
                else
                {
                    pieces[i] = char.ToUpperInvariant(piece[0]) + piece.Substring(1).ToLowerInvariant();
                }
            }

            return pieces.Length > 0 ? string.Join(" ", pieces) : policy;
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
