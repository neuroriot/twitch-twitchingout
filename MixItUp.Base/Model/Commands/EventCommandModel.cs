﻿using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat.Trovo;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class EventCommandModel : CommandModelBase
    {
        private const string genericImage = "https://static-cdn.jtvnw.net/jtv_user_pictures/12caba55-1276-49b7-a8bc-88b960ecb5da-profile_image-70x70.png";

        private static SemaphoreSlim followEventsInQueueSemaphore = new SemaphoreSlim(1);
        public static int FollowEventsInQueue = 0;

        public static Dictionary<string, string> GetEventTestSpecialIdentifiers(EventTypeEnum eventType)
        {
            Dictionary<string, string> specialIdentifiers = CommandModelBase.GetGeneralTestSpecialIdentifiers();
            switch (eventType)
            {
                // Generic
                case EventTypeEnum.ChannelRaided:
                    specialIdentifiers["hostviewercount"] = "123";
                    specialIdentifiers["raidviewercount"] = "123";
                    break;
                case EventTypeEnum.ChannelSubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    break;
                case EventTypeEnum.ChannelResubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubmonths"] = "5";
                    specialIdentifiers["usersubstreak"] = "3";
                    break;
                case EventTypeEnum.ChannelSubscriptionGifted:
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubmonthsgifted"] = "3";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.ChannelMassSubscriptionsGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    specialIdentifiers["subsgiftedlifetimeamount"] = "100";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["isanonymous"] = "false";
                    break;

                // Twitch
                case EventTypeEnum.TwitchChannelRaided:
                    specialIdentifiers["hostviewercount"] = "123";
                    specialIdentifiers["raidviewercount"] = "123";
                    break;
                case EventTypeEnum.TwitchChannelSubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    break;
                case EventTypeEnum.TwitchChannelResubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubmonths"] = "5";
                    specialIdentifiers["usersubstreak"] = "3";
                    break;
                case EventTypeEnum.TwitchChannelSubscriptionGifted:
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubmonthsgifted"] = "3";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.TwitchChannelMassSubscriptionsGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    specialIdentifiers["subsgiftedlifetimeamount"] = "100";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.TwitchChannelBitsCheered:
                    specialIdentifiers["bitsamount"] = "10";
                    specialIdentifiers["bitslifetimeamount"] = "100";
                    specialIdentifiers["messagenocheermotes"] = "Test Message";
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.TwitchChannelPointsRedeemed:
                    specialIdentifiers["rewardname"] = "Test Reward";
                    specialIdentifiers["rewardcost"] = "100";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelHypeTrainBegin:
                    specialIdentifiers["hypetraintotalpoints"] = "1";
                    specialIdentifiers["hypetrainlevelpoints"] = "123";
                    specialIdentifiers["hypetrainlevelgoal"] = "500";
                    break;
                case EventTypeEnum.TwitchChannelHypeTrainLevelUp:
                    specialIdentifiers["hypetraintotalpoints"] = "1";
                    specialIdentifiers["hypetrainlevelpoints"] = "123";
                    specialIdentifiers["hypetrainlevelgoal"] = "500";
                    specialIdentifiers["hypetrainlevel"] = "2";
                    break;
                case EventTypeEnum.TwitchChannelHypeTrainEnd:
                    specialIdentifiers["hypetraintotallevel"] = "5";
                    specialIdentifiers["hypetraintotalpoints"] = "1234";
                    break;

                // Trovo
                case EventTypeEnum.TrovoChannelRaided:
                    specialIdentifiers["raidviewercount"] = "123";
                    break;
                case EventTypeEnum.TrovoChannelSubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TrovoChannelResubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubmonths"] = "5";
                    break;
                case EventTypeEnum.TrovoChannelMassSubscriptionsGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    break;
                case EventTypeEnum.TrovoChannelSpellCast:
                    specialIdentifiers[TrovoChatSpellViewModel.SpellNameSpecialIdentifier] = "Spell Name";
                    specialIdentifiers[TrovoChatSpellViewModel.SpellQuantitySpecialIdentifier] = "5";
                    specialIdentifiers[TrovoChatSpellViewModel.SpellTotalValueSpecialIdentifier] = "250";
                    specialIdentifiers[TrovoChatSpellViewModel.SpellValueTypeSpecialIdentifier] = MixItUp.Base.Resources.TrovoElixir;
                    specialIdentifiers[TrovoChatSpellViewModel.SpellValueSpecialIdentifier] = "50";
                    break;

                // Chat
                case EventTypeEnum.ChatUserTimeout:
                    specialIdentifiers["timeoutlength"] = "5m";
                    break;

                // Donation
                case EventTypeEnum.GenericDonation:
                case EventTypeEnum.StreamlabsDonation:
                case EventTypeEnum.TiltifyDonation:
                case EventTypeEnum.ExtraLifeDonation:
                case EventTypeEnum.TipeeeStreamDonation:
                case EventTypeEnum.TreatStreamDonation:
                case EventTypeEnum.RainmakerDonation:
                case EventTypeEnum.JustGivingDonation:
                case EventTypeEnum.StreamElementsDonation:
                case EventTypeEnum.StreamElementsMerchPurchase:
                    UserDonationModel donation = new UserDonationModel()
                    {
                        Amount = 12.34,
                        Message = "Test donation message",
                        ImageLink = genericImage
                    };

                    switch (eventType)
                    {
                        case EventTypeEnum.StreamlabsDonation: donation.Source = UserDonationSourceEnum.Streamlabs; break;
                        case EventTypeEnum.TiltifyDonation: donation.Source = UserDonationSourceEnum.Tiltify; break;
                        case EventTypeEnum.ExtraLifeDonation: donation.Source = UserDonationSourceEnum.ExtraLife; break;
                        case EventTypeEnum.TipeeeStreamDonation: donation.Source = UserDonationSourceEnum.TipeeeStream; break;
                        case EventTypeEnum.TreatStreamDonation: donation.Source = UserDonationSourceEnum.TreatStream; break;
                        case EventTypeEnum.RainmakerDonation: donation.Source = UserDonationSourceEnum.Rainmaker; break;
                        case EventTypeEnum.JustGivingDonation: donation.Source = UserDonationSourceEnum.JustGiving; break;
                        case EventTypeEnum.StreamElementsDonation: donation.Source = UserDonationSourceEnum.StreamElements; break;
                        case EventTypeEnum.StreamElementsMerchPurchase: donation.Source = UserDonationSourceEnum.StreamElements; break;
                    }

                    foreach (var kvp in donation.GetSpecialIdentifiers())
                    {
                        specialIdentifiers[kvp.Key] = kvp.Value;
                    }

                    if (eventType == EventTypeEnum.TreatStreamDonation)
                    {
                        specialIdentifiers["donationtype"] = "Pizza";
                    }

                    if (eventType == EventTypeEnum.StreamElementsMerchPurchase)
                    {
                        specialIdentifiers["allitems"] = "Shirt x2, Mug x3, Hat x4";
                        specialIdentifiers["totalitems"] = "9";
                    }
                    break;
                case EventTypeEnum.PatreonSubscribed:
                    specialIdentifiers[SpecialIdentifierStringBuilder.PatreonTierNameSpecialIdentifier] = "Super Tier";
                    specialIdentifiers[SpecialIdentifierStringBuilder.PatreonTierAmountSpecialIdentifier] = "12.34";
                    specialIdentifiers[SpecialIdentifierStringBuilder.PatreonTierImageSpecialIdentifier] = genericImage;
                    break;

                // Streamloots
                case EventTypeEnum.StreamlootsCardRedeemed:
                    specialIdentifiers["streamlootscardname"] = "Test Card";
                    specialIdentifiers["streamlootscarddescription"] = "Test Description";
                    specialIdentifiers["streamlootscardimage"] = "https://res.cloudinary.com/streamloots/image/upload/f_auto,c_scale,w_250,q_90/static/e19c7bf6-ca3e-49a8-807e-b2e9a1a47524/en_dl_character.png";
                    specialIdentifiers["streamlootscardvideo"] = "https://cdn.streamloots.com/uploads/5c645b78666f31002f2979d1/3a6bf1dc-7d61-4f93-be0a-f5dc1d0d33b6.webm";
                    specialIdentifiers["streamlootscardsound"] = "https://static.streamloots.com/b355d1ef-d931-4c16-a48f-8bed0076401b/alerts/default.mp3";
                    specialIdentifiers["streamlootscardalertmessage"] = "This is an alert message";
                    specialIdentifiers["streamlootsmessage"] = "Test Message";
                    break;
                case EventTypeEnum.StreamlootsPackPurchased:
                case EventTypeEnum.StreamlootsPackGifted:
                    specialIdentifiers["streamlootspurchasequantity"] = "1";
                    break;
            }

            int eventNumber = (int)eventType;
            if (eventNumber >= 200 && eventNumber < 300)
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = StreamingPlatformTypeEnum.Twitch.ToString();
            }
            else if (eventNumber >= 300 && eventNumber < 400)
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = StreamingPlatformTypeEnum.YouTube.ToString();
            }
            else if (eventNumber >= 400 && eventNumber < 500)
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = StreamingPlatformTypeEnum.Trovo.ToString();
            }
            else if (eventNumber >= 500 && eventNumber < 600)
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = StreamingPlatformTypeEnum.Glimesh.ToString();
            }
            else
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = ChannelSession.Settings.DefaultStreamingPlatform.ToString();
            }

            return specialIdentifiers;
        }

        [DataMember]
        public EventTypeEnum EventType { get; set; }

        public EventCommandModel(EventTypeEnum eventType) : base(EnumLocalizationHelper.GetLocalizedName(eventType), CommandTypeEnum.Event) { this.EventType = eventType; }

        [Obsolete]
        public EventCommandModel() : base() { }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return EventCommandModel.GetEventTestSpecialIdentifiers(this.EventType); }

        public override async Task<Result> CustomValidation(CommandParametersModel parameters)
        {
            if (this.UpdateFollowEventModerationCount())
            {
                bool allowFollowEvent = false;
                await EventCommandModel.followEventsInQueueSemaphore.WaitAndRelease(() =>
                {
                    if (EventCommandModel.FollowEventsInQueue < ChannelSession.Settings.ModerationFollowEventMaxInQueue)
                    {
                        EventCommandModel.FollowEventsInQueue++;
                        allowFollowEvent = true;
                    }
                    return Task.CompletedTask;
                });

                if (!allowFollowEvent)
                {
                    return new Result(MixItUp.Base.Resources.ModerationFollowEventCommandCanceledMessage) { DisplayMessage = false };
                }
            }

            return await base.CustomValidation(parameters);
        }

        public override async Task PostRun(CommandParametersModel parameters)
        {
            await base.PostRun(parameters);

            if (this.UpdateFollowEventModerationCount())
            {
                EventCommandModel.FollowEventsInQueue = Math.Max(EventCommandModel.FollowEventsInQueue - 1, 0);
            }
        }

        private bool UpdateFollowEventModerationCount()
        {
            if (ChannelSession.Settings.ModerationFollowEvent)
            {
                if (this.EventType == EventTypeEnum.TwitchChannelFollowed)
                {
                    return true;
                }
            }
            return false;
        }

        public override void TrackTelemetry() { ServiceManager.Get<ITelemetryService>().TrackCommand(this.Type, this.EventType.ToString()); }
    }
}
