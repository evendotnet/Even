using Akka.Actor;
using Akka.Dispatch;
using Akka.Dispatch.SysMsg;
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Even
{
    internal static class AkkaAsyncHelper
    {
        /// <summary>
        /// Causes the actor to await until the task is completed to process new messages.
        /// If the task is completed, works synchronously without any overhead
        /// </summary>
        public static void Await(Func<Task> func)
        {
            var task = func();

            // if task is null, treat as synchronous execution
            if (task == null)
                return;

            if (task.IsFaulted)
                ExceptionDispatchInfo.Capture(task.Exception.InnerException).Throw();

            // if task is completed, return synchronously
            if (task.IsCompleted)
                return;

            // dispatch to actor scheduler only if needed
            ActorTaskScheduler.RunTask(() => task);
        }
    }
}
