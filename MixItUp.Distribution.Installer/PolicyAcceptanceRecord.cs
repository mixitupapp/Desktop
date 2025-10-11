using System;

namespace MixItUp.Distribution.Installer
{
    internal sealed class PolicyAcceptanceRecord
    {
        public PolicyAcceptanceRecord(string policy, string version, DateTime acceptedAtUtc)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            AcceptedAtUtc = acceptedAtUtc;
        }

        public string Policy { get; }

        public string Version { get; }

        public DateTime AcceptedAtUtc { get; }
    }
}
