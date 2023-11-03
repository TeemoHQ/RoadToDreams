using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoHandler.DbModel
{
    public class VideoPool
    {
        public int Id { get; set; }

        public string Tag { get; set; }

        public string Url { get; set; }

        public string LocalPath { get; set; }

        public int Downloaded { get; set; }

        public int Duration { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int VideoId { get; set; }

        public int VideoFileId { get; set; }

        public int Page { get; set; }
    }
}
