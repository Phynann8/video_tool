using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;

namespace Start.UI.Services
{
    public sealed class ClipboardUrlMonitor : IDisposable
    {
        private const int WmClipboardUpdate = 0x031D;
        private static readonly Regex HttpUrlPattern = new(
            @"^https?://[^\s<>""']+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private HwndSource? _source;
        private bool _isRegistered;
        private string _lastUrl = string.Empty;

        public bool IsEnabled { get; private set; }

        public event EventHandler<string>? UrlDetected;

        public void Attach(Window window)
        {
            if (_source != null)
                return;

            var handle = new WindowInteropHelper(window).Handle;
            _source = HwndSource.FromHwnd(handle);
            _source?.AddHook(WindowMessageFilter);
            _isRegistered = _source != null && AddClipboardFormatListener(handle);
            IsEnabled = _isRegistered;
        }

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled && _isRegistered;
        }

        private IntPtr WindowMessageFilter(
            IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WmClipboardUpdate && IsEnabled)
                CheckClipboard();

            return IntPtr.Zero;
        }

        private void CheckClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText())
                    return;

                var text = Clipboard.GetText().Trim();
                if (!HttpUrlPattern.IsMatch(text) || string.Equals(text, _lastUrl, StringComparison.Ordinal))
                    return;

                _lastUrl = text;
                UrlDetected?.Invoke(this, text);
            }
            catch (ExternalException)
            {
                // Another process may own the clipboard briefly.
            }
            catch (InvalidOperationException)
            {
                // Clipboard access can fail while the WPF dispatcher is shutting down.
            }
        }

        public void Dispose()
        {
            if (_source == null)
                return;

            var handle = _source.Handle;
            if (_isRegistered)
                RemoveClipboardFormatListener(handle);

            _source.RemoveHook(WindowMessageFilter);
            _source = null;
            _isRegistered = false;
            IsEnabled = false;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    }
}
