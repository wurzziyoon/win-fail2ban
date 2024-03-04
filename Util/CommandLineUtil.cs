using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace WinFail2Ban.Util
{
    public class CommandLineUtil
    {
        public static string ExecCmd(string cmd)
        {
            string output = "";
            // 创建一个进程对象
            Process process = new Process();

            // 设置进程启动信息
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "netsh.exe";
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            // 设置要执行的PowerShell命令
            startInfo.Arguments = cmd;

            // 设置以管理员身份运行
            startInfo.Verb = "runas";

            // 将启动信息应用到进程对象
            process.StartInfo = startInfo;

            try
            {
                // 启动进程
                process.Start();

                // 读取标准输出和错误输出
                output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                // 等待进程执行完毕
                process.WaitForExit();

                // 输出结果
                Console.WriteLine("Output:");
                Console.WriteLine(output);
                Console.WriteLine("Error:");
                Console.WriteLine(error);
            }
            
            catch (SecurityException)
            {
                throw new Exception("Failed to run command as administrator.");
            }
            return output;
        }
    }
}
