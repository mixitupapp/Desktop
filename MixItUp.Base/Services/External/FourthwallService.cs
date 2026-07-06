using MixItUp.Base.Model.User;
using MixItUp.Base.Model.Webhooks;
using MixItUp.Base.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class FourthwallWebhookEvent
    {
        [JsonProperty("testMode")]
        public bool TestMode { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("webhookId")]
        public string WebhookId { get; set; }

        [JsonProperty("shopId")]
        public string ShopId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("apiVersion")]
        public string ApiVersion { get; set; }

        [JsonProperty("data")]
        public FourthwallWebhookEventData Data { get; set; }
    }

    // Lenient shared shape across DONATION / ORDER_PLACED / GIFT_PURCHASE payloads.
    public class FourthwallWebhookEventData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("amounts")]
        public FourthwallAmounts Amounts { get; set; }

        [JsonProperty("offers")]
        public List<FourthwallOffer> Offers { get; set; }

        public UserDonationModel ToGenericDonation(string eventId, string eventType)
        {
            bool isAnonymous = string.IsNullOrEmpty(this.Username);

            // Fourthwall money values are already in currency units (10 = $10.00), not cents.
            double amount = this.Amounts?.Total?.Value ?? 0;

            return new UserDonationModel()
            {
                Source = UserDonationSourceEnum.Fourthwall,

                ID = eventId,
                Username = isAnonymous ? null : this.Username,
                IsAnonymous = isAnonymous,

                Type = eventType,
                Message = this.Message,

                Amount = Math.Round(amount, 2),

                DateTime = DateTimeOffset.Now,
            };
        }
    }

    public class FourthwallAmounts
    {
        [JsonProperty("total")]
        public FourthwallMoney Total { get; set; }
    }

    public class FourthwallMoney
    {
        [JsonProperty("value")]
        public double? Value { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }
    }

    public class FourthwallOffer
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class FourthwallService
    {
        public const string DonationEventType = "DONATION";
        public const string OrderPlacedEventType = "ORDER_PLACED";
        public const string GiftPurchaseEventType = "GIFT_PURCHASE";

        private const int MaxTrackedEventIds = 500;

        private readonly object eventIdLock = new object();
        private readonly HashSet<string> seenEventIds = new HashSet<string>();
        private readonly Queue<string> seenEventIdOrder = new Queue<string>();

        public FourthwallService()
        {
            MixItUpService.RegisterWebhookServiceHandler(WebhookServices.Fourthwall, this.ProcessWebhookEvent);
        }

        public async Task ProcessWebhookEvent(string payload)
        {
            try
            {
                FourthwallWebhookEvent fourthwallEvent = JsonConvert.DeserializeObject<FourthwallWebhookEvent>(payload);
                if (fourthwallEvent?.Data == null || string.IsNullOrEmpty(fourthwallEvent.Type))
                {
                    Logger.Log($"Fourthwall - Invalid webhook payload - {payload}");
                    return;
                }

                if (!this.TryMarkEventProcessed(fourthwallEvent.Id))
                {
                    return;
                }

                EventTypeEnum eventType;
                switch (fourthwallEvent.Type)
                {
                    case DonationEventType: eventType = EventTypeEnum.FourthwallDonation; break;
                    case OrderPlacedEventType: eventType = EventTypeEnum.FourthwallOrderPlaced; break;
                    case GiftPurchaseEventType: eventType = EventTypeEnum.FourthwallGiftPurchase; break;
                    default:
                        Logger.Log($"Fourthwall - Unknown webhook event type - {fourthwallEvent.Type}");
                        return;
                }

                UserDonationModel donation = fourthwallEvent.Data.ToGenericDonation(fourthwallEvent.Id, fourthwallEvent.Type);

                Dictionary<string, string> additionalSpecialIdentifiers = new Dictionary<string, string>();
                additionalSpecialIdentifiers["fourthwallitemname"] = fourthwallEvent.Data.Offers?.FirstOrDefault()?.Name ?? string.Empty;

                await EventService.ProcessDonationEvent(eventType, donation, additionalSpecialIdentifiers: additionalSpecialIdentifiers);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private bool TryMarkEventProcessed(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return true;
            }

            lock (this.eventIdLock)
            {
                if (!this.seenEventIds.Add(eventId))
                {
                    return false;
                }

                this.seenEventIdOrder.Enqueue(eventId);
                while (this.seenEventIdOrder.Count > MaxTrackedEventIds)
                {
                    this.seenEventIds.Remove(this.seenEventIdOrder.Dequeue());
                }

                return true;
            }
        }
    }
}
