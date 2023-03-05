// CoreUtil
// 
// Copyright (C) 1997-2010 Daiyuu Nobori. All Rights Reserved.
// Copyright (C) 2004-2010 SoftEther Corporation. All Rights Reserved.

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
using System.Security.Cryptography;
using System.Diagnostics;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Security.Principal;
using System.Xml.Linq;

namespace CoreUtil
{
    [Serializable]
    public class EventReaderStatus
    {
        public string EventLogName;
        public long NumIndex;

        public override string ToString()
        {
            return Str.ObjectToXMLString(this);
        }

        public static EventReaderStatus FromString(string str)
        {
            try
            {
                return (EventReaderStatus)Str.XMLStringToObject(str);
            }
            catch
            {
                return null;
            }
        }
    }

    public class EventReaderEntry
    {
        public string Message;
        public DateTime TimeGenerated;
        public int EventID;
        public long Index;
        public string Source;
        public string UserName;
    }

    public class EventReaderResults
    {
        public readonly EventReaderEntry[] Entries;
        public readonly EventReaderStatus Status;

        public EventReaderResults(EventReaderEntry[] entries, EventReaderStatus status)
        {
            this.Entries = entries;
            this.Status = status;
        }
    }

    public class EventReader
    {
        readonly EventLogQuery log;
        public readonly string EventLogName;

        public const int DefaultMaxEntries = 1000;


        public EventReader(string eventLogName)
        {
            this.EventLogName = eventLogName;
            log = new EventLogQuery(this.EventLogName, PathType.LogName, "*");
        }

        public EventReaderResults Poll(EventReaderStatus status)
        {
            return Poll(status, 0);
        }
        public EventReaderResults Poll(EventReaderStatus status, int maxEntries)
        {
            if (maxEntries == 0)
            {
                maxEntries = DefaultMaxEntries;
            }
            if (status == null || this.EventLogName.Equals(status.EventLogName, StringComparison.InvariantCultureIgnoreCase) == false)
            {
                status = new EventReaderStatus();
                status.EventLogName = this.EventLogName;
                status.NumIndex = 0;
            }

            try
            {
                using (EventLogReader reader = new EventLogReader(this.log))
                {
                    List<EventRecord> list = new List<EventRecord>();

                    reader.Seek(SeekOrigin.End, -maxEntries);

                    while (list.Count < maxEntries)
                    {
                        var record = reader.ReadEvent();
                        if (record == null)
                        {
                            break;
                        }

                        bool skip = false;

                        if (record.ProviderName.Equals("Microsoft-Windows-Kernel-General", StringComparison.OrdinalIgnoreCase) && (record.Id == 1 || record.Id == 24))
                        {
                            // Windows Time kernel message: skip
                            skip = true;
                        }

                        if (skip == false)
                        {
                            list.Add(record);
                        }
                    }

                    list.Reverse();

                    long lastIndex = 0;
                    if (status.NumIndex != 0 && list.Max(x => x.RecordId).GetValueOrDefault(0) >= status.NumIndex)
                    {
                        lastIndex = status.NumIndex;
                    }

                    List<EventReaderEntry> retList = new List<EventReaderEntry>();

                    foreach (var r in list)
                    {
                        try
                        {
                            if (r.RecordId.GetValueOrDefault(0) >= 1)
                            {
                                if (r.RecordId <= lastIndex)
                                {
                                    break;
                                }

                                string userName = "";
                                try
                                {
                                    if (r.UserId != null)
                                    {
                                        userName = r.UserId.Translate(typeof(NTAccount)).ToString();
                                    }
                                }
                                catch { }

                                var e = new EventReaderEntry()
                                {
                                    EventID = r.Id,
                                    Index = r.RecordId.GetValueOrDefault(0),
                                    Message = r.FormatDescription(),
                                    Source = r.ProviderName,
                                    TimeGenerated = r.TimeCreated.GetValueOrDefault(Cdf.ZeroDateTimeValue),
                                    UserName = userName,
                                };

                                if (e.Message == null)
                                {
                                    //if (e.Source == "VSTTExecution")
                                    {
                                        //if (r.RecordId == 33629)
                                        {
                                            string xmlTmp = r.ToXml();

                                            // メッセージ変換 DLL がない場合、適当に XML から Data を抽出する
                                            // 参考: https://stackoverflow.com/questions/67762516/how-to-convert-eventrecord-xml-to-dictionary-includes-all-parameters-c-sharp
                                            {
                                                XDocument doc = XDocument.Parse(xmlTmp);
                                                Dictionary<string, string> dataDictionary = new Dictionary<string, string>();
                                                foreach (XElement element in doc.Descendants().Where(p => p.HasElements == false))
                                                {
                                                    int keyInt = 0;
                                                    string keyName = element.Name.LocalName;
                                                    while (dataDictionary.ContainsKey(keyName))
                                                    {
                                                        keyName = element.Name.LocalName + "_" + keyInt++;
                                                    }
                                                    dataDictionary.Add(keyName, element.Value);

                                                    if (element.HasAttributes)
                                                    {
                                                        var lmsAttribute = element.FirstAttribute;
                                                        if (lmsAttribute != null)
                                                        {
                                                            dataDictionary.Add($"{keyName}_{lmsAttribute.Name.LocalName}", lmsAttribute.Value);
                                                        }
                                                    }
                                                }

                                                if (dataDictionary.TryGetValue("Data", out string dataStr))
                                                {
                                                    e.Message = $"Event_Raw_Text: {dataStr}";
                                                }
                                            }
                                        }
                                    }
                                }

                                retList.Add(e);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                    }

                    EventReaderStatus retStatus = new EventReaderStatus();
                    retStatus.EventLogName = this.EventLogName;
                    retStatus.NumIndex = 0;

                    EventReaderResults ret = new EventReaderResults(retList.ToArray(), retStatus);

                    if (retList.Any())
                    {
                        ret.Status.NumIndex = retList[0].Index;
                    }
                    else
                    {
                        ret.Status.NumIndex = status.NumIndex;
                    }

                    return ret;
                }
            }
            catch
            {
                EventReaderStatus retStatus = new EventReaderStatus();
                retStatus.EventLogName = this.EventLogName;
                retStatus.NumIndex = 0;

                return new EventReaderResults(new EventReaderEntry[0], retStatus);
            }
        }

        public static string GetEventLogMessageDll(EventLogEntry entry, EventLog owner)
        {
            Type eventLogEntryClassType = typeof(EventLogEntry);
            int id = (int)entry.InstanceId;
            id = id & 0x3fffffff;
            object[] args1 =
                {
                    "EventMessageFile",
                };
            string messageLibraryNames =
                (string)eventLogEntryClassType.GetMethod("GetMessageLibraryNames",
                BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(entry, args1);

            if (messageLibraryNames != null)
            {
                return messageLibraryNames;
            }

            string regKeyName = string.Format(@"SYSTEM\CurrentControlSet\Services\Eventlog\{0}\{1}",
                owner.Log,
                entry.Source);

            string value = Reg.ReadStr(RegRoot.LocalMachine, regKeyName, "EventMessageFile");
            if (Str.IsEmptyStr(value))
            {
                value = Reg.ReadStr(RegRoot.LocalMachine, regKeyName, "CategoryMessageFile");
                if (Str.IsEmptyStr(value))
                {
                    string guid = Reg.ReadStr(RegRoot.LocalMachine, regKeyName, "ProviderGuid");
                    if (Str.IsEmptyStr(guid) == false)
                    {
                        string key2 = string.Format(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WINEVT\Publishers\{0}", guid);
                        value = Reg.ReadStr(RegRoot.LocalMachine, key2, "MessageFileName");
                    }
                }
            }

            return value;
        }

        public static string GetEventLogMessage(EventLogEntry entry)
        {
            Type eventLogEntryClassType = typeof(EventLogEntry);
            int id = (int)entry.InstanceId;
            EventLog owner = (EventLog)eventLogEntryClassType.GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(entry);
            string messageLibraryNames = GetEventLogMessageDll(entry, owner);
            Console.WriteLine(messageLibraryNames);

            Type eventLogClassType = typeof(EventLog);
            object[] args2 =
                {
                    messageLibraryNames,
                    (uint)id,
                    entry.ReplacementStrings,
                };
            string msg =
                (string)eventLogClassType.GetMethod("FormatMessageWrapper",
                BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, args2);

            if (Str.IsEmptyStr(msg))
            {
                msg = entry.Message;
            }

            return msg;
        }
    }
}
