using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoHandler.DbModel;

namespace VideoHandler.Repositories
{
    public class VideoRepository
    {
        private MySqlConnection _connection;
        public VideoRepository(string conn)
        {
            _connection = new MySqlConnection(conn);
        }

        public async Task<VideoResult> LastVideoResultGet()
        {
            return await _connection.QueryFirstOrDefaultAsync<VideoResult>("select id,orgin_id orginid,words_id wordsid,bgm_id bgmid,path  from video_result WHERE id = (SELECT MAX(id) FROM video_result)");
        }

        public async Task<VideoWords> LastVideoWordsGet(int minWordsId)
        {
            return await _connection.QueryFirstOrDefaultAsync<VideoWords>($"select id,content,emotion_type EmotionType  from video_words WHERE id >{minWordsId} limit 1");
        }

        public async Task<VideoOrgin> LastVideoOrginGet(string tag, int minId)
        {
            return await _connection.QueryFirstOrDefaultAsync<VideoOrgin>($"select id,orientate_type OrientateType,name,path,tag tag  from video_orgin WHERE id >{minId} and tag='{tag}' and status=1");
        }

        public async Task<VideoBgm> RandVideoBgmGet(int emotionType, int bgmId)
        {
            return await _connection.QueryFirstOrDefaultAsync<VideoBgm>($"select id,emotion_type emotionType,name,path  from video_bgm WHERE emotion_type={emotionType} and id!={bgmId} order by rand() LIMIT 1");
        }

        public async Task<bool> VideoResultSave(VideoResult videoResult)
        {
            return await _connection.ExecuteAsync($"insert into video_result(orgin_id,words_id,bgm_id,path,add_time) values({videoResult.OrginId},{videoResult.WordsId},{videoResult.BgmId},'{videoResult.Path}',now(3))") > 0;
        }


        public async Task<bool> VideoPoolSave(List<VideoPool> list)
        {
            var insertList = new List<string>();
            var sql = $" INSERT ignore INTO `video_pool` (`tag`, `url`, `local_path`, `downloaded`, `duration`, `width`, `height`, `video_id`, `video_file_id`, `page`, `status`, `add_time`) VALUES ";
            foreach (var listChunk in list.Chunk(200))
            {
                foreach (var item in listChunk)
                {
                    insertList.Add($" ('{item.Tag}', '{item.Url}', null, 0, {item.Duration}, {item.Width}, {item.Height}, {item.VideoId}, {item.VideoFileId}, '{item.Page}', '1', now(3)) ");
                }
            }
            sql += string.Join(",", insertList);
            var res = await _connection.ExecuteAsync(sql) > 0;
            return res;
        }

        public async Task<List<VideoPool>> VideoPoolGetRand(string tag, int count, List<int> filterIds, int durationMin = 0, int durationMax = 0)
        {
            var where = string.Empty;
            if (durationMin > 0)
            {
                where += $" and duration>{durationMin} ";
            }

            if (durationMin > 0)
            {
                where += $" and duration< {durationMax} ";
            }

            if (filterIds != null && filterIds.Count > 0)
            {
                where += $" and id not in ({string.Join(',', filterIds)})  ";
            }
            var sql = $" select id,tag,url,duration,width,height,video_id videoid from video_pool where downloaded=0 and status=1 and tag='{tag}' {where} order by rand() limit {count}";
            var res = await _connection.QueryAsync<VideoPool>(sql);
            return res.ToList();
        }

        public async Task<int> AfterDownload(VideoPool video)
        {
            var orientate = video.Width > video.Height ? 0 : 1;
            var name = $"{video.Tag}_{video.VideoId}";
            var sql = $" update video_pool set downloaded=1 ,local_path='{video.LocalPath}' where id={video.Id}; " +
                $" INSERT INTO `video_orgin` (`orientate_type`, `name`, `path`,`tag`,add_time) VALUES ({orientate}, '{name}', '{video.LocalPath}','{video.Tag}',now(3));SELECT LAST_INSERT_ID();";
            var res = await _connection.QueryFirstOrDefaultAsync<int>(sql);
            return res;
        }

        public async Task<bool> AfterConcat(List<int> ids, int orientate, string name, string path, string tag)
        {
            var sql = $" update video_orgin set status=2  where id in @ids; " +
                $" INSERT INTO `video_orgin` (`orientate_type`, `name`, `path`,`tag`,add_time) VALUES ({orientate}, '{name}', '{path}','{tag}',now(3))";
            var res = await _connection.ExecuteAsync(sql, new { ids }) > 0;
            return res;
        }
    }
}
