using Dapper;
using Google.Protobuf.WellKnownTypes;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VideoHandler.DbModel;

namespace VideoHandler
{
    //API key:HsuZCJYlo5KiN6Bk0Nr0Dt3ikLD6xySX7EGKuRlTDkl7vOjxdxWr0P5B
    public class PexelsSpider
    {
        private static HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            Proxy = new WebProxy("http://127.0.0.1:7890"),
            UseProxy = true
        });

        private static Regex _pexelsPathRegex = new Regex("(https://www.pexels.com/download/video/[^\"]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);


        //反爬了 不能用
        [Obsolete]
        public static async Task<bool> GetVideoBySpider(string mysqlConnectionString, string tag = "forest", int count = 10)
        {
            try
            {
                using var connection = new MySqlConnection(mysqlConnectionString);
                var historyUndownLoadPathList = await connection.QueryAsync<VideoPool>($"select id,tag,url,local_path LocalPath,downloaded,lenth from video_pool where downloaded=0  limit {count} ");
                if (historyUndownLoadPathList == null || historyUndownLoadPathList.Count() < count)
                {
                    var url = $"https://www.pexels.com/search/videos/{tag}/";
                    var html = await _httpClient.GetStringAsync(url);
                    var localPathList = _pexelsPathRegex.Matches(html).Select(s => s.Value).ToList();
                    if (localPathList == null || localPathList.Count <= 0)
                    {
                        Console.WriteLine("爬取pexels资源连接失败");
                        return false;
                    }
                    var oldUrlList = await connection.QueryAsync<string>("select url from video_pool ");
                    if (oldUrlList != null && oldUrlList.Count() > 0)
                    {
                        localPathList.RemoveAll(s => oldUrlList.Contains(s));
                    }
                    var sql = "INSERT INTO `video_pool` (`tag`, `url`, `downloaded`, `lenth`, `add_time`) VALUES ";
                    var valuesSql = new List<string>();
                    for (int i = 0; i < localPathList.Count; i++)
                    {
                        valuesSql.Add($"('{tag}','{localPathList[i]}',0,0,now(3))");
                    }
                    sql += string.Join(",", valuesSql);
                    await connection.ExecuteAsync(sql);
                }
                if (!Directory.Exists("Donwload"))
                {
                    Directory.CreateDirectory("Donwload");
                }
                historyUndownLoadPathList = await connection.QueryAsync<VideoPool>($"select id,tag,url,local_path LocalPath,downloaded,lenth from video_pool where downloaded=0  limit {count} ");
                foreach (var item in historyUndownLoadPathList)
                {
                    item.LocalPath = $"Donwload/{tag}_{DateTime.Now:yyyyMMddhhmmss}.mp4";
                    var stream = await _httpClient.GetStreamAsync(item.Url);
                    using (var fileStream = new FileStream(item.LocalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                    await connection.ExecuteAsync($"update video_pool set downloaded=1 where id={item.Id}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// https://www.pexels.com/api/documentation/#videos-search__parameters__orientation
        /// </summary>
        /// <param name="mysqlConnectionString"></param>
        /// <param name="tag"></param>
        /// <param name="count"></param>
        /// <param name="orientation">landscape, portrait or square.</param>
        /// <returns></returns>
        public static async Task<(bool, List<VideoPool>)> GetVideoByApi(int duration = 12, string tag = "forest", int page = 1, int count = 80, string orientation = "landscape", string size = "large")
        {
            try
            {
                if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                {
                    _httpClient.DefaultRequestHeaders.Add("Authorization", "HsuZCJYlo5KiN6Bk0Nr0Dt3ikLD6xySX7EGKuRlTDkl7vOjxdxWr0P5B");
                }
                var searchPath = $"https://api.pexels.com/videos/search?query={tag}&page={page}&per_page={count}&size={size}&orientation={orientation}";
                var searchRes = await _httpClient.GetAsync(searchPath);
                var res = await searchRes.Content.ReadAsStringAsync();
                var listModel = JsonConvert.DeserializeObject<PexelsApiSearchRes>(res);
                if (listModel?.videos != null && listModel.videos.Count > 0)
                {
                    listModel.videos = listModel!.videos.Where(s => s.duration >= duration)?.ToList() ?? new List<Video>();
                    var poolList = new List<VideoPool>();
                    foreach (var video in listModel.videos)
                    {
                        var fileInfo = orientation == "portrait" ? video.video_files.FirstOrDefault(s => s.width > 1080) : video.video_files.FirstOrDefault(s => s.height > 1080);
                        if (fileInfo != null)
                        {
                            poolList.Add(new VideoPool
                            {
                                Downloaded = 0,
                                Duration = video.duration,
                                Height = fileInfo.height,
                                Width = fileInfo.width,
                                Tag = tag,
                                Url = fileInfo.link,
                                VideoId = video.id,
                                VideoFileId = fileInfo.id,
                                Page = page,
                                WebType=(int)EnumWebType.pexels
                            });
                        }
                    }
                    return (true, poolList);
                }
                return (false, null);
            }
            catch (Exception)
            {
                return (true, null);
            }
        }

        /// <summary>
        /// 下载 并且修改数据库状态
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task<bool> DownLoad(VideoPool video, string resourcePath)
        {
            try
            {
                var res = await _httpClient.GetAsync(video.Url);
                if (res != null && res.StatusCode == HttpStatusCode.Forbidden)
                {
                    res = await _httpClient.GetAsync(video.Url);
                    if (res != null && res.StatusCode == HttpStatusCode.Forbidden)
                    {
                        Console.WriteLine("重试失败，准备换地址");
                        return false;
                    }
                }
                var stream = await res.Content.ReadAsStreamAsync();
                resourcePath += video.Width > video.Height ? "/horizontal_video" : "/vertical_video";
                resourcePath += $"/{video.Tag.Replace(" ", "_")}";
                if (!Directory.Exists(resourcePath))
                {
                    Directory.CreateDirectory(resourcePath);
                }
                video.LocalPath = resourcePath + $"/{video.VideoId}.mp4";
                if (File.Exists(video.LocalPath))
                {
                    File.Delete(video.LocalPath);
                }
                using (var fileStream = new FileStream(video.LocalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fileStream);
                }
                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }

        }
    }
}
