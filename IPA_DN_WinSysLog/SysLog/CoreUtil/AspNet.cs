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
using System.Xml;
using System.Xml.Serialization;
using CoreUtil;
using CoreUtil.Internal;

// Web アプリクラス
public partial class App
{
	public void Test2()
	{
	}
}

// 標準ページクラス
public class AspPage
{
	Page page;
	const string AspItemPageName = "AspPage_ItemName";
	const string CookieNamePrefix = "se_aspnet2015_cookie_";
	public readonly string PhysicalFileName;
	public readonly string PhysicalFileBody;
	public const int SqlTimeout = 120;
	public readonly TimeSpan CookieLifeTime = new TimeSpan(120, 0, 0, 0);

	// Web アプリインスタンス
	App app = null;
	public App App
	{
		get
		{
			return this.app;
		}
	}

	public delegate App LoadPageDelegate(AspPage asp);
	static LoadPageDelegate load_page_delegate = null;

	// ページ変数
	public readonly string HttpReferer;
	public readonly string HttpUserAgent;
	public readonly string HttpHostName;
	public readonly string HttpClientIp;
	public readonly string HttpCookieDomainName;
	public readonly string HttpQueryStringAll;
	public readonly string HttpUrl;
	public readonly string Title;
	public readonly string TitleOnHeader;
	public readonly string SiteTitle;

	public Page Page
	{
		get { return page; }
	}

	// 静的初期化メソッド
	public static void InitAspNet(LoadPageDelegate load_proc)
	{
		AspPage.load_page_delegate = load_proc;
	}

	// コンストラクタ
	public AspPage(Page page)
	{
		this.page = page;

		// ページに関連付け
		page.Items[AspItemPageName] = this;

		// HTML ファイル名および本文を読み込む
		try
		{
			this.PhysicalFileName = AspUtil.WebPathToFilePath(this.Page, this.Page.Request.Path);
			this.PhysicalFileBody = IO.ReadAllTextWithAutoGetEncoding(this.PhysicalFileName);
		}
		catch
		{
		}

		// サイトのタイトル
		this.SiteTitle = AppSetting("site_title");

		// 変数を読み込む
		try
		{
			this.HttpReferer = Str.NormalizeString(this.Page.Request.UrlReferrer.ToString());
		}
		catch
		{
			this.HttpReferer = "";
		}
		this.HttpUserAgent = Str.NormalizeString(this.Page.Request.UserAgent);
		this.HttpHostName = Str.NormalizeString(AspUtil.RemovePortFromHostHeader(Str.NormalizeString((string)page.Request.Headers["Host"])));
		this.HttpClientIp = Str.NormalizeString(this.Page.Request.UserHostAddress);
		//this.HttpCookieDomainName = Str.NormalizeString(this.HttpHostName.ToLowerInvariant());
		this.HttpQueryStringAll = Str.NormalizeString(this.GetServerVariables("QUERY_STRING"));

		// アクセスされた URL
		this.HttpUrl = AspUtil.RemoveDefaultHtml(this.Page.Request.Path);
		if (Str.IsEmptyStr(this.HttpQueryStringAll) == false)
		{
			this.HttpUrl += "?" + this.HttpQueryStringAll;
		}

		// HTML 上のページタイトルの取得
		this.Title = AspUtil.GetTitleFromHtml(this.PhysicalFileBody);
		this.TitleOnHeader = AspUtil.GetTitleFromHtml(this.PhysicalFileBody, true);

		// ページタイトルにサイトタイトルを追加
		SetTitle(this.TitleOnHeader);

		// アプリのインスタンスを作成
		if (AspPage.load_page_delegate != null)
		{
			this.app = AspPage.load_page_delegate(this);
		}
	}

	// 対象オブジェクト名の設定
	string target_object_name = "";
	public string TargetObjectName
	{
		set
		{
			this.target_object_name = Str.NormalizeString(value);

			SetTitle(Str.CombineStringArray2(" - ", this.target_object_name, this.TitleOnHeader));
		}
		get
		{
			return this.target_object_name;
		}
	}

	public string TargetObjectNameHtml
	{
		get
		{
			return Str.ToHtml(TargetObjectName);
		}
	}

	// HTML タイトルの設定
	public void SetTitle(string title)
	{
		string new_title = Str.CombineStringArray2(" - ", title, this.SiteTitle);

		this.Page.Header.Title = Str.ToHtml(new_title);
	}

	// アプリ文字列の取得
	public static string AppSetting(string name)
	{
		return Str.NormalizeString(ConfigurationManager.AppSettings[name]);
	}

	// SQL 接続文字列の取得
	public static string ConnectionString(string name)
	{
		try
		{
			return Str.NormalizeString(ConfigurationManager.ConnectionStrings[name].ConnectionString);
		}
		catch
		{
			return "";
		}
	}

	// QueryString の取得
	public string QueryString(string name)
	{
		return Str.NormalizeString(this.Page.Request.QueryString[name]);
	}
	public int QueryStringInt(string name)
	{
		return QueryStringInt(name, 0);
	}
	public int QueryStringInt(string name, int default_value)
	{
		string str = QueryString(name);
		if (Str.IsEmptyStr(str) == false)
		{
			return Str.StrToInt(str);
		}
		else
		{
			return default_value;
		}
	}
	public long QueryStringLong(string name)
	{
		return Str.StrToLong(QueryString(name));
	}
	public bool QueryStringBool(string name)
	{
		return Str.StrToBool(QueryString(name));
	}

	// フォーム文字列の取得
	public string Form(string name)
	{
		return Str.NormalizeString(this.Page.Request.Form[name]);
	}
	public int FormInt(string name)
	{
		return Str.StrToInt(Form(name));
	}
	public long FormLong(string name)
	{
		return Str.StrToLong(Form(name));
	}
	public bool FormBool(string name)
	{
		return Str.StrToBool(Form(name));
	}
	public bool FormChecked(string name)
	{
		string s = Form(name);
		return !Str.IsEmptyStr(s);
	}

	// フォーム文字列または QueryString 文字列 (PostBack 以外の場合) の取得
	public string FormOrInitQueryString(string name)
	{
		return FormOrInitQueryString(name, null);
	}
	public string FormOrInitQueryString(string name, string default_value)
	{
		Str.NormalizeString(ref default_value);

		if (this.Page.IsPostBack)
		{
			return Form(name);
		}

		string s = QueryString(name);
		if (Str.IsEmptyStr(s))
		{
			s = default_value;
		}
		if (Str.IsEmptyStr(s) == false)
		{
			return s;
		}

		return Form(name);
	}

	// QueryString の取得 (PostBack 以外)
	public string QueryStringInit(string name)
	{
		return QueryStringInit(name, null);
	}
	public string QueryStringInit(string name, string default_value)
	{
		Str.NormalizeString(ref default_value);
		if (this.Page.IsPostBack)
		{
			return "";
		}

		string s = QueryString(name);
		if (Str.IsEmptyStr(s))
		{
			s = default_value;
		}

		return s;
	}

	// PostBack でない場合、DropDownList を QueryString に併せて自動選択
	public void DropDownListAutoSelectByQueryString(DropDownList dl)
	{
		DropDownListAutoSelectByQueryString(dl, null);
	}
	public void DropDownListAutoSelectByQueryString(DropDownList dl, string default_value)
	{
		Str.NormalizeString(ref default_value);

		if (Str.IsEmptyStr(dl.ID))
		{
			return;
		}
		string s = QueryStringInit(dl.ID);
		if (Str.IsEmptyStr(s))
		{
			s = default_value;
		}
		if (Str.IsEmptyStr(s))
		{
			return;
		}

		try
		{
			dl.SelectedValue = s;
		}
		catch
		{
		}
	}

	// クッキー文字列の設定
	public void SetCookie(string name, string value, bool save)
	{
		string cookie_name = CookieNamePrefix + Str.NormalizeString(name).ToLowerInvariant();
		HttpCookie c = Page.Request.Cookies[cookie_name];
		if (c == null)
		{
			c = new HttpCookie(cookie_name, value);
		}
		else
		{
			Page.Response.Cookies.Remove(cookie_name);
		}

		if (save)
		{
			c.Expires = DateTime.Now + CookieLifeTime;
		}

		c.Value = value;
		if (Str.IsEmptyStr(this.HttpCookieDomainName) == false)
		{
			c.Domain = this.HttpCookieDomainName;
		}
		c.Path = "/";
		page.Response.Cookies.Add(c);
	}

	// クッキー文字列の取得
	public string Cookie(string name)
	{
		string cookie_name = CookieNamePrefix + Str.NormalizeString(name).ToLowerInvariant();
		HttpCookie c = Page.Request.Cookies[cookie_name];
		if (c == null)
		{
			return "";
		}

		string ret = "";

		try
		{
			ret = c.Value;

			if (c.Expires.Ticks != 0)
			{
				c.Expires = DateTime.Now + CookieLifeTime;

				c.Domain = this.HttpCookieDomainName;
				c.Path = "/";
				page.Response.Cookies.Add(c);
			}
		}
		catch
		{
		}

		return ret;
	}
	public int CookieInt(string name)
	{
		return Str.StrToInt(Cookie(name));
	}
	public long CookieLong(string name)
	{
		return Str.StrToLong(Cookie(name));
	}
	public bool CookieBool(string name)
	{
		return Str.StrToBool(Cookie(name));
	}

	// HTTP リクエスト全文
	public string HttpRequestFullText
	{
		get
		{
			StringBuilder sb = new StringBuilder();

			HttpRequest req = page.Request;

			int i;

			for (i = 0; i < req.ServerVariables.Count; i++)
			{
				string key = req.ServerVariables.Keys[i];
				string value = req.ServerVariables[i];
				sb.AppendLine(string.Format("{0}: \"{1}\"", key, value));
			}

			Stream st = req.InputStream;
			long stPos = st.Position;
			long size2 = st.Length;
			int size = (int)Math.Min(size2, int.MaxValue);

			byte[] data = new byte[size];
			st.Read(data, 0, data.Length);

			st.Position = stPos;

			sb.AppendLine(string.Format("[POST_DATA] SIZE={0}", size));
			sb.AppendLine(Str.Utf8Encoding.GetString(data));

			return sb.ToString();
		}
	}

	// HTTP コントロールデータ一覧
	public string HttpControlFullText
	{
		get
		{
			StringBuilder sb = new StringBuilder();

			enumControls(sb, page);

			return sb.ToString();
		}
	}
	void enumControls(StringBuilder sb, Control c)
	{
		int num = c.Controls.Count;

		int i;

		if (c is WebControl)
		{
			processControl(sb, (WebControl)c);
		}

		for (i = 0; i < num; i++)
		{
			Control c2 = c.Controls[i];

			enumControls(sb, c2);
		}
	}

	void processControl(StringBuilder sb, WebControl ctl)
	{
		try
		{
			sb.AppendLine(string.Format("\"{0}\"({1})=\"{2}\"",
				ctl.ID,
				ctl.GetType().Name,
				controlToText(ctl)));
		}
		catch
		{
		}
	}

	static string controlToText(WebControl ctl)
	{
		if (ctl == null)
		{
			return "null";
		}
		if (ctl is Label)
		{
			return ((Label)ctl).Text;
		}
		if (ctl is Button)
		{
			return ((Button)ctl).Text;
		}
		if (ctl is LinkButton)
		{
			return ((LinkButton)ctl).Text;
		}
		if (ctl is Calendar)
		{
			return ((Calendar)ctl).SelectedDate.ToString();
		}
		if (ctl is CheckBox)
		{
			return ((CheckBox)ctl).Checked.ToString();
		}
		if (ctl is CheckBoxList)
		{
			string ret = "";
			CheckBoxList cb = (CheckBoxList)ctl;

			int i;
			for (i = 0; i < cb.Items.Count; i++)
			{
				ListItem item = cb.Items[i];

				ret += string.Format("{0}({1})={2} ", item.Value, item.Text, item.Selected);
			}

			return ret;
		}
		if (ctl is RadioButton)
		{
			return ((RadioButton)ctl).Text;
		}
		if (ctl is RadioButtonList)
		{
			RadioButtonList rb = (RadioButtonList)ctl;

			ListItem i = rb.SelectedItem;

			if (i == null)
			{
				return "null";
			}
			else
			{
				return i.Value + "(" + i.Text + ")";
			}
		}
		if (ctl is LinkButton)
		{
			return ((LinkButton)ctl).Text;
		}
		if (ctl is FileUpload)
		{
			return ((FileUpload)ctl).FileName;
		}
		if (ctl is HyperLink)
		{
			return ((HyperLink)ctl).Text + " " + ((HyperLink)ctl).NavigateUrl;
		}
		if (ctl is TextBox)
		{
			return ((TextBox)ctl).Text;
		}
		if (ctl is DropDownList)
		{
			DropDownList dd = (DropDownList)ctl;

			if (dd.SelectedItem == null)
			{
				return "null";
			}
			else
			{
				return dd.SelectedItem.Value;
			}
		}
		if (ctl is ListBox)
		{
			string ret = "";
			ListBox lb = (ListBox)ctl;

			int i;
			for (i = 0; i < lb.Items.Count; i++)
			{
				ListItem item = lb.Items[i];

				ret += string.Format("{0}({1})={2} ", item.Value, item.Text, item.Selected);
			}

			return ret;
		}

		return "unknown";
	}

	// テキストボックスをフォーカスする
	public void SetTextBoxAutoFocus(TextBox t)
	{
		t.Attributes["onfocus"] = "this.select();";
	}

	// ボタンに確認メッセージを追加する
	public void AddConfirmMessageToButton(Button b)
	{
		AddConfirmMessageToButton(b, "この内容で送信します。よろしいですか?");
	}
	public void AddConfirmMessageToButton(Button b, string msg)
	{
		b.Attributes["onclick"] = "return confirm('" + msg + "');";
	}

	// リダイレクト
	public void Redirect(string url)
	{
		this.Page.Response.Redirect(url);
	}

	// サーバー変数の取得
	public string GetServerVariables(string name)
	{
		return Str.NormalizeString(this.Page.Request.ServerVariables[name]);
	}

	// ページの Load プロシージャからこれを呼び出すとよい
	public static void OnLoading(Page page)
	{
		InitSqlTimeout(page);
	}

	// SqlDataSource のタイムアウトを設定する
	public static void InitSqlTimeout(Page page)
	{
		foreach (Control forms in page.Controls)
		{
			foreach (Control c in forms.Controls)
			{
				if (c != null)
				{
					SqlDataSource s = null;

					try
					{
						s = (SqlDataSource)c;
					}
					catch
					{
					}

					if (s != null)
					{
						s.Selecting += new SqlDataSourceSelectingEventHandler(setAllSqlDataSourceTimeoutSelecting);
					}
				}
			}
		}
	}
	static void setAllSqlDataSourceTimeoutSelecting(object sender, SqlDataSourceSelectingEventArgs e)
	{
		e.Command.CommandTimeout = AspPage.SqlTimeout;
	}

	// Navi の文字列の生成
	public string GetNaviStr()
	{
		System.Web.UI.Page page = this.Page;

		// 現在のパスの取得
		string currentPathBkup;
		string currentPath = AspUtil.RemoveDefaultHtml(page.Request.Path);
		currentPathBkup = currentPath;

		// 現在のパスからルートパスまでのリストを生成する
		ArrayList list = new ArrayList();

		if (currentPath.StartsWith("/"))
		{
			currentPath = currentPath.Substring(1);
		}

		string[] strs = currentPath.Split('/');

		list.Add("/");	// トップページからはじまる場合 (/jp/ などがトップでない場合)

		int i, j;
		for (i = 1; i < strs.Length; i++)
		{
			string tmp = "";
			for (j = 0; j < i; j++)
			{
				tmp += "/" + strs[j];
			}
			if (list.Contains(tmp + "/") == false)
			{
				list.Add(tmp + "/");
			}
		}

		if (currentPath.EndsWith("/") == false)
		{
			if (list.Contains("/" + currentPath) == false)
			{
				list.Add("/" + currentPath);
			}
		}

		string[] paths = new string[list.Count];
		list.CopyTo(paths);

		string ret = "";
		int strWidth = 0;

		for (i = 0;i < paths.Length;i++)
		{
			string virtualPath = paths[i];
			bool is_last = ((paths.Length - 1) == i);

			string filePath = AspUtil.WebPathToFilePath(page, virtualPath);
			if (filePath != null)
			{
				string title = Str.NormalizeString(AspUtil.GetTitleFromHtmlFile(filePath));

				if (is_last)
				{
					if (Str.IsEmptyStr(this.TargetObjectName) == false)
					{
						title = Str.CombineStringArray2(" - ", title, this.TargetObjectName);
					}
				}

				if (Str.IsEmptyStr(title) == false)
				{
					if (ret != "")
					{
						ret += " <img src=\"" + NormalizeUrl("~/common_images/arrow_navi.gif") + "\"> ";
					}
					if (currentPathBkup != virtualPath || this.Page.IsPostBack)
					{
						ret += "<a href=\"" + virtualPath + "\">";
					}
					else
					{
						ret += "<font color=\"#222299\">";
					}
					ret += HttpUtility.HtmlEncode(title);
					if (currentPathBkup != virtualPath)
					{
						ret += "</a>";
					}
					else
					{
						ret += "</font>";
					}
					strWidth += title.Length;
				}
			}
		}
		return "<b>" + ret + "</b>";
	}

	// 相対 URL を絶対 URL に変換する
	public string NormalizeUrl(string url)
	{
		return System.Web.VirtualPathUtility.ToAbsolute(url);
	}
	
	// まだオブジェクトが作成されていないようであれば作成する
	public static AspPage GetObject(Page page)
	{
		if (page.Items[AspItemPageName] == null)
		{
			return new AspPage(page);
		}
		else
		{
			return (AspPage)page.Items[AspItemPageName];
		}
	}

	// URL に付加する return url の文字列を作成する
	public string GetReturnUrlStr(string url_for_postback)
	{
		try
		{
			string url = this.HttpUrl;
			if (this.Page.IsPostBack)
			{
				if (Str.IsEmptyStr(url_for_postback) == false)
				{
					url = url_for_postback;
				}
			}

			return Str.ByteToHex(Str.Utf8Encoding.GetBytes(url));
		}
		catch
		{
			return "";
		}
	}

	// return url の文字列を復元する
	public string RestoreReturnUrlStr(string s)
	{
		try
		{
			if (Str.IsEmptyStr(s))
			{
				return "";
			}

			return Str.Utf8Encoding.GetString(Str.HexToByte(s));
		}
		catch
		{
			return "";
		}
	}

	// return url にジャンプする
	public void RedirectToReturnUrl(string s, string default_url)
	{
		string url = RestoreReturnUrlStr(s);
		if (Str.IsEmptyStr(url))
		{
			url = default_url;
		}
		this.Redirect(url);
	}
}

