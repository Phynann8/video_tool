using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace UniversalMediaDownloader.NativeHost
{
    internal class Program
    {
        private const string PipeName = "UniversalMediaDownloaderPipe";

        static async Task Main(string[] args)
        {
            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();

            while (true)
            {
                byte[] lengthBuffer = new byte[4];
                int bytesRead = await ReadExactAsync(stdin, lengthBuffer, 0, 4);
                if (bytesRead < 4)
                {
                    // Stdin closed by browser
                    break;
                }

                int messageLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
                if (messageLength <= 0 || messageLength > 1024 * 1024)
                {
                    await SendResponseAsync(stdout, new { status = "error", message = "Invalid message length" });
                    continue;
                }

                byte[] messageBuffer = new byte[messageLength];
                int bodyRead = await ReadExactAsync(stdin, messageBuffer, 0, messageLength);
                if (bodyRead < messageLength)
                {
                    break;
                }

                string messageText = Encoding.UTF8.GetString(messageBuffer);

                try
                {
                    bool forwarded = await ForwardToAppAsync(messageText);
                    if (forwarded)
                    {
                        await SendResponseAsync(stdout, new { status = "ok", message = "Forwarded to Universal Media Downloader" });
                    }
                    else
                    {
                        await SendResponseAsync(stdout, new { status = "error", message = "Could not reach Universal Media Downloader app" });
                    }
                }
                catch (Exception ex)
                {
                    await SendResponseAsync(stdout, new { status = "error", message = ex.Message });
                }
            }
        }

        private static async Task<bool> ForwardToAppAsync(string jsonPayload)
        {
            // Attempt connecting to running named pipe
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await pipeClient.ConnectAsync(attempt == 0 ? 500 : 3000);

                    using var writer = new StreamWriter(pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                    using var reader = new StreamReader(pipeClient, Encoding.UTF8, leaveOpen: true);

                    await writer.WriteLineAsync(jsonPayload);
                    string? ack = await reader.ReadLineAsync();
                    return ack?.Contains("\"status\":\"ok\"") == true || ack?.Contains("\"status\": \"ok\"") == true;
                }
                catch (TimeoutException)
                {
                    if (attempt == 0)
                    {
                        // App may not be running. Try to locate and launch it.
                        LaunchDesktopApp();
                        await Task.Delay(1000);
                    }
                }
                catch (FileNotFoundException)
                {
                    if (attempt == 0)
                    {
                        LaunchDesktopApp();
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Pipe error: {ex.Message}");
                    break;
                }
            }

            return false;
        }

        private static void LaunchDesktopApp()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                // Check relative paths where Start.UI.exe might reside
                string[] candidates =
                {
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Start.UI", "bin", "Debug", "net9.0-windows", "Start.UI.exe")),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "out_publish", "Start.UI.exe")),
                    Path.GetFullPath(Path.Combine(baseDir, "Start.UI.exe"))
                };

                foreach (var exe in candidates)
                {
                    if (File.Exists(exe))
                    {
                        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to launch app: {ex.Message}");
            }
        }

        private static async Task SendResponseAsync(Stream stdout, object responseObject)
        {
            string json = JsonSerializer.Serialize(responseObject);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(json);
            byte[] lengthBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, bodyBytes.Length);

            await stdout.WriteAsync(lengthBytes);
            await stdout.WriteAsync(bodyBytes);
            await stdout.FlushAsync();
        }

        private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead));
                if (read == 0) break;
                totalRead += read;
            }
            return totalRead;
        }
    }
}
