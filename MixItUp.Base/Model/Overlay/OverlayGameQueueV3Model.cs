﻿using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    public class OverlayGameQueueV3Model : OverlayVisualTextV3ModelBase
    {
        public const string AnimationItemElementName = "item";
        public const string RemoveItemPostAnimationFunction = "list.removeChild(item);";

        public const string ItemSwapAnimationPropertyName = "ItemSwapAnimation";
        public const string AnimationOldItemElementName = "oldItem";
        public const string SwapItemPostAnimationFunction = "swapItems(oldItem, newItem);";

        public static readonly string DefaultHTML = OverlayResources.OverlayGameQueueDefaultHTML;
        public static readonly string DefaultCSS = OverlayResources.OverlayGameQueueDefaultCSS + "\n\n" + OverlayResources.OverlayTextDefaultCSS;
        public static readonly string DefaultJavascript = OverlayResources.OverlayGameQueueDefaultJavascript;

        [DataMember]
        public string BackgroundColor { get; set; }
        [DataMember]
        public string BorderColor { get; set; }

        [DataMember]
        public int TotalToShow { get; set; }

        [DataMember]
        public OverlayAnimationV3Model ItemAddedAnimation { get; set; } = new OverlayAnimationV3Model();
        [DataMember]
        public OverlayAnimationV3Model ItemRemovedAnimation { get; set; } = new OverlayAnimationV3Model();

        public OverlayGameQueueV3Model() : base(OverlayItemV3Type.GameQueue) { }

        public async Task ClearGameQueue()
        {
            await this.CallFunction("clear", new Dictionary<string, object>());
        }

        public async Task UpdateGameQueue(IEnumerable<UserV2ViewModel> users)
        {
            if (users == null || users.Count() == 0)
            {
                return;
            }

            JArray jarr = new JArray();

            foreach (UserV2ViewModel user in users.Take(this.TotalToShow))
            {
                JObject jobj = new JObject();
                jobj["User"] = JObject.FromObject(user);
                jarr.Add(jobj);
            }

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Items"] = jarr;
            await this.CallFunction("update", data);
        }

        public override Dictionary<string, object> GetGenerationProperties()
        {
            Dictionary<string, object> properties = base.GetGenerationProperties();

            properties[nameof(this.BackgroundColor)] = this.BackgroundColor;
            properties[nameof(this.BorderColor)] = this.BorderColor;

            properties[nameof(this.ItemAddedAnimation)] = this.ItemAddedAnimation.GenerateAnimationJavascript(OverlayGameQueueV3Model.AnimationItemElementName);
            properties[nameof(this.ItemRemovedAnimation)] = this.ItemRemovedAnimation.GenerateAnimationJavascript(OverlayGameQueueV3Model.AnimationItemElementName, postAnimation: OverlayGameQueueV3Model.RemoveItemPostAnimationFunction);
            properties[OverlayGameQueueV3Model.ItemSwapAnimationPropertyName] = this.ItemRemovedAnimation.GenerateAnimationJavascript(OverlayGameQueueV3Model.AnimationOldItemElementName, postAnimation: OverlayGameQueueV3Model.SwapItemPostAnimationFunction);

            return properties;
        }

        protected override async Task WidgetEnableInternal()
        {
            await base.WidgetEnableInternal();

            GameQueueService.OnGameQueueUpdated += GameQueueService_OnGameQueueUpdated;
        }

        protected override async Task WidgetDisableInternal()
        {
            await base.WidgetDisableInternal();

            GameQueueService.OnGameQueueUpdated -= GameQueueService_OnGameQueueUpdated;
        }

        private async void GameQueueService_OnGameQueueUpdated(object sender, EventArgs e)
        {
            await this.UpdateGameQueue(ServiceManager.Get<GameQueueService>().Queue.ToList().Select(p => p.User));
        }
    }
}
