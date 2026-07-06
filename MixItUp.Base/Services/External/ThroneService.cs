using MixItUp.Base.Model.User;
using MixItUp.Base.Model.Webhooks;
using MixItUp.Base.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class ThroneWebhookEvent
    {
        [JsonProperty("contract_version")]
        public string ContractVersion { get; set; }

        [JsonProperty("event_id")]
        public string EventId { get; set; }

        [JsonProperty("event_type")]
        public string EventType { get; set; }

        [JsonProperty("data")]
        public ThroneWebhookEventData Data { get; set; }
    }

    public class ThroneWebhookEventData
    {
        private const string AnonymousGifterUsername = "Anonymous";

        [JsonProperty("creator_id")]
        public string CreatorId { get; set; }

        [JsonProperty("creator_username")]
        public string CreatorUsername { get; set; }

        [JsonProperty("gifter_username")]
        public string GifterUsername { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("item_name")]
        public string ItemName { get; set; }

        [JsonProperty("item_thumbnail_url")]
        public string ItemThumbnailUrl { get; set; }

        [JsonProperty("price")]
        public int? Price { get; set; }

        [JsonProperty("amount")]
        public int? Amount { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("is_surprise_gift")]
        public bool? IsSurpriseGift { get; set; }

        public UserDonationModel ToGenericDonation(string eventId, string eventType)
        {
            // Crowdfunded gifts carry no gifter and anonymous senders are literally "Anonymous".
            bool isAnonymous = string.IsNullOrEmpty(this.GifterUsername) || string.Equals(this.GifterUsername, AnonymousGifterUsername, StringComparison.OrdinalIgnoreCase);

            return new UserDonationModel()
            {
                Source = UserDonationSourceEnum.Throne,

                ID = eventId,
                Username = isAnonymous ? null : this.GifterUsername,
                IsAnonymous = isAnonymous,

                Type = eventType,
                Message = this.Message,
                ImageLink = this.ItemThumbnailUrl,

                Amount = Math.Round((this.Price ?? this.Amount ?? 0) / 100.0, 2),

                DateTime = DateTimeOffset.Now,
            };
        }
    }

    public class ThroneService
    {
        public const string GiftPurchasedEventType = "gift_purchased";
        public const string ContributionPurchasedEventType = "contribution_purchased";
        public const string GiftCrowdfundedEventType = "gift_crowdfunded";

        private const int MaxTrackedEventIds = 500;

        private readonly object eventIdLock = new object();
        private readonly HashSet<string> seenEventIds = new HashSet<string>();
        private readonly Queue<string> seenEventIdOrder = new Queue<string>();

        public ThroneService()
        {
            MixItUpService.RegisterWebhookServiceHandler(WebhookServices.Throne, this.ProcessWebhookEvent);
        }

        public async Task ProcessWebhookEvent(string payload)
        {
            try
            {
                ThroneWebhookEvent throneEvent = JsonConvert.DeserializeObject<ThroneWebhookEvent>(payload);
                if (throneEvent?.Data == null || string.IsNullOrEmpty(throneEvent.EventType))
                {
                    Logger.Log($"Throne - Invalid webhook payload - {payload}");
                    return;
                }

                if (!this.TryMarkEventProcessed(throneEvent.EventId))
                {
                    return;
                }

                EventTypeEnum eventType;
                switch (throneEvent.EventType)
                {
                    case GiftPurchasedEventType: eventType = EventTypeEnum.ThroneGiftPurchased; break;
                    case ContributionPurchasedEventType: eventType = EventTypeEnum.ThroneContribution; break;
                    case GiftCrowdfundedEventType: eventType = EventTypeEnum.ThroneGiftCrowdfunded; break;
                    default:
                        Logger.Log($"Throne - Unknown webhook event type - {throneEvent.EventType}");
                        return;
                }

                UserDonationModel donation = throneEvent.Data.ToGenericDonation(throneEvent.EventId, throneEvent.EventType);

                Dictionary<string, string> additionalSpecialIdentifiers = new Dictionary<string, string>();
                additionalSpecialIdentifiers["throneitemname"] = throneEvent.Data.ItemName ?? string.Empty;

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
