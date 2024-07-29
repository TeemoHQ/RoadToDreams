using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoHandler.Pexels
{
    public class PexelsApiSearchRes
    {
        public int page { get; set; }
        public int per_page { get; set; }

        public List<Video> videos { get; set; }
    }

    public class Video
    {
        public int id { get; set; }

        public int duration { get; set; }

        public List<VideoFile> video_files { get; set; }
    }

    public class VideoFile
    {
        public int id { get; set; }

        public string quality { get; set; }

        public string file_type { get; set; }

        public int width { get; set; }

        public int height { get; set; }

        public string link { get; set; }
    }
}
