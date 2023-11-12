﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Twitch;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using StreamingClient.Base.Util;
using StreamingClient.Base.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Twitch.Base.Clients;
using Twitch.Base.Models.Clients.Chat;
using Twitch.Base.Models.NewAPI.Bits;
using Twitch.Base.Models.NewAPI.Chat;
using Twitch.Base.Models.NewAPI.Users;

namespace MixItUp.Base.Services.Twitch
{
    public class FrankerFaceZEmoteModel : ChatEmoteViewModelBase
    {
        public string id { get; set; }
        public string name { get; set; }
        public JObject urls { get; set; }

        public override string ID { get { return this.id; } protected set { } }
        public override string Name { get { return this.name; } protected set { } }
        public override string ImageURL
        {
            get
            {
                if (this.urls != null && this.urls.Count > 0)
                {
                    return this.urls.ContainsKey("2") ? this.urls["2"].ToString() : this.urls[this.urls.GetKeys().First()].ToString();
                }
                return string.Empty;
            }
            protected set { }
        }
    }

    public class TwitchTMIChatModel
    {
        public long chatter_count { get; set; }
        public TwitchTMIChatGroupsModel chatters { get; set; } = new TwitchTMIChatGroupsModel();
    }

    public class TwitchTMIChatGroupsModel
    {
        public List<string> broadcaster { get; set; } = new List<string>();
        public List<string> vips { get; set; } = new List<string>();
        public List<string> moderators { get; set; } = new List<string>();
        public List<string> staff { get; set; } = new List<string>();
        public List<string> admins { get; set; } = new List<string>();
        public List<string> global_mods { get; set; } = new List<string>();
        public List<string> viewers { get; set; } = new List<string>();
    }

    public class TwitchChatService : StreamingPlatformServiceBase
    {
        private const int MaxMessageLength = 500;

        private static List<string> ExcludedDiagnosticPacketLogging = new List<string>() { "PING", ChatMessagePacketModel.CommandID, ChatUserJoinPacketModel.CommandID, ChatUserLeavePacketModel.CommandID };

        private const string SubMysteryGiftUserNoticeMessageTypeID = "submysterygift";
        private const string SubGiftPaidUpgradeUserNoticeMessageTypeID = "giftpaidupgrade";
        private const string AnnouncementUserNoticeMessageTypeID = "announcement";

        private List<string> emoteSetIDs = new List<string>();

        public IDictionary<string, TwitchChatEmoteViewModel> Emotes { get { return this.emotes; } }
        private Dictionary<string, TwitchChatEmoteViewModel> emotes = new Dictionary<string, TwitchChatEmoteViewModel>();

        public IDictionary<string, FrankerFaceZEmoteModel> FrankerFaceZEmotes { get { return this.frankerFaceZEmotes; } }
        private Dictionary<string, FrankerFaceZEmoteModel> frankerFaceZEmotes = new Dictionary<string, FrankerFaceZEmoteModel>();

        public IDictionary<string, Dictionary<string, ChatBadgeModel>> ChatBadges { get { return this.chatBadges; } }
        private Dictionary<string, Dictionary<string, ChatBadgeModel>> chatBadges = new Dictionary<string, Dictionary<string, ChatBadgeModel>>();

        public IEnumerable<TwitchBitsCheermoteViewModel> BitsCheermotes { get { return this.bitsCheermotes.ToList(); } }
        private List<TwitchBitsCheermoteViewModel> bitsCheermotes = new List<TwitchBitsCheermoteViewModel>();

        private ChatClient userClient;
        private ChatClient botClient;

        private CancellationTokenSource cancellationTokenSource;

        private const int userJoinLeaveEventsTotalToProcess = 25;
        private SemaphoreSlim userJoinLeaveEventsSemaphore = new SemaphoreSlim(1);
        private HashSet<string> userJoinEvents = new HashSet<string>();
        private HashSet<string> userLeaveEvents = new HashSet<string>();

        private HashSet<string> initialUserLogins = new HashSet<string>();

        private HashSet<string> userBans = new HashSet<string>();

        private SemaphoreSlim messageSemaphore = new SemaphoreSlim(1);

        public TwitchChatService() { }

        public bool IsUserConnected { get { return this.userClient != null && this.userClient.IsOpen(); } }
        public bool IsBotConnected { get { return this.botClient != null && this.botClient.IsOpen(); } }

        public override string Name { get { return MixItUp.Base.Resources.TwitchChat; } }

        public async Task<Result> ConnectUser()
        {
            if (ServiceManager.Get<TwitchSessionService>().UserConnection != null)
            {
                return await this.AttemptConnect((Func<Task<Result>>)(async () =>
                {
                    try
                    {
                        this.cancellationTokenSource = new CancellationTokenSource();

                        this.userClient = new ChatClient(ServiceManager.Get<TwitchSessionService>().UserConnection.Connection);

                        if (ChannelSession.AppSettings.DiagnosticLogging)
                        {
                            this.userClient.OnSentOccurred += Client_OnSentOccurred;
                        }

                        this.initialUserLogins.Clear();

                        this.userClient.OnPacketReceived += Client_OnPacketReceived;
                        this.userClient.OnDisconnectOccurred += UserClient_OnDisconnectOccurred;
                        this.userClient.OnPingReceived += UserClient_OnPingReceived;
                        this.userClient.OnUserJoinReceived += UserClient_OnUserJoinReceived;
                        this.userClient.OnUserLeaveReceived += UserClient_OnUserLeaveReceived;
                        this.userClient.OnUserStateReceived += UserClient_OnUserStateReceived;
                        this.userClient.OnGlobalUserStateReceived += UserClient_OnGlobalUserStateReceived;
                        this.userClient.OnUserNoticeReceived += UserClient_OnUserNoticeReceived;
                        this.userClient.OnChatClearReceived += UserClient_OnChatClearReceived;
                        this.userClient.OnMessageReceived += UserClient_OnMessageReceived;
                        this.userClient.OnClearMessageReceived += UserClient_OnClearMessageReceived;

                        this.userClient.OnUserListReceived += UserClient_OnUserListReceived;
                        await this.userClient.Connect();

                        await Task.Delay(1000);

                        await this.userClient.AddCommandsCapability();
                        await this.userClient.AddTagsCapability();
                        await this.userClient.AddMembershipCapability();

                        await Task.Delay(1000);

                        await this.userClient.Join(ServiceManager.Get<TwitchSessionService>().User);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        AsyncRunner.RunAsyncBackground(this.ChatterJoinLeaveBackground, this.cancellationTokenSource.Token, 2500);
                        AsyncRunner.RunAsyncBackground(this.ChatterUpdateBackground, this.cancellationTokenSource.Token, 60000);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                        await Task.Delay(3000);

                        return new Result();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        return new Result(ex);
                    }
                }));
            }
            return new Result(Resources.TwitchConnectionFailed);
        }

        public async Task DisconnectUser()
        {
            try
            {
                if (this.userClient != null)
                {
                    if (ChannelSession.AppSettings.DiagnosticLogging)
                    {
                        this.userClient.OnSentOccurred -= Client_OnSentOccurred;
                    }
                    this.userClient.OnPacketReceived -= Client_OnPacketReceived;
                    this.userClient.OnDisconnectOccurred -= UserClient_OnDisconnectOccurred;
                    this.userClient.OnPingReceived -= UserClient_OnPingReceived;
                    this.userClient.OnUserJoinReceived -= UserClient_OnUserJoinReceived;
                    this.userClient.OnUserLeaveReceived -= UserClient_OnUserLeaveReceived;
                    this.userClient.OnUserStateReceived -= UserClient_OnUserStateReceived;
                    this.userClient.OnUserNoticeReceived -= UserClient_OnUserNoticeReceived;
                    this.userClient.OnChatClearReceived -= UserClient_OnChatClearReceived;
                    this.userClient.OnMessageReceived -= UserClient_OnMessageReceived;
                    this.userClient.OnClearMessageReceived -= UserClient_OnClearMessageReceived;

                    await this.userClient.Disconnect();
                }

                if (this.cancellationTokenSource != null)
                {
                    this.cancellationTokenSource.Cancel();
                    this.cancellationTokenSource = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            this.userClient = null;
        }

        public async Task<Result> ConnectBot()
        {
            if (ServiceManager.Get<TwitchSessionService>().UserConnection != null)
            {
                return await this.AttemptConnect((Func<Task<Result>>)(async () =>
                {
                    try
                    {
                        this.botClient = new ChatClient(ServiceManager.Get<TwitchSessionService>().BotConnection.Connection);

                        if (ChannelSession.AppSettings.DiagnosticLogging)
                        {
                            this.botClient.OnSentOccurred += Client_OnSentOccurred;
                        }
                        this.botClient.OnDisconnectOccurred += BotClient_OnDisconnectOccurred;
                        this.botClient.OnPingReceived += BotClient_OnPingReceived;

                        await this.botClient.Connect();

                        await Task.Delay(1000);

                        await this.botClient.AddCommandsCapability();
                        await this.botClient.AddTagsCapability();
                        await this.botClient.AddMembershipCapability();

                        await Task.Delay(1000);

                        await this.botClient.Join(ServiceManager.Get<TwitchSessionService>().User);

                        await Task.Delay(3000);

                        return new Result();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        return new Result(ex);
                    }
                }));
            }
            return new Result(Resources.TwitchConnectionFailed);
        }

        public async Task DisconnectBot()
        {
            try
            {
                if (this.botClient != null)
                {
                    if (ChannelSession.AppSettings.DiagnosticLogging)
                    {
                        this.botClient.OnSentOccurred -= Client_OnSentOccurred;
                    }
                    this.botClient.OnDisconnectOccurred -= BotClient_OnDisconnectOccurred;
                    this.botClient.OnPingReceived -= BotClient_OnPingReceived;

                    await this.botClient.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            this.botClient = null;
        }

        public async Task Initialize()
        {
            List<Task> initializationTasks = new List<Task>();

            List<Task<IEnumerable<ChatEmoteModel>>> twitchEmoteTasks = new List<Task<IEnumerable<ChatEmoteModel>>>();
            twitchEmoteTasks.Add(ServiceManager.Get<TwitchSessionService>().UserConnection.GetGlobalEmotes());
            twitchEmoteTasks.Add(ServiceManager.Get<TwitchSessionService>().UserConnection.GetChannelEmotes(ServiceManager.Get<TwitchSessionService>().User));
            if (this.emoteSetIDs != null)
            {
                twitchEmoteTasks.Add(ServiceManager.Get<TwitchSessionService>().UserConnection.GetEmoteSets(this.emoteSetIDs));
            }

            initializationTasks.AddRange(twitchEmoteTasks);

            Task<IEnumerable<ChatBadgeSetModel>> globalChatBadgesTask = ServiceManager.Get<TwitchSessionService>().UserConnection.GetGlobalChatBadges();
            initializationTasks.Add(globalChatBadgesTask);
            Task<IEnumerable<ChatBadgeSetModel>> channelChatBadgesTask = ServiceManager.Get<TwitchSessionService>().UserConnection.GetChannelChatBadges(ServiceManager.Get<TwitchSessionService>().User);
            initializationTasks.Add(channelChatBadgesTask);

            if (ChannelSession.Settings.ShowAlejoPronouns)
            {
                ServiceManager.Get<ITelemetryService>().TrackService("AlejoPronouns");
                initializationTasks.Add(ServiceManager.Get<AlejoPronounsService>().Initialize());
            }

            if (ChannelSession.Settings.ShowBetterTTVEmotes)
            {
                ServiceManager.Get<ITelemetryService>().TrackService("BetterTTV");
                initializationTasks.Add(ServiceManager.Get<BetterTTVService>().DownloadGlobalBetterTTVEmotes());
                initializationTasks.Add(ServiceManager.Get<BetterTTVService>().DownloadTwitchBetterTTVEmotes(ServiceManager.Get<TwitchSessionService>().User.id));
            }

            if (ChannelSession.Settings.ShowFrankerFaceZEmotes)
            {
                ServiceManager.Get<ITelemetryService>().TrackService("FrankerFaceZ");
                initializationTasks.Add(this.DownloadFrankerFaceZEmotes());
                initializationTasks.Add(this.DownloadFrankerFaceZEmotes(ServiceManager.Get<TwitchSessionService>().Username));
            }

            Task<IEnumerable<BitsCheermoteModel>> cheermotesTask = ServiceManager.Get<TwitchSessionService>().UserConnection.GetBitsCheermotes(ServiceManager.Get<TwitchSessionService>().User);
            initializationTasks.Add(cheermotesTask);

            await Task.WhenAll(initializationTasks);

            foreach (Task<IEnumerable<ChatEmoteModel>> emoteTask in twitchEmoteTasks)
            {
                if (emoteTask.IsCompleted && emoteTask.Result != null)
                {
                    foreach (ChatEmoteModel emote in emoteTask.Result)
                    {
                        this.emotes[emote.name] = new TwitchChatEmoteViewModel(emote);
                    }
                }
            }

            foreach (ChatBadgeSetModel badgeSet in globalChatBadgesTask.Result)
            {
                this.chatBadges[badgeSet.set_id] = new Dictionary<string, ChatBadgeModel>();
                foreach (ChatBadgeModel badge in badgeSet.versions)
                {
                    this.chatBadges[badgeSet.set_id][badge.id] = badge;
                }
            }

            foreach (ChatBadgeSetModel badgeSet in channelChatBadgesTask.Result)
            {
                this.chatBadges[badgeSet.set_id] = new Dictionary<string, ChatBadgeModel>();
                foreach (ChatBadgeModel badge in badgeSet.versions)
                {
                    this.chatBadges[badgeSet.set_id][badge.id] = badge;
                }
            }

            List<TwitchBitsCheermoteViewModel> cheermotes = new List<TwitchBitsCheermoteViewModel>();
            foreach (BitsCheermoteModel bitsCheermote in cheermotesTask.Result)
            {
                if (bitsCheermote.tiers.Any(t => t.can_cheer))
                {
                    this.bitsCheermotes.Add(new TwitchBitsCheermoteViewModel(bitsCheermote));
                }
            }

            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                foreach (string user in this.initialUserLogins)
                {
                    this.userJoinEvents.Add(user);
                }
                return Task.CompletedTask;
            });
            this.initialUserLogins.Clear();
        }

        public async Task SendMessage(string message, bool sendAsStreamer = false, string replyMessageID = null)
        {
            await this.messageSemaphore.WaitAndRelease(async () =>
            {
                ChatClient client = this.GetChatClient(sendAsStreamer);
                if (client != null)
                {
                    string subMessage = null;
                    do
                    {
                        message = ChatService.SplitLargeMessage(message, MaxMessageLength, out subMessage);
                        if (!string.IsNullOrEmpty(replyMessageID))
                        {
                            await client.SendReplyMessage(ServiceManager.Get<TwitchSessionService>().User, message, replyMessageID);
                        }
                        else
                        {
                            await client.SendMessage(ServiceManager.Get<TwitchSessionService>().User, message);
                        }
                        message = subMessage;
                        await Task.Delay(500);
                    }
                    while (!string.IsNullOrEmpty(message));
                }
            });
        }

        public async Task SendWhisperMessage(UserV2ViewModel user, string message, bool sendAsStreamer = false)
        {
            await this.messageSemaphore.WaitAndRelease(async () =>
            {
                ChatClient client = this.GetChatClient(sendAsStreamer);
                UserModel sender = (!sendAsStreamer && ServiceManager.Get<TwitchSessionService>().IsBotConnected) ? ServiceManager.Get<TwitchSessionService>().Bot : ServiceManager.Get<TwitchSessionService>().User;
                UserModel receiver = user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).GetTwitchNewAPIUserModel();
                if (client != null)
                {
                    string subMessage = null;
                    do
                    {
                        message = ChatService.SplitLargeMessage(message, MaxMessageLength, out subMessage);
                        await client.SendWhisperMessage(sender, receiver, message);
                        message = subMessage;
                        await Task.Delay(500);
                    }
                    while (!string.IsNullOrEmpty(message));
                }
            });
        }

        public async Task DeleteMessage(ChatMessageViewModel message)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.DeleteMessage(ServiceManager.Get<TwitchSessionService>().User, message.ID);
                }
            });
        }

        public async Task ClearMessages()
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.ClearChat(ServiceManager.Get<TwitchSessionService>().User);
                }
            });
        }

        public async Task ModUser(UserV2ViewModel user)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.ModUser(ServiceManager.Get<TwitchSessionService>().User, user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).GetTwitchNewAPIUserModel());
                }
            });
        }

        public async Task UnmodUser(UserV2ViewModel user)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.UnmodUser(ServiceManager.Get<TwitchSessionService>().User, user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).GetTwitchNewAPIUserModel());
                }
            });
        }

        public async Task TimeoutUser(UserV2ViewModel user, int lengthInSeconds)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.TimeoutUser(ServiceManager.Get<TwitchSessionService>().User, user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).GetTwitchNewAPIUserModel(), lengthInSeconds);
                }
            });
        }

        public async Task BanUser(UserV2ViewModel user)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.BanUser(ServiceManager.Get<TwitchSessionService>().User, user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).GetTwitchNewAPIUserModel(), "Manual Ban from Mix It Up");
                }
            });
        }

        public async Task UnbanUser(UserV2ViewModel user)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.UnbanUser(ServiceManager.Get<TwitchSessionService>().User, user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).GetTwitchNewAPIUserModel());
                }
            });
        }

        public async Task RunCommercial(int lengthInSeconds)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.RunCommercial(ServiceManager.Get<TwitchSessionService>().User, lengthInSeconds);
                }
            });
        }

        private ChatClient GetChatClient(bool sendAsStreamer = false) { return (this.botClient != null && !sendAsStreamer) ? this.botClient : this.userClient; }

        private async Task ChatterJoinLeaveBackground(CancellationToken cancellationToken)
        {
            List<string> joinsToProcess = new List<string>();
            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                for (int i = 0; i < userJoinLeaveEventsTotalToProcess && i < this.userJoinEvents.Count(); i++)
                {
                    string username = this.userJoinEvents.First();
                    joinsToProcess.Add(username);
                    this.userJoinEvents.Remove(username);
                }
                return Task.CompletedTask;
            });

            List<string> leavesToProcess = new List<string>();
            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                for (int i = 0; i < userJoinLeaveEventsTotalToProcess && i < this.userLeaveEvents.Count(); i++)
                {
                    string username = this.userLeaveEvents.First();
                    leavesToProcess.Add(username);
                    this.userLeaveEvents.Remove(username);
                }
                return Task.CompletedTask;
            });

            if (joinsToProcess.Count > 0)
            {
                List<UserV2ViewModel> processedUsers = new List<UserV2ViewModel>();
                foreach (string username in joinsToProcess)
                {
                    UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatformUsername(StreamingPlatformTypeEnum.Twitch, username, performPlatformSearch: true);
                    if (user != null)
                    {
                        processedUsers.Add(user);
                    }
                }

                await ServiceManager.Get<UserService>().AddOrUpdateActiveUser(processedUsers);
            }

            if (leavesToProcess.Count > 0)
            {
                List<UserV2ViewModel> processedUsers = new List<UserV2ViewModel>();
                foreach (string username in leavesToProcess)
                {
                    if (!string.IsNullOrEmpty(username))
                    {
                        UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatformUsername(StreamingPlatformTypeEnum.Twitch, username);
                        if (user != null)
                        {
                            processedUsers.Add(user);
                        }
                    }
                }
                await ServiceManager.Get<UserService>().RemoveActiveUsers(processedUsers);
            }
        }

        private async Task ChatterUpdateBackground(CancellationToken cancellationToken)
        {
            IEnumerable<ChatterModel> chatterModels = await ServiceManager.Get<TwitchSessionService>().UserConnection.GetChatters(ServiceManager.Get<TwitchSessionService>().User);

            HashSet<string> chatters = (chatterModels != null) ? new HashSet<string>(chatterModels.Select(c => c.user_login)) : new HashSet<string>();

            HashSet<string> joinsToProcess = new HashSet<string>();
            List<UserV2ViewModel> leavesToProcess = new List<UserV2ViewModel>();

            IEnumerable<UserV2ViewModel> activeUsers = ServiceManager.Get<UserService>().GetActiveUsers(StreamingPlatformTypeEnum.Twitch);
            HashSet<string> activeUsernames = new HashSet<string>();
            activeUsernames.AddRange(activeUsers.Select(u => u.Username));

            foreach (string chatter in chatters)
            {
                if (!activeUsernames.Contains(chatter))
                {
                    joinsToProcess.Add(chatter);
                }
            }

            foreach (UserV2ViewModel activeUser in activeUsers)
            {
                if (!chatters.Contains(activeUser.Username))
                {
                    leavesToProcess.Add(activeUser);
                }
            }

            if (joinsToProcess.Count > 0)
            {
                List<UserV2ViewModel> processedUsers = new List<UserV2ViewModel>();
                foreach (string username in joinsToProcess)
                {
                    UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatformUsername(StreamingPlatformTypeEnum.Twitch, username, performPlatformSearch: true);
                    if (user != null)
                    {
                        processedUsers.Add(user);
                    }
                }

                await ServiceManager.Get<UserService>().AddOrUpdateActiveUser(processedUsers);
            }

            if (leavesToProcess.Count > 0)
            {
                await ServiceManager.Get<UserService>().RemoveActiveUsers(leavesToProcess);
            }
        }

        private async Task DownloadFrankerFaceZEmotes(string channelName = null)
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient())
                {
                    JObject jobj = await client.GetJObjectAsync((!string.IsNullOrEmpty(channelName)) ? "https://api.frankerfacez.com/v1/room/" + channelName : "https://api.frankerfacez.com/v1/set/global");
                    if (jobj != null && jobj.ContainsKey("sets"))
                    {
                        JObject setsJObj = (JObject)jobj["sets"];
                        foreach (var kvp in setsJObj)
                        {
                            JObject setJObj = (JObject)kvp.Value;
                            if (setJObj != null && setJObj.ContainsKey("emoticons"))
                            {
                                JArray emoticonsJArray = (JArray)setJObj["emoticons"];
                                foreach (FrankerFaceZEmoteModel emote in emoticonsJArray.ToTypedArray<FrankerFaceZEmoteModel>())
                                {
                                    this.frankerFaceZEmotes[emote.name] = emote;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private async void UserClient_OnPingReceived(object sender, EventArgs e)
        {
            try
            {
                Logger.Log(LogLevel.Debug, "Twitch User Client - Ping");
                await this.userClient.Pong();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async void BotClient_OnPingReceived(object sender, EventArgs e)
        {
            try
            {
                Logger.Log(LogLevel.Debug, "Twitch Bot Client - Ping");
                await this.botClient.Pong();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async void UserClient_OnUserJoinReceived(object sender, ChatUserJoinPacketModel userJoin)
        {
            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                if (!string.IsNullOrEmpty(userJoin.UserLogin))
                {
                    this.userJoinEvents.Add(userJoin.UserLogin);
                }
                return Task.CompletedTask;
            });
        }

        private async void UserClient_OnUserLeaveReceived(object sender, ChatUserLeavePacketModel userLeave)
        {
            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                if (!string.IsNullOrEmpty(userLeave.UserLogin))
                {
                    this.userLeaveEvents.Add(userLeave.UserLogin);
                }
                return Task.CompletedTask;
            });
        }

        private void UserClient_OnUserStateReceived(object sender, ChatUserStatePacketModel userState)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatformUsername(StreamingPlatformTypeEnum.Twitch, userState.UserDisplayName);
            if (user != null)
            {
                user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).SetUserProperties(userState);
            }
        }

        private void UserClient_OnGlobalUserStateReceived(object sender, ChatGlobalUserStatePacketModel userState)
        {
            if (userState.EmoteSetsDictionary != null)
            {
                this.emoteSetIDs.AddRange(userState.EmoteSetsDictionary);
            }
        }

        private async void UserClient_OnUserNoticeReceived(object sender, ChatUserNoticePacketModel userNotice)
        {
            try
            {
                if (SubMysteryGiftUserNoticeMessageTypeID.Equals(userNotice.MessageTypeID) && userNotice.SubTotalGifted > 0)
                {
                    UserV2ViewModel gifter = UserV2ViewModel.CreateUnassociated("An Anonymous Gifter");
                    if (!TwitchMassGiftedSubEventModel.IsAnonymousGifter(userNotice))
                    {
                        gifter = await ServiceManager.Get<UserService>().GetUserByPlatformID(StreamingPlatformTypeEnum.Twitch, userNotice.UserID.ToString(), performPlatformSearch: true);
                        gifter.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).SetUserProperties(userNotice);
                    }
                    await ServiceManager.Get<TwitchPubSubService>().AddMassGiftedSub(new TwitchMassGiftedSubEventModel(userNotice, gifter));
                }
                else if (SubGiftPaidUpgradeUserNoticeMessageTypeID.Equals(userNotice.MessageTypeID))
                {
                    UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, userNotice.UserID.ToString());
                    if (user == null)
                    {
                        user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(userNotice));
                    }
                    user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).SetUserProperties(userNotice);

                    await ServiceManager.Get<TwitchPubSubService>().AddSub(new TwitchSubEventModel(user, userNotice));
                }
                else if (AnnouncementUserNoticeMessageTypeID.Equals(userNotice.MessageTypeID))
                {
                    UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, userNotice.UserID.ToString());
                    if (user == null)
                    {
                        user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(userNotice));
                    }
                    user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).SetUserProperties(userNotice);

                    await ServiceManager.Get<ChatService>().AddMessage(new TwitchChatMessageViewModel(userNotice, user));
                }
            }
            catch (Exception ex)
            {
                Logger.ForceLog(LogLevel.Debug, JSONSerializerHelper.SerializeToString(userNotice));
                Logger.Log(ex);
                throw ex;
            }
        }

        private async void UserClient_OnChatClearReceived(object sender, ChatClearChatPacketModel chatClear)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, chatClear.UserID);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(chatClear));
            }

            if (chatClear.IsClear)
            {
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, MixItUp.Base.Resources.ChatCleared, ChannelSession.Settings.AlertModerationColor));
            }
            else if (chatClear.IsTimeout)
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Twitch);
                parameters.Arguments.Add("@" + user.Username);
                parameters.TargetUser = user;
                parameters.SpecialIdentifiers["timeoutlength"] = chatClear.BanDuration.ToString();
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserTimeout, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTimedOut, user.FullDisplayName, chatClear.BanDuration), ChannelSession.Settings.AlertModerationColor));
            }
            else if (chatClear.IsBan)
            {
                if (!userBans.Contains(user.Username))
                {
                    userBans.Add(user.Username);

                    CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Twitch);
                    parameters.Arguments.Add("@" + user.Username);
                    parameters.TargetUser = user;
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserBan, parameters);

                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertBanned, user.FullDisplayName), ChannelSession.Settings.AlertModerationColor));

                    await ServiceManager.Get<UserService>().RemoveActiveUser(user.ID);
                }
            }
        }

        private async void UserClient_OnMessageReceived(object sender, ChatMessagePacketModel message)
        {
            if (message != null)
            {
                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatformID(StreamingPlatformTypeEnum.Twitch, message.UserID, performPlatformSearch: true);
                TwitchChatMessageViewModel twitchMessage = null;
                if (string.IsNullOrEmpty(message.UserLogin) || !message.UserLogin.Equals("jtv"))
                {
                    if (!string.IsNullOrEmpty(message.Message))
                    {
                        twitchMessage = new TwitchChatMessageViewModel(message, user);
                        await ServiceManager.Get<ChatService>().AddMessage(twitchMessage);
                    }
                }

                if (int.TryParse(message.PinnedChatPaidAmount, out int amount) && int.TryParse(message.PinnedChatPaidExponent, out int exponent) && amount > 0)
                {
                    double decimalNumber = (double)amount;
                    if (exponent > 0)
                    {
                        decimalNumber = decimalNumber / Math.Pow(10.0, exponent);
                    }

                    CommandParametersModel parameters = (twitchMessage != null) ? new CommandParametersModel(twitchMessage) : new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
                    parameters.SpecialIdentifiers["hypechatamountnumberdigits"] = amount.ToString();
                    parameters.SpecialIdentifiers["hypechatamountnumber"] = decimalNumber.ToString();
                    parameters.SpecialIdentifiers["hypechatamount"] = CurrencyHelper.ToCurrencyString(message.PinnedChatPaidCurrency, decimalNumber);
                    parameters.SpecialIdentifiers["hypechatlevel"] = message.PinnedChatPaidLevel;
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelHypeChat, parameters);
                }
            }
        }

        private async void UserClient_OnClearMessageReceived(object sender, ChatClearMessagePacketModel packet)
        {
            if (packet != null && !string.IsNullOrEmpty(packet.ID) && !string.IsNullOrEmpty(packet.UserLogin))
            {
                UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatformUsername(StreamingPlatformTypeEnum.Twitch, packet.UserLogin);
                if (user == null)
                {
                    user = UserV2ViewModel.CreateUnassociated(packet.UserLogin);
                }

                await ServiceManager.Get<ChatService>().DeleteMessage(new TwitchChatMessageViewModel(packet, user), externalDeletion: true);
            }
        }

        private void UserClient_OnUserListReceived(object sender, ChatUsersListPacketModel userList)
        {
            this.initialUserLogins.AddRange(userList.UserLogins);
            this.userClient.OnUserListReceived -= UserClient_OnUserListReceived;
        }

        private void Client_OnPacketReceived(object sender, ChatRawPacketModel packet)
        {
            if (!TwitchChatService.ExcludedDiagnosticPacketLogging.Contains(packet.Command))
            {
                if (ChannelSession.AppSettings.DiagnosticLogging)
                {
                    Logger.Log(LogLevel.Debug, string.Format("Twitch Client Packet Received: {0}", JSONSerializerHelper.SerializeToString(packet)));
                }
            }
        }

        private void Client_OnSentOccurred(object sender, string packet)
        {
            Logger.Log(LogLevel.Debug, string.Format("Twitch Chat Packet Sent: {0}", packet));
        }

        private async void UserClient_OnDisconnectOccurred(object sender, WebSocketCloseStatus closeStatus)
        {
            ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.TwitchUserChat);

            Result result;
            await this.DisconnectUser();
            do
            {
                await Task.Delay(2500);

                result = await this.ConnectUser();
            }
            while (!result.Success);

            ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.TwitchUserChat);
        }

        private async void BotClient_OnDisconnectOccurred(object sender, WebSocketCloseStatus closeStatus)
        {
            ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.TwitchBotChat);

            Result result;
            await this.DisconnectBot();
            do
            {
                await Task.Delay(2500);

                result = await this.ConnectBot();
            }
            while (!result.Success);

            ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.TwitchBotChat);
        }
    }
}