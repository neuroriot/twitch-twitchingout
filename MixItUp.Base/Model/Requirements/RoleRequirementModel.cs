﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Services.Trovo;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Requirements
{
    [DataContract]
    public class RoleRequirementModel : RequirementModelBase
    {
        private static DateTimeOffset requirementErrorCooldown = DateTimeOffset.MinValue;

        [Obsolete]
        [DataMember]
        public OldUserRoleEnum Role { get; set; }
        [Obsolete]
        [DataMember]
        public HashSet<OldUserRoleEnum> RoleList { get; set; } = new HashSet<OldUserRoleEnum>();

        [DataMember]
        public StreamingPlatformTypeEnum StreamingPlatform { get; set; } = StreamingPlatformTypeEnum.All;

        [DataMember]
        public UserRoleEnum UserRole { get; set; }
        [DataMember]
        public HashSet<UserRoleEnum> UserRoleList { get; set; } = new HashSet<UserRoleEnum>();

        [DataMember]
        public int SubscriberTier { get; set; } = 1;

        [DataMember]
        public string TrovoCustomRole { get; set; }

        [DataMember]
        public string PatreonBenefitID { get; set; }

        public RoleRequirementModel(StreamingPlatformTypeEnum streamingPlatform, UserRoleEnum role, int subscriberTier = 1, string trovoCustomRole = null, string patreonBenefitID = null)
        {
            this.StreamingPlatform = streamingPlatform;
            this.UserRole = role;
            this.SubscriberTier = subscriberTier;
            this.TrovoCustomRole = trovoCustomRole;
            this.PatreonBenefitID = patreonBenefitID;
        }

        public RoleRequirementModel(StreamingPlatformTypeEnum streamingPlatform, IEnumerable<UserRoleEnum> roleList, int subscriberTier = 1, string trovoCustomRole = null, string patreonBenefitID = null)
        {
            this.StreamingPlatform = streamingPlatform;
            this.UserRoleList = new HashSet<UserRoleEnum>(roleList);
            this.SubscriberTier = subscriberTier;
            this.TrovoCustomRole = trovoCustomRole;
            this.PatreonBenefitID = patreonBenefitID;
        }

        public RoleRequirementModel() { }

        public string DisplayRole
        {
            get
            {
                if (this.UserRoleList.Count > 0)
                {
                    return MixItUp.Base.Resources.Multiple;
                }
                else
                {
                    return EnumLocalizationHelper.GetLocalizedName(this.UserRole);
                }
            }
        }

        protected override DateTimeOffset RequirementErrorCooldown { get { return RoleRequirementModel.requirementErrorCooldown; } set { RoleRequirementModel.requirementErrorCooldown = value; } }

        public override Task<Result> Validate(CommandParametersModel parameters)
        {
            if (this.StreamingPlatform == StreamingPlatformTypeEnum.All || parameters.Platform == StreamingPlatformTypeEnum.All || parameters.Platform == this.StreamingPlatform)
            {
                if (this.UserRoleList.Count > 0)
                {
                    foreach (UserRoleEnum role in this.UserRoleList)
                    {
                        if (parameters.User.HasRole(role))
                        {
                            if (role == UserRoleEnum.Subscriber || role == UserRoleEnum.YouTubeMember)
                            {
                                if (ChannelSession.Settings.ExplicitUserRoleRequirements)
                                {
                                    if (parameters.User.SubscriberTier == this.SubscriberTier)
                                    {
                                        return Task.FromResult(new Result());
                                    }
                                }
                                else
                                {
                                    if (this.SubscriberTier == 1 || parameters.User.SubscriberTier >= this.SubscriberTier)
                                    {
                                        return Task.FromResult(new Result());
                                    }
                                }
                            }
                            else
                            {
                                return Task.FromResult(new Result());
                            }
                        }
                    }
                }
                else
                {
                    if (parameters.User.MeetsRole(this.UserRole))
                    {
                        if (this.UserRole == UserRoleEnum.Subscriber || this.UserRole == UserRoleEnum.YouTubeMember)
                        {
                            if (ChannelSession.Settings.ExplicitUserRoleRequirements)
                            {
                                if (parameters.User.SubscriberTier == this.SubscriberTier)
                                {
                                    return Task.FromResult(new Result());
                                }
                            }
                            else
                            {
                                if (parameters.User.ExceedRole(this.UserRole) || this.SubscriberTier == 1 || parameters.User.SubscriberTier >= this.SubscriberTier)
                                {
                                    return Task.FromResult(new Result());
                                }
                            }
                        }
                        else
                        {
                            return Task.FromResult(new Result());
                        }
                    }
                }

                if (parameters.Platform == StreamingPlatformTypeEnum.Trovo && !string.IsNullOrEmpty(this.TrovoCustomRole) && ServiceManager.Get<TrovoSessionService>().IsConnected)
                {
                    TrovoUserPlatformV2Model trovoUser = parameters.User.GetPlatformData<TrovoUserPlatformV2Model>(StreamingPlatformTypeEnum.Trovo);
                    if (trovoUser != null && trovoUser.CustomRoles.Contains(this.TrovoCustomRole))
                    {
                        return Task.FromResult(new Result());
                    }
                }

                if (!string.IsNullOrEmpty(this.PatreonBenefitID) && ServiceManager.Get<PatreonService>().IsConnected)
                {
                    PatreonBenefit benefit = ServiceManager.Get<PatreonService>().Campaign.GetBenefit(this.PatreonBenefitID);
                    if (benefit != null)
                    {
                        PatreonTier tier = parameters.User.PatreonTier;
                        if (tier != null && tier.BenefitIDs.Contains(benefit.ID))
                        {
                            return Task.FromResult(new Result());
                        }
                    }
                }
            }

            return Task.FromResult(this.CreateErrorMessage(parameters));
        }

        [Obsolete]
        public void UpgradeFromOldRoles()
        {
            this.UserRole = UserRoles.ConvertFromOldRole(this.Role);
            foreach (OldUserRoleEnum oldRole in this.RoleList)
            {
                this.UserRoleList.Add(UserRoles.ConvertFromOldRole(oldRole));
            }
        }

        private Result CreateErrorMessage(CommandParametersModel parameters)
        {
            if (this.StreamingPlatform != StreamingPlatformTypeEnum.All && parameters.Platform != StreamingPlatformTypeEnum.All && parameters.Platform != this.StreamingPlatform)
            {
                return new Result(string.Format(MixItUp.Base.Resources.RoleErrorIncorrectStreamingPlatform, this.StreamingPlatform));
            }
            else
            {
                List<string> roleNames = new List<string>();
                if (this.UserRoleList.Count > 0)
                {
                    foreach (UserRoleEnum role in this.UserRoleList)
                    {
                        roleNames.Add(this.GetRoleName(role));
                    }
                }
                else
                {
                    roleNames.Add(this.GetRoleName(this.UserRole));
                }
                return new Result(string.Format(MixItUp.Base.Resources.RoleErrorInsufficientRole, string.Join(" / ", roleNames)));
            }
        }

        private string GetRoleName(UserRoleEnum role)
        {
            string roleName = EnumLocalizationHelper.GetLocalizedName(role);
            if (role == UserRoleEnum.Subscriber)
            {
                string tierText = string.Empty;
                switch (this.SubscriberTier)
                {
                    case 1: tierText = MixItUp.Base.Resources.Tier1; break;
                    case 2: tierText = MixItUp.Base.Resources.Tier2; break;
                    case 3: tierText = MixItUp.Base.Resources.Tier3; break;
                }
                roleName = tierText + " " + roleName;
            }
            return roleName;
        }
    }
}
