// SysLog
// 
// Copyright (C) 2004-2008 SoftEther Corporation. All Rights Reserved.

using System;
using System.Threading;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Web.Mail;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using CoreUtil;
using SysLog;

#pragma warning disable 0618

namespace SysLog
{

    static class CharsetAutoDetectUtil
    {
        // テキストファイルのエンコーディングを取得する
        public static Encoding DetectEncoding(byte[] data, Encoding defaultEncoding)
        {
            Encoding tmp = null;

            try
            {
                tmp = DetectEncodingCore(data);
            }
            catch { }

            if (tmp == null)
            {
                tmp = defaultEncoding;
            }

            return tmp;
        }

        static Encoding DetectEncodingCore(byte[] data)
        {
            const byte bESC = 0x1B;
            const byte bAT = 0x40;
            const byte bDollar = 0x24;
            const byte bAnd = 0x26;
            const byte bOP = 0x28;
            const byte bB = 0x42;
            const byte bD = 0x44;
            const byte bJ = 0x4A;
            const byte bI = 0x49;

            int len = data.Length;
            int binary = 0;
            int ucs2 = 0;
            int sjis = 0;
            int euc = 0;
            int utf8 = 0;
            byte b1, b2;

            for (int i = 0; i < len; i++)
            {
                if (data[i] <= 0x06 || data[i] == 0x7F || data[i] == 0xFF)
                {
                    //'binary'
                    binary++;
                    if (len - 1 > i && data[i] == 0x00
                        && i > 0 && data[i - 1] <= 0x7F)
                    {
                        //smells like raw unicode
                        ucs2++;
                    }
                }
            }


            if (binary > 0)
            {
                if (ucs2 > 0)
                {
                    //JIS
                    //ucs2(Unicode)

                    int n1 = 0, n2 = 0;
                    for (int i = 0; i < (len / 2); i++)
                    {
                        byte e1 = data[i * 2];
                        byte e2 = data[i * 2 + 1];

                        if (e1 == 0 && e2 != 0)
                        {
                            n1++;
                        }
                        else if (e1 != 0 && e2 == 0)
                        {
                            n2++;
                        }
                    }

                    if (n1 > n2)
                    {
                        return Encoding.GetEncoding("utf-16BE");
                    }
                    else
                    {
                        return System.Text.Encoding.Unicode;
                    }
                }
                else
                {
                    //binary
                    return null;
                }
            }

            for (int i = 0; i < len - 1; i++)
            {
                b1 = data[i];
                b2 = data[i + 1];

                if (b1 == bESC)
                {
                    if (b2 >= 0x80)
                        //not Japanese
                        //ASCII
                        return Str.AsciiEncoding;
                    else if (len - 2 > i &&
                        b2 == bDollar && data[i + 2] == bAT)
                        //JIS_0208 1978
                        //JIS
                        return Str.ISO2022JPEncoding;
                    else if (len - 2 > i &&
                        b2 == bDollar && data[i + 2] == bB)
                        //JIS_0208 1983
                        //JIS
                        return Str.ISO2022JPEncoding;
                    else if (len - 5 > i &&
                        b2 == bAnd && data[i + 2] == bAT && data[i + 3] == bESC &&
                        data[i + 4] == bDollar && data[i + 5] == bB)
                        //JIS_0208 1990
                        //JIS
                        return Str.ISO2022JPEncoding;
                    else if (len - 3 > i &&
                        b2 == bDollar && data[i + 2] == bOP && data[i + 3] == bD)
                        //JIS_0212
                        //JIS
                        return Str.ISO2022JPEncoding;
                    else if (len - 2 > i &&
                        b2 == bOP && (data[i + 2] == bB || data[i + 2] == bJ))
                        //JIS_ASC
                        //JIS
                        return Str.ISO2022JPEncoding;
                    else if (len - 2 > i &&
                        b2 == bOP && data[i + 2] == bI)
                        //JIS_KANA
                        //JIS
                        return Str.ISO2022JPEncoding;
                }
            }

            for (int i = 0; i < len - 1; i++)
            {
                b1 = data[i];
                b2 = data[i + 1];
                if (((b1 >= 0x81 && b1 <= 0x9F) || (b1 >= 0xE0 && b1 <= 0xFC)) &&
                    ((b2 >= 0x40 && b2 <= 0x7E) || (b2 >= 0x80 && b2 <= 0xFC)))
                {
                    sjis += 2;
                    i++;
                }
            }
            for (int i = 0; i < len - 1; i++)
            {
                b1 = data[i];
                b2 = data[i + 1];
                if (((b1 >= 0xA1 && b1 <= 0xFE) && (b2 >= 0xA1 && b2 <= 0xFE)) ||
                    (b1 == 0x8E && (b2 >= 0xA1 && b2 <= 0xDF)))
                {
                    euc += 2;
                    i++;
                }
                else if (len - 2 > i &&
                    b1 == 0x8F && (b2 >= 0xA1 && b2 <= 0xFE) &&
                    (data[i + 2] >= 0xA1 && data[i + 2] <= 0xFE))
                {
                    euc += 3;
                    i += 2;
                }
            }
            for (int i = 0; i < len - 1; i++)
            {
                b1 = data[i];
                b2 = data[i + 1];
                if ((b1 >= 0xC0 && b1 <= 0xDF) && (b2 >= 0x80 && b2 <= 0xBF))
                {
                    utf8 += 2;
                    i++;
                }
                else if (len - 2 > i &&
                    (b1 >= 0xE0 && b1 <= 0xEF) && (b2 >= 0x80 && b2 <= 0xBF) &&
                    (data[i + 2] >= 0x80 && data[i + 2] <= 0xBF))
                {
                    utf8 += 3;
                    i += 2;
                }
            }

            if (euc > sjis && euc > utf8)
                //EUC
                return Str.EucJpEncoding;
            else if (sjis > euc && sjis > utf8)
                //SJIS
                return Str.ShiftJisEncoding;
            else if (utf8 > euc && utf8 > sjis)
                //UTF8
                return Str.Utf8Encoding;

            return null;
        }
    }

    class Entry
    {
        public DateTime DateTime;
        public IPAddress SrcIp;
        public string String;
    }

    public class SysLogSvc
    {
        public SysLogSvc()
        {
            init();
        }

        ThreadObj acceptThread;
        ThreadObj sendMailThread;
        bool halt;
        Event haltEvent;
        Sock sockv4;
        Sock sockv6;
        Listener listener;

        FileLogger logger = new FileLogger(Path.Combine(Env.ExeFileDir, "Log"));

        Cache<int, ReadIni> config_cache = new Cache<int, ReadIni>(new TimeSpan(0, 0, 30), CacheType.DoNotUpdateExpiresWhenAccess);

        ReadIni config
        {
            get
            {
                ReadIni ret = null;

                lock (config_cache)
                {
                    if (config_cache[0] == null)
                    {
                        ret = new ReadIni(Path.Combine(Env.ExeFileDir, "SysLog.config"));

                        config_cache.Add(0, ret);
                    }
                    else
                    {
                        ret = config_cache[0];
                    }
                }

                return ret;
            }
        }

        object socketLock = new object();

        void listenerAcceptProc(Listener l, Sock s, object obj)
        {
        }

        public string SmtpServerName;
        public int SmtpPort;
        public string SmtpUsername, SmtpPassword;
        public int SmtpNumTry;

        public string SmtpServerName2;
        public int SmtpPort2;
        public string SmtpUsername2, SmtpPassword2;
        public int SmtpNumTry2;

        public string SubjectPrefix;
        public string Footer;

        void sendMailThreadProc(object param)
        {
            while (halt == false)
            {
                ReadIni ini = config;

                SmtpServerName = ini["SmtpServerName"].StrValue;
                SmtpPort = (int)ini["SmtpPort"].IntValue;
                if (SmtpPort == 0)
                {
                    SmtpPort = 25;
                }
                SmtpNumTry = (int)ini["SmtpNumTry"].IntValue;
                if (SmtpNumTry == 0)
                {
                    SmtpNumTry = 1;
                }
                SmtpUsername = ini["SmtpUsername"].StrValue;
                SmtpPassword = ini["SmtpPassword"].StrValue;

                SmtpServerName2 = ini["SmtpServerName2"].StrValue;
                SmtpPort2 = (int)ini["SmtpPort2"].IntValue;
                if (SmtpPort2 == 0)
                {
                    SmtpPort2 = 25;
                }
                SmtpNumTry2 = (int)ini["SmtpNumTry2"].IntValue;
                if (SmtpNumTry2 == 0)
                {
                    SmtpNumTry2 = 1;
                }
                SmtpUsername2 = ini["SmtpUsername2"].StrValue;
                SmtpPassword2 = ini["SmtpPassword2"].StrValue;

                SubjectPrefix = ini["SubjectPrefix"].StrValue;
                Footer = ini["Footer"].StrValue;

                long now = Time.NowLongMillisecs;
                long interval = (long)config["SendMailInterval"].Int64Value * (long)1000;
                interval = Math.Max(interval, 100);
                int waitTime = (int)(interval / 10);
                waitTime = Math.Max(waitTime, 50);
                waitTime = Math.Min(waitTime, 1000);

                StringBuilder sb = new StringBuilder();
                string title = "";
                string header = "";

                lock (entryList)
                {
                    bool b = false;

                    if (entryList.Count >= 1)
                    {
                        if ((lastEntryTick + interval) <= now)
                        {
                            b = true;
                        }
                    }

                    int fc = (int)config["SendMailForceCount"].IntValue;
                    if (fc == 0)
                    {
                        fc = int.MaxValue;
                    }

                    if (entryList.Count >= fc)
                    {
                        b = true;
                    }


                    if (b)
                    {
                        lastEntryTick = now;

                        int n = 0;

                        Dictionary<IPAddress, int> d = new Dictionary<IPAddress, int>();

                        Dictionary<IPAddress, string> hostNameDict = new Dictionary<IPAddress, string>();

                        foreach (Entry en in entryList)
                        {
                            sb.AppendFormat("■ {0}: {1}", ++n, en.DateTime);
                            sb.AppendLine();
                            sb.AppendFormat("【{0}", en.SrcIp);

                            string hostname = "";
                            try
                            {
                                hostname = Domain.GetHostName(en.SrcIp, 100)[0];

                                hostNameDict[en.SrcIp] = hostname;
                            }
                            catch
                            {
                                hostname = "";
                            }

                            if (hostname.Equals(en.SrcIp.ToString(), StringComparison.InvariantCultureIgnoreCase) == false)
                            {
                                if (Str.IsEmptyStr(hostname) == false)
                                {
                                    sb.AppendFormat(" ({0})", hostname);
                                }
                            }
                            sb.Append("】");

                            sb.AppendLine();
                            sb.AppendLine(en.String);
                            sb.AppendLine();

                            if (d.ContainsKey(en.SrcIp) == false)
                            {
                                d.Add(en.SrcIp, 1);
                            }
                            else
                            {
                                d[en.SrcIp]++;
                            }
                        }

                        title = string.Format("{0} 件のログ", entryList.Count);

                        if (d.Keys.Count >= 2)
                        {
                            title += " (" + d.Keys.Count + " 台のホストから)";
                        }

                        header = "【" + title + "】\r\n" + string.Format("報告日時: {0}\r\n", DateTime.Now);

                        if (d.Keys.Count >= 2)
                        {
                            header += "\r\n★ホスト一覧\r\n";

                            foreach (IPAddress ip in d.Keys)
                            {
                                int num = d[ip];

                                string hostname = "";
                                hostNameDict.TryGetValue(ip, out hostname);

                                string tmp = "";

                                if (Str.IsEmptyStr(hostname) == false)
                                {
                                    tmp = " (" + hostname + ")";
                                }

                                header += " 【" + ip.ToString() + tmp + "】 (" + num + " 件)\r\n";
                            }
                        }
                        header += "\r\n";

                        entryList.Clear();
                    }
                }

                if (sb.Length >= 1)
                {
                    mailSend(title, header + sb.ToString());
                }

                haltEvent.Wait(waitTime);
            }
        }

        List<long> send_log = new List<long>();
        int quota_num_deleted = 0;
        bool last_quota_ok = true;

        void mailSend(string title, string body)
        {
            ReadIni c = config;

            // クォータ数の検査
            int send_quota_count = (int)c["SendQuotaCount"].IntValue;
            int send_quota_period = (int)c["SendQuotaPeriod"].IntValue * 1000;
            bool quota_ok = true;
            bool quota_ok_this_is_last = false;
            long min_tick = 0;
            if (send_quota_count > 1 && send_quota_period > 1)
            {
                long tick = Time.Tick64;

                long period_start_tick = tick - (long)send_quota_period;
                period_start_tick = Math.Max(period_start_tick, 0);

                List<long> o = new List<long>();
                int i, num;

                num = 0;
                for (i = 0; i < send_log.Count; i++)
                {
                    if (send_log[i] >= period_start_tick)
                    {
                        num++;
                        min_tick = send_log[i];
                    }
                    else
                    {
                        o.Add(send_log[i]);
                    }
                }

                // GC
                foreach (long del in o)
                {
                    send_log.Remove(del);
                }

                if (num <= send_quota_count)
                {
                    send_log.Add(tick);
                }

                if (num >= send_quota_count)
                {
                    if (num == send_quota_count)
                    {
                        quota_ok_this_is_last = true;
                    }
                    else
                    {
                        quota_ok = false;
                        quota_num_deleted++;
                        //title += " num=" + num + " quota_num_deleted=" + quota_num_deleted;
                    }
                }
            }

            if (quota_ok == false)
            {
                last_quota_ok = false;
                //title += " quota_ok==false";
            }
            else
            {
                if (last_quota_ok == false)
                {
                    last_quota_ok = true;
                    title += " (クォータ制限終了)";

                    string bt = "クォータ制限終了\r\n送信抑制されたメール数: " + quota_num_deleted + "\r\n";

                    send_log.Clear();

                    quota_num_deleted = 0;
                    quota_ok_this_is_last = false;

                    body = bt + body;
                }

                if (quota_ok_this_is_last)
                {
                    long tick = Time.Tick64;
                    long t2 = tick - min_tick;
                    DateTime first = DateTime.Now.AddMilliseconds((double)-t2).AddMilliseconds(send_quota_period);

                    title += " (クォータ制限開始)";

                    string bt = "クォータ制限値: " + send_quota_count + " 個 / " + (send_quota_period / 1000) + " 秒\r\n" +
                        "上記を超過したので送信制限を開始\r\n" +
                        "次回送信再開予定: " + Str.DateTimeToStr(first) + "\r\n\r\n";

                    body = bt + body;
                }
            }

            if (Str.IsEmptyStr(this.Footer) == false)
            {
                body += "\r\n\r\n" + this.Footer + "\r\n";
            }

            if (Str.IsEmptyStr(this.SubjectPrefix) == false)
            {
                title = this.SubjectPrefix + " " + title;
            }

            string[] keys = c.GetKeys();

            foreach (string key in keys)
            {
                if (key.StartsWith("SmtpTo", StringComparison.InvariantCultureIgnoreCase))
                {
                    string mail = c[key].StrValue;
                    bool noquota = false;

                    string[] tokens = mail.Split(',');

                    if (tokens.Length >= 2)
                    {
                        mail = tokens[0];
                        noquota = Str.StrCmpi(tokens[1], "noquota");
                    }

                    if (noquota || quota_ok)
                    {
                        try
                        {
                            Console.WriteLine("{0} に送信中...", mail);
                            //SendMail sm = new SendMail(c["SmtpServerName"].StrValue, SendMailVersion.Ver2_With_NetMail);
                            //sm.Send(c["SmtpFrom"].StrValue, mail, title, body);

                            MailSendMain(c["SmtpFrom"].StrValue, mail, title, body);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
            }
        }


        // メール送信メイン
        public void MailSendMain(string from, string to, string subject, string body)
        {
            int i;

            bool ok = false;

            for (i = 0; i < SmtpNumTry; i++)
            {
                SendMail sm = new SendMail(SmtpServerName, SendMailVersion.Ver2_With_NetMail, SmtpUsername, SmtpPassword);

                sm.SmtpPort = SmtpPort;

                if (sm.Send(from, to, subject, body))
                {
                    ok = true;
                    break;
                }
            }

            if (ok == false)
            {
                for (i = 0; i < SmtpNumTry2; i++)
                {
                    SendMail sm = new SendMail(SmtpServerName2, SendMailVersion.Ver2_With_NetMail, SmtpUsername2, SmtpPassword2);

                    sm.SmtpPort = SmtpPort2;

                    if (sm.Send(from, to, subject, body))
                    {
                        break;
                    }
                }
            }
        }

        void acceptThreadProc(object param)
        {
            sockv4 = null;
            sockv6 = null;

            listener = new Listener(514, new AcceptProc(listenerAcceptProc), null);
            sendMailThread = new ThreadObj(new ThreadProc(sendMailThreadProc));

            ThreadObj.NoticeInited();

            LABEL_START:

            while (true)
            {
                lock (socketLock)
                {
                    try
                    {
                        int port = (int)config["Port"].IntValue;

                        Console.WriteLine("ポート {0} を開いています...", port);
                        Sock s = Sock.NewUDP(port);

                        sockv4 = s;

                        try
                        {
                            sockv6 = Sock.NewUDP(port, IPAddress.IPv6Any, true);
                        }
                        catch
                        {
                        }

                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());

                        haltEvent.Wait(1000);
                    }
                }
            }

            Console.WriteLine("完了。");

            SockEvent se = new SockEvent();
            se.JoinSock(sockv4);
            if (sockv6 != null)
            {
                se.JoinSock(sockv6);
            }

            long nextRead = Time.Tick64;

            string[] configKeys = config.GetKeys();

            while (halt == false)
            {
                long now = Time.Tick64;

                if (now >= nextRead)
                {
                    // reload!
                    nextRead = now + (5 * 1000);
                    configKeys = config.GetKeys();
                }

                try
                {
                    IPEndPoint srcIp, srcIpV6;

                    if (se.Event.Wait(1000) == false)
                    {
                        configKeys = config.GetKeys();
                    }

                    byte[] data = sockv4.RecvFrom(out srcIp, 65535);
                    byte[] datav6 = sockv6.RecvFrom(out srcIpV6, 65535);

                    if (halt)
                    {
                        break;
                    }

                    try
                    {
                        if (data != null && data.Length >= 1)
                        {
                            sysLogRecved(data, srcIp.Address, configKeys);
                        }

                        if (datav6 != null && datav6.Length >= 1)
                        {
                            sysLogRecved(datav6, srcIpV6.Address, configKeys);
                        }

                        pollEventLog(configKeys);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
                catch
                {
                    goto LABEL_START;
                }
            }

            listener.Stop();

            Console.WriteLine("終了。");
        }

        // イベントログのポーリング
        void pollEventLog(string[] configKeys)
        {
            foreach (string key in configKeys)
            {
                if (key.StartsWith("EventLogName", StringComparison.OrdinalIgnoreCase))
                {
                    string value = config[key].StrValue;

                    if (Str.IsEmptyStr(value) == false)
                    {
                        pollEventLogMain(value);
                    }
                }
            }
        }

        void pollEventLogMain(string eventLogName)
        {
            string regKeyName = @"Software\SoftEther Corporation\SysLog";
            string regValueName = "status_" + eventLogName;

            EventReaderStatus status = null;
            try
            {
                status = EventReaderStatus.FromString(Reg.ReadStr(RegRoot.LocalMachine, regKeyName, regValueName));
            }
            catch
            {
            }

            EventReader r = new EventReader(eventLogName);

            EventReaderResults ret = r.Poll(status);

            Reg.WriteStr(RegRoot.LocalMachine, regKeyName, regValueName, ret.Status.ToString());

            Array.Reverse(ret.Entries);

            foreach (var e in ret.Entries)
            {
                string str = string.Format("{0}: EVENT_ID={1};Src={4};Index={2};Msg={3};User={5};",
                    e.TimeGenerated,
                    e.EventID,
                    e.Index,
                    e.Message.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", " / "),
                    e.Source,
                    e.UserName);

                sysLogRecved(Encoding.GetEncoding(config["RecvEncoding"].StrValue).GetBytes(str), null, null);
            }
        }

        List<Entry> entryList = new List<Entry>();
        long lastEntryTick = 0;

        Cache<string, string> HostNameCache = new Cache<string, string>(TimeSpan.FromDays(1), CacheType.DoNotUpdateExpiresWhenAccess);
        Cache<string, string> HostNameNegativeCache = new Cache<string, string>(TimeSpan.FromMinutes(15), CacheType.DoNotUpdateExpiresWhenAccess);

        void sysLogRecved(byte[] data, IPAddress ip, string[] configKeys)
        {
            DateTime now = DateTime.Now;
            ReadIni c = config;

            if (configKeys == null)
            {
                configKeys = c.GetKeys();
            }

            if (ip == null)
            {
                ip = Domain.StrToIP("127.0.0.1");
            }

            string ipStr = ip.ToString();

            if (config["AppendHostInfoToData"].BoolValue)
            {
                // ホスト情報をデータに追加する
                string hostname = HostNameCache[ipStr];
                if (Str.IsEmptyStr(hostname))
                {
                    try
                    {
                        if (Str.IsEmptyStr(HostNameNegativeCache[ipStr]))
                        {
                            for (int i = 0; i < 1; i++)
                            {
                                try
                                {
                                    hostname = Domain.GetHostName(ip, 1000)[0];
                                    if (hostname == ipStr)
                                    {
                                        hostname = "";
                                    }
                                }
                                catch
                                {
                                    hostname = "";
                                }

                                if (Str.IsEmptyStr(hostname) == false)
                                {
                                    break;
                                }
                            }
                            if (Str.IsEmptyStr(hostname) == false)
                            {
                                HostNameCache.Add(ipStr, hostname);
                            }
                            else
                            {
                                HostNameNegativeCache.Add(ipStr, "<none>");
                            }
                        }
                    }
                    catch
                    {
                        hostname = "";
                        HostNameNegativeCache.Add(ipStr, "<none>");
                    }
                }
                if (hostname == null)
                {
                    hostname = "";
                }
                if (hostname.Equals(ip.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    hostname = "";
                }
                if (ip.ToString() == "127.0.0.1")
                {
                    hostname = Env.MachineName;
                }

                if (Str.IsEmptyStr(hostname))
                {
                    hostname = ip.ToString();
                }
                else
                {
                    hostname = ip.ToString() + " (" + hostname + ")";
                }

                byte[] hostNameByteArray = Str.AsciiEncoding.GetBytes(string.Format("[{0}] -> ", hostname));

                Buf b = new Buf();
                b.Write(hostNameByteArray);
                b.Write(data);

                data = b.ByteData;
            }


            // 受信バイナリデータから文字コードを自動推測
            Encoding encoding;

            if (config["DisableRecvEncodingAutoDetect"].BoolValue)
            {
                // 自動推測無効
                encoding = Encoding.GetEncoding(config["RecvEncoding"].StrValue);
            }
            else
            {
                // 自動推測有効
                encoding = CharsetAutoDetectUtil.DetectEncoding(data, Encoding.GetEncoding(config["RecvEncoding"].StrValue));
            }

            string str = encoding.GetString(data);

            bool ignore = false;

            string str2 = ip.ToString() + " " + str;

            foreach (string key in configKeys)
            {
                if (key.StartsWith("IgnoreString", StringComparison.OrdinalIgnoreCase))
                {
                    string value = c[key].StrValue.Trim();

                    // 単一
                    if (str2.IndexOf(value, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        ignore = true;
                    }

                    if (value.IndexOf("&&") != -1)
                    {
                        // 複数   AND 条件
                        string[] sps = { "&&" };
                        string[] tokens = value.Split(sps, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 2)
                        {
                            bool flag = true;
                            foreach (string token in tokens)
                            {
                                string token2 = token.Trim();

                                if (str2.IndexOf(token2, StringComparison.OrdinalIgnoreCase) == -1)
                                {
                                    flag = false;
                                }
                            }
                            if (flag)
                            {
                                ignore = true;
                            }
                        }
                    }
                }
            }
            Console.WriteLine("{0}: {1}", ip, str);

            // ログ
            if (config["SaveLog"].BoolValue)
            {
                logger.Flush = true;
                logger.Write(string.Format("{0}: {1}", ip, str));
            }

            // 他の syslog サーバーに転送

            Sock s = Sock.NewUDP(0);

            foreach (string key in configKeys)
            {
                if (key.StartsWith("TransferServer", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string value = c[key].StrValue;

                        string[] values = value.Split(':');
                        string target;
                        int port = 514;

                        target = values[0];

                        if (values.Length == 2)
                        {
                            port = Str.StrToInt(values[1]);
                        }

                        // 名前解決
                        IPAddress targetIp = Domain.GetIP(target)[0];

                        // 送信
                        s.SendTo(targetIp, port, data);

                        Console.WriteLine("{0}:{1} に転送", targetIp, port);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

            // メール送信
            if (ignore == false && config["SendMail"].BoolValue)
            {
                Entry en = new Entry();
                en.DateTime = now;
                en.SrcIp = ip;
                en.String = str;

                lock (entryList)
                {
                    entryList.Add(en);
                    lastEntryTick = Time.NowLongMillisecs;
                }
            }
        }

        void init()
        {
            halt = false;
            haltEvent = new Event(true);
            acceptThread = new ThreadObj(new ThreadProc(acceptThreadProc));

            acceptThread.WaitForInit();
        }

        public void Stop()
        {
            lock (this)
            {
                if (acceptThread != null)
                {
                    halt = true;
                    haltEvent.Set();

                    lock (socketLock)
                    {
                        try
                        {
                            sockv4.Disconnect();
                        }
                        catch
                        {
                        }
                        try
                        {
                            sockv6.Disconnect();
                        }
                        catch
                        {
                        }
                    }

                    acceptThread.WaitForEnd();
                    sendMailThread.WaitForEnd();

                    acceptThread = null;
                }
            }
        }
    }
}


