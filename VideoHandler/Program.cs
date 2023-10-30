using System;
using System.Diagnostics;
using System.Drawing;

namespace VideoHandler
{
    internal class Program
    {
        
        static void Main()
        {
            string imagePath = "Files/背景.jpg";
            string videoPath = $"{DateTime.Now:yyyyMMddHHmmss}.mp4";
            string videoPathWithText = $"{DateTime.Now:yyyyMMddHHmmss}_text.mp4";
            string audioPath = "Files/夏目bgm.ogg";

            // 使用 FFmpeg 生成视频
            //GenerateVideo(imagePath, audioPath, videoPath);

            // 在视频的第5、10和15秒插入文字
            AddTextToVideo("20231027164150.mp4", videoPathWithText);
        }

        static void GenerateVideo(string imagePath, string audioPath, string videoPath)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "D:\\soft\\ffmpeg\\bin\\ffmpeg",
                Arguments = $"-loop 1 -i {imagePath} -i {audioPath} -c:v libx264 -t 20 -pix_fmt yuv420p -shortest {videoPath}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                process.WaitForExit();
            }
        }

        static void AddTextToVideo(string videoPath, string outputVideoPath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "D:\\soft\\ffmpeg\\bin\\ffmpeg",
                    Arguments = $"-i {videoPath} -vf \"" +
                $" drawtext=fontfile=Files/STSONG.TTF:text='庄姝璇这是我机器动态生成的视频':x=(w-text_w)/2:y=(h-text_h)/2:fontsize=50:fontcolor=black:enable='between(t,1,5)'," +
                $" drawtext=fontfile=Files/STSONG.TTF:text='文案，字体，背景，音乐，排版可以动态替换':x=(w-text_w)/2:y=(h-text_h)/2:fontsize=50:fontcolor=black:enable='between(t,5,9)' ," +
                $" drawtext=fontfile=Files/STSONG.TTF:text='你强哥我厉不厉害':x=(w-text_w)/2:y=(h-text_h)/2:fontsize=50:fontcolor=black:enable='between(t,9,14)'\" {outputVideoPath}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = false,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            process.Start();

            string processOutput = null;
            while ((processOutput = process.StandardError.ReadLine()) != null)
            {
                // do something with processOutput
               Console.WriteLine(processOutput);
            }
        }
    }
}