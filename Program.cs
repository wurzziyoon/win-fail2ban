using WinFail2Ban.DB;
using WinFail2Ban.Model;
using WinFail2Ban.Util;
using Newtonsoft.Json;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;
using WinFail2Ban.Compare;
using System.Threading;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace WinFail2Ban
{
    internal class Program
    {
        const string RULE_NAME = "BLOCK_REMOTE_LOGIN";
        const string EVENT_SOURCE = "Security";
        const int EVENT_ID = 4625;
        const string LOGIN_FAILED_FLG = "3";
        const int PAGE_SIZE = 20;

        private static int FailuresCount
        {
            get
            {
                int result = 3;
                string times = ConfigurationManager.AppSettings["FailuresCount"];
                if (!string.IsNullOrEmpty(times))
                {
                    if (int.TryParse(times, out result))
                    {
                        return result;
                    }
                }
                return result;

            }
        }

        static int Main(string[] args)
        {
            SqliteUtil sqlite = SqliteUtil.GetInstance();
            if (!sqlite.HasDbFile)
            {
                sqlite.Init();
            }
            List<IpRecord> blockedIps = GetAllBlackListIps(sqlite);
            List<IpRecord> whiteListIps = GetAllWhiteListIps(sqlite);

            string result = "";
            try
            {
                List<HackInfo> hackInfoHistory = new List<HackInfo>();
                sqlite.ExecuteReader<HackInfo>("select * from History;", t =>
                {
                    hackInfoHistory = t;
                });
                EventLog eventLog = new EventLog(EVENT_SOURCE);
                eventLog.Source = EVENT_SOURCE;
                List<HackInfo> hackInfos = new List<HackInfo>();
                var temp = eventLog.Entries.Cast<EventLogEntry>().ToList().OrderByDescending(t => t.TimeGenerated).ToList();
                foreach (EventLogEntry entry in eventLog.Entries)
                {
                    if (entry.EventID == EVENT_ID)
                    {
                        if (entry.ReplacementStrings.Length > 19 && entry.ReplacementStrings[10] == LOGIN_FAILED_FLG)
                        {
                            HackInfo hackInfo = new HackInfo()
                            {
                                CreateDate = entry.TimeGenerated.ToString("yyyyMMddHHmmss"),
                                Id = (entry.Index + entry.ReplacementStrings[19] + EVENT_SOURCE + EVENT_ID).GetHashCode().ToString(),
                                IpAddress = entry.ReplacementStrings[19],
                                RemoteWorkGroup = entry.ReplacementStrings[13],
                                EventId = EVENT_ID,
                                EventSource = EVENT_SOURCE,
                                Index = entry.Index,
                            };
                            hackInfos.Add(hackInfo);
                        }
                    }
                }

                List<IpRecord> newBlockedIp = CalcByDay(hackInfos, blockedIps, whiteListIps);

                List<string> insertBlackListSql = new List<string>();
                foreach (IpRecord ip in newBlockedIp)
                {
                    {
                        string sql = $"insert into BlackList(IpAddress,Reason,CreateDate) values('{ip.IpAddress}','{ip.Reason}',{DateTime.Now.ToString("yyyyMMddHHmmss")});";
                        insertBlackListSql.Add(sql);
                    }
                }
                sqlite.ExecuteMulitLineNoneQuery(insertBlackListSql);
                blockedIps.AddRange(newBlockedIp);
                blockedIps = blockedIps.Distinct(new BlockedIpCompare()).OrderBy(t =>
                {
                    return t.IpAddress.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries)[0];
                }).ToList();
                int pageCount = (blockedIps.Count / PAGE_SIZE) + 1;
                int len = 0;
                for (int i = 0; i < pageCount; i++)
                {
                    //创建多个规则添加黑名单,单一规则若有太多IP系统会导致无法屏蔽IP
                    List<string> pageContent = blockedIps.Skip(i * PAGE_SIZE).Take(PAGE_SIZE).Select(t => t.IpAddress).ToList();
                    string strIp = String.Join(",", pageContent);
                    if (string.IsNullOrEmpty(strIp))
                    {
                        continue;
                    }
                    len += pageContent.Count();
                    string showRuleDetailCmd = $"advfirewall firewall show rule name=\"{RULE_NAME}_{i + 1}\"";
                    string deleteOldRuleCmd = $"advfirewall firewall delete rule name=\"{RULE_NAME}_{i + 1}\"";
                    string addNewRuleCmd = $"advfirewall firewall add rule name=\"{RULE_NAME}_{i + 1}\" dir=in action=block description=\"WinFail2Ban添加的需要阻止的IP黑名单-PAGE({i + 1})\" remoteip=\"{String.Join(",", pageContent)}\"";
                    result += ($"\r\n\r\nCMD:\r\n{addNewRuleCmd}\r\n\r\nBefore:\r\n{CommandLineUtil.ExecCmd(showRuleDetailCmd)}\r\n\r\n{CommandLineUtil.ExecCmd(deleteOldRuleCmd)}\r\n\r\n{CommandLineUtil.ExecCmd(addNewRuleCmd)}New:\r\n{CommandLineUtil.ExecCmd(showRuleDetailCmd)}\r\n\r\n");
                }
                RewriteBlockIps(blockedIps);
            }
            catch (Exception ex)
            {
                result += ex.ToString() + "\r\n" + ex.InnerException + "\r\n" + ex.StackTrace;
                File.WriteAllText($"DataErrorLog{DateTime.Now.ToString("yyyyMMddhhmmss")}.txt", result, Encoding.UTF8);
                return 1;
            }

            return 0;
        }

        static void RewriteBlockIps(List<IpRecord> blockedIps)
        {
            File.WriteAllText("BlockedIp.json", JsonConvert.SerializeObject(blockedIps), Encoding.UTF8);
        }
        static List<IpRecord> GetAllBlackListIps(SqliteUtil sqlite)
        {
            List<IpRecord> result = new List<IpRecord>();

            sqlite.ExecuteReader<IpRecord>("select * from BlackList;", t =>
            {
                result = t;
            });
            return result;
        }
        static List<IpRecord> GetAllWhiteListIps(SqliteUtil sqlite)
        {
            List<IpRecord> result = new List<IpRecord>();

            sqlite.ExecuteReader<IpRecord>("select * from WhiteList;", t =>
            {
                result = t;
            });
            return result;
        }
        static List<IpRecord> CalcByDay(List<HackInfo> info, List<IpRecord> blockedIps, List<IpRecord> whiteIps = null)
        {
            List<string> allDay = info.Select(t => t.CreateDate.Substring(0, 8)).Distinct().ToList();
            List<IpRecord> result = new List<IpRecord>();
            long nowDate = long.Parse(DateTime.Now.ToString("yyyyMMddHHmmss"));
            List<string> blockedIp = blockedIps.Select(t => t.IpAddress).ToList();
            foreach (string day in allDay)
            {
                List<HackInfo> infoByDay = info.Where(t => t.CreateDate.StartsWith(day)).ToList();
                var members = infoByDay.GroupBy(t => t.IpAddress)
                    .Select(group => new
                    {
                        GroupKey = group.Key,
                        Count = group.Count()
                    }).Where(t =>
                    {
                        if (whiteIps != null && whiteIps.Count > 0)
                        {
                            foreach (IpRecord whiteIp in whiteIps)
                            {
                                Regex regex = new Regex(whiteIp.IpAddress);
                                if (regex.IsMatch(t.GroupKey))
                                {
                                    if (whiteIp.ExpiredDate == 0 || nowDate <= whiteIp.ExpiredDate)
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                        return t.Count > FailuresCount && result.Where(v => v.IpAddress == t.GroupKey).Count() == 0 && !blockedIp.Contains(t.GroupKey);
                    });
                foreach (var group in members)
                {
                    Console.WriteLine($"Group: {group.GroupKey}, Count: {group.Count}");
                    result.Add(new IpRecord()
                    {
                        IpAddress = group.GroupKey,
                        Reason = "Too Many Failed Remote Login"
                    });
                }
            }
            return result;
        }
    }
}
