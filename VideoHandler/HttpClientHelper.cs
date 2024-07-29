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

namespace VideoHandler.Pexels
{
    //API key:HsuZCJYlo5KiN6Bk0Nr0Dt3ikLD6xySX7EGKuRlTDkl7vOjxdxWr0P5B
    public class HttpClientHelper
    {
        public static void Init()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(1000);
        }

        private static HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            Proxy = new WebProxy("http://127.0.0.1:7890"),
            UseProxy = true
        });
        /// <summary>
        /// 下载 
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

        /// <summary>
        /// 下载 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task<bool> DownLoad(string url, string savePath)
        {
            try
            {
                var res = await _httpClient.GetAsync(url);
                if (res == null || res.StatusCode != HttpStatusCode.OK)
                {
                    Console.WriteLine($"下载失败 {url} ");
                    return false;
                }
                var stream = await res.Content.ReadAsStreamAsync();
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                }
                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
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
