using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Start.UI.ViewModels;

namespace Start.UI.Services
{
    public class InterceptedDownloadPayload
    {
        public string Url { get; set; } = string.Empty;
        public string? Cookies { get; set; }
        public string? Referer { get; set; }
        public string? UserAgent { get; set; }
        public string? Title { get; set; }
    }

    public sealed class NativePipeServerService : IDisposable
    {
        public const string PipeName = "UniversalMediaDownloaderPipe";
        private readonly MainViewModel _mainViewModel;
        private readonly ISystemTrayService _systemTrayService;
        private CancellationTokenSource? _cts;
        private Task? _serverTask;

        public NativePipeServerService(MainViewModel mainViewModel, ISystemTrayService systemTrayService)
        {
            _mainViewModel = mainViewModel;
            _systemTrayService = systemTrayService;
        }

        public void Start()
        {
            if (_serverTask != null) return;

            _cts = new CancellationTokenSource();
            _serverTask = Task.Run(() => RunServerAsync(_cts.Token));
        }

        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using var pipeServer = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipeServer.WaitForConnectionAsync(cancellationToken);

                    using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
                    using var writer = new StreamWriter(pipeServer, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                    string? line = await reader.ReadLineAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var payload = JsonSerializer.Deserialize<InterceptedDownloadPayload>(
                            line,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (payload != null && !string.IsNullOrWhiteSpace(payload.Url))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _systemTrayService.RestoreWindow();
                                _mainViewModel.SetInterceptedDownload(
                                    payload.Url,
                                    payload.Cookies,
                                    payload.Referer,
                                    payload.UserAgent,
                                    payload.Title);
                            });

                            await writer.WriteLineAsync("{\"status\":\"ok\"}");
                        }
                        else
                        {
                            await writer.WriteLineAsync("{\"status\":\"error\",\"message\":\"Empty URL\"}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Delay slightly on pipe error before re-creating server
                    try
                    {
                        await Task.Delay(500, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
