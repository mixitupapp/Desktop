using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Twitch.Clients.EventSub;
using MixItUp.Base.Model.Twitch.EventSub;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for DebugControl.xaml
    /// </summary>
    public partial class DebugControl : MainControlBase
    {
        private const string genericImage = "https://static-cdn.jtvnw.net/jtv_user_pictures/12caba55-1276-49b7-a8bc-88b960ecb5da-profile_image-70x70.png";

        public DebugControl()
        {
            InitializeComponent();
        }

        private UserV2ViewModel GetTestUser()
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUsers().FirstOrDefault(u => u.ID != ChannelSession.User.ID);
            if (user == null)
            {
                user = ChannelSession.User;
            }
            return user;
        }

        private async void TriggerGenericDonation_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUsers().FirstOrDefault(u => u.ID != ChannelSession.User.ID);
            if (user == null)
            {
                user = ChannelSession.User;
            }

            UserDonationModel donation = new UserDonationModel()
            {
                Source = UserDonationSourceEnum.Streamlabs,

                ID = Guid.NewGuid().ToString(),
                Username = user.Username,
                Message = "This is a donation message!",

                Amount = 12.34,

                DateTime = DateTimeOffset.Now,

                User = user
            };

            await EventService.ProcessDonationEvent(EventTypeEnum.StreamlabsDonation, donation);
        }

        private async void TriggerTwitchTier1Sub_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUsers().FirstOrDefault(u => u.ID != ChannelSession.User.ID && u.HasPlatformData(Base.Model.StreamingPlatformTypeEnum.Twitch));
            if (user == null)
            {
                user = ChannelSession.User;
            }

            TwitchUserPlatformV2Model twitchUser = user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch);

            await ServiceManager.Get<TwitchSession>().Client.ProcessMockNotification(new NotificationMessage()
            {
                Metadata = new MessageMetadata()
                {
                    SubscriptionType = "channel.chat.notification"
                },
                Payload = new NotificationMessagePayload()
                {
                    Event = JObject.FromObject(new ChatNotification()
                    {
                        notice_type = "sub",

                        broadcaster_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                        broadcaster_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                        broadcaster_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                        chatter_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                        chatter_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                        chatter_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                        message_id = Guid.NewGuid().ToString(),
                        message = new ChatMessageNotificationMessage()
                        {
                            text = "This is a message",
                            fragments = new List<ChatMessageNotificationFragment>()
                            {
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "This"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "is"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "a"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "messge"
                                },
                            }
                        },

                        sub = new ChatNotificationSub()
                        {
                            sub_tier = "1000"
                        }
                    })
                }
            });
        }

        private async void Twitch1GiftedTier1Sub_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUsers().FirstOrDefault(u => u.ID != ChannelSession.User.ID && u.HasPlatformData(Base.Model.StreamingPlatformTypeEnum.Twitch));
            if (user == null)
            {
                user = ChannelSession.User;
            }

            TwitchUserPlatformV2Model twitchUser = user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch);

            await ServiceManager.Get<TwitchSession>().Client.ProcessMockNotification(new NotificationMessage()
            {
                Metadata = new MessageMetadata()
                {
                    SubscriptionType = "channel.chat.notification"
                },
                Payload = new NotificationMessagePayload()
                {
                    Event = JObject.FromObject(new ChatNotification()
                    {
                        notice_type = "sub_gift",

                        broadcaster_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                        broadcaster_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                        broadcaster_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                        chatter_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                        chatter_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                        chatter_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                        message_id = Guid.NewGuid().ToString(),
                        message = new ChatMessageNotificationMessage()
                        {
                            text = "This is a message",
                            fragments = new List<ChatMessageNotificationFragment>()
                            {
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "This"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "is"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "a"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "messge"
                                },
                            }
                        },

                        sub_gift = new ChatNotificationSubGift()
                        {
                            sub_tier = "1000",
                            duration_months = 1,

                            recipient_user_id = twitchUser.ID,
                            recipient_user_login = twitchUser.Username,
                            recipient_user_name = twitchUser.DisplayName
                        }
                    })
                }
            });
        }

        private async void Twitch5GiftedTier1Sub_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUsers().FirstOrDefault(u => u.ID != ChannelSession.User.ID && u.HasPlatformData(Base.Model.StreamingPlatformTypeEnum.Twitch));
            if (user == null)
            {
                user = ChannelSession.User;
            }

            TwitchUserPlatformV2Model twitchUser = user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch);

            string communityGiftID = Guid.NewGuid().ToString();
            for (int i = 0; i < 5; i++)
            {
                await ServiceManager.Get<TwitchSession>().Client.ProcessMockNotification(new NotificationMessage()
                {
                    Metadata = new MessageMetadata()
                    {
                        SubscriptionType = "channel.chat.notification"
                    },
                    Payload = new NotificationMessagePayload()
                    {
                        Event = JObject.FromObject(new ChatNotification()
                        {
                            notice_type = "sub_gift",

                            broadcaster_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                            broadcaster_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                            broadcaster_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                            chatter_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                            chatter_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                            chatter_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                            message_id = Guid.NewGuid().ToString(),
                            message = new ChatMessageNotificationMessage()
                            {
                                text = "This is a message",
                                fragments = new List<ChatMessageNotificationFragment>()
                            {
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "This"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "is"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "a"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "messge"
                                },
                            }
                            },

                            sub_gift = new ChatNotificationSubGift()
                            {
                                sub_tier = "1000",
                                duration_months = 1,
                                community_gift_id = communityGiftID,

                                recipient_user_id = twitchUser.ID,
                                recipient_user_login = twitchUser.Username,
                                recipient_user_name = twitchUser.DisplayName
                            }
                        })
                    }
                });
            }

            await ServiceManager.Get<TwitchSession>().Client.ProcessMockNotification(new NotificationMessage()
            {
                Metadata = new MessageMetadata()
                {
                    SubscriptionType = "channel.chat.notification"
                },
                Payload = new NotificationMessagePayload()
                {
                    Event = JObject.FromObject(new ChatNotification()
                    {
                        notice_type = "community_sub_gift",

                        broadcaster_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                        broadcaster_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                        broadcaster_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                        chatter_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                        chatter_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                        chatter_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                        message_id = Guid.NewGuid().ToString(),
                        message = new ChatMessageNotificationMessage()
                        {
                            text = "This is a message",
                            fragments = new List<ChatMessageNotificationFragment>()
                            {
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "This"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "is"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "a"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "messge"
                                },
                            }
                        },

                        community_sub_gift = new ChatNotificationCommunitySubGift()
                        {
                            id = communityGiftID,
                            total = 5,
                            cumulative_total = 200,
                            sub_tier = "1000",
                        }
                    })
                }
            });
        }


        private async void TwitchAnon5GiftedTier1Sub_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUsers().FirstOrDefault(u => u.ID != ChannelSession.User.ID && u.HasPlatformData(Base.Model.StreamingPlatformTypeEnum.Twitch));
            if (user == null)
            {
                user = ChannelSession.User;
            }

            TwitchUserPlatformV2Model twitchUser = user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch);

            string communityGiftID = Guid.NewGuid().ToString();
            for (int i = 0; i < 5; i++)
            {
                await ServiceManager.Get<TwitchSession>().Client.ProcessMockNotification(new NotificationMessage()
                {
                    Metadata = new MessageMetadata()
                    {
                        SubscriptionType = "channel.chat.notification"
                    },
                    Payload = new NotificationMessagePayload()
                    {
                        Event = JObject.FromObject(new ChatNotification()
                        {
                            notice_type = "sub_gift",

                            broadcaster_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                            broadcaster_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                            broadcaster_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                            chatter_is_anonymous = true,

                            message_id = Guid.NewGuid().ToString(),
                            message = new ChatMessageNotificationMessage()
                            {
                                text = "This is a message",
                                fragments = new List<ChatMessageNotificationFragment>()
                            {
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "This"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "is"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "a"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "messge"
                                },
                            }
                            },

                            sub_gift = new ChatNotificationSubGift()
                            {
                                sub_tier = "1000",
                                duration_months = 1,
                                community_gift_id = communityGiftID,

                                recipient_user_id = twitchUser.ID,
                                recipient_user_login = twitchUser.Username,
                                recipient_user_name = twitchUser.DisplayName
                            }
                        })
                    }
                });
            }

            await ServiceManager.Get<TwitchSession>().Client.ProcessMockNotification(new NotificationMessage()
            {
                Metadata = new MessageMetadata()
                {
                    SubscriptionType = "channel.chat.notification"
                },
                Payload = new NotificationMessagePayload()
                {
                    Event = JObject.FromObject(new ChatNotification()
                    {
                        notice_type = "community_sub_gift",

                        broadcaster_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                        broadcaster_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                        broadcaster_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                        chatter_is_anonymous = true,

                        message_id = Guid.NewGuid().ToString(),
                        message = new ChatMessageNotificationMessage()
                        {
                            text = "This is a message",
                            fragments = new List<ChatMessageNotificationFragment>()
                            {
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "This"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "is"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "a"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "messge"
                                },
                            }
                        },

                        community_sub_gift = new ChatNotificationCommunitySubGift()
                        {
                            id = communityGiftID,
                            total = 5,
                            cumulative_total = 200,
                            sub_tier = "1000",
                        }
                    })
                }
            });
        }

        private async void Twitch100BitsCheer_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUsers().FirstOrDefault(u => u.ID != ChannelSession.User.ID && u.HasPlatformData(Base.Model.StreamingPlatformTypeEnum.Twitch));
            if (user == null)
            {
                user = ChannelSession.User;
            }

            TwitchUserPlatformV2Model twitchUser = user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch);

            await ServiceManager.Get<TwitchSession>().Client.ProcessMockNotification(new NotificationMessage()
            {
                Metadata = new MessageMetadata()
                {
                    SubscriptionType = "channel.chat.message"
                },
                Payload = new NotificationMessagePayload()
                {
                    Event = JObject.FromObject(new ChatMessageNotification()
                    {
                        broadcaster_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                        broadcaster_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                        broadcaster_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                        chatter_user_id = ServiceManager.Get<TwitchSession>().StreamerModel.id,
                        chatter_user_login = ServiceManager.Get<TwitchSession>().StreamerModel.login,
                        chatter_user_name = ServiceManager.Get<TwitchSession>().StreamerModel.display_name,

                        message_id = Guid.NewGuid().ToString(),
                        message = new ChatMessageNotificationMessage()
                        {
                            text = "This is a message",
                            fragments = new List<ChatMessageNotificationFragment>()
                            {
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "This"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "is"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "a"
                                },
                                new ChatMessageNotificationFragment()
                                {
                                    type = "text",
                                    text = "messge"
                                },
                            }
                        },

                        cheer = new ChatMessageNotificationCheer()
                        {
                            bits = 1234
                        },
                    })
                }
            });
        }

        private async Task TriggerFourthwallEvent(string type)
        {
            UserV2ViewModel user = this.GetTestUser();

            JObject payload = JObject.FromObject(new
            {
                testMode = true,
                id = Guid.NewGuid().ToString(),
                webhookId = Guid.NewGuid().ToString(),
                shopId = "test-shop",
                type = type,
                apiVersion = "2023-01-01",
                data = new
                {
                    id = Guid.NewGuid().ToString(),
                    status = "paid",
                    email = "test@example.com",
                    username = user.Username,
                    message = "This is a test message!",
                    amounts = new { total = new { value = 12.34, currency = "USD" } },
                    offers = new[] { new { name = "Test Item" } }
                }
            });

            await ServiceManager.Get<FourthwallService>().ProcessWebhookEvent(payload.ToString());
        }

        private async void TriggerFourthwallDonation_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerFourthwallEvent("DONATION"); }

        private async void TriggerFourthwallOrderPlaced_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerFourthwallEvent("ORDER_PLACED"); }

        private async void TriggerFourthwallGiftPurchase_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerFourthwallEvent("GIFT_PURCHASE"); }

        private async Task TriggerThroneEvent(string eventType)
        {
            UserV2ViewModel user = this.GetTestUser();

            JObject payload = JObject.FromObject(new
            {
                contract_version = "1.0",
                event_id = Guid.NewGuid().ToString(),
                event_type = eventType,
                data = new
                {
                    creator_id = "test-creator",
                    creator_username = ChannelSession.User.Username,
                    gifter_username = user.Username,
                    message = "This is a test message!",
                    item_name = "Test Item",
                    item_thumbnail_url = genericImage,
                    price = 1234,
                    amount = 1234,
                    currency = "USD",
                    is_surprise_gift = false
                }
            });

            await ServiceManager.Get<ThroneService>().ProcessWebhookEvent(payload.ToString());
        }

        private async void TriggerThroneGiftPurchased_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerThroneEvent("gift_purchased"); }

        private async void TriggerThroneContribution_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerThroneEvent("contribution_purchased"); }

        private async void TriggerThroneGiftCrowdfunded_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerThroneEvent("gift_crowdfunded"); }

        private async Task TriggerKoFiEvent(string type, bool isFirstSubscriptionPayment = false)
        {
            UserV2ViewModel user = this.GetTestUser();

            bool isSubscription = string.Equals(type, "Subscription", StringComparison.OrdinalIgnoreCase);
            bool isShopOrder = string.Equals(type, "Shop Order", StringComparison.OrdinalIgnoreCase);

            JObject payload = JObject.FromObject(new
            {
                verification_token = "test-token",
                message_id = Guid.NewGuid().ToString(),
                timestamp = DateTimeOffset.Now.ToString("o"),
                type = type,
                is_public = true,
                from_name = user.Username,
                message = "This is a test message!",
                amount = "12.34",
                url = "https://ko-fi.com/Home/CoffeeShop",
                email = "test@example.com",
                currency = "USD",
                is_subscription_payment = isSubscription,
                is_first_subscription_payment = isSubscription && isFirstSubscriptionPayment,
                kofi_transaction_id = Guid.NewGuid().ToString(),
                tier_name = isSubscription ? "Test Tier" : string.Empty,
                shop_items = isShopOrder
                    ? new object[]
                    {
                        new { direct_link_code = "abc123", variation_name = "Large", quantity = 2 },
                        new { direct_link_code = "def456", variation_name = "Medium", quantity = 1 }
                    }
                    : new object[0]
            });

            await ServiceManager.Get<KoFiService>().ProcessWebhookEvent(payload.ToString());
        }

        // Ko-fi's live payload for a one-time payment actually sends "Donation" (confirmed via
        // testing), not the "Tip" their webhook docs describe - mirror that here for fidelity.
        private async void TriggerKoFiTip_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerKoFiEvent("Donation"); }

        private async void TriggerKoFiCommission_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerKoFiEvent("Commission"); }

        private async void TriggerKoFiMembership_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerKoFiEvent("Subscription", isFirstSubscriptionPayment: false); }

        private async void TriggerKoFiFirstMembership_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerKoFiEvent("Subscription", isFirstSubscriptionPayment: true); }

        private async void TriggerKoFiShopOrder_Click(object sender, System.Windows.RoutedEventArgs e) { await this.TriggerKoFiEvent("Shop Order"); }

        private async void TriggerPallyDonation_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UserV2ViewModel user = this.GetTestUser();

            PallyCampaignTipNotifyPayload payload = new PallyCampaignTipNotifyPayload()
            {
                CampaignTip = new PallyCampaignTip()
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = user.Username,
                    Message = "This is a test message!",
                    GrossAmountInCents = 1234,
                    CreatedAt = DateTimeOffset.Now,
                },
                Page = new PallyPage()
                {
                    Id = "test-page-id",
                    Slug = "test-page",
                    Title = "Test Page",
                    Url = "https://pally.gg/test-page"
                }
            };

            await ServiceManager.Get<PallyService>().ProcessCampaignTip(payload);
        }
    }
}
