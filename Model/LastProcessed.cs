using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFail2Ban.Model
{
    public class LastProcessed
    {
        public string  Id { get; set; }
        public string Source { get; set; }
        public int EventId { get; set; }
        public string ProcessDate { get; set; }

        public long Index { get; set; }
    }
}
