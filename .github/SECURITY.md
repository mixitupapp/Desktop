# Security & Incident Response Policy

> **Version:** 1.0.0  
> **Effective Date:** 2025-10-22  
> **Last Reviewed:** 2025-10-19  
> **Supersedes:** N/A (initial release)

This policy covers responsible disclosure expectations and our public incident response commitments for Mix It Up services.

## 1. Responsible Disclosure Guidelines

- Report privately to <security@mixitupapp.com>. Please encrypt sensitive findings when possible.
- **Acknowledgement SLA:** we acknowledge receipt within 5 business days and keep you informed as we triage.
- **Supported targets:** latest public release builds and the current `main` branch of Mix It Up Desktop. Testing against non-public environments, partner systems, or infrastructure you do not own is out of scope.
- **Include details** such as product/version or commit hash, reproduction steps, estimated impact, and a proof of concept if feasible.
- **Coordinated disclosure:** we request up to 90 days (or another mutually agreed window) to remediate before public disclosure.
- **No disruption:** avoid actions that degrade service quality or violate applicable laws. We do not authorize testing of non-public systems without written permission.
- **Safe Harbor:** If you make a good-faith effort to comply with this policy, we will not pursue legal action or refer the matter to law enforcement for security research activities that are consistent with this policy.

## 2. Security Incident Response

We maintain an internal runbook that follows standard phases—identify, contain, eradicate, recover, and capture lessons learned. When we confirm a security incident involving personal data or service integrity, the response team:

1. Assembles the appropriate engineering, operations, and communications stakeholders.
2. Assesses scope and risk, including potential regulatory reporting triggers (for example, GDPR/UK GDPR 72-hour windows).
3. Implements containment, remediation, and recovery steps, validating fixes before restoring normal operations.
4. Records timeline, root cause, and corrective actions for post-incident reviews.

Where required by law or contract, we notify regulators and affected individuals and provide recommended mitigation steps. We may also contact impacted creators or partners directly if rapid coordination is needed.

Report suspected or active incidents to <security@mixitupapp.com>. For time-sensitive matters, include “URGENT” in the subject line so the on-call team can escalate quickly.

## 3. Logs and Backups (Operational)

Content vs. Ops Metadata. Application/content data are retained as stated in the Privacy Policy and Terms. Operational metadata (for example, timestamps, response codes, IPs, byte counts) may be retained on U.S. systems for security and reliability. Content payloads are not mined for secondary purposes.

Backups. Disaster-recovery backups are maintained on U.S. systems and age out on a rolling schedule; restoration triggers the same deletion routines. We target the following maximum retention windows we can meet in practice:

- Operational security logs: up to 90 days
- Backups: up to 365 days

## 4. Changelog

- **v1.0.0 (2025-10-19):** Initial published version
