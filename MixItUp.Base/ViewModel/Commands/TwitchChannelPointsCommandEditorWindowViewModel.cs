﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Util;
using System;
using System.Linq;
using System.Threading.Tasks;
using Twitch.Base.Models.NewAPI.ChannelPoints;

namespace MixItUp.Base.ViewModel.Commands
{
    public class TwitchChannelPointsCommandEditorWindowViewModel : CommandEditorWindowViewModelBase
    {
        public ThreadSafeObservableCollection<CustomChannelPointRewardModel> ChannelPointRewards { get; set; } = new ThreadSafeObservableCollection<CustomChannelPointRewardModel>();

        public CustomChannelPointRewardModel ChannelPointReward
        {
            get { return this.channelPointReward; }
            set
            {
                this.channelPointReward = value;
                this.NotifyPropertyChanged();

                this.Name = (this.channelPointReward != null) ? this.channelPointReward.title : string.Empty;
            }
        }
        private CustomChannelPointRewardModel channelPointReward;

        private Guid existingChannelPointRewardID = Guid.Empty;

        public TwitchChannelPointsCommandEditorWindowViewModel(TwitchChannelPointsCommandModel existingCommand)
            : base(existingCommand)
        {
            this.existingChannelPointRewardID = existingCommand.ChannelPointRewardID;
        }

        public TwitchChannelPointsCommandEditorWindowViewModel() : base(CommandTypeEnum.TwitchChannelPoints) { }

        public override Task<Result> Validate()
        {
            if (this.ChannelPointReward == null)
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ChannelPointRewardMissing));
            }

            return Task.FromResult(new Result());
        }

        public override Task<CommandModelBase> CreateNewCommand() { return Task.FromResult<CommandModelBase>(new TwitchChannelPointsCommandModel(this.ChannelPointReward.title, this.ChannelPointReward.id)); }

        public override async Task UpdateExistingCommand(CommandModelBase command)
        {
            await base.UpdateExistingCommand(command);
            ((TwitchChannelPointsCommandModel)command).ChannelPointRewardID = this.ChannelPointReward.id;
        }

        public override Task SaveCommandToSettings(CommandModelBase command)
        {
            ServiceManager.Get<CommandService>().TwitchChannelPointsCommands.Remove((TwitchChannelPointsCommandModel)this.existingCommand);
            ServiceManager.Get<CommandService>().TwitchChannelPointsCommands.Add((TwitchChannelPointsCommandModel)command);
            return Task.CompletedTask;
        }

        protected override async Task OnOpenInternal()
        {
            foreach (CustomChannelPointRewardModel channelPoint in (await ServiceManager.Get<TwitchSessionService>().UserConnection.GetCustomChannelPointRewards(ServiceManager.Get<TwitchSessionService>().User)).OrderBy(c => c.title))
            {
                this.ChannelPointRewards.Add(channelPoint);
            }

            if (this.existingChannelPointRewardID != Guid.Empty)
            {
                this.ChannelPointReward = this.ChannelPointRewards.FirstOrDefault(c => c.id.Equals(this.existingChannelPointRewardID));
            }
            else if (!string.IsNullOrEmpty(this.Name))
            {
                this.ChannelPointReward = this.ChannelPointRewards.FirstOrDefault(c => c.title.Equals(this.Name));
            }

            await base.OnOpenInternal();
        }
    }
}
