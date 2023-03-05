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
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Reflection;

namespace CoreUtil
{
	[Serializable]
	public class EventReaderStatus
	{
		public string EventLogName;
		public int NumIndex;

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

	public class EventReaderResults
	{
		public readonly EventLogEntry[] Entries;
		public readonly EventReaderStatus Status;

		public EventReaderResults(EventLogEntry[] entries, EventReaderStatus status)
		{
			this.Entries = entries;
			this.Status = status;
		}
	}

	public class EventReader
	{
		EventLog log;
		public const int DefaultMaxEntries = 100;

		public EventReader(string eventLogName)
		{
			log = new EventLog(eventLogName);
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
			if (status == null || log.Log.Equals(status.EventLogName, StringComparison.InvariantCultureIgnoreCase) == false)
			{
				status = new EventReaderStatus();
				status.EventLogName = log.Log;
				status.NumIndex = 0;
			}

			try
			{
				List<EventLogEntry> list = new List<EventLogEntry>();
				int lastIndex = 0;
				if (status.NumIndex != 0 && log.Entries[log.Entries.Count - 1].Index >= status.NumIndex)
				{
					lastIndex = status.NumIndex;
				}

				int i, num;
				num = log.Entries.Count;

				for (i = num - 1; i >= 0; i--)
				{
					EventLogEntry e = log.Entries[i];

					if (e.Index <= lastIndex)
					{
						break;
					}

					list.Add(e);

					if (list.Count >= maxEntries)
					{
						break;
					}
				}

				EventReaderStatus retStatus = new EventReaderStatus();
				retStatus.EventLogName = log.Log;
				retStatus.NumIndex = 0;

				EventLogEntry[] ee = list.ToArray();

				EventReaderResults ret = new EventReaderResults(ee, retStatus);

				if (ee.Length >= 1)
				{
					ret.Status.NumIndex = ee[0].Index;
				}
				else
				{
					ret.Status.NumIndex = status.NumIndex;
				}

				return ret;
			}
			catch
			{
				EventReaderStatus retStatus = new EventReaderStatus();
				retStatus.EventLogName = log.Log;
				retStatus.NumIndex = 0;

				return new EventReaderResults(new EventLogEntry[0], retStatus);
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
