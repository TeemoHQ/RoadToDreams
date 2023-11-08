using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoHandler
{
    public class AppSettings
    {
        public string MysqlConnectionString { get; set; }

        public string ResourcesPath { get; set; }

        public int WordsCount { get; set; } = 3;

        /// <summary>
        /// 华文楷体  C:\Windows\Fonts
        /// </summary>
        public string FontType { get; set; } = "STKAITI.TTF";

        public int FontSize { get; set; } = 60;

        public string FontColor { get; set; } = "white";

        public int VideoTime { get; set; } = 30;

        public bool WordSameTime { get; set; }

        public bool DownloadConcat { get; set; }
    }

}
