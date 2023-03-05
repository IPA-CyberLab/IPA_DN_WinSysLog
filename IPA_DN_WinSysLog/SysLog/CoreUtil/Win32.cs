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
using System.Web.Mail;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using System.DirectoryServices;
using CoreUtil;
using CoreUtil.Internal;

namespace CoreUtil
{
	public static class Win32
	{
		// 初期化
		static Win32()
		{
		}

		// ユーザーの作成
		public static void CreateUser(string machineName, string userName, string password, string description)
		{
			Str.NormalizeString(ref userName);
			Str.NormalizeString(ref password);
			Str.NormalizeString(ref description);

			using (DirectoryEntry sam = OpenSam(machineName))
			{
				using (DirectoryEntry newUser = sam.Children.Add(userName, "user"))
				{
					newUser.Invoke("SetPassword", new object[] { password });
					newUser.Invoke("Put", new object[] { "Description", description });
					newUser.CommitChanges();
					Console.WriteLine(newUser.Path);
				}
			}

			try
			{
				AddUserToGroup(machineName, userName, "Users");
			}
			catch
			{
			}
		}

		// ユーザーのパスワードを変更
		public static void ChangeUserPassword(string machineName, string userName, string oldPassword, string newPassword)
		{
			Str.NormalizeString(ref userName);
			Str.NormalizeString(ref oldPassword);
			Str.NormalizeString(ref newPassword);

			using (DirectoryEntry sam = OpenSam(machineName))
			{
				using (DirectoryEntry user = sam.Children.Find(userName, "user"))
				{
					user.Invoke("ChangePassword", oldPassword, newPassword);
				}
			}
		}

		// ユーザーのパスワードを強制変更
		public static void SetUserPassword(string machineName, string userName, string password)
		{
			Str.NormalizeString(ref userName);
			Str.NormalizeString(ref password);

			using (DirectoryEntry sam = OpenSam(machineName))
			{
				using (DirectoryEntry user = sam.Children.Find(userName, "user"))
				{
					user.Invoke("SetPassword", password);
				}
			}
		}

		// グループ内のユーザー一覧を取得
		public static string[] GetMembersOfGroup(string machineName, string groupName)
		{
			List<string> ret = new List<string>();

			Str.NormalizeString(ref groupName);

			using (DirectoryEntry sam = OpenSam(machineName))
			{
				using (DirectoryEntry g = sam.Children.Find(groupName, "group"))
				{
					object members = g.Invoke("Members", null);

					foreach (object member in (IEnumerable)members)
					{
						using (DirectoryEntry e = new DirectoryEntry(member))
						{
							ret.Add(e.Name);
						}
					}

					return ret.ToArray();
				}
			}
		}

		// ユーザーがグループのメンバーかどうか取得
		public static bool IsUserMemberOfGroup(string machineName, string userName, string groupName)
		{
			Str.NormalizeString(ref userName);
			Str.NormalizeString(ref groupName);

			using (DirectoryEntry sam = OpenSam(machineName))
			{
				using (DirectoryEntry g = sam.Children.Find(groupName, "group"))
				{
					using (DirectoryEntry u = sam.Children.Find(userName, "user"))
					{
						return (bool)g.Invoke("IsMember", u.Path);
					}
				}
			}
		}

		// ユーザーをグループから削除
		public static void DeleteUserFromGroup(string machineName, string userName, string groupName)
		{
			Str.NormalizeString(ref userName);
			Str.NormalizeString(ref groupName);

			using (DirectoryEntry sam = OpenSam(machineName))
			{
				using (DirectoryEntry g = sam.Children.Find(groupName, "group"))
				{
					using (DirectoryEntry u = sam.Children.Find(userName, "user"))
					{
						g.Invoke("Remove", u.Path);
					}
				}
			}
		}

		// ユーザーをグループに追加
		public static void AddUserToGroup(string machineName, string userName, string groupName)
		{
			Str.NormalizeString(ref userName);
			Str.NormalizeString(ref groupName);

			using (DirectoryEntry sam = OpenSam(machineName))
			{
				using (DirectoryEntry g = sam.Children.Find(groupName, "group"))
				{
					using (DirectoryEntry u = sam.Children.Find(userName, "user"))
					{
						g.Invoke("Add", u.Path);
					}
				}
			}
		}

		// ユーザーの削除
		public static void DeleteUser(string machineName, string userName)
		{
			Str.NormalizeString(ref userName);

			using (DirectoryEntry sam = OpenSam(machineName))
			{
				using (DirectoryEntry u = sam.Children.Find(userName, "user"))
				{
					sam.Children.Remove(u);
				}
			}
		}

		// ユーザーが存在するかどうか取得
		public static bool IsUserExists(string machineName, string userName)
		{
			Str.NormalizeString(ref userName);

			using (DirectoryEntry sam = OpenSam(machineName))
			{
				try
				{
					using (DirectoryEntry user = sam.Children.Find(userName, "user"))
					{
						if (user == null)
						{
							return false;
						}

						return true;
					}
				}
				catch (COMException ce)
				{
					if ((uint)ce.ErrorCode == 0x800708AD)
					{
						return false;
					}
					else
					{
						throw;
					}
				}
			}
		}

		// SAM を開く
		public static DirectoryEntry OpenSam()
		{
			return OpenSam(null);
		}
		public static DirectoryEntry OpenSam(string machineName)
		{
			if (Str.IsEmptyStr(machineName))
			{
				machineName = Env.MachineName;
			}

			return new DirectoryEntry(string.Format("WinNT://{0},computer",
				machineName));
		}
	}
}
