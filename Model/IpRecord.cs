using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFail2Ban.Model
{
    public class IpRecord 
    {
        public string IpAddress { get; set; }
        public string Reason { get; set; }

       public long CreateDate { get; set; }

        public long ExpiredDate { get; set; }
    }
}
