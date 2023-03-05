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

namespace SysLogApp
{
    [RunInstaller(true)]
    public class SysLogServiceInstaller : Installer
    {
        public SysLogServiceInstaller()
        {
            ServiceProcessInstaller proc = new ServiceProcessInstaller();
            ServiceInstaller svc = new ServiceInstaller();

            proc.Account = ServiceAccount.LocalSystem;
            svc.StartType = ServiceStartMode.Automatic;
            svc.DisplayName = "SE IPA CyberLab SysLog";
            svc.ServiceName = "syslog";
            svc.Description = "SE IPA CyberLab SysLog Service";
            this.Installers.Add(svc);
            this.Installers.Add(proc);
        }
    }

    public class SysLogService : ServiceBase
    {
        object lockObj = new object();
        SysLogSvc s = null;

        public SysLogService()
        {
        }

        protected override void OnStart(string[] args)
        {
            lock (lockObj)
            {
                if (s == null)
                {
                    s = new SysLogSvc();
                }
            }
        }

        protected override void OnStop()
        {
            lock (lockObj)
            {
                if (s != null)
                {
                    s.Stop();
                    s = null;
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
                {
                    new SysLogService()
                };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
