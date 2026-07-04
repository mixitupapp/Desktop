using System;
using System.Collections.Generic;

namespace MixItUp.Base.Model.Webhooks
{
    public static class WebhookServices
    {
        public const string General = "general";
        public const string Throne = "throne";
        public const string Fourthwall = "fourthwall";
        public const string Kofi = "kofi";

        public static bool IsGeneral(string service)
        {
            return string.IsNullOrEmpty(service) || string.Equals(service, General, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class GetWebhooksResponseModel
    {
        public IEnumerable<Webhook> Webhooks { get; set; }
        public int MaxNumberOfWebhooks { get; set; }
    }

    public class Webhook
    {
        public Guid Id { get; set; }
        public string Secret { get; set; }
        public string Service { get; set; } = WebhookServices.General;
    }
}
