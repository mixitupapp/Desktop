using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class JustGivingUser
    {
        public Guid userId { get; set; }
        public uint accountId { get; set; }
        public JArray profileImageUrls { get; set; }
    }

    public class JustGivingFundraiserSummary
    {
        public uint charityId { get; set; }
        public uint pageId { get; set; }

        public string pageShortName { get; set; }

        public string pageStatus { get; set; }

        public bool IsActive { get { return (!string.IsNullOrEmpty(this.pageStatus) && this.pageStatus.Equals("Active")); } }
    }

    public class JustGivingCharity
    {
        public int id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string logoAbsoluteUrl { get; set; }
    }

    public class JustGivingFundraiser
    {
        public string pageId { get; set; }
        public string pageShortName { get; set; }
        public string pageGuid { get; set; }

        public string activityId { get; set; }

        public long eventId { get; set; }
        public string eventName { get; set; }
        public string eventCategory { get; set; }

        public string title { get; set; }
        public string status { get; set; }

        public string fundraisingTarget { get; set; }
        public string totalRaisedPercentageOfFundraisingTarget { get; set; }
        public string totalRaisedOffline { get; set; }
        public string totalRaisedOnline { get; set; }
        public string totalRaisedSms { get; set; }
        public string grandTotalRaisedExcludingGiftAid { get; set; }
        public string totalEstimatedGiftAid { get; set; }

        public JustGivingCharity charity { get; set; }

        public bool IsActive { get { return (!string.IsNullOrEmpty(this.status) && this.status.Equals("Active")); } }
    }

    public class JustGivingDonationGroup
    {
        public string id { get; set; }
        public string pageShortName { get; set; }
        public List<JustGivingDonation> donations { get; set; }
        public JObject pagination { get; set; }
    }

    public class JustGivingDonation
    {
        public uint id { get; set; }

        public string donorDisplayName { get; set; }
        public string donorRealName { get; set; }

        public string image { get; set; }
        public string message { get; set; }
        public string donationDate { get; set; }

        public string amount { get; set; }
        public string currencyCode { get; set; }
        public string donorLocalAmount { get; set; }
        public string donorLocalCurrencyCode { get; set; }

        public DateTimeOffset DateTime
        {
            get
            {
                // Dates arrive as /Date(<unix ms>[+-]<hhmm>)/. The offset is optional and is already baked
                // into the epoch value, so only the milliseconds are worth reading.
                if (!string.IsNullOrEmpty(this.donationDate))
                {
                    int start = this.donationDate.IndexOf('(');
                    int end = this.donationDate.LastIndexOf(')');
                    if (start >= 0 && end > start)
                    {
                        string date = this.donationDate.Substring(start + 1, end - start - 1);

                        // Start past the first character so a negative epoch is not read as the offset
                        int offsetIndex = date.IndexOfAny(new char[] { '+', '-' }, 1);
                        if (offsetIndex > 0)
                        {
                            date = date.Substring(0, offsetIndex);
                        }

                        if (long.TryParse(date, NumberStyles.Integer, CultureInfo.InvariantCulture, out long dateLong))
                        {
                            return DateTimeOffset.FromUnixTimeMilliseconds(dateLong);
                        }
                    }
                }
                return DateTimeOffset.MinValue;
            }
        }

        public UserDonationModel ToGenericDonation()
        {
            // API amounts are invariant-formatted strings; parsing with the machine culture corrupts them
            double.TryParse(this.donorLocalAmount, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount);
            return new UserDonationModel()
            {
                Source = UserDonationSourceEnum.JustGiving,

                ID = this.id.ToString(),
                Username = this.donorDisplayName,
                Message = this.message,

                Amount = amount,
                CurrencyCode = this.donorLocalCurrencyCode,

                DateTime = this.DateTime,
            };
        }
    }

    public class JustGivingService : IExternalService
    {
        private const string BaseAddress = "https://api.justgiving.com/v1/";

        private const string ClientID = "1e30b383";

        // The API addresses a page as {prefix}/{pageShortName}, where the prefix is the segment out of the
        // page URL: "fundraising" for classic pages, "page" for the ones the 2024 redesign introduced.
        // Only the prefixed form resolves a redesigned page, so everything goes through it.
        public const string DefaultPagePrefix = "fundraising";

        private static readonly string[] PagePathPrefixes = new string[] { "fundraising", "page" };

        public string Name { get { return MixItUp.Base.Resources.JustGiving; } }
        public bool IsConnected { get; private set; }

        public JustGivingFundraiser Fundraiser { get; private set; }


        private Dictionary<uint, JustGivingDonation> donationsReceived = new Dictionary<uint, JustGivingDonation>();

        private string pageReference;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private DateTimeOffset startTime;

        public JustGivingService() { }

        /// <summary>
        /// Reduces a fundraising page URL down to the "{prefix}/{pageShortName}" reference the API expects.
        /// Accepts a bare short name or a full URL in any of the supported formats, with or without scheme,
        /// www, query, or fragment. Returns null when nothing usable can be pulled out of the input.
        /// </summary>
        public static string ParsePageReference(string webPageURL)
        {
            if (string.IsNullOrWhiteSpace(webPageURL))
            {
                return null;
            }

            string path = webPageURL.Trim();

            int fragmentIndex = path.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                path = path.Substring(0, fragmentIndex);
            }

            int queryIndex = path.IndexOf('?');
            if (queryIndex >= 0)
            {
                path = path.Substring(0, queryIndex);
            }

            int schemeIndex = path.IndexOf("://", StringComparison.OrdinalIgnoreCase);
            if (schemeIndex >= 0)
            {
                path = path.Substring(schemeIndex + 3);
            }

            // Only drop the first segment when it actually looks like the host, so a typed short name survives
            int hostIndex = path.IndexOf('/');
            if (hostIndex >= 0 && path.Substring(0, hostIndex).IndexOf("justgiving.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                path = path.Substring(hostIndex + 1);
            }

            string[] segments = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            // A bare short name came from a classic vanity URL, so it keeps the classic prefix
            string prefix = JustGivingService.DefaultPagePrefix;
            string shortName = segments[0];

            foreach (string knownPrefix in JustGivingService.PagePathPrefixes)
            {
                if (string.Equals(segments[0], knownPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (segments.Length < 2)
                    {
                        return null;
                    }
                    prefix = knownPrefix;
                    shortName = segments[1];
                    break;
                }
            }

            return !string.IsNullOrWhiteSpace(shortName) ? $"{prefix}/{shortName}" : null;
        }

        /// <summary>
        /// Brings a stored reference up to the prefixed form. Settings written before prefix support hold a
        /// bare short name, which could only ever have been a classic page.
        /// </summary>
        public static string NormalizePageReference(string pageReference)
        {
            if (string.IsNullOrWhiteSpace(pageReference))
            {
                return null;
            }
            return pageReference.Contains("/") ? pageReference : $"{JustGivingService.DefaultPagePrefix}/{pageReference}";
        }

        private static string BuildPagePath(string pageReference)
        {
            string reference = JustGivingService.NormalizePageReference(pageReference);
            int separatorIndex = reference.IndexOf('/');
            return $"{Uri.EscapeDataString(reference.Substring(0, separatorIndex))}/{Uri.EscapeDataString(reference.Substring(separatorIndex + 1))}";
        }

        public async Task<Result> Connect()
        {
            try
            {
                if (!string.IsNullOrEmpty(ChannelSession.Settings.JustGivingPageShortName))
                {
                    this.pageReference = JustGivingService.NormalizePageReference(ChannelSession.Settings.JustGivingPageShortName);
                    this.Fundraiser = await this.GetFundraiser(this.pageReference);
                    if (this.Fundraiser != null)
                    {
                        this.cancellationTokenSource = new CancellationTokenSource();

                        this.startTime = DateTimeOffset.Now;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        AsyncRunner.RunAsyncBackground(this.BackgroundDonationCheck, this.cancellationTokenSource.Token, 60000);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                        ServiceManager.Get<ITelemetryService>().TrackService("JustGiving");
                        this.IsConnected = true;
                        return new Result();
                    }
                }
                return new Result(Resources.JustGivingUserDataFailed);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result(ex);
            }
        }

        public Task Disconnect()
        {
            this.Fundraiser = null;
            this.pageReference = null;
            if (this.cancellationTokenSource != null)
            {
                this.cancellationTokenSource.Cancel();
                this.cancellationTokenSource = null;
            }
            this.IsConnected = false;
            return Task.CompletedTask;
        }

        public async Task<JustGivingFundraiser> GetFundraiser(string pageReference)
        {
            try
            {
                using (AdvancedHttpClient client = this.GetHttpClient())
                {
                    return await client.GetAsync<JustGivingFundraiser>($"fundraising/pages/{JustGivingService.BuildPagePath(pageReference)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return null;
        }

        public async Task<IEnumerable<JustGivingDonation>> GetRecentDonations(string pageReference)
        {
            try
            {
                using (AdvancedHttpClient client = this.GetHttpClient())
                {
                    JustGivingDonationGroup group = await client.GetAsync<JustGivingDonationGroup>($"fundraising/pages/{JustGivingService.BuildPagePath(pageReference)}/donations");
                    if (group != null && group.donations != null)
                    {
                        return group.donations;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return new List<JustGivingDonation>();
        }

        private AdvancedHttpClient GetHttpClient()
        {
            string applicationKey = ServiceManager.Get<SecretsService>().GetSecret("JustGivingSecret");
            if (string.IsNullOrEmpty(applicationKey))
            {
                // Without this the API answers every call with appIdUnauthorized, which surfaces as a generic connect failure
                Logger.Log(LogLevel.Error, "JustGiving application key is not available, all requests will be rejected");
            }

            AdvancedHttpClient client = new AdvancedHttpClient(JustGivingService.BaseAddress);
            client.DefaultRequestHeaders.Add("x-app-id", JustGivingService.ClientID);
            client.DefaultRequestHeaders.Add("x-application-key", applicationKey);
            return client;
        }

        private async Task BackgroundDonationCheck(CancellationToken token)
        {
            if (!token.IsCancellationRequested)
            {
                if (this.Fundraiser != null)
                {
                    foreach (JustGivingDonation jgDonation in await this.GetRecentDonations(this.pageReference))
                    {
                        if (!donationsReceived.ContainsKey(jgDonation.id))
                        {
                            donationsReceived[jgDonation.id] = jgDonation;
                            UserDonationModel donation = jgDonation.ToGenericDonation();
                            if (donation.DateTime > this.startTime)
                            {
                                await EventService.ProcessDonationEvent(EventTypeEnum.JustGivingDonation, donation);
                            }
                        }
                    }
                }
            }
        }
    }
}
