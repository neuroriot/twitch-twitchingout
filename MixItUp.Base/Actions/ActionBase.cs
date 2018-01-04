﻿using Mixer.Base.Model.User;
using Mixer.Base.Util;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Actions
{
    public enum ActionTypeEnum
    {
        Chat,
        Currency,
        [Name("External Program")]
        ExternalProgram,
        Input,
        Overlay,
        Sound,
        Wait,
        [Name("OBS Studio")]
        OBSStudio,
        XSplit,
        Counter,
        [Name("Game Queue")]
        GameQueue,
        Interactive,
        [Name("Text To Speech")]
        TextToSpeech,
        [Obsolete]
        Rank,
        [Name("Web Request")]
        WebRequest,
        [Name("Action Group")]
        ActionGroup,

        Custom = 99,
    }

    [DataContract]
    public abstract class ActionBase
    {
        [DataMember]
        public ActionTypeEnum Type { get; set; }

        public ActionBase() { }

        public ActionBase(ActionTypeEnum type)
        {
            this.Type = type;
        }

        public async Task Perform(UserViewModel user, IEnumerable<string> arguments)
        {
            await this.AsyncSemaphore.WaitAsync();

            try
            {
                await this.PerformInternal(user, arguments);
            }
            catch (Exception ex) { Logger.Log(ex); }
            finally { this.AsyncSemaphore.Release(); }
        }

        protected abstract Task PerformInternal(UserViewModel user, IEnumerable<string> arguments);

        protected async Task<string> ReplaceStringWithSpecialModifiers(string str, UserViewModel user, IEnumerable<string> arguments)
        {
            SpecialIdentifierStringBuilder siString = new SpecialIdentifierStringBuilder(str);
            await siString.ReplaceCommonSpecialModifiers(user, arguments);
            return siString.ToString();
        }

        protected abstract SemaphoreSlim AsyncSemaphore { get; }
    }
}
