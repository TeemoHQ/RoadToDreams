using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoHandler.DbModel
{
    public class VideoResult
    {
        public int Id { get; set; }

        public int OrginId { get; set; }

        public int WordsId { get; set; }

        public int BgmId { get; set; }

        public string Path { get; set; }
    }
}
