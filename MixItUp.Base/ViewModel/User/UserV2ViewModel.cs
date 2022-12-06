﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.User
{
    public class UserV2ViewModel : UIViewModelBase, IEquatable<UserV2ViewModel>, IComparable<UserV2ViewModel>
    {
        public const string UserDefaultColor = "MaterialDesignBody";
        public static readonly TimeSpan RefreshTimeSpan = TimeSpan.FromMinutes(5);

        public static UserV2ViewModel CreateUnassociated(string username = null) { return new UserV2ViewModel(StreamingPlatformTypeEnum.None, UserV2Model.CreateUnassociated(username)); }

        private StreamingPlatformTypeEnum platform;
        private UserV2Model model;
        private UserPlatformV2ModelBase platformModel;

        private object cachePropertiesLock = new object();

        public UserV2ViewModel(UserV2Model model) : this(StreamingPlatformTypeEnum.None, model) { }

        public UserV2ViewModel(StreamingPlatformTypeEnum platform, UserV2Model model)
        {
            this.model = model;

            if (this.platform != StreamingPlatformTypeEnum.None)
            {
                this.platformModel = this.GetPlatformData<UserPlatformV2ModelBase>(this.platform);
            }

            if (this.platformModel == null && this.HasPlatformData(ChannelSession.Settings.DefaultStreamingPlatform))
            {
                this.platformModel = this.GetPlatformData<UserPlatformV2ModelBase>(ChannelSession.Settings.DefaultStreamingPlatform);
            }

            if (this.platformModel == null && this.Model.GetPlatforms().Count > 0)
            {
                this.platformModel = this.GetPlatformData<UserPlatformV2ModelBase>(this.Model.GetPlatforms().First());
            }

            if (this.platformModel != null)
            {
                this.platform = this.platformModel.Platform;
            }
            else
            {
                throw new InvalidOperationException($"User data does not contain any platform data - {model.ID} - {platform}");
            }
        }

        public UserV2Model Model { get { return this.model; } }

        public UserPlatformV2ModelBase PlatformModel { get { return this.platformModel; } }

        public Guid ID { get { return this.Model.ID; } }

        public StreamingPlatformTypeEnum Platform { get { return this.platform; } }

        public HashSet<StreamingPlatformTypeEnum> AllPlatforms { get { return this.Model.GetPlatforms(); } }

        public bool IsUnassociated { get { return this.Platform == StreamingPlatformTypeEnum.None; } }

        public string PlatformID { get { return this.PlatformModel.ID; } }

        public string Username { get { return this.PlatformModel.Username; } }

        public string DisplayName { get { return !string.IsNullOrEmpty(this.PlatformModel.DisplayName) ? this.PlatformModel.DisplayName : this.Username; } }

        public string FullDisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(this.PlatformModel.DisplayName))
                {
                    if (!string.Equals(this.DisplayName, this.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{this.DisplayName} ({this.Username})";
                    }
                    else
                    {
                        return this.DisplayName;
                    }
                }
                else
                {
                    return this.Username;
                }
            }
        }

        public string AvatarLink { get { return this.PlatformModel.AvatarLink; } }

        public bool ShowUserAvatar { get { return !ChannelSession.Settings.HideUserAvatar; } }

        public string LastSeenString { get { return (this.LastActivity != DateTimeOffset.MinValue) ? this.LastActivity.ToFriendlyDateTimeString() : "Unknown"; } }

        public HashSet<UserRoleEnum> Roles { get { return this.PlatformModel.Roles; } }

        public HashSet<UserRoleEnum> DisplayRoles
        {
            get
            {
                HashSet<UserRoleEnum> roles = new HashSet<UserRoleEnum>(this.Roles);
                if (roles.Count > 1)
                {
                    roles.Remove(UserRoleEnum.User);
                }
                if (roles.Contains(UserRoleEnum.Subscriber) || roles.Contains(UserRoleEnum.YouTubeSubscriber))
                {
                    roles.Remove(UserRoleEnum.Follower);
                }
                if (roles.Contains(UserRoleEnum.YouTubeMember))
                {
                    roles.Remove(UserRoleEnum.Subscriber);
                }
                if (roles.Contains(UserRoleEnum.TrovoSuperMod))
                {
                    roles.Remove(UserRoleEnum.Moderator);
                }
                if (roles.Contains(UserRoleEnum.Streamer))
                {
                    roles.Remove(UserRoleEnum.Subscriber);
                }    
                roles.Remove(UserRoleEnum.TwitchAffiliate);
                roles.Remove(UserRoleEnum.TwitchPartner);
                return roles;
            }
        }

        public string RolesString
        {
            get
            {
                lock (this.rolesStringLock)
                {
                    if (this.rolesString == null)
                    {
                        List<string> displayRoles = new List<string>(this.Roles.OrderByDescending(r => r).Select(r => r.ToString()));
                        //displayRoles.AddRange(this.CustomRoles);
                        this.rolesString = string.Join(", ", displayRoles);
                    }
                    return this.rolesString;
                }
            }
            private set
            {
                lock (this.rolesStringLock)
                {
                    this.rolesString = value;
                }
            }
        }
        private string rolesString = null;
        private object rolesStringLock = new object();

        public string DisplayRolesString
        {
            get
            {
                lock (this.displayRolesStringLock)
                {
                    if (this.displayRolesString == null)
                    {
                        List<string> displayRoles = new List<string>(this.DisplayRoles.OrderByDescending(r => r).Select(r => EnumLocalizationHelper.GetLocalizedName(r)));
                        //displayRoles.AddRange(this.CustomRoles);
                        this.displayRolesString = string.Join(", ", displayRoles);
                    }
                    return this.displayRolesString;
                }
            }
            private set
            {
                lock (this.displayRolesStringLock)
                {
                    this.displayRolesString = value;
                }
            }
        }
        private string displayRolesString = null;
        private object displayRolesStringLock = new object();

        public UserRoleEnum PrimaryRole
        {
            get
            {
                lock (cachePropertiesLock)
                {
#pragma warning disable CS0612 // Type or member is obsolete
                    if (this.primaryRole == UserRoleEnum.Banned)
#pragma warning restore CS0612 // Type or member is obsolete
                    {
                        this.primaryRole = this.Roles.Max();
                    }
                    return this.primaryRole;
                }
            }
        }
#pragma warning disable CS0612 // Type or member is obsolete
        private UserRoleEnum primaryRole = UserRoleEnum.Banned;
#pragma warning restore CS0612 // Type or member is obsolete

        public string PrimaryRoleString { get { return EnumLocalizationHelper.GetLocalizedName(this.PrimaryRole); } }

        public bool IsPlatformSubscriber { get { return this.Roles.Contains(UserRoleEnum.Subscriber) || this.Roles.Contains(UserRoleEnum.YouTubeMember); } }

        public bool IsExternalSubscriber
        {
            get
            {
                if (this.PatreonUser != null && ServiceManager.Get<PatreonService>().IsConnected && !string.IsNullOrEmpty(ChannelSession.Settings.PatreonTierSubscriberEquivalent))
                {
                    PatreonTier userTier = this.PatreonTier;
                    PatreonTier equivalentTier = ServiceManager.Get<PatreonService>().Campaign.GetTier(ChannelSession.Settings.PatreonTierSubscriberEquivalent);
                    if (userTier != null && equivalentTier != null && userTier.Amount >= equivalentTier.Amount)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool IsFollower { get { return this.HasRole(UserRoleEnum.Follower) || this.HasRole(UserRoleEnum.YouTubeSubscriber); } }
        public bool IsRegular { get { return this.HasRole(UserRoleEnum.Regular); } }
        public bool IsSubscriber { get { return this.IsPlatformSubscriber || this.IsExternalSubscriber; } }

        public string Color
        {
            get
            {
                lock (cachePropertiesLock)
                {
                    if (this.color == null)
                    {
                        if (ChannelSession.Settings.UseCustomUsernameColors)
                        {
                            foreach (UserRoleEnum role in this.Roles.OrderByDescending(r => r))
                            {
                                if (ChannelSession.Settings.CustomUsernameRoleColors.ContainsKey(role))
                                {
                                    string name = ChannelSession.Settings.CustomUsernameRoleColors[role];
                                    if (ColorSchemes.HTMLColorSchemeDictionary.ContainsKey(name))
                                    {
                                        this.color = ColorSchemes.HTMLColorSchemeDictionary[name];
                                        break;
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(this.color))
                        {
                            if (this.Platform == StreamingPlatformTypeEnum.Twitch)
                            {
                                this.color = ((TwitchUserPlatformV2Model)this.PlatformModel).Color;
                            }
                        }

                        if (string.IsNullOrEmpty(this.color))
                        {
                            this.color = UserV2ViewModel.UserDefaultColor;
                        }
                    }
                    return this.color;
                }
            }
        }
        private string color;

        public string ChannelLink
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Twitch) { return $"https://www.twitch.tv/{this.Username}"; }
                else if (this.Platform == StreamingPlatformTypeEnum.YouTube) { return ((YouTubeUserPlatformV2Model)this.PlatformModel).YouTubeURL; }
                else if (this.Platform == StreamingPlatformTypeEnum.Glimesh) { return $"https://www.glimesh.tv/{this.Username}"; }
                else if (this.Platform == StreamingPlatformTypeEnum.Trovo) { return $"https://trovo.live/{this.Username}"; }
                return string.Empty;
            }
        }

        public string PlatformImageURL { get { return StreamingPlatforms.GetPlatformImage(this.Platform); } }

        public bool ShowPlatformImage { get { return ServiceManager.GetAll<IStreamingPlatformSessionService>().Count(s => s.IsConnected) > 1; } }

        public string PlatformBadgeLink
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Twitch) { return "/Assets/Images/Twitch-Small.png"; }
                else if (this.Platform == StreamingPlatformTypeEnum.YouTube) { return "/Assets/Images/YouTube.png"; }
                else if (this.Platform == StreamingPlatformTypeEnum.Glimesh) { return "/Assets/Images/Glimesh.png"; }
                else if (this.Platform == StreamingPlatformTypeEnum.Trovo) { return "/Assets/Images/Trovo.png"; }
                return null;
            }
        }
        public bool ShowPlatformBadge { get { return true; } }

        public DateTimeOffset? AccountDate { get { return this.PlatformModel.AccountDate; } set { this.PlatformModel.AccountDate = value; } }
        public string AccountAgeString { get { return (this.AccountDate != null) ? this.AccountDate.GetValueOrDefault().GetAge() : MixItUp.Base.Resources.Unknown; } }
        public int AccountDays { get { return (this.AccountDate != null) ? this.AccountDate.GetValueOrDefault().TotalDaysFromNow() : 0; } }

        public DateTimeOffset? FollowDate { get { return this.PlatformModel.FollowDate; } set { this.PlatformModel.FollowDate = value; } }
        public string FollowAgeString { get { return (this.FollowDate != null) ? this.FollowDate.GetValueOrDefault().GetAge() : MixItUp.Base.Resources.NotFollowing; } }
        public int FollowDays { get { return (this.FollowDate != null) ? this.FollowDate.GetValueOrDefault().TotalDaysFromNow() : 0; } }
        public int FollowMonths { get { return (this.FollowDate != null) ? this.FollowDate.GetValueOrDefault().TotalMonthsFromNow() : 0; } }

        public DateTimeOffset? SubscribeDate { get { return this.PlatformModel.SubscribeDate; } set { this.PlatformModel.SubscribeDate = value; } }
        public string SubscribeAgeString { get { return (this.SubscribeDate != null) ? this.SubscribeDate.GetValueOrDefault().GetAge() : MixItUp.Base.Resources.NotSubscribed; } }
        public int SubscribeDays { get { return (this.SubscribeDate != null) ? this.SubscribeDate.GetValueOrDefault().TotalDaysFromNow() : 0; } }
        public int SubscribeMonths { get { return (this.SubscribeDate != null) ? this.SubscribeDate.GetValueOrDefault().TotalMonthsFromNow() : 0; } }

        public int SubscriberTier { get { return this.PlatformModel.SubscriberTier; } set { this.PlatformModel.SubscriberTier = value; } }
        public string SubscriberTierString
        {
            get
            {
                return (this.IsPlatformSubscriber) ? $"{MixItUp.Base.Resources.Tier} {this.SubscriberTier}" : MixItUp.Base.Resources.NotSubscribed;
            }
        }
        public string PlatformSubscriberBadgeLink { get { return this.PlatformModel.SubscriberBadgeLink; } }
        public bool ShowPlatformSubscriberBadge { get { return !ChannelSession.Settings.HideUserSubscriberBadge && this.IsPlatformSubscriber && !string.IsNullOrEmpty(this.PlatformSubscriberBadgeLink); } }

        public string PlatformRoleBadgeLink { get { return this.PlatformModel.RoleBadgeLink; } }
        public bool ShowPlatformRoleBadge { get { return !ChannelSession.Settings.HideUserRoleBadge && !string.IsNullOrEmpty(this.PlatformRoleBadgeLink); } }

        public string PlatformSpecialtyBadgeLink { get { return this.PlatformModel.SpecialtyBadgeLink; } }
        public bool ShowPlatformSpecialtyBadge { get { return !ChannelSession.Settings.HideUserRoleBadge && !string.IsNullOrEmpty(this.PlatformSpecialtyBadgeLink); } }

        public Dictionary<Guid, int> CurrencyAmounts { get { return this.Model.CurrencyAmounts; } }

        public Dictionary<Guid, Dictionary<Guid, int>> InventoryAmounts { get { return this.Model.InventoryAmounts; } }

        public Dictionary<Guid, int> StreamPassAmounts { get { return this.Model.StreamPassAmounts; } }

        public int OnlineViewingMinutes
        {
            get { return this.Model.OnlineViewingMinutes; }
            set
            {
                this.Model.OnlineViewingMinutes = value;
                this.NotifyPropertyChanged("OnlineViewingMinutes");
                this.NotifyPropertyChanged("OnlineViewingMinutesOnly");
                this.NotifyPropertyChanged("OnlineViewingHoursOnly");
            }
        }

        public int OnlineViewingMinutesOnly
        {
            get { return this.OnlineViewingMinutes % 60; }
            set
            {
                this.OnlineViewingMinutes = (this.OnlineViewingHoursOnly * 60) + value;
                this.NotifyPropertyChanged("OnlineViewingMinutes");
                this.NotifyPropertyChanged("OnlineViewingMinutesOnly");
                this.NotifyPropertyChanged("OnlineViewingHoursOnly");
            }
        }

        public int OnlineViewingHoursOnly
        {
            get { return this.OnlineViewingMinutes / 60; }
            set
            {
                this.OnlineViewingMinutes = value * 60 + this.OnlineViewingMinutesOnly;
                this.NotifyPropertyChanged("OnlineViewingMinutes");
                this.NotifyPropertyChanged("OnlineViewingMinutesOnly");
                this.NotifyPropertyChanged("OnlineViewingHoursOnly");
            }
        }

        public string OnlineViewingTimeString { get { return string.Format("{0} Hours & {1} Mins", this.OnlineViewingHoursOnly, this.OnlineViewingMinutesOnly); } }

        public int PrimaryCurrency
        {
            get
            {
                CurrencyModel currency = ChannelSession.Settings.Currency.Values.FirstOrDefault(c => !c.IsRank && c.IsPrimary);
                if (currency != null)
                {
                    return currency.GetAmount(this);
                }
                return 0;
            }
        }

        public int PrimaryRankPoints
        {
            get
            {

                CurrencyModel rank = ChannelSession.Settings.Currency.Values.FirstOrDefault(c => c.IsRank && c.IsPrimary);
                if (rank != null)
                {
                    return rank.GetAmount(this);
                }
                return 0;
            }
        }

        public string PrimaryRankNameAndPoints
        {
            get
            {
                CurrencyModel rank = ChannelSession.Settings.Currency.Values.FirstOrDefault(c => c.IsRank && c.IsPrimary);
                if (rank != null)
                {
                    return string.Format("{0} - {1}", rank.Name, rank.GetAmount(this));
                }

                return string.Empty;
            }
        }

        public long TotalStreamsWatched
        {
            get { return this.Model.TotalStreamsWatched; }
            set { this.Model.TotalStreamsWatched = value; }
        }

        public double TotalAmountDonated
        {
            get { return this.Model.TotalAmountDonated; }
            set { this.Model.TotalAmountDonated = value; }
        }

        public long TotalSubsGifted
        {
            get { return this.Model.TotalSubsGifted; }
            set { this.Model.TotalSubsGifted = value; }
        }

        public long TotalSubsReceived
        {
            get { return this.Model.TotalSubsReceived; }
            set { this.Model.TotalSubsReceived = value; }
        }

        public long TotalChatMessageSent
        {
            get { return this.Model.TotalChatMessageSent; }
            set { this.Model.TotalChatMessageSent = value; }
        }

        public long TotalTimesTagged
        {
            get { return this.Model.TotalTimesTagged; }
            set { this.Model.TotalTimesTagged = value; }
        }

        public long TotalCommandsRun
        {
            get { return this.Model.TotalCommandsRun; }
            set { this.Model.TotalCommandsRun = value; }
        }

        public long TotalMonthsSubbed
        {
            get { return this.Model.TotalMonthsSubbed; }
            set { this.Model.TotalMonthsSubbed = value; }
        }

        public uint ModerationStrikes
        {
            get { return this.Model.ModerationStrikes; }
            set { this.Model.ModerationStrikes = value; }
        }

        public bool IsSpecialtyExcluded
        {
            get { return this.Model.IsSpecialtyExcluded; }
            set { this.Model.IsSpecialtyExcluded = value; }
        }

        public CommandModelBase EntranceCommand
        {
            get { return ChannelSession.Settings.GetCommand(this.Model.EntranceCommandID); }
            set { this.Model.EntranceCommandID = (value != null) ? value.ID : Guid.Empty; }
        }

        public string Title
        {
            get
            {
                if (!string.IsNullOrEmpty(this.CustomTitle))
                {
                    return this.CustomTitle;
                }

                UserTitleModel title = ChannelSession.Settings.UserTitles.OrderByDescending(t => t.UserRole).ThenByDescending(t => t.Months).FirstOrDefault(t => t.MeetsTitle(this));
                if (title != null)
                {
                    return title.Name;
                }

                return MixItUp.Base.Resources.NoTitle;
            }
        }

        public string CustomTitle
        {
            get { return this.Model.CustomTitle; }
            set { this.Model.CustomTitle = value; }
        }

        public Guid EntranceCommandID
        {
            get { return this.Model.EntranceCommandID; }
            set { this.Model.EntranceCommandID = value; }
        }

        public List<Guid> CustomCommandIDs { get { return this.Model.CustomCommandIDs; } }

        public string Notes
        {
            get { return this.Model.Notes; }
            set { this.Model.Notes = value; }
        }

        public DateTimeOffset LastActivity { get { return this.Model.LastActivity; } }

        public DateTimeOffset LastUpdated { get; private set; }

        public bool IsInChat { get; set; }

        public string SortableID
        {
            get
            {
                lock (cachePropertiesLock)
                {
                    if (this.sortableID == null)
                    {
                        UserRoleEnum role = this.PrimaryRole;
                        if (role < UserRoleEnum.Subscriber)
                        {
                            role = UserRoleEnum.User;
                        }
                        this.sortableID = (99999 - role) + "-" + this.Username + "-" + this.Platform.ToString();
                    }
                    return this.sortableID;
                }
            }
        }
        private string sortableID;

        public int WhispererNumber { get; set; }

        public bool HasWhisperNumber { get { return this.WhispererNumber > 0; } }

        public string PatreonID { get { return this.Model.PatreonUserID; } }

        public PatreonCampaignMember PatreonUser
        {
            get
            {
                if (this.patreonUser != null)
                {
                    return this.patreonUser;
                }

                if (!string.IsNullOrEmpty(this.Model.PatreonUserID) && ServiceManager.Get<PatreonService>().IsConnected)
                {
                    this.patreonUser = ServiceManager.Get<PatreonService>().CampaignMembers.FirstOrDefault(m => this.Model.PatreonUserID.Equals(m.ID));
                }

                return this.patreonUser;
            }
            set
            {
                this.patreonUser = value;
                if (this.patreonUser != null)
                {
                    this.Model.PatreonUserID = this.patreonUser.ID;
                }
                else
                {
                    this.model.PatreonUserID = null;
                }
            }
        }
        private PatreonCampaignMember patreonUser;

        public PatreonTier PatreonTier
        {
            get
            {
                if (ServiceManager.Get<PatreonService>().IsConnected && this.PatreonUser != null)
                {
                    return ServiceManager.Get<PatreonService>().Campaign.GetTier(this.PatreonUser.TierID);
                }
                return null;
            }
        }

        public void UpdateLastActivity() { this.Model.LastActivity = DateTimeOffset.Now; }

        public void UpdateViewingMinutes(Dictionary<StreamingPlatformTypeEnum, bool> liveStreams)
        {
            this.OnlineViewingMinutes++;
            ChannelSession.Settings.Users.ManualValueChanged(this.ID);

            if (ChannelSession.Settings.RegularUserMinimumHours > 0 && this.OnlineViewingHoursOnly >= ChannelSession.Settings.RegularUserMinimumHours)
            {
                this.Roles.Add(UserRoleEnum.Regular);
            }
            else
            {
                this.Roles.Remove(UserRoleEnum.Regular);
            }
        }

        public bool HasRole(UserRoleEnum role) { return this.Roles.Contains(role); }

        public bool MeetsRole(UserRoleEnum role)
        {
            if ((role == UserRoleEnum.Subscriber || role == UserRoleEnum.YouTubeMember) && this.IsSubscriber)
            {
                return true;
            }

            if (ChannelSession.Settings.ExplicitUserRoleRequirements)
            {
                Logger.Log($"Perform explicit user role check: {role}");
                return this.HasRole(role);
            }

            Logger.Log($"Perform regular user role check: {this.PrimaryRole} >= {role}");
            return this.PrimaryRole >= role;
        }

        public bool ExceedRole(UserRoleEnum role) { return this.PrimaryRole > role; }

        public async Task AddModerationStrike(string moderationReason = null)
        {
            Dictionary<string, string> extraSpecialIdentifiers = new Dictionary<string, string>();
            extraSpecialIdentifiers.Add(ModerationService.ModerationReasonSpecialIdentifier, moderationReason);

            this.ModerationStrikes++;
            if (this.ModerationStrikes == 1)
            {
                await ServiceManager.Get<CommandService>().Queue(ChannelSession.Settings.ModerationStrike1CommandID, new CommandParametersModel(this, extraSpecialIdentifiers));
            }
            else if (this.ModerationStrikes == 2)
            {
                await ServiceManager.Get<CommandService>().Queue(ChannelSession.Settings.ModerationStrike2CommandID, new CommandParametersModel(this, extraSpecialIdentifiers));
            }
            else if (this.ModerationStrikes >= 3)
            {
                await ServiceManager.Get<CommandService>().Queue(ChannelSession.Settings.ModerationStrike3CommandID, new CommandParametersModel(this, extraSpecialIdentifiers));
            }
        }

        public void RemoveModerationStrike()
        {
            if (this.ModerationStrikes > 0)
            {
                this.ModerationStrikes--;
            }
        }

        public bool HasPlatformData(StreamingPlatformTypeEnum platform) { return this.Model.HasPlatformData(platform); }

        public T GetPlatformData<T>(StreamingPlatformTypeEnum platform) where T : UserPlatformV2ModelBase { return this.Model.GetPlatformData<T>(platform); }

        public async Task Refresh(bool force = false)
        {
            if (!this.IsUnassociated)
            {
                TimeSpan lastUpdatedTimeSpan = DateTimeOffset.Now - this.LastUpdated;
                if (force || lastUpdatedTimeSpan > RefreshTimeSpan)
                {
                    this.LastUpdated = DateTimeOffset.Now;

                    DateTimeOffset refreshStart = DateTimeOffset.Now;

                    await this.platformModel.Refresh();

                    this.RefreshPatreonProperties();

                    this.ClearCachedProperties();

                    double refreshTime = (DateTimeOffset.Now - refreshStart).TotalMilliseconds;
                    Logger.Log($"User refresh time: {refreshTime} ms");
                    if (refreshTime > 1000)
                    {
                        Logger.Log(LogLevel.Error, string.Format("Long user refresh time detected for the following user: {0} - {1} - {2} ms", this.ID, this.Username, refreshTime));
                    }
                }
            }
        }

        public void RefreshPatreonProperties()
        {
            if (ServiceManager.Get<PatreonService>().IsConnected && this.PatreonUser == null)
            {
                IEnumerable<PatreonCampaignMember> campaignMembers = ServiceManager.Get<PatreonService>().CampaignMembers;

                if (!string.IsNullOrEmpty(this.model.PatreonUserID))
                {
                    this.PatreonUser = campaignMembers.FirstOrDefault(u => u.UserID.Equals(this.model.PatreonUserID));
                }
                else
                {
                    this.PatreonUser = campaignMembers.FirstOrDefault(u => this.Platform == u.User.Platform && string.Equals(u.User.PlatformUserID, this.PlatformID, StringComparison.InvariantCultureIgnoreCase));
                }

                if (this.PatreonUser != null)
                {
                    this.model.PatreonUserID = this.PatreonUser.UserID;
                }
                else
                {
                    this.model.PatreonUserID = null;
                }
            }
        }

        public async Task MergeUserData(UserV2ViewModel other)
        {
            this.model.AddPlatformData(other.platformModel);
            this.model.OnlineViewingMinutes += other.model.OnlineViewingMinutes;
            
            foreach (var kvp in other.model.CurrencyAmounts)
            {
                if (!this.model.CurrencyAmounts.ContainsKey(kvp.Key))
                {
                    this.model.CurrencyAmounts[kvp.Key] = 0;
                }
                this.model.CurrencyAmounts[kvp.Key] += kvp.Value;
            }

            foreach (var kvp in other.model.InventoryAmounts)
            {
                if (!this.model.InventoryAmounts.ContainsKey(kvp.Key))
                {
                    this.model.InventoryAmounts[kvp.Key] = new Dictionary<Guid, int>();
                }

                foreach (var itemKVP in kvp.Value)
                {
                    if (!this.model.InventoryAmounts[kvp.Key].ContainsKey(itemKVP.Key))
                    {
                        this.model.InventoryAmounts[kvp.Key][itemKVP.Key] = 0;
                    }
                    this.model.InventoryAmounts[kvp.Key][itemKVP.Key] += itemKVP.Value;
                }
            }

            foreach (var kvp in other.model.StreamPassAmounts)
            {
                if (!this.model.StreamPassAmounts.ContainsKey(kvp.Key))
                {
                    this.model.StreamPassAmounts[kvp.Key] = 0;
                }
                this.model.StreamPassAmounts[kvp.Key] += kvp.Value;
            }

            if (string.IsNullOrEmpty(this.model.CustomTitle)) { this.model.CustomTitle = other.model.CustomTitle; }
            if (!this.model.IsSpecialtyExcluded) { this.model.IsSpecialtyExcluded = other.model.IsSpecialtyExcluded; }
            if (this.model.EntranceCommandID == Guid.Empty) { this.model.EntranceCommandID = other.model.EntranceCommandID; }
            
            foreach (Guid id in other.model.CustomCommandIDs)
            {
                this.model.CustomCommandIDs.Add(id);
            }

            if (string.IsNullOrEmpty(this.model.PatreonUserID)) { this.model.PatreonUserID = other.model.PatreonUserID; }

            if (string.IsNullOrEmpty(this.model.Notes))
            {
                this.model.Notes = other.model.Notes;
            }
            else if (!string.IsNullOrEmpty(other.model.Notes))
            {
                this.model.Notes += Environment.NewLine + Environment.NewLine + other.model.Notes;
            }

            this.model.TotalStreamsWatched += other.Model.TotalStreamsWatched;
            this.model.TotalAmountDonated += other.Model.TotalAmountDonated;
            this.model.TotalSubsGifted += other.Model.TotalSubsGifted;
            this.model.TotalSubsReceived += other.Model.TotalSubsReceived;
            this.model.TotalChatMessageSent += other.Model.TotalChatMessageSent;
            this.model.TotalTimesTagged += other.Model.TotalTimesTagged;
            this.model.TotalCommandsRun += other.Model.TotalCommandsRun;
            this.model.TotalMonthsSubbed += other.Model.TotalMonthsSubbed;

            await ServiceManager.Get<UserService>().RemoveActiveUser(other.ID);
            await ServiceManager.Get<UserService>().RemoveActiveUser(this.ID);

            ServiceManager.Get<UserService>().DeleteUserData(other.ID);
            ServiceManager.Get<UserService>().SetUserData(this.model);

            await ServiceManager.Get<UserService>().AddOrUpdateActiveUser(this);
        }

        public void MergeUserData(UserImportModel import)
        {
            this.OnlineViewingMinutes += import.OnlineViewingMinutes;
            foreach (var kvp in import.CurrencyAmounts)
            {
                if (!this.CurrencyAmounts.ContainsKey(kvp.Key))
                {
                    this.CurrencyAmounts[kvp.Key] = 0;
                }
                this.CurrencyAmounts[kvp.Key] += kvp.Value;
            }
        }

        private void ClearCachedProperties()
        {
            lock (cachePropertiesLock)
            {
                this.rolesString = null;
                this.displayRolesString = null;
                this.color = null;
                this.sortableID = null;
#pragma warning disable CS0612 // Type or member is obsolete
                this.primaryRole = UserRoleEnum.Banned;
#pragma warning restore CS0612 // Type or member is obsolete
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is UserV2ViewModel)
            {
                return this.Equals((UserV2ViewModel)obj);
            }
            return false;
        }

        public bool Equals(UserV2ViewModel other)
        {
            return this.ID.Equals(other.ID);
        }

        public override int GetHashCode()
        {
            return this.ID.GetHashCode();
        }

        public int CompareTo(UserV2ViewModel other) { return this.SortableID.CompareTo(other.SortableID); }

        public override string ToString() { return this.Username; }
    }
}
