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
using CoreUtil;
using CoreUtil.Internal;

#if ASPNET
using Resources.CoreUtil.Properties;
#else
using CoreUtil.Properties;
#endif

namespace CoreUtil
{
	public class CdfFormManager
	{
		public const string FormIDPrefix = "cdf_form_manager_";

		public readonly string FormName;
		public readonly string[] SubFormName;
		public readonly string ParentUrl = null;

		public readonly Page Page;

		public static void ClearAll(Page page)
		{
			List<string> o = new List<string>();

			foreach (string name in page.Session.Keys)
			{
				if (name.StartsWith(FormIDPrefix, StringComparison.InvariantCultureIgnoreCase))
				{
					if (Str.InStr(name, "exclude_on_clear_all") == false)
					{
						o.Add(name);
					}
				}
			}

			foreach (string s in o)
			{
				page.Session[s] = null;
			}
		}

		// サブフォームの深さの取得
		public int SubFormDepth
		{
			get
			{
				return this.SubFormName.Length;
			}
		}

		// 最上位のフォームかどうか
		public bool IsTopForm
		{
			get
			{
				return (SubFormDepth == 0);
			}
		}

		// 最上位のフォーム ID の取得
		public string TopFormID
		{
			get
			{
				return FormIDPrefix + FormName + "_";
			}
		}

		// フォーム ID の取得
		public string FormID
		{
			get
			{
				StringBuilder sb = new StringBuilder();

				sb.Append(FormIDPrefix);
				sb.Append(FormName + "_");
				foreach (string s in this.SubFormName)
				{
					sb.Append("_" + s);
				}

				return sb.ToString();
			}
		}

		// データのクリア
		public void ClearData()
		{
			ClearData(false);
		}
		public void ClearData(bool clearTop)
		{
			List<string> o = new List<string>();
			string key = TopFormID;

			if (clearTop == false)
			{
				key = FormID;
			}

			foreach (string name in Page.Session.Keys)
			{
				if (name.StartsWith(key, StringComparison.InvariantCultureIgnoreCase))
				{
					o.Add(name);
				}
			}

			foreach (string s in o)
			{
				Page.Session[s] = null;
			}
		}

		// データの設定
		public void SetData(object data)
		{
			if (data == null)
			{
				ClearData();
				return;
			}

			SetData(data, data.GetType());
		}
		public void SetData(object data, Type type)
		{
			if (data == null)
			{
				ClearData();
				return;
			}

			Page.Session[FormID] = Str.ObjectToXMLSimple(data, type);
		}

		// データの取得
		public object GetData(Type type)
		{
			string s = (string)Page.Session[FormID];

			if (Str.IsEmptyStr(s))
			{
				return null;
			}

			return Str.XMLToObjectSimple(s, type);
		}

		// データが存在するかどうか取得
		public bool IsDataExists
		{
			get
			{
				return (Page.Session[FormID] != null);
			}
		}
		public bool IsNew
		{
			get
			{
				return !IsDataExists;
			}
		}
		public bool IsNewEx(Type type)
		{
			try
			{
				object o = GetData(type);
				if (o != null)
				{
					return false;
				}
			}
			catch
			{
			}

			return true;
		}

		// 文字列コレクション
		public string GetString(string name)
		{
			string s = (string)Page.Session[FormID + "!string!" + Str.MakeSafeFileName(name)];

			return s;
		}
		public void SetString(string name, string value)
		{
			Page.Session[FormID + "!string!" + Str.MakeSafeFileName(name)] = value;
		}

		// 引数
		public object Arg
		{
			get
			{
				object o = Page.Session[FormID + "!arg"];

				return o;
			}

			set
			{
				Page.Session[FormID + "!arg"] = value;
			}
		}
		public int IntArg
		{
			get
			{
				object o = Arg;

				if (o is int)
				{
					return (int)o;
				}
				else
				{
					return -1;
				}
			}

			set
			{
				Arg = value;
			}
		}

		// 上位データが存在するかどうか
		public bool IsParentNew
		{
			get
			{
				return !IsParentDataExists;
			}
		}
		public bool IsParentDataExists
		{
			get
			{
				CdfFormManager p = this.Parent;
				if (p == null)
				{
					return false;
				}

				return p.IsDataExists;
			}
		}

		// 上位を取得
		public CdfFormManager Parent
		{
			get
			{
				if (IsTopForm)
				{
					return null;
				}

				List<string> o = new List<string>();
				int i;

				for (i = 0; i < this.SubFormName.Length - 1; i++)
				{
					o.Add(this.SubFormName[i]);
				}

				return new CdfFormManager(this.Page, this.FormName, null, o.ToArray());
			}
		}

		// 下位を取得
		public CdfFormManager GetChild(string subFormName)
		{
			List<string> o = new List<string>();

			foreach (string s in this.SubFormName)
			{
				o.Add(s);
			}

			o.Add(subFormName);

			return new CdfFormManager(this.Page, this.FormName, null, o.ToArray());
		}

		public CdfFormManager(Page page, string formName)
			: this(page, formName, null, new string[0])
		{
		}
		public CdfFormManager(Page page, string formName, string parentPageUrl, params string[] subFormName)
		{
			this.FormName = formName;
			this.SubFormName = subFormName;
			this.ParentUrl = parentPageUrl;

			this.Page = page;

			if (IsParentNew)
			{
				RedirectToParent();
			}
		}

		public void RedirectToChild(string url)
		{
			RedirectToChild(url, null);
		}
		public void RedirectToChild(string url, ClickData cd)
		{
			/*if (cd != null && cd.Button != null)
			{
				this.SetString("lastfocus", cd.Button.ID);
			}
			else
			{
				this.SetString("lastfocus", null);
			}*/

			AspUtil.Redirect(Page, url, true);
		}

		public void RedirectToParent()
		{
			if (Str.IsEmptyStr(ParentUrl) == false)
			{
				AspUtil.Redirect(Page, ParentUrl, true);
			}
		}
	}
}

