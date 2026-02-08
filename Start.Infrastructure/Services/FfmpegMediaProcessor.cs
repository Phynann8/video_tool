using System;
using System.Threading;
using System.Threading.Tasks;
using Start.Core.Interfaces;
using Start.Infrastructure.Helpers;

namespace Start.Infrastructure.Services
{
    public class FfmpegMediaProcessor : IMediaProcessor
    {
        private const string FfmpegExecutable = "ffmpeg.exe";

        public async Task<string> MergeMediaAsync(string videoPath, string audioPath, string outputPath, CancellationToken cancellationToken)
        {
            // ffmpeg -i video -i audio -c:v copy -c:a aac -strict experimental output.mp4
            // Using copy for video to be fast, aac for audio (or copy if compatible)
            
            var arguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac \"{outputPath}\" -y"; 
            // -y to overwrite if exists

            await ProcessRunner.RunProcessAsync(FfmpegExecutable, arguments, cancellationToken);
            
            return outputPath;
        }
    }
}
