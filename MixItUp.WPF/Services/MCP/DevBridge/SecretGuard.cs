#if !DEV_BRIDGE
#error The MCP dev bridge is being compiled without the DEV_BRIDGE constant. These files must only ever build under the Dev configuration. See Directory.Build.props.
#endif

using System;
using System.Reflection;

namespace MixItUp.WPF.Services.MCP.DevBridge
{
    /// <summary>
    /// The single policy for what must never be echoed back to a caller.
    /// </summary>
    /// <remarks>
    /// This app holds live OAuth access and refresh tokens for four platforms plus API keys for a long
    /// tail of external services. A tool result is read straight into an agent's context, which is a
    /// worse place for a credential to land than a local log file, so anything that looks like a secret
    /// reports <see cref="RedactedText"/> in place of its value.
    /// <para>
    /// <b>Why this became its own file in Phase B.</b> The tree tools could only ever surface what a
    /// control displayed, so matching on the control's x:Name caught the realistic cases. Reading a
    /// property path does not have that property: a caller can name
    /// <c>APIKey</c> on a service view model, or walk into an OAuthTokenModel, and reach the value
    /// directly with no control and no name to match against. Redaction therefore has to key on the
    /// member being read and on the type declaring it, not on what happens to be on screen.
    /// </para>
    /// <para>
    /// Name matching is imprecise by nature, so every check here errs toward redacting. A false positive
    /// costs a caller one property it cannot read and can work around; a false negative puts a live
    /// credential in a transcript.
    /// </para>
    /// </remarks>
    internal static class SecretGuard
    {
        public const string RedactedText = "[REDACTED]";

        /// <summary>
        /// Fragments that mean a member's value must not be reported. Matched as a substring against a
        /// lowercased name, so <c>TwitchBotOAuthToken</c> is caught by "oauth" and by "token".
        /// </summary>
        private static readonly string[] SecretNameFragments = new string[]
        {
            "password", "passwd", "secret", "token", "apikey", "api_key", "clientid", "client_id",
            "clientsecret", "credential", "bearer", "oauth", "authkey", "accesskey", "privatekey",
            "webhook", "connectionstring",
        };

        /// <summary>
        /// Types that exist to carry credentials, where every member is suspect regardless of its name.
        /// </summary>
        /// <remarks>
        /// OAuthTokenModel is the important one. Its members are named accessToken, refreshToken and
        /// expiresIn, so the name list already covers the first two, but "expiresIn" and any future
        /// member would sail straight through. Treating the whole type as secret means a member added
        /// later is redacted by default rather than leaking until someone notices.
        /// </remarks>
        private static readonly string[] SecretTypeNames = new string[]
        {
            "OAuthTokenModel", "SecretsService", "TokenIntrospectionModel",
        };

        /// <summary>
        /// Whether a member name looks like it holds a credential.
        /// </summary>
        public static bool LooksSecret(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lowered = name.ToLowerInvariant();
            foreach (string fragment in SecretNameFragments)
            {
                if (lowered.Contains(fragment))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether a type exists to carry credentials, so all of its members are redacted.
        /// </summary>
        public static bool IsSecretType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            string name = type.Name;
            foreach (string secret in SecretTypeNames)
            {
                if (string.Equals(name, secret, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            // A token model added later is caught by shape rather than needing to be listed.
            return name.EndsWith("TokenModel", StringComparison.Ordinal);
        }

        /// <summary>
        /// Whether reading <paramref name="memberName"/> off <paramref name="ownerType"/> must be
        /// redacted.
        /// </summary>
        public static bool ShouldRedact(Type ownerType, string memberName)
        {
            return IsSecretType(ownerType) || LooksSecret(memberName);
        }

        /// <summary>
        /// Whether a value must be redacted purely because of what it is, independent of how it was
        /// reached.
        /// </summary>
        /// <remarks>
        /// A PasswordBox is the case that matters. Reaching one through a property path rather than
        /// through a tree dump would otherwise bypass the unconditional check the describer applies, and
        /// its Password property would be readable via a path that never mentions the word.
        /// </remarks>
        public static bool IsSecretValue(object value)
        {
            return value is System.Windows.Controls.PasswordBox || IsSecretType(value?.GetType());
        }

        /// <summary>
        /// The member's value, or the redaction marker, whichever the policy allows.
        /// </summary>
        public static string Guard(Type ownerType, string memberName, Func<string> render)
        {
            return ShouldRedact(ownerType, memberName) ? RedactedText : render();
        }

        /// <summary>
        /// Whether a member on a type is one whose value must not be reported.
        /// </summary>
        public static bool ShouldRedact(MemberInfo member)
        {
            return member != null && ShouldRedact(member.DeclaringType, member.Name);
        }
    }
}
