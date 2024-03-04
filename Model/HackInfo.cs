using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFail2Ban.Model
{
    public class HackInfo
    {
        public string CreateDate { get; set; }
        public string Id { get; set; }
        public string IpAddress { get; set; }
        public string RemoteWorkGroup { get; set; }
        public string EventSource { get; set; }
        public long EventId { get; set; }

        public long Index { get; set; }
        
    }
}
