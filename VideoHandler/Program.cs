using Dapper;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Diagnostics;
using System.Drawing;
using VideoHandler.DbModel;
using static System.Net.Mime.MediaTypeNames;

namespace VideoHandler
{
    public class Program
    {
        public static AppSettings appSettings = null;

        static async Task Main()
        {
            var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
            appSettings = config.GetSection("AppSettings").Get<AppSettings>();
            if (appSettings is null)
            {
                Console.WriteLine("配置错误");
                Console.ReadKey();
                return;
            }
            var outputDirPath = "Output";
            if (!Directory.Exists(outputDirPath))
            {
                Directory.CreateDirectory(outputDirPath);
            }
            Console.WriteLine("开始构造");
            using var connection = new MySqlConnection(appSettings.MysqlConnectionString);
            var lastModel = await connection.QueryFirstOrDefaultAsync<VideoResult>("select id,orgin_id orginid,words_id wordsid,bgm_id bgnid,path  from video_result WHERE id = (SELECT MAX(id) FROM video_result)");
            var minId = lastModel?.OrginId;
            var minWordsId = lastModel?.WordsId;
            var videoOrgin = await connection.QueryFirstOrDefaultAsync<VideoOrgin>($"select id,orientate_type OrientateType,name,path,emotion_type emotionType  from video_orgin WHERE id >{minId ?? 0}");
            var bgm = await connection.QueryFirstOrDefaultAsync<VideoBgm>($"select id,emotion_type emotionType,name,path  from video_bgm WHERE emotion_type={videoOrgin.EmotionType} and id!={lastModel?.BgmId ?? 0} LIMIT 1");
            //var videoWords = await connection.QueryAsync<VideoWords>($"select id,content  from video_words WHERE id >{minWordsId ?? 0} and emotion_type={videoOrgin.EmotionType} limit {appSettings.WordsCount}");
            //if (!string.IsNullOrWhiteSpace(bgm?.Path) && !string.IsNullOrWhiteSpace(videoOrgin?.Path) && videoWords != null && videoWords.Count() == appSettings.WordsCount)
            //{
            //    AddTextToVideo(bgm.Path, videoOrgin.Path, $"{outputDirPath}\\{DateTime.Now:yyyyMMddHHmmss}.mp4", videoWords.Select(s => s.Content).ToList());
            //}
            var videoWords = await connection.QueryFirstOrDefaultAsync<VideoWords>($"select id,content  from video_words WHERE id >{minWordsId ?? 0} and emotion_type={videoOrgin.EmotionType} limit 1");
            if (!string.IsNullOrWhiteSpace(bgm?.Path) && !string.IsNullOrWhiteSpace(videoOrgin?.Path) && videoWords != null)
            {
                var isHadVoice = CheckVideoAudioStream(videoOrgin.Path);
                var outputVideoPath = $"{outputDirPath}/{DateTime.Now:yyyyMMddHHmmss}.mp4";
                AddTextToVideo(bgm.Path, videoOrgin.Path, outputVideoPath, videoWords.Content, isHadVoice);
                await connection.ExecuteAsync($"insert into video_result(orgin_id,words_id,bgm_id,path,add_time) values({videoOrgin.Id},{videoWords.Id},{bgm.Id},'{outputVideoPath}',now(3))");
            }
        }

        static void AddTextToVideo(string audioPath, string videoPath, string outputVideoPath, string words, bool isHadVoice = false)
        {
            //循环：Set number of times input stream shall be looped. Loop 0 means no loop, loop -1 means infinite loop
            var loopSet = $"-stream_loop -1 ";
            //输入音频
            var inputSet = $" -i {appSettings.ResourcesPath}/{videoPath} -i {appSettings.ResourcesPath}/{audioPath} ";
            //过滤掉视频原声 插入音乐作为bgm
            var bgmSet = isHadVoice ? $" -filter_complex \"[0:a]anull[a];[1:a]aformat=fltp:44100:stereo[background];[a][background]amix=inputs=2:duration=first:dropout_transition=3[outa]\"  -map 0:v -map \"[outa]\" " : string.Empty;
            //插入文案 文案的时间分配， 文案标点符号自动换行
            var videoFilterSet = "  -vf \"";

            char[] delimiters = { '；', '。', ';', '.' };
            var drawTextList = new List<string>();

            string[] parts = words.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            var timeGap = (appSettings.VideoTime / parts.Count()) - 1;
            timeGap = timeGap > 6 ? 6 : timeGap;
            for (int j = 0; j < parts.Count(); j++)
            {
                var start = j + 1 + (timeGap * j);
                var end = start + timeGap;
                Console.WriteLine($"start:{start}  end:{end}");
                drawTextList.Add($" drawtext=fontfile={appSettings.FontType}:text='{parts[j]}':x=(w-text_w)/2:y=(h-text_h)/2:fontsize={appSettings.FontSize}:fontcolor={appSettings.FontColor}:enable='between(t,{start},{end})' ");
            }

            videoFilterSet += string.Join(", ", drawTextList) + "\"";
            var timeSet = $" -t {appSettings.VideoTime} ";
            var arg = loopSet + inputSet + bgmSet + videoFilterSet + timeSet + outputVideoPath;
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "D:\\soft\\ffmpeg\\bin\\ffmpeg",
                    Arguments = arg,
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

        static bool CheckVideoAudioStream(string videoPath)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i {appSettings.ResourcesPath}/{videoPath}",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                using (var reader = process.StandardError)
                {
                    string result = reader.ReadToEnd();
                    Console.WriteLine(result);
                    return result.Contains("Audio:");
                }
            }
        }




        static void AddTextToVideo(string audioPath, string videoPath, string outputVideoPath, List<string> words)
        {
            //循环：Set number of times input stream shall be looped. Loop 0 means no loop, loop -1 means infinite loop
            var loopSet = $"-stream_loop -1 ";
            //输入音频
            var inputSet = $" -i {appSettings.ResourcesPath}/{videoPath} -i {appSettings.ResourcesPath}/{audioPath} ";
            //过滤掉视频原声 插入音乐作为bgm
            var bgmSet = $" -filter_complex \"[0:a]anull[a];[1:a]aformat=fltp:44100:stereo[background];[a][background]amix=inputs=2:duration=first:dropout_transition=3[outa]\"  -map 0:v -map \"[outa]\" ";
            //插入文案 文案的时间分配， 文案标点符号自动换行
            var videoFilterSet = "  -vf \"";
            var timeGap = (appSettings.VideoTime / words.Count) - 1;
            char[] delimiters = { '，', '；', '。', ';', ',', '.' };
            var drawTextList = new List<string>();
            for (int i = 0; i < words.Count; i++)
            {
                var start = i + 1 + (timeGap * i);
                var end = start + timeGap;
                Console.WriteLine($"start:{start}  end:{end}");
                string[] parts = words[i].Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                var baseHeight = parts.Count() == 1 ? -0 : -60;
                for (int j = 0; j < parts.Count(); j++)
                {
                    drawTextList.Add($" drawtext=fontfile={appSettings.FontType}:text='{parts[j]}':x=(w-text_w)/2:y=((h-text_h)/2)+({baseHeight}):fontsize={appSettings.FontSize}:fontcolor={appSettings.FontColor}:enable='between(t,{start},{end})' ");
                    baseHeight += 60;
                }
            }
            videoFilterSet += string.Join(", ", drawTextList) + "\"";
            var timeSet = $" -t {appSettings.VideoTime} ";
            var arg = loopSet + inputSet + bgmSet + videoFilterSet + timeSet + outputVideoPath;
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "D:\\soft\\ffmpeg\\bin\\ffmpeg",
                    Arguments = arg,
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



























        /// <summary>
        /// Demo
        /// </summary>
        /// <param name="audioPath"></param>
        /// <param name="videoPath"></param>
        /// <param name="outputVideoPath"></param>
        static void AddTextToVideo(string audioPath, string videoPath, string outputVideoPath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "D:\\soft\\ffmpeg\\bin\\ffmpeg",
                    Arguments = $"-stream_loop -1 -i {videoPath} -i {audioPath} -filter_complex \"[0:a]anull[a];[1:a]aformat=fltp:44100:stereo[background];[a][background]amix=inputs=2:duration=first:dropout_transition=3[outa]\"  -map 0:v -map \"[outa]\" " +
                    $" -vf \"" +
                    $" drawtext=fontfile=Files/STKAITI.TTF:text='没有真正快乐的人，只有比较想的开的人':x=(w-text_w)/2:y=(h-text_h)/2:fontsize=50:fontcolor=white:enable='between(t,1,6)' ," +
                    $" drawtext=fontfile=Files/STKAITI.TTF:text='逃不过暴风雨，那就在雨中舞蹈':x=(w-text_w)/2:y=(h-text_h)/2:fontsize=50:fontcolor=white:enable='between(t,7,13)'," +
                    $" drawtext=fontfile=Files/STKAITI.TTF:text='庄姝璇最美,帮我搜集一点文案吧':x=(w-text_w)/2:y=(h-text_h)/2:fontsize=50:fontcolor=white:enable='between(t,13,15)'\"" +
                    $" -t 15 {outputVideoPath}",
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