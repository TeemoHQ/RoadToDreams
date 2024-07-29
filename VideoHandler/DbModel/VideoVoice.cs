using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoHandler.DbModel
{
    public class VideoVoice
    {
        public int Id { get; set; }

        public DateTime CreateTime { get; set; }

        public string Path { get; set; }
    }
}
