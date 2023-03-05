using System;
using System.Collections.Generic;
using System.Text;
using SysLog;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Net;
using System.ServiceProcess;
using System.Configuration.Install;
using System.ComponentModel;
using CoreUtil;

#pragma warning disable CS0162 // 到達できないコードが検出されました
#pragma warning disable CS0618 // 型またはメンバーが旧型式です

namespace SysLogApp
{
    class Program
    {
        static void Main(string[] args)
        {
            if (false)
            {
                // デバッグ
                EventReader r = new EventReader("Application");

                var a = r.Poll(new EventReaderStatus(), 1000);

                foreach (var e in a.Entries)
                {
                    //if (e.Source.Equals("Microsoft-Windows-Kernel-General", StringComparison.OrdinalIgnoreCase) && e.EventID == 16)
                    if (e.Message.IndexOf("Event_Raw_Text") != -1)
                    {
                        Console.WriteLine($"{e.Index} {e.EventID} {e.TimeGenerated} {e.UserName} {e.Message}");
                    }
                }

                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine("User Mode");
                SysLogSvc s = new SysLogSvc();

                Console.ReadLine();

                s.Stop();
            }
        }
    }
}
