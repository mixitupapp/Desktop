using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Services.Twitch.API;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    public enum OverlayLeaderboardTypeV3Enum
    {
        ViewingTime,
        Consumable,
        TwitchBits,
        CumulativeMonthsSubbed,
        SubsGifted,
        SubscriptionStreak,
    }

    public enum OverlayLeaderboardDateRangeV3Enum
    {
        Daily,
        Weekly,
        Monthly,
        Yearly,
        AllTime,
    }

    [DataContract]
    public class OverlayLeaderboardHeaderV3Model : OverlayHeaderV3ModelBase
    {
        public OverlayLeaderboardHeaderV3Model() { }
    }

    [DataContract]
    public class OverlayLeaderboardV3Model : OverlayVisualTextV3ModelBase
    {
        public const string DetailsAmountPropertyName = "Amount";
        public const int DefaultRefreshTimeSeconds = 60;

        public static readonly string DefaultHTML = OverlayResources.OverlayLeaderboardDefaultHTML;
        public static readonly string DefaultCSS = OverlayResources.OverlayLeaderboardDefaultCSS + Environment.NewLine + Environment.NewLine + OverlayResources.OverlayTextDefaultCSS + Environment.NewLine + Environment.NewLine + OverlayResources.OverlayHeaderTextDefaultCSS;
        public static readonly string DefaultJavascript = OverlayResources.OverlayLeaderboardDefaultJavascript;

        [DataMember]
        public OverlayLeaderboardTypeV3Enum LeaderboardType { get; set; }

        [DataMember]
        public Guid ConsumableID { get; set; }

        [DataMember]
        public OverlayLeaderboardDateRangeV3Enum TwitchBitsDataRange { get; set; }

        [DataMember]
        public OverlayLeaderboardHeaderV3Model Header { get; set; }

        [DataMember]
        public string BackgroundColor { get; set; }
        [DataMember]
        public string BorderColor { get; set; }

        [DataMember]
        public int TotalToShow { get; set; }

        [DataMember]
        public int RefreshTimeSeconds { get; set; }

        [DataMember]
        public OverlayAnimationV3Model ItemAddedAnimation { get; set; } = new OverlayAnimationV3Model();
        [DataMember]
        public OverlayAnimationV3Model ItemRemovedAnimation { get; set; } = new OverlayAnimationV3Model();

        private CancellationTokenSource cancellationTokenSource;
        private string lastLeaderboardState;

        public OverlayLeaderboardV3Model() : base(OverlayItemV3Type.Leaderboard)
        {
            this.RefreshTimeSeconds = DefaultRefreshTimeSeconds;
        }

        public async Task ClearLeaderboard()
        {
            this.lastLeaderboardState = null;
            await this.CallFunction("clear", new Dictionary<string, object>());
        }

        public async Task UpdateLeaderboard(IEnumerable<Tuple<UserV2ViewModel, long>> users)
        {
            if (users == null)
            {
                return;
            }

            JArray jarr = new JArray();
            List<string> leaderboardState = new List<string>();

            foreach (var kvp in users.Take(this.TotalToShow))
            {
                if (kvp.Item2 == 0)
                {
                    continue;
                }

                JObject jobj = new JObject();
                jobj[UserProperty] = JObject.FromObject(kvp.Item1);
                jobj["Details"] = kvp.Item2;
                jarr.Add(jobj);

                if (kvp.Item1 != null)
                {
                    leaderboardState.Add($"{kvp.Item1.ID}:{kvp.Item2}");
                }
            }

            string currentLeaderboardState = string.Join("|", leaderboardState);
            if (string.Equals(this.lastLeaderboardState, currentLeaderboardState))
            {
                return;
            }

            await this.ClearLeaderboard();
            this.lastLeaderboardState = currentLeaderboardState;

            if (jarr.Count == 0)
            {
                return;
            }

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Items"] = jarr;
            await this.CallFunction("update", data);
        }

        public override Dictionary<string, object> GetGenerationProperties()
        {
            Dictionary<string, object> properties = base.GetGenerationProperties();

            foreach (var kvp in this.Header.GetGenerationProperties())
            {
                properties[kvp.Key] = kvp.Value;
            }

            properties[nameof(this.BackgroundColor)] = this.BackgroundColor;
            properties[nameof(this.BorderColor)] = this.BorderColor;

            this.ItemAddedAnimation.AddAnimationProperties(properties, nameof(this.ItemAddedAnimation));
            this.ItemRemovedAnimation.AddAnimationProperties(properties, nameof(this.ItemRemovedAnimation));

            return properties;
        }

        public override async Task Uninitialize()
        {
            await base.Uninitialize();

            if (this.cancellationTokenSource != null)
            {
                this.cancellationTokenSource.Cancel();
                this.cancellationTokenSource = null;
            }

            this.lastLeaderboardState = null;
        }

        protected override Task Loaded()
        {
            if (this.cancellationTokenSource != null)
            {
                this.cancellationTokenSource.Cancel();
                this.cancellationTokenSource = null;
            }

            this.cancellationTokenSource = new CancellationTokenSource();
            this.lastLeaderboardState = null;
            int refreshTimeMilliseconds = 1000 * ((this.RefreshTimeSeconds > 0) ? this.RefreshTimeSeconds : DefaultRefreshTimeSeconds);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            if (this.LeaderboardType == OverlayLeaderboardTypeV3Enum.ViewingTime)
            {
                AsyncRunner.RunAsyncBackground(this.ViewingTimeBackgroundTask, this.cancellationTokenSource.Token, refreshTimeMilliseconds);
            }
            else if (this.LeaderboardType == OverlayLeaderboardTypeV3Enum.Consumable)
            {
                if (ChannelSession.Settings.Currency.ContainsKey(this.ConsumableID))
                {
                    AsyncRunner.RunAsyncBackground(this.ConsumableBackgroundTask, this.cancellationTokenSource.Token, refreshTimeMilliseconds);
                }
            }
            else if (this.LeaderboardType == OverlayLeaderboardTypeV3Enum.TwitchBits)
            {
                AsyncRunner.RunAsyncBackground(this.TwitchBitsBackgroundTask, this.cancellationTokenSource.Token, refreshTimeMilliseconds);
            }
            else if (this.LeaderboardType == OverlayLeaderboardTypeV3Enum.CumulativeMonthsSubbed)
            {
                AsyncRunner.RunAsyncBackground(this.CumulativeMonthsSubbedBackgroundTask, this.cancellationTokenSource.Token, refreshTimeMilliseconds);
            }
            else if (this.LeaderboardType == OverlayLeaderboardTypeV3Enum.SubsGifted)
            {
                AsyncRunner.RunAsyncBackground(this.SubsGiftedBackgroundTask, this.cancellationTokenSource.Token, refreshTimeMilliseconds);
            }
            else if (this.LeaderboardType == OverlayLeaderboardTypeV3Enum.SubscriptionStreak)
            {
                AsyncRunner.RunAsyncBackground(this.SubscriptionStreakBackgroundTask, this.cancellationTokenSource.Token, refreshTimeMilliseconds);
            }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            return Task.CompletedTask;
        }

        private async Task ViewingTimeBackgroundTask(CancellationToken cancellationToken)
        {
            List<UserV2ViewModel> users = new List<UserV2ViewModel>();

            IEnumerable<UserV2Model> userDataList = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
            foreach (UserV2Model userData in userDataList.OrderByDescending(u => u.OnlineViewingMinutes))
            {
                if (userData.IsSpecialtyExcluded)
                {
                    continue;
                }

                if (userData.OnlineViewingMinutes == 0)
                {
                    break;
                }

                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByID(StreamingPlatformTypeEnum.All, userData.ID);
                if (user != null)
                {
                    users.Add(user);
                }

                if (users.Count >= this.TotalToShow)
                {
                    break;
                }
            }

            await this.UpdateLeaderboard(users.Select(u => new Tuple<UserV2ViewModel, long>(u, u.OnlineViewingHoursOnly)));
        }

        private async Task ConsumableBackgroundTask(CancellationToken cancellationToken)
        {
            if (ChannelSession.Settings.Currency.TryGetValue(this.ConsumableID, out var currency))
            {
                List<UserV2ViewModel> users = new List<UserV2ViewModel>();

                IEnumerable<UserV2Model> userDataList = await SpecialIdentifierStringBuilder.GetUserOrderedCurrencyList(currency);
                foreach (UserV2Model userData in userDataList)
                {
                    if (userData.IsSpecialtyExcluded)
                    {
                        continue;
                    }

                    if (currency.GetAmount(userData) == 0)
                    {
                        break;
                    }

                    UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByID(StreamingPlatformTypeEnum.All, userData.ID);
                    if (user != null)
                    {
                        users.Add(user);
                    }

                    if (users.Count >= this.TotalToShow)
                    {
                        break;
                    }
                }

                await this.UpdateLeaderboard(users.Select(u => new Tuple<UserV2ViewModel, long>(u, currency.GetAmount(u))));
            }
        }

        private async Task TwitchBitsBackgroundTask(CancellationToken cancellationToken)
        {
            BitsLeaderboardModel bitsLeaderboard = null;
            switch (this.TwitchBitsDataRange)
            {
                case OverlayLeaderboardDateRangeV3Enum.Daily:
                    bitsLeaderboard = await ServiceManager.Get<TwitchSession>().StreamerService.GetBitsLeaderboard(BitsLeaderboardPeriodEnum.Day, this.TotalToShow);
                    break;
                case OverlayLeaderboardDateRangeV3Enum.Weekly:
                    bitsLeaderboard = await ServiceManager.Get<TwitchSession>().StreamerService.GetBitsLeaderboard(BitsLeaderboardPeriodEnum.Week, this.TotalToShow);
                    break;
                case OverlayLeaderboardDateRangeV3Enum.Monthly:
                    bitsLeaderboard = await ServiceManager.Get<TwitchSession>().StreamerService.GetBitsLeaderboard(BitsLeaderboardPeriodEnum.Month, this.TotalToShow);
                    break;
                case OverlayLeaderboardDateRangeV3Enum.Yearly:
                    bitsLeaderboard = await ServiceManager.Get<TwitchSession>().StreamerService.GetBitsLeaderboard(BitsLeaderboardPeriodEnum.Year, this.TotalToShow);
                    break;
                case OverlayLeaderboardDateRangeV3Enum.AllTime:
                    bitsLeaderboard = await ServiceManager.Get<TwitchSession>().StreamerService.GetBitsLeaderboard(BitsLeaderboardPeriodEnum.All, this.TotalToShow);
                    break;
            }

            if (bitsLeaderboard != null && bitsLeaderboard.users != null)
            {
                List<Tuple<UserV2ViewModel, long>> users = new List<Tuple<UserV2ViewModel, long>>();
                foreach (BitsLeaderboardUserModel bitsUser in bitsLeaderboard.users.OrderBy(u => u.rank).Take(this.TotalToShow))
                {
                    UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: bitsUser.user_id, performPlatformSearch: true);
                    if (user != null && !user.IsSpecialtyExcluded)
                    {
                        users.Add(new Tuple<UserV2ViewModel, long>(user, bitsUser.score));
                    }
                }

                await this.UpdateLeaderboard(users);
            }
        }

        private async Task CumulativeMonthsSubbedBackgroundTask(CancellationToken cancellationToken)
        {
            await this.UpdateUserDataLeaderboard(u => u.TotalMonthsSubbed);
        }

        private async Task SubsGiftedBackgroundTask(CancellationToken cancellationToken)
        {
            await this.UpdateUserDataLeaderboard(u => u.TotalSubsGifted);
        }

        private async Task UpdateUserDataLeaderboard(Func<UserV2Model, long> selector)
        {
            List<Tuple<UserV2ViewModel, long>> users = new List<Tuple<UserV2ViewModel, long>>();

            IEnumerable<UserV2Model> userDataList = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
            foreach (UserV2Model userData in userDataList.Where(u => u.GetPlatforms().Count > 0).OrderByDescending(selector))
            {
                if (userData.IsSpecialtyExcluded)
                {
                    continue;
                }

                if (selector(userData) <= 0)
                {
                    break;
                }

                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByID(StreamingPlatformTypeEnum.All, userData.ID);
                if (user != null)
                {
                    users.Add(new Tuple<UserV2ViewModel, long>(user, selector(userData)));
                }

                if (users.Count >= this.TotalToShow)
                {
                    break;
                }
            }

            await this.UpdateLeaderboard(users);
        }

        private async Task SubscriptionStreakBackgroundTask(CancellationToken cancellationToken)
        {
            List<Tuple<UserV2ViewModel, long>> users = new List<Tuple<UserV2ViewModel, long>>();

            IEnumerable<UserV2Model> userDataList = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
            var streaks = userDataList
                .Where(u => !u.IsSpecialtyExcluded && u.GetPlatforms().Count > 0)
                .Select(u => new { User = u, Date = u.GetEarliestSubscribeDate() })
                .Where(u => u.Date.HasValue)
                .OrderBy(u => u.Date.Value);

            foreach (var streak in streaks)
            {
                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByID(StreamingPlatformTypeEnum.All, streak.User.ID);
                if (user != null)
                {
                    users.Add(new Tuple<UserV2ViewModel, long>(user, Math.Max(1, streak.Date.Value.TotalMonthsFromNow())));
                }

                if (users.Count >= this.TotalToShow)
                {
                    break;
                }
            }

            await this.UpdateLeaderboard(users);
        }
    }
}
