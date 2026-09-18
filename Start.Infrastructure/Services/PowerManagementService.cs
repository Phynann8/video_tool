using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Start.Core.Interfaces;

namespace Start.Infrastructure.Services
{
    public sealed class PowerManagementService : IPowerManagementService
    {
        [Flags]
        private enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [DllImport("PowrProf.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        private readonly object _syncLock = new();
        private int _preventCount = 0;

        public bool IsSleepPrevented => _preventCount > 0;

        public void PreventSleep()
        {
            lock (_syncLock)
            {
                _preventCount++;
                if (_preventCount == 1 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetThreadExecutionState(
                        EXECUTION_STATE.ES_CONTINUOUS |
                        EXECUTION_STATE.ES_SYSTEM_REQUIRED |
                        EXECUTION_STATE.ES_AWAYMODE_REQUIRED);
                }
            }
        }

        public void AllowSleep()
        {
            lock (_syncLock)
            {
                if (_preventCount > 0)
                {
                    _preventCount--;
                }

                if (_preventCount == 0 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
                }
            }
        }

        public async Task<bool> RequestShutdownAsync(
            int countdownSeconds = 30,
            Action<int>? onCountdownTick = null,
            CancellationToken cancellationToken = default)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            try
            {
                for (int remaining = countdownSeconds; remaining > 0; remaining--)
                {
                    onCountdownTick?.Invoke(remaining);
                    await Task.Delay(1000, cancellationToken);
                }

                var psi = new ProcessStartInfo("shutdown.exe", "/s /t 0 /c \"Downloads finished.\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public Task<bool> RequestSleepAsync()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Task.FromResult(false);

            try
            {
                bool success = SetSuspendState(false, true, true);
                return Task.FromResult(success);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}
