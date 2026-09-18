using System;
using System.Threading;
using System.Threading.Tasks;

namespace Start.Core.Interfaces
{
    public interface IPowerManagementService
    {
        /// <summary>
        /// Prevents Windows from going to sleep or suspending network adapters while downloads are active.
        /// </summary>
        void PreventSleep();

        /// <summary>
        /// Restores standard Windows power management once downloads complete or pause.
        /// </summary>
        void AllowSleep();

        /// <summary>
        /// True if sleep prevention is currently active.
        /// </summary>
        bool IsSleepPrevented { get; }

        /// <summary>
        /// Schedules a system shutdown with countdown and cancellation capability.
        /// </summary>
        Task<bool> RequestShutdownAsync(int countdownSeconds = 30, Action<int>? onCountdownTick = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Transitions system into sleep state.
        /// </summary>
        Task<bool> RequestSleepAsync();
    }
}
