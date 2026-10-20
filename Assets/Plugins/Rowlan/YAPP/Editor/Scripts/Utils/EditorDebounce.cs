using System;
using UnityEditor;

namespace Rowlan.Yapp
{
    /// <summary>
    /// Debounces an editor action.
    /// Calling Invoke() restarts the delay.
    /// The action executes only once after no calls occur for delaySeconds.
    /// 
    /// Use eg to invoke a MicroVerse update only after dragging / painting finished or the user didn't drag for the specified amount of time.
    /// 
    /// </summary>
    public class EditorDebounce : IDisposable
    {
        private readonly Action action;
        private readonly double delaySeconds;

        private double executeAt;
        private bool scheduled;
        private bool disposed;

        public EditorDebounce(double delaySeconds, Action action)
        {
            if (delaySeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));

            this.action = action ?? throw new ArgumentNullException(nameof(action));
            this.delaySeconds = delaySeconds;
        }

        /// <summary>
        /// Schedules the action. Repeated calls restart the delay.
        /// </summary>
        public void Invoke()
        {
            if (disposed)
                return;

            executeAt = EditorApplication.timeSinceStartup + delaySeconds;

            if (scheduled)
                return;

            scheduled = true;
            EditorApplication.update += Update;
        }

        /// <summary>
        /// Cancels any pending execution.
        /// </summary>
        public void Cancel()
        {
            if (!scheduled)
                return;

            EditorApplication.update -= Update;
            scheduled = false;
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup < executeAt)
                return;

            Cancel();
            action.Invoke();
        }

        /// <summary>
        /// Cleanup when the owner is destroyed.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            Cancel();
            disposed = true;
        }
    }
}