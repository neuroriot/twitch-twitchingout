﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixItUp.Base.Util
{
    public interface IDialogShower
    {
        Task ShowMessage(string message);

        Task<bool> ShowConfirmation(string message);

        Task<string> ShowTextEntry(string message, string defaultValue = null);

        Task<object> ShowCustom(object dialog);

        Task<object> ShowCustomTimed(object dialog, int timeout);

        void CloseCurrent();
    }

    public static class DialogHelper
    {
        public static IDialogShower dialogShower;

        public static void Initialize(IDialogShower dialogShower)
        {
            DialogHelper.dialogShower = dialogShower;
        }

        public static async Task ShowMessage(string message) { await DialogHelper.dialogShower.ShowMessage(message); }

        public static async Task<bool> ShowConfirmation(string message) { return await DialogHelper.dialogShower.ShowConfirmation(message); }

        public static async Task<string> ShowTextEntry(string message, string defaultValue = null) { return await DialogHelper.dialogShower.ShowTextEntry(message, defaultValue); }

        public static async Task<object> ShowCustom(object dialog) { return await DialogHelper.dialogShower.ShowCustom(dialog); }

        public static async Task<object> ShowCustomTimed(object dialog, int timeout) { return await DialogHelper.dialogShower.ShowCustomTimed(dialog, timeout); }

        public static async Task ShowFailedResults(IEnumerable<Result> results)
        {
            if (results.Any(r => !r.Success))
            {
                StringBuilder error = new StringBuilder();
                error.AppendLine(MixItUp.Base.Resources.TheFollowingErrorsMustBeFixed);
                error.AppendLine();
                foreach (Result result in results.Where(r => !r.Success))
                {
                    error.AppendLine(" - " + result.Message);
                }
                await DialogHelper.ShowMessage(error.ToString());
            }
        }

        public static void CloseCurrent() { DialogHelper.dialogShower.CloseCurrent(); }
    }
}
