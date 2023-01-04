﻿using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using StreamingClient.Base.Util;
using StreamingClient.Base.Web;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class IFTTTService : OAuthExternalServiceBase
    {
        private const string WebHookURLFormat = "https://maker.ifttt.com/trigger/{0}/with/key/{1}";

        public IFTTTService() : base("") { }

        public override string Name { get { return MixItUp.Base.Resources.IFTTT; } }

        public override Task<Result> Connect()
        {
            return Task.FromResult(new Result(false));
        }

        public override Task Disconnect()
        {
            this.token = null;
            return Task.CompletedTask;
        }

        public async Task SendTrigger(string eventName, Dictionary<string, string> values)
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient())
                {
                    JObject jobj = new JObject();
                    foreach (var kvp in values)
                    {
                        jobj[kvp.Key] = kvp.Value;
                    }
                    HttpContent content = new StringContent(JSONSerializerHelper.SerializeToString(jobj), Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(string.Format(WebHookURLFormat, eventName, this.token.accessToken), content);
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.Log(await response.Content.ReadAsStringAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        protected override Task<Result> InitializeInternal()
        {
            return Task.FromResult(new Result());
        }

        protected override Task RefreshOAuthToken()
        {
            return Task.CompletedTask;
        }
    }
}
