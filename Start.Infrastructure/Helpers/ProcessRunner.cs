using System.Diagnostics;
using System.Text;

namespace Start.Infrastructure.Helpers
{
    public static class ProcessRunner
    {
        public static async Task<string> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken, Action<string>? onOutput = null)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = processStartInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null) 
                {
                    outputBuilder.AppendLine(e.Data);
                    onOutput?.Invoke(e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException($"Failed to start process: {fileName}");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Process exited with code {process.ExitCode}. Error: {errorBuilder}");
                }

                return outputBuilder.ToString();
            }
            catch (Exception ex)
            {
                 // Re-throw with more context if needed, or log
                 throw new Exception($"Error running {fileName} {arguments}: {ex.Message}", ex);
            }
        }
    }
}
