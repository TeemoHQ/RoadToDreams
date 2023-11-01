using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoHandler.DbModel
{
    public class VideoBgm
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Path { get; set; }

        public int EmotionType { get; set; }
    }
}
