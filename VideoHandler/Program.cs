using Dapper;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using PixabaySharp;
using PixabaySharp.Models;
using PixabaySharp.Utility;
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using VideoHandler.DbModel;
using VideoHandler.Repositories;
using static System.Net.Mime.MediaTypeNames;

namespace VideoHandler
{
    public class Program
    {
        public static AppSettings appSettings = null;
        // Handle the SpeechRecognized event.  
        static void recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            Console.WriteLine("Recognized text: " + e.Result.Text);
        }
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
            if (appSettings.IsBackground)
            {
                Console.WriteLine($"WebType:{appSettings.WebType} tag:{appSettings.SearchTag} 开始执行");
                if (string.IsNullOrWhiteSpace(appSettings.SearchTag))
                {
                    Console.WriteLine("请配置tag");
                    return;
                }
                await BackgroundHandler();
            }
            var outputDirPath = "Output";
            if (!Directory.Exists(outputDirPath))
            {
                Directory.CreateDirectory(outputDirPath);
            }
            Console.WriteLine("开始构造");
            var repostitory = new VideoRepository(appSettings.MysqlConnectionString);
            while (true)
            {
                try
                {
                    var lastModel = await repostitory.LastVideoResultGet();
                    var minId = lastModel?.OrginId;
                    var minWordsId = lastModel?.WordsId;

                    var toadyVideoWords = await repostitory.TodayVideoWordsGet();
                    if (toadyVideoWords.Count <= 0)
                    {
                        await repostitory.AddTodayVideoWords(appSettings.BgmIdList??new List<int> { 0,0,0});
                        await Task.Delay(3000);
                        continue;
                    }

                    var videoWords = await repostitory.LastVideoWordsGet(minWordsId ?? 0);
                    if (videoWords == null)
                    {
                        Console.WriteLine("无可用文案");
                        await Task.Delay(10 * 60 * 1000);
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(videoWords.Content))
                    {
                        videoWords.EmotionType = (int)EnumEmotion.忧郁;
                    }
                    var tag = appSettings.SearchTag;
                    var videoOrgin = await repostitory.LastVideoOrginGet(tag, minId ?? 0);
                    if (videoOrgin == null)
                    {
                        Console.WriteLine("无可用原始视频,准备开始抓取视频");
                        if (!await DownLoad(repostitory, tag, appSettings.ConcatCount, new List<int>(), new List<VideoPool>()))
                        {
                            Console.WriteLine($"videopool 资源池视频不够了 {appSettings.SearchTag} WebType:{appSettings.WebType}");
                            if (!await BackgroundHandler())
                            {
                                await Task.Delay(10 * 60 * 1000);
                            }
                        }
                        continue;
                    }
                    var bgm = videoWords.BgmId > 0? await repostitory.VideoBgmGet(videoWords.BgmId):
                        await repostitory.RandVideoBgmGet(videoWords.EmotionType, lastModel?.BgmId ?? 0);
                    if (!string.IsNullOrWhiteSpace(bgm?.Path) && !string.IsNullOrWhiteSpace(videoOrgin?.Path) && videoWords != null)
                    {
                        Console.WriteLine("开始检查视频音频");
                        var isHadVoice = CheckVideoAudioStream(videoOrgin.Path);
                        var outputVideoPath = $"{outputDirPath}/{DateTime.Now:yyyyMMddHHmmss}_{videoOrgin.Id}.mp4";
                        Console.WriteLine($"开始添加文本,如果没有文本那么只添加bgm");
                        if (!string.IsNullOrWhiteSpace(videoWords.Content))
                        {
                            if (appSettings.WordSameTime)
                            {
                                AddTextToVideoSameTime(bgm.Path, videoOrgin.Path, outputVideoPath, videoWords.Content, isHadVoice);
                            }
                            else
                            {
                                AddTextToVideo(bgm.Path, videoOrgin.Path, outputVideoPath, videoWords.Content, isHadVoice);
                            }
                        }
                        else
                        {
                            AddTextToVideoWithoutWords(bgm.Path, videoOrgin.Path, outputVideoPath, videoOrgin.Duration, isHadVoice);
                        }

                        await repostitory.VideoResultSave(new DbModel.VideoResult { OrginId = videoOrgin.Id, WordsId = videoWords.Id, BgmId = bgm.Id, Path = outputVideoPath });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    await Task.Delay(10 * 60 * 1000);
                    continue;
                }

            }

        }
        static async Task<bool> DownLoad(VideoRepository repostitory, string tag, int count, List<int> orginVideoIds, List<VideoPool> alreadyDownloadVideoPoolIds)
        {
            var currentCount = count - orginVideoIds.Count;
            var videopoolIds = alreadyDownloadVideoPoolIds.Select(s => s.Id).ToList();
            var videopoolList = await repostitory.VideoPoolGet(tag, currentCount, videopoolIds, 10, 40, appSettings.WebType == EnumWebType.pexels);
            if (videopoolList.Count == currentCount)
            {
                foreach (var item in videopoolList)
                {
                    if (await PexelsSpider.DownLoad(item, appSettings.ResourcesPath))
                    {
                        var id = await repostitory.AfterDownload(item);
                        orginVideoIds.Add(id);
                        alreadyDownloadVideoPoolIds.Add(item);
                        Console.WriteLine($"{id} 下载成功");
                    }
                    else
                    {
                        return await DownLoad(repostitory, tag, count, orginVideoIds, alreadyDownloadVideoPoolIds);
                    }
                }
                if (appSettings.DownloadConcat)
                {
                    var localPath = $"{appSettings.ResourcesPath}/concat/{DateTime.Now:yyyyMMddHHmmss}_{tag.Replace(" ", "_")}.mp4";
                    var videoPathList = await repostitory.ConcatLoaclPath(orginVideoIds);
                    CancatVideoFilesWithEncode(videoPathList, localPath);
                    var duration = alreadyDownloadVideoPoolIds.Sum(s => s.Duration);
                    await repostitory.AfterConcat(orginVideoIds, 1, $"{tag.Replace(" ", "_")}_{string.Join('_', videopoolList.Select(s => s.VideoId))}", localPath, tag, duration);
                }
                return true;
            }
            return false;
        }

        static void AddTextToVideoWithoutWords(string audioPath, string videoPath, string outputVideoPath, int time, bool isHadVoice = false)
        {
            //循环：Set number of times input stream shall be looped. Loop 0 means no loop, loop -1 means infinite loop
            var loopSet = $"-stream_loop -1 ";
            //输入音频
            var inputSet = $" -i {videoPath} -i {audioPath} ";
            //过滤掉视频原声 插入音乐作为bgm
            var bgmSet = isHadVoice ? $" -filter_complex \"[0:a]anull[a];[1:a]aformat=fltp:44100:stereo[background];[a][background]amix=inputs=2:duration=first:dropout_transition=3[outa]\"  -map 0:v -map \"[outa]\" " : string.Empty;

            var timeSet = $" -t {time} ";
            var arg = loopSet + inputSet + bgmSet + timeSet + outputVideoPath;
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
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


        static void AddTextToVideoSameTime(string audioPath, string videoPath, string outputVideoPath, string words, bool isHadVoice = false)
        {
            //循环：Set number of times input stream shall be looped. Loop 0 means no loop, loop -1 means infinite loop
            var loopSet = $"-stream_loop -1 ";
            //输入音频
            var inputSet = $" -i {videoPath} -i {audioPath} ";
            //过滤掉视频原声 插入音乐作为bgm
            var bgmSet = isHadVoice ? $" -filter_complex \"[0:a]anull[a];[1:a]aformat=fltp:44100:stereo[background];[a][background]amix=inputs=2:duration=first:dropout_transition=3[outa]\"  -map 0:v -map \"[outa]\" " : string.Empty;
            //插入文案 文案的时间分配， 文案标点符号自动换行
            var videoFilterSet = "  -vf \"";

            char[] delimiters = { '；', '。', ';', '.', ',', '，' };
            var drawTextList = new List<string>();
            words = $"「{words}」";//『』
            string[] parts = words.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            var start = 0;
            var end = 10;
            var baseHeight = parts.Count() == 1 ? -0 : -appSettings.FontSize;
            for (int j = 0; j < parts.Count(); j++)
            {
                drawTextList.Add($" drawtext=fontfile={appSettings.FontType}:text='{parts[j]}':x=(w-text_w)/2:y=((h-text_h)/2)+({baseHeight}):fontsize={appSettings.FontSize}:fontcolor={appSettings.FontColor}:enable='between(t,{start},{end})' ");
                baseHeight += appSettings.FontSize;
            }

            videoFilterSet += string.Join(", ", drawTextList) + "\"";
            var timeSet = $" -t {appSettings.VideoTime} ";
            var arg = loopSet + inputSet + bgmSet + videoFilterSet + timeSet + outputVideoPath;
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
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

        static void AddTextToVideo(string audioPath, string videoPath, string outputVideoPath, string words, bool isHadVoice = false)
        {
            //循环：Set number of times input stream shall be looped. Loop 0 means no loop, loop -1 means infinite loop
            var loopSet = $"-stream_loop -1 ";
            //输入音频
            var inputSet = $" -i {videoPath} -i {audioPath} ";
            //过滤掉视频原声 插入音乐作为bgm
            var bgmSet = isHadVoice ? $" -filter_complex \"[0:a]anull[a];[1:a]aformat=fltp:44100:stereo[background];[a][background]amix=inputs=2:duration=first:dropout_transition=3[outa]\"  -map 0:v -map \"[outa]\" " : string.Empty;
            //插入文案 文案的时间分配， 文案标点符号自动换行
            var videoFilterSet = "  -vf \"";

            char[] delimiters = { '；', '。', ';', '.' };
            var drawTextList = new List<string>();
            string[] parts = words.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            var timeGap = (appSettings.VideoTime / parts.Count()) - 1;
            //timeGap = timeGap > 6 ? 6 : timeGap;
            for (int j = 0; j < parts.Count(); j++)
            {
                var start = j + 1 + (timeGap * j);
                var end = start + timeGap;
                Console.WriteLine($"start:{start}  end:{end}");
                char[] delimitersSameTime = { ',', '，' };
                parts[j] = $"「{parts[j]}」";
                var partSameTimeList = parts[j].Split(delimitersSameTime, StringSplitOptions.RemoveEmptyEntries);
                if (partSameTimeList.Count() > 1)
                {
                    var baseHeight = partSameTimeList.Count() == 1 ? -0 : -appSettings.FontSize;
                    foreach (var item in partSameTimeList)
                    {
                        var temp = new string(item);
                        drawTextList.Add($" drawtext=fontfile={appSettings.FontType}:text='{temp}':x=(w-text_w)/2:y=((h-text_h)/2)+({baseHeight}):fontsize={appSettings.FontSize}:fontcolor={appSettings.FontColor}:enable='between(t,{start},{end})' ");
                        baseHeight += appSettings.FontSize;
                    }
                }
                else
                {
                    drawTextList.Add($" drawtext=fontfile={appSettings.FontType}:text='{parts[j]}':x=(w-text_w)/2:y=(h-text_h)/2:fontsize={appSettings.FontSize}:fontcolor={appSettings.FontColor}:enable='between(t,{start},{end})' ");
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

        static bool CheckVideoAudioStream(string videoPath)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i {videoPath}",
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

        static void TextToVoice()
        {
            // 实例化 SpeechSynthesizer.  
            SpeechSynthesizer synth = new SpeechSynthesizer();
            // 配置音频输出.   
            //synth.SetOutputToDefaultAudioDevice();
            synth.SetOutputToWaveFile("1.mp3");
            synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult);
            // 字符串转语言.  
            synth.Speak("有时候，坚持了你最不想干的事情之后，便可得到你最想要的东西");

        }

        static void CancatVoiceFiles(string dirPath, string outputPath)
        {
            string[] files = Directory.GetFiles(dirPath, "*.mp3", SearchOption.AllDirectories);
            var listFilePath = $"list{DateTime.Now:yyyyMMddhhmmss}.txt";
            using (StreamWriter writer = new StreamWriter(listFilePath))
            {
                foreach (string file in files)
                {
                    writer.WriteLine($"file '{file}'");
                }
            }
            var arg = $"-f concat -safe 0 -i {listFilePath} -c copy {outputPath}";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
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

        [Obsolete("使用CancatVideoFilesWithEncode")]
        static void CancatVideoFiles(List<string> fileList, string outputPath)
        {
            var listFilePath = $"list{DateTime.Now:yyyyMMddhhmmss}.txt";
            using (StreamWriter writer = new StreamWriter(listFilePath))
            {
                foreach (string file in fileList)
                {
                    writer.WriteLine($"file '{file}'");
                }
            }
            var arg = $"-f concat -safe 0 -i {listFilePath} -c copy {outputPath}";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
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

        static void CancatVideoFilesWithEncode(List<string> fileList, string outputPath)
        {
            //ffmpeg -i E:/Personal/Resources/horizontal_video/autumn/5756887.mp4 -i E:/Personal/Resources/horizontal_video/autumn/5384345.mp4 -i E:/Personal/Resources/horizontal_video/autumn/6209891.mp4 -i E:/Personal/Resources/horizontal_video/autumn/5851973.mp4 -i E:/Personal/Resources/horizontal_video/autumn/10112780.mp4 -filter_complex "[0:v]setpts=PTS-STARTPTS, scale=1920:1080, setpts=PTS-STARTPTS[v0]; [1:v]setpts=PTS-STARTPTS, scale=1920:1080, setpts=PTS-STARTPTS[v1]; [2:v]setpts=PTS-STARTPTS, scale=1920:1080, setpts=PTS-STARTPTS[v2]; [3:v]setpts=PTS-STARTPTS, scale=1920:1080, setpts=PTS-STARTPTS[v3]; [4:v]setpts=PTS-STARTPTS, scale=1920:1080, setpts=PTS-STARTPTS[v4]; [v0][v1][v2][v3][v4]concat=n=5:v=1:a=0[outv]" -map "[outv]" -g 25 -c:v libx264 -preset veryfast -crf 23 output.mp4
            //不同编码的视频合并 可能无法播放
            var inputArg = string.Empty;
            var encodeArg = string.Empty;
            var encodeArg2 = string.Empty;
            for (int i = 0; i < fileList.Count; i++)
            {
                inputArg += $" -i {fileList[i]} ";
                encodeArg += $"[{i}:v]setpts=PTS-STARTPTS, scale=1920:1080, setpts=PTS-STARTPTS[v{i}];";
                encodeArg2 += $"[v{i}]";
            }
            var arg = inputArg + $" -filter_complex \"" + encodeArg + encodeArg2 + $"concat=n={fileList.Count}:v=1:a=0[outv]\" -map \"[outv]\" -g 25 -c:v libx264 -preset veryfast -crf 23 " + outputPath;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
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
                    FileName = "ffmpeg",
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

        static string EmotionToVideoTag(EnumEmotion enumEmotion)
        {
            switch (enumEmotion)
            {
                case EnumEmotion.忧郁:
                    return "sea waves";
                case EnumEmotion.自定义:
                    return "forest";
                case EnumEmotion.动漫://秋的视频质量太差 大部分都是人
                    return "sea waves";
                default:
                    break;
            }
            return null;
        }

        #region 后台任务

        static async Task<bool> BackgroundHandler()
        {
            if (appSettings.WebType == EnumWebType.pexels)
            {
                return await PexelsLoad(appSettings.SearchTag);
            }
            else
            {
                return await PixabayLoad(appSettings.SearchTag);
            }

        }

        private static async Task<bool> PexelsLoad(string tag)
        {
            List<VideoPool> videoPool = new List<VideoPool>();
            for (int i = 1; i < 10000; i++)
            {
                Console.WriteLine($"第{i}批次");
                (bool bo, List<VideoPool> videoPoolTemp) = await PexelsSpider.GetVideoByApi(page: i, tag: tag);
                if (!bo)
                {
                    break;
                }
                if (videoPoolTemp != null)
                {
                    videoPoolTemp.ForEach(s => videoPool.Add(s));
                }
            }
            var r = new VideoRepository(appSettings.MysqlConnectionString);
            await r.VideoPoolSave(videoPool);
            return true;
        }

        private static async Task<bool> PixabayLoad(string tag)
        {
            var client = new PixabaySharpClient("40564603-47bed66d2c4354d13706f415a");
            List<VideoPool> videoPool = new List<VideoPool>();
            var r = new VideoRepository(appSettings.MysqlConnectionString);
            var page = (await r.VideoPoolGetLastPage(tag)) + 1;
            Console.WriteLine($"第{page}页批次");
            var result = await client.QueryVideosAsync(new VideoQueryBuilder()
            {
                //VideoType = PixabaySharp.Enums.VideoType.Film,
                Category = PixabaySharp.Enums.Category.Nature,
                //IsEditorsChoice = true,
                Query = tag,
                Page = page,
                PerPage = 200
            });
            if (result == null || result.Videos == null || result.Videos.Count <= 0)
            {
                Console.WriteLine("已经查询到最后一页了");
                return false;
            }
            foreach (var VideoItem in result.Videos)
            {
                var fileInfo = VideoItem.Videos.Large;
                if (VideoItem.Videos.Large != null && !string.IsNullOrWhiteSpace(VideoItem.Videos.Large.Url))
                {
                    fileInfo = VideoItem.Videos.Large;
                  
                }
                else if(VideoItem.Videos.Medium != null && !string.IsNullOrWhiteSpace(VideoItem.Videos.Medium.Url))
                {
                    fileInfo = VideoItem.Videos.Medium;
                }
                if (fileInfo != null && !string.IsNullOrWhiteSpace(fileInfo.Url))
                {
                    videoPool.Add(new VideoPool
                    {
                        Downloaded = 0,
                        Duration = VideoItem.Duration,
                        Height = fileInfo.Height,
                        Width = fileInfo.Width,
                        Tag = tag,
                        Url = fileInfo.Url,
                        VideoId = VideoItem.Id,
                        VideoFileId = 0,
                        Page = page,
                        WebType = (int)EnumWebType.pixabay
                    });
                }

            }

            await r.VideoPoolSave(videoPool);
            Console.WriteLine($"下载结束 一共{videoPool.Count}个");
            return true;
        }

        #endregion

    }
}