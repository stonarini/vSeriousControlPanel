using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace vSeriousControlPanel
{
    public class LogEntry
    {
        public string Prefix { get; set; }
        public string Message { get; set; }
        public Brush Color { get; set; }
    }
}
