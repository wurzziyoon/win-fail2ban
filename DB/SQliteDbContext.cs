using WinFail2Ban.Model;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFail2Ban.DB
{
    public class SQliteDbContext : DbContext { public SQliteDbContext() : base("DefaultConnection") { } public DbSet<HackInfo> HackInfo { set; get; } }

}
