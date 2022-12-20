﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Commands.Games;
using MixItUp.Base.Model.Requirements;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using StreamingClient.Base.Model.OAuth;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public enum SettingsBackupRateEnum
    {
        None = 0,
        Daily,
        Weekly,
        Monthly,
    }

    public static class OAuthTokenModelStaticMethods
    {
        public static void Reset(this OAuthTokenModel token)
        {
            if (token != null)
            {
                token.accessToken = String.Empty;
                token.refreshToken = String.Empty;
            }
        }
    }

    public class SettingsService
    {
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public void Initialize() { Directory.CreateDirectory(SettingsV3Model.SettingsDirectoryName); }

        public async Task<IEnumerable<SettingsV3Model>> GetAllSettings()
        {
            bool backupSettingsLoaded = false;
            bool settingsLoadFailure = false;

            List<SettingsV3Model> allSettings = new List<SettingsV3Model>();
            foreach (string filePath in Directory.GetFiles(SettingsV3Model.SettingsDirectoryName))
            {
                if (filePath.EndsWith(SettingsV3Model.SettingsFileExtension))
                {
                    SettingsV3Model setting = null;
                    try
                    {
                        setting = await this.LoadSettings(filePath);
                        if (setting != null)
                        {
                            allSettings.Add(setting);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }

                    if (setting == null)
                    {
                        string localBackupFilePath = string.Format($"{filePath}.{SettingsV3Model.SettingsLocalBackupFileExtension}");
                        if (File.Exists(localBackupFilePath))
                        {
                            try
                            {
                                setting = await this.LoadSettings(localBackupFilePath);
                                if (setting != null)
                                {
                                    allSettings.Add(setting);
                                    backupSettingsLoaded = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(ex);
                            }
                        }
                    }

                    if (setting == null)
                    {
                        settingsLoadFailure = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(ChannelSession.AppSettings.BackupSettingsFilePath) && ChannelSession.AppSettings.BackupSettingsToReplace != Guid.Empty)
            {
                Logger.Log(LogLevel.Debug, "Restored settings file detected, starting restore process");

                SettingsV3Model settings = allSettings.FirstOrDefault(s => s.ID.Equals(ChannelSession.AppSettings.BackupSettingsToReplace));
                if (settings != null)
                {
                    File.Delete(settings.SettingsFilePath);
                    File.Delete(settings.DatabaseFilePath);

                    // Adding delay to ensure the above files are actually deleted
                    await Task.Delay(2000);

                    await ServiceManager.Get<IFileService>().UnzipFiles(ChannelSession.AppSettings.BackupSettingsFilePath, SettingsV3Model.SettingsDirectoryName);

                    ChannelSession.AppSettings.BackupSettingsFilePath = null;
                    ChannelSession.AppSettings.BackupSettingsToReplace = Guid.Empty;

                    return await this.GetAllSettings();
                }
            }
            else if (ChannelSession.AppSettings.SettingsToDelete != Guid.Empty)
            {
                Logger.Log(LogLevel.Debug, "Settings deletion detected, starting deletion process");

                SettingsV3Model settings = allSettings.FirstOrDefault(s => s.ID.Equals(ChannelSession.AppSettings.SettingsToDelete));
                if (settings != null)
                {
                    File.Delete(settings.SettingsFilePath);
                    File.Delete(settings.DatabaseFilePath);

                    ChannelSession.AppSettings.SettingsToDelete = Guid.Empty;

                    return await this.GetAllSettings();
                }
            }

            if (backupSettingsLoaded)
            {
                await DialogHelper.ShowMessage(Resources.BackupSettingsLoadedError);
            }
            if (settingsLoadFailure)
            {
                await DialogHelper.ShowMessage(Resources.SettingsLoadFailure);
            }

            return allSettings;
        }

        public async Task Initialize(SettingsV3Model settings)
        {
            await settings.Initialize();
        }

        public async Task Save(SettingsV3Model settings)
        {
            if (settings != null)
            {
                Logger.Log(LogLevel.Debug, "Settings save operation started");

                await semaphore.WaitAndRelease(async () =>
                {
                    settings.CopyLatestValues();
                    await FileSerializerHelper.SerializeToFile(settings.SettingsFilePath, settings);
                    await settings.SaveDatabaseData();
                });

                Logger.Log(LogLevel.Debug, "Settings save operation finished");
            }
        }

        public async Task SaveLocalBackup(SettingsV3Model settings)
        {
            if (settings != null)
            {
                Logger.Log(LogLevel.Debug, "Settings local backup save operation started");

                if (ServiceManager.Get<IFileService>().GetFileSize(settings.SettingsFilePath) == 0)
                {
                    Logger.Log(LogLevel.Debug, "Main settings file is empty, aborting local backup settings save operation");
                    return;
                }

                await semaphore.WaitAndRelease(async () =>
                {
                    await FileSerializerHelper.SerializeToFile(settings.SettingsLocalBackupFilePath, settings);
                });

                Logger.Log(LogLevel.Debug, "Settings local backup save operation finished");
            }
        }

        public async Task SavePackagedBackup(SettingsV3Model settings, string filePath)
        {
            await this.Save(ChannelSession.Settings);

            try
            {
                if (Directory.Exists(Path.GetDirectoryName(filePath)))
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    ServiceManager.Get<IDatabaseService>().ClearAllPools();

                    using (ZipArchive zipFile = ZipFile.Open(filePath, ZipArchiveMode.Create))
                    {
                        zipFile.CreateEntryFromFile(settings.SettingsFilePath, Path.GetFileName(settings.SettingsFilePath));
                        zipFile.CreateEntryFromFile(settings.DatabaseFilePath, Path.GetFileName(settings.DatabaseFilePath));
                    }
                    return;
                }
                else
                {
                    Logger.Log(LogLevel.Error, $"Directory does not exist for saving packaged backup: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            await DialogHelper.ShowMessage(MixItUp.Base.Resources.BackupGenerationFailed);
        }

        public async Task<Result<SettingsV3Model>> RestorePackagedBackup(string filePath)
        {
            try
            {
                string tempFilePath = ServiceManager.Get<IFileService>().GetTempFolder();
                string tempFolder = Path.GetDirectoryName(tempFilePath);

                string settingsFile = null;
                string databaseFile = null;

                ServiceManager.Get<IDatabaseService>().ClearAllPools();

                try
                {
                    using (ZipArchive zipFile = ZipFile.Open(filePath, ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry entry in zipFile.Entries)
                        {
                            string extractedFilePath = Path.Combine(tempFolder, entry.Name);
                            if (File.Exists(extractedFilePath))
                            {
                                File.Delete(extractedFilePath);
                            }

                            if (extractedFilePath.EndsWith(SettingsV3Model.SettingsFileExtension, StringComparison.InvariantCultureIgnoreCase))
                            {
                                settingsFile = extractedFilePath;
                            }
                            else if (extractedFilePath.EndsWith(SettingsV3Model.DatabaseFileExtension, StringComparison.InvariantCultureIgnoreCase))
                            {
                                databaseFile = extractedFilePath;
                            }
                        }
                        zipFile.ExtractToDirectory(tempFolder);
                    }
                }
                catch (Exception ex) { Logger.Log(ex); }

                int currentVersion = -1;
                if (!string.IsNullOrEmpty(settingsFile))
                {
                    currentVersion = await SettingsV3Upgrader.GetSettingsVersion(settingsFile);
                }

                if (currentVersion == -1)
                {
                    return new Result<SettingsV3Model>(MixItUp.Base.Resources.SettingsBackupNotValid);
                }

                if (currentVersion > SettingsV3Model.LatestVersion)
                {
                    return new Result<SettingsV3Model>(MixItUp.Base.Resources.SettingsBackupTooNew);
                }

                return new Result<SettingsV3Model>(await FileSerializerHelper.DeserializeFromFile<SettingsV3Model>(settingsFile, ignoreErrors: true));
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result<SettingsV3Model>(ex);
            }
        }

        public async Task<bool> PerformAutomaticBackupIfApplicable(SettingsV3Model settings)
        {
            if (settings.SettingsBackupRate != SettingsBackupRateEnum.None)
            {
                Logger.Log(LogLevel.Debug, "Checking whether to perform automatic backup");

                DateTimeOffset newResetDate = settings.SettingsLastBackup;

                if (settings.SettingsBackupRate == SettingsBackupRateEnum.Daily) { newResetDate = newResetDate.AddDays(1); }
                else if (settings.SettingsBackupRate == SettingsBackupRateEnum.Weekly) { newResetDate = newResetDate.AddDays(7); }
                else if (settings.SettingsBackupRate == SettingsBackupRateEnum.Monthly) { newResetDate = newResetDate.AddMonths(1); }

                if (newResetDate < DateTimeOffset.Now)
                {
                    string backupPath = Path.Combine(SettingsV3Model.SettingsDirectoryName, SettingsV3Model.DefaultAutomaticBackupSettingsDirectoryName);
                    if (!string.IsNullOrEmpty(settings.SettingsBackupLocation))
                    {
                        backupPath = settings.SettingsBackupLocation;
                    }

                    try
                    {
                        if (!Directory.Exists(backupPath))
                        {
                            Directory.CreateDirectory(backupPath);
                        }

                        string filePath = Path.Combine(backupPath, settings.Name + "-Backup-" + DateTimeOffset.Now.ToString("MM-dd-yyyy") + "." + SettingsV3Model.SettingsBackupFileExtension);

                        await this.SavePackagedBackup(settings, filePath);

                        settings.SettingsLastBackup = DateTimeOffset.Now;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, "Failed to create automatic backup directory");
                        Logger.Log(ex);
                        return false;
                    }
                }
            }
            return true;
        }

        private async Task<SettingsV3Model> LoadSettings(string filePath)
        {
            return await SettingsV3Upgrader.UpgradeSettingsToLatest(filePath);
        }
    }

    public static class SettingsV3Upgrader
    {
        public static async Task<SettingsV3Model> UpgradeSettingsToLatest(string filePath)
        {
            int currentVersion = await GetSettingsVersion(filePath);
            if (currentVersion < 0)
            {
                // Settings file is invalid, we can't use this
                return null;
            }
            else if (currentVersion > SettingsV3Model.LatestVersion)
            {
                // Future build, like a preview build, we can't load this
                return null;
            }
            else if (currentVersion < SettingsV3Model.LatestVersion)
            {
                await SettingsV3Upgrader.Version6Upgrade(currentVersion, filePath);
            }
            SettingsV3Model settings = await FileSerializerHelper.DeserializeFromFile<SettingsV3Model>(filePath, ignoreErrors: true);
            settings.Version = SettingsV3Model.LatestVersion;
            return settings;
        }

        public static async Task Version6Upgrade(int version, string filePath)
        {
            if (version < 6)
            {
                string fileData = await ServiceManager.Get<IFileService>().ReadFile(filePath);
                SettingsV3Model settings = await FileSerializerHelper.DeserializeFromFile<SettingsV3Model>(filePath, ignoreErrors: true);
                await settings.Initialize();

                if (settings.StreamingPlatformAuthentications.ContainsKey(StreamingPlatformTypeEnum.Twitch) && settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].UserOAuthToken != null)
                {
                    // Force OAuth token reset for new scopes
                    settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].UserOAuthToken.Reset();
                }

                await ServiceManager.Get<SettingsService>().Save(settings);
            }
        }

        public static void MultiPlatformCommandUpgrade(CommandModelBase command)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            if (command is BetGameCommandModel)
            {
                BetGameCommandModel gCommand = (BetGameCommandModel)command;
                gCommand.StarterUserRole = UserRoles.ConvertFromOldRole(gCommand.StarterRole);

                foreach (GameOutcomeModel outcome in gCommand.BetOptions)
                {
                    outcome.UpgradeProbabilitySettings();
                }
            }
            else if (command is BidGameCommandModel)
            {
                BidGameCommandModel gCommand = (BidGameCommandModel)command;
                gCommand.StarterUserRole = UserRoles.ConvertFromOldRole(gCommand.StarterRole);
            }
            else if (command is DuelGameCommandModel)
            {
                DuelGameCommandModel gCommand = (DuelGameCommandModel)command;
                gCommand.SuccessfulOutcome.UpgradeProbabilitySettings();
            }
            else if (command is HeistGameCommandModel)
            {
                HeistGameCommandModel gCommand = (HeistGameCommandModel)command;
                gCommand.UserSuccessOutcome.UpgradeProbabilitySettings();
            }
            else if (command is RouletteGameCommandModel)
            {
                RouletteGameCommandModel gCommand = (RouletteGameCommandModel)command;
                gCommand.UserSuccessOutcome.UpgradeProbabilitySettings();
            }
            else if (command is StealGameCommandModel)
            {
                StealGameCommandModel gCommand = (StealGameCommandModel)command;
                gCommand.SuccessfulOutcome.UpgradeProbabilitySettings();
            }
            else if (command is SpinGameCommandModel)
            {
                SpinGameCommandModel gCommand = (SpinGameCommandModel)command;
                foreach (GameOutcomeModel outcome in gCommand.Outcomes)
                {
                    outcome.UpgradeProbabilitySettings();
                }
            }
            else if (command is SlotMachineGameCommandModel)
            {
                SlotMachineGameCommandModel gCommand = (SlotMachineGameCommandModel)command;
                foreach (SlotMachineGameOutcomeModel outcome in gCommand.Outcomes)
                {
                    outcome.UpgradeProbabilitySettings();
                }
            }

            foreach (RequirementModelBase requirement in command.Requirements.Requirements)
            {
                if (requirement is RoleRequirementModel)
                {
                    RoleRequirementModel rRequirement = (RoleRequirementModel)requirement;
                    rRequirement.UpgradeFromOldRoles();
                }
            }

            foreach (ActionModelBase action in command.Actions)
            {
                if (action is ConsumablesActionModel)
                {
                    ConsumablesActionModel cAction = (ConsumablesActionModel)action;
                    cAction.UserRoleToApplyTo = UserRoles.ConvertFromOldRole(cAction.UsersToApplyTo);
                }
                else if (action is GameQueueActionModel)
                {
                    GameQueueActionModel gqAction = (GameQueueActionModel)action;
                    if (gqAction.RoleRequirement != null)
                    {
                        gqAction.RoleRequirement.UpgradeFromOldRoles();
                    }
                }
                else if (action is InputActionModel)
                {
                    InputActionModel iAction = (InputActionModel)action;
                    if (iAction.Key.HasValue)
                    {
                        iAction.VirtualKey = ServiceManager.Get<IInputService>().ConvertOldKeyEnum(iAction.Key.GetValueOrDefault());
                    }
                }
            }
#pragma warning restore CS0612 // Type or member is obsolete
        }

        public static async Task<int> GetSettingsVersion(string filePath)
        {
            string fileData = await ServiceManager.Get<IFileService>().ReadFile(filePath);
            if (string.IsNullOrEmpty(fileData))
            {
                return -1;
            }
            JObject settingsJObj = JObject.Parse(fileData);
            return (int)settingsJObj["Version"];
        }
    }
}
