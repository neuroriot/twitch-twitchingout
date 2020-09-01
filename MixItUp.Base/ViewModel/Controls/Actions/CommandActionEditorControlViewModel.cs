﻿using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using StreamingClient.Base.Util;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Controls.Actions
{
    public class CommandActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.Command; } }

        public IEnumerable<CommandActionTypeEnum> ActionTypes { get { return EnumHelper.GetEnumList<CommandActionTypeEnum>(); } }

        public CommandActionTypeEnum SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowCommandsSection");
                this.NotifyPropertyChanged("ShowCommandGroupsSection");
            }
        }
        private CommandActionTypeEnum selectedActionType;

        public bool ShowCommandsSection { get { return this.SelectedActionType == CommandActionTypeEnum.RunCommand || this.SelectedActionType == CommandActionTypeEnum.EnableCommand || this.SelectedActionType == CommandActionTypeEnum.DisableCommand; } }

        public IEnumerable<CommandTypeEnum> CommandTypes { get { return EnumHelper.GetEnumList<CommandTypeEnum>(); } }

        public CommandTypeEnum SelectedCommandType
        {
            get { return this.selectedCommandType; }
            set
            {
                this.selectedCommandType = value;
                this.SelectedCommand = null;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("Commands");
            }
        }
        private CommandTypeEnum selectedCommandType;

        public IEnumerable<CommandModelBase> Commands
        {
            get
            {
                // TODO
                return new List<CommandModelBase>();
            }
        }

        public CommandModelBase SelectedCommand
        {
            get { return this.selectedCommand; }
            set
            {
                this.selectedCommand = value;
                this.NotifyPropertyChanged();
            }
        }
        private CommandModelBase selectedCommand;

        public string CommandArguments
        {
            get { return this.commandArguments; }
            set
            {
                this.commandArguments = value;
                this.NotifyPropertyChanged();
            }
        }
        private string commandArguments;

        public bool ShowCommandGroupsSection { get { return this.SelectedActionType == CommandActionTypeEnum.EnableCommandGroup || this.SelectedActionType == CommandActionTypeEnum.DisableCommandGroup; } }

        public IEnumerable<string> CommandGroups { get { return ChannelSession.Settings.CommandGroups.Keys.ToList(); } }

        public string SelectedCommandGroup
        {
            get { return this.selectedCommandGroup; }
            set
            {
                this.selectedCommandGroup = value;
                this.NotifyPropertyChanged();
            }
        }
        private string selectedCommandGroup;

        public CommandActionEditorControlViewModel(CommandActionModel action)
        {
            this.SelectedActionType = action.ActionType;
            if (this.ShowCommandsSection)
            {
                this.SelectedCommand = action.Command;
            }
            else if (this.ShowCommandGroupsSection)
            {
                this.SelectedCommandGroup = action.CommandGroupName;
            }
        }

        public CommandActionEditorControlViewModel() { }

        public override Task<Result> Validate()
        {
            if (this.ShowCommandsSection)
            {
                if (this.SelectedCommand == null)
                {
                    return Task.FromResult(new Result(MixItUp.Base.Resources.CommandActionSelectCommand));
                }
            }
            else if (this.ShowCommandGroupsSection)
            {
                if (string.IsNullOrEmpty(this.SelectedCommandGroup))
                {
                    return Task.FromResult(new Result(MixItUp.Base.Resources.CommandActionSelectCommandGroup));
                }
            }
            return Task.FromResult(new Result());
        }

        public override Task<ActionModelBase> GetAction()
        {
            if (this.ShowCommandsSection)
            {
                return Task.FromResult<ActionModelBase>(new CommandActionModel(this.SelectedActionType, this.SelectedCommand, this.CommandArguments));
            }
            else if (this.ShowCommandGroupsSection)
            {
                return Task.FromResult<ActionModelBase>(new CommandActionModel(this.SelectedActionType, this.SelectedCommandGroup));
            }
            return Task.FromResult<ActionModelBase>(null);
        }
    }
}
