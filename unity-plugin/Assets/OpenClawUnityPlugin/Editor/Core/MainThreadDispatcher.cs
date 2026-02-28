using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 将后台线程的操作调度到 Unity 主线程执行。
    /// HTTP/WebSocket 后台线程通过 Dispatch() 提交任务，阻塞等待主线程在
    /// EditorApplication.update 中消费并返回结果。
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<PendingTask> _queue = new ConcurrentQueue<PendingTask>();

        public static void Initialize()
        {
            EditorApplication.update += Tick;
        }

        public static void Shutdown()
        {
            EditorApplication.update -= Tick;

            // 清空队列，将所有等待中的任务标记为取消，防止后台线程永久阻塞
            while (_queue.TryDequeue(out var pending))
                pending.Tcs.TrySetCanceled();
        }

        /// <summary>
        /// 在主线程执行 action 并返回结果。后台线程调用此方法会阻塞，直到主线程执行完毕。
        /// </summary>
        public static T Dispatch<T>(Func<T> action)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(new PendingTask(() => action(), tcs));

            try
            {
                return (T)tcs.Task.GetAwaiter().GetResult();
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException("[OpenClaw] MainThreadDispatcher was shut down while waiting.");
            }
        }

        /// <summary>无返回值版本</summary>
        public static void Dispatch(Action action)
        {
            Dispatch<object>(() => { action(); return null; });
        }

        // 在 EditorApplication.update（主线程）中执行队列里的任务
        private static void Tick()
        {
            // 每帧最多处理 20 个任务，防止单帧卡顿
            int budget = 20;
            while (budget-- > 0 && _queue.TryDequeue(out var pending))
            {
                try
                {
                    var result = pending.Action();
                    pending.Tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    pending.Tcs.TrySetException(ex);
                }
            }
        }

        private class PendingTask
        {
            public Func<object>                    Action { get; }
            public TaskCompletionSource<object>    Tcs    { get; }

            public PendingTask(Func<object> action, TaskCompletionSource<object> tcs)
            {
                Action = action;
                Tcs    = tcs;
            }
        }
    }
}
