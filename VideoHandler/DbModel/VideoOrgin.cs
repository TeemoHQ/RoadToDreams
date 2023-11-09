using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoHandler.DbModel
{
    public class VideoOrgin
    {
        public int Id { get; set; }

        public int OrientateType { get; set; }

        public string Name { get; set; }

        public string Path { get; set; }

        public int EmotionType { get; set; }

        public int Duration { get; set; }
    }
}
