using WinFail2Ban.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFail2Ban.Compare
{
    public class BlockedIpCompare : IEqualityComparer<IpRecord>
    {
        public bool Equals(IpRecord x, IpRecord y)
        {
            if (x == null || y == null)
                return false;
            return x.IpAddress == y.IpAddress && x.Reason == y.Reason;
        }

        public int GetHashCode(IpRecord obj)
        {
            if (obj != null) { 
                return (obj.IpAddress + obj.Reason).GetHashCode();
            }
            return 0;
        }
    }
}
