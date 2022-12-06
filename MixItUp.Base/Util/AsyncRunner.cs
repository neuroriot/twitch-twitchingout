﻿using StreamingClient.Base.Util;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Util
{
    public static class AsyncRunner
    {
        public static async Task RunAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, includeStackTrace: true);
            }
        }

        public static async Task<T> RunAsync<T>(Task<T> task)
        {
            try
            {
                await task;
                return task.Result;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, includeStackTrace: true);
            }
            return default(T);
        }

        public static async Task RunAsync(Func<Task> task)
        {
            try
            {
                await task();
            }
            catch (Exception ex)
            {
                Logger.Log(ex, includeStackTrace: true);
            }
        }

        public static Task<T> RunAsyncBackground<T>(Func<CancellationToken, T> action, CancellationToken token)
        {
            return Task.Run(() =>
            {
                try
                {
                    return action(token);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                return default(T);
            }, token);
        }

        public static Task RunAsyncBackground(Func<CancellationToken, Task> backgroundTask, CancellationToken token)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await backgroundTask(token);
                }
                catch (ThreadAbortException) { return; }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { Logger.Log(ex); }
            }, token);
        }

        public static Task RunAsyncBackground(Func<CancellationToken, Task> backgroundTask, CancellationToken token, int delayInMilliseconds)
        {
            return Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await backgroundTask(token);
                    }
                    catch (ThreadAbortException) { return; }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex) { Logger.Log(ex); }

                    try
                    {
                        if (delayInMilliseconds > 0 && !token.IsCancellationRequested)
                        {
                            await Task.Delay(delayInMilliseconds, token);
                        }
                    }
                    catch (ThreadAbortException) { return; }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex) { Logger.Log(ex); }
                }
            }, token);
        }
    }
}