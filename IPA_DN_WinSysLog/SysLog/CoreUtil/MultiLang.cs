﻿// 多言語対応
using System;
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
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using CoreUtil;

// ASP.NET ユーティリティ
public static class AspUtil
{
	// 文字列を表示できる形式に整形する
	public static string NormalizeStringToHtml(string s)
	{
		return CrLfToBR(TabAndSpaceToTag(HttpUtility.HtmlEncode(s)));
	}

	// タブやスペースを対応する文字に変換する
	public static string TabAndSpaceToTag(string s)
	{
		return s.Replace("\t", "    ").Replace(" ", "&nbsp;");
	}

	// 改行を <BR> に変換する
	public static string CrLfToBR(string s)
	{
		char[] splitters = { '\r', '\n' };
		string[] lines = s.Split(splitters, StringSplitOptions.RemoveEmptyEntries);

		StringBuilder b = new StringBuilder();
		foreach (string line in lines)
		{
			b.AppendLine(line + "<BR>");
		}

		return b.ToString();
	}

	// 指定された HTML のタイトルを取得する
	public static string GetTitleFromHtml(string src)
	{
		return GetTitleFromHtml(src, false);
	}
	public static string GetTitleFromHtml(string src, bool no_alternative)
	{
		string tmp;
		string upper;
		int i;

		if (no_alternative == false)
		{
			string at = GetAlternativeTitleFromHtml(src);
			if (Str.IsEmptyStr(at) == false)
			{
				return at;
			}
		}

		upper = src.ToLower();
		i = upper.IndexOf("</title>");
		if (i == -1)
		{
			return "";
		}

		tmp = src.Substring(0, i);

		i = tmp.IndexOf("<title>");
		if (i == -1)
		{
			return "";
		}

		return tmp.Substring(i + 7);
	}
	public static string GetTitleFromHtmlFile(string filename)
	{
		string body = IO.ReadAllTextWithAutoGetEncoding(filename);

		return GetTitleFromHtml(body);
	}

	// 指定された HTML のタイトルを取得する
	public static string GetAlternativeTitleFromHtml(string src)
	{
		string tmp;
		string upper;
		int i;

		upper = src.ToLower();
		i = upper.IndexOf("</at>");
		if (i == -1)
		{
			return null;
		}

		tmp = src.Substring(0, i);

		i = tmp.IndexOf("<at>");
		if (i == -1)
		{
			return null;
		}

		string ret = tmp.Substring(i + 4);

		if (ret.Length == 0)
		{
			return null;
		}
		else
		{
			return ret;
		}
	}

	public static void Redirect(Page page, string url)
	{
		Redirect(page, url, true);
	}
	public static void Redirect(Page page, string url, bool endSession)
	{
		MultiLang ml = new MultiLang(page, true);

		ml.Redirect(url, true);
	}

	public static string GetCurrentRequestUrl(Page page)
	{
		string s = (string)page.Request.Headers["SEISAPI_PHYSICAL_URL"];
		if (Str.IsEmptyStr(s) == false)
		{
			string[] tokens = s.Split('?');
			return tokens[0];
		}
		return page.Request.Path;
	}

	public static string GetCurrentPhysicalFilePathForUser(Page page)
	{
		string s = (string)page.Request.Headers["SEISAPI_ORIGINAL_FILEPATH"];
		if (Str.IsEmptyStr(s) == false)
		{
			return s;
		}
		return page.Request.PhysicalPath;
	}

	// URL が Default.aspx を指す場合は Default.aspx を抜き取る
	public static string RemoveDefaultHtml(string url)
	{
		string tmp = url.ToLower();
		if (tmp.EndsWith("/default.asp") || tmp.EndsWith("/default.aspx") || tmp.EndsWith("/default.htm") || tmp.EndsWith("/default.html"))
		{
			return GetUrlDirNameFromPath(url);
		}
		else
		{
			return url;
		}
	}

	// URL からフォルダ名だけを抜き出す
	public static string GetUrlDirNameFromPath(string url)
	{
		string ret = "";
		string[] strs = url.Split('/');
		int i;
		if (strs.Length >= 1)
		{
			for (i = 0; i < strs.Length - 1; i++)
			{
				ret += strs[i] + "/";
			}
		}
		return ret;
	}


	// ホストヘッダにホスト名とポート番号がある場合はホスト名だけ抽出する
	public static string RemovePortFromHostHeader(string str)
	{
		try
		{
			string[] ret = str.Split(':');

			return ret[0];
		}
		catch
		{
			return str;
		}
	}

	// Web のパスを物理的なファイルパスに変換する
	public static string WebPathToFilePath(System.Web.UI.Page page, string path)
	{
		string appRootFilePath = page.Request.PhysicalApplicationPath;
		string appRootVirtualPath = page.Request.ApplicationPath;
		string ret;

		path = RemoveDefaultHtml(path);
		if (path.ToUpper().StartsWith(appRootVirtualPath.ToUpper()) == false)
		{
			return null;
		}

		path = path.Substring(appRootVirtualPath.Length).Replace("/", "\\");

		if (path.StartsWith("\\"))
		{
			path = path.Substring(1);
		}

		ret = appRootFilePath + path;

		if (ret.IndexOf("..") != -1)
		{
			return null;
		}

		if (ret.EndsWith("\\"))
		{
			// Default.aspx 等を検索する
			ret = GetDefaultDocumentIfExists(ret);
		}

		return ret;
	}

	// ディレクトリ名から Default.aspx などを取得する
	public static string GetDefaultDocumentIfExists(string dir)
	{
		string[] targets =
			{
				"default.aspx",
				"default.asp",
				"default.html",
				"default.htm",
				"index.html",
				"index.htm",
			};

		foreach (string s in targets)
		{
			string name = dir + s;

			if (IsFileExists(name))
			{
				return name;
			}
		}

		return null;
	}

	// 指定されたファイルが存在するかどうか確認する
	public static bool IsFileExists(string name)
	{
		return File.Exists(name);
	}
}

// 多言語対応クラス
public class MultiLang
{
	public readonly Page Page;
	public readonly HttpRequest Request;
	public readonly HttpResponse Response;
	public readonly bool IsUrlModefied;
	public readonly string OriginalUrl;
	public readonly string PhysicalUrl;
	public readonly bool IsFilenameModified;
	public readonly string OriginalFileName;
	public readonly string OriginalFilePath;
	public readonly string Args;
	public readonly CoreLanguageClass CurrentLanguage;
	public readonly CoreLanguageClass ContentsPrintLanguage;
	public readonly string CurrentLanguageCode;
	public readonly bool IsCurrentLanguageSupported;
	public readonly string GoogleTranslateUrl;
	public readonly string OriginalFullUrl;
	public readonly bool IsSSL;
	public readonly string Host;
	public readonly string BasicHostName;
	MultiLanguageFilterStream mfs;
	public readonly List<KeyValuePair<string, string>> ReplaceList;

	public bool DisableFilter
	{
		get
		{
			return mfs.DisableFilter;
		}
		set
		{
			mfs.DisableFilter = value;
		}
	}

	// HTML 本文を読み込む
	public readonly string HtmlBody = "";
	public readonly string HtmlFileName = "";

	static MultiLang()
	{
		// "ja" の代わりに "jp" を使う
		CoreLanguageList.RegardsJapanAsJP = true;
	}

	public bool IsJapanese
	{
		get
		{
			if (this.CurrentLanguage == CoreLanguageList.Japanese)
			{
				return true;
			}
			return false;
		}
	}

	public bool IsJapanesePrinting
	{
		get
		{
			if (this.ContentsPrintLanguage == CoreLanguageList.Japanese)
			{
				return true;
			}
			return false;
		}
	}

	public void Redirect(string url, bool endSession)
	{
		url = ConvertPath(url);
		if (url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("ftp://") ||
			url.StartsWith("/"))
		{
			// 絶対パス
		}
		else
		{
			// 相対パス
			string originalUrl = OriginalUrl;
			if (originalUrl.EndsWith("/"))
			{
			}
			else
			{
				int i;
				for (i = originalUrl.Length - 1; i >= 0; i--)
				{
					if (originalUrl[i] == '/' || originalUrl[i] == '\\')
					{
						originalUrl = originalUrl.Substring(0, i + 1);
						break;
					}
				}
			}
			url = originalUrl + url;
		}
		Response.Redirect(url, endSession);
	}

	public MultiLang(Page currentPage)
		: this(currentPage, false)
	{
	}
	public MultiLang(Page currentPage, bool fast) : this(currentPage, fast, null)
	{
	}
	public MultiLang(Page currentPage, bool fast, string basicHostName)
		: this(currentPage, fast, basicHostName, new List<KeyValuePair<string, string>>())
	{
	}
	public MultiLang(Page currentPage, bool fast, string basicHostName, List<KeyValuePair<string, string>> replaceList)
	{
		this.Page = currentPage;

		this.Request = Page.Request;
		this.Response = Page.Response;
		this.BasicHostName = basicHostName;
		string tmp = Page.Request.ServerVariables["HTTPS"];
		string hostRaw = Page.Request.Headers["Host"];
		this.ReplaceList = replaceList;
		bool isSsl = false;
		string[] tokens;
		string host = "";

		tokens = hostRaw.Split(':');
		if (tokens.Length >= 1)
		{
			host = tokens[0];
		}

		host = host.ToLower();

		if (tmp != null)
		{
			if (tmp.Equals("on", StringComparison.InvariantCultureIgnoreCase))
			{
				isSsl = true;
			}
		}

		this.IsSSL = isSsl;
		this.Host = host;

		this.IsUrlModefied = Str.StrToBool((string)Request.Headers["SEISAPI_MODIFIED_URL"]);
		this.OriginalUrl = (string)Request.Headers["SEISAPI_ORIGINAL_URL"];

		int i = -1;
		if (Str.IsEmptyStr(this.OriginalUrl) == false)
		{
			i = this.OriginalUrl.IndexOf("?");
		}
		if (i != -1)
		{
			this.OriginalUrl = this.OriginalUrl.Substring(0, i);
		}

		if (Str.IsEmptyStr(this.OriginalUrl) || this.IsUrlModefied == false)
		{
			this.OriginalUrl = AspUtil.RemoveDefaultHtml(AspUtil.GetCurrentRequestUrl(Page));
		}

		string s = (string)Request.Headers["SEISAPI_ORIGINAL_FILENAME"];
		if (Str.IsEmptyStr(s) == false)
		{
			this.IsFilenameModified = true;
			this.OriginalFileName = s;
			this.OriginalFilePath = (string)Request.Headers["SEISAPI_ORIGINAL_FILEPATH"];
		}

		// パスから現在の言語コードを取得する
		string langCode = GetCurrentLangCodeFromPath(this.OriginalUrl);

		// 言語コードから言語を特定する
		this.CurrentLanguage = CoreLanguageList.GetLanguageClassByName(langCode);
		this.CurrentLanguageCode = CurrentLanguage.Name;

		// HTML 本文を読み込む
		try
		{
			HtmlFileName = AspUtil.WebPathToFilePath(currentPage, AspUtil.GetCurrentRequestUrl(currentPage));
		}
		catch
		{
		}

		if (this.IsFilenameModified)
		{
			HtmlFileName = Path.Combine(Path.GetDirectoryName(HtmlFileName), Path.GetFileName(OriginalFilePath));
		}

		try
		{
			if (fast == false)
			{
				HtmlBody = File.ReadAllText(HtmlFileName, Str.Utf8Encoding);
			}
		}
		catch
		{
		}

		PhysicalUrl = AspUtil.RemoveDefaultHtml(AspUtil.GetCurrentRequestUrl((currentPage)));

		Args = currentPage.Request.ServerVariables["QUERY_STRING"];

		// 現在の言語がサポートされているかどうか確認する
		if (CurrentLanguage == CoreLanguageList.Japanese)
		{
			IsCurrentLanguageSupported = true;
		}
		else
		{
			IsCurrentLanguageSupported = Str.SearchStr(HtmlBody, string.Format("<!-- ml:{0} -->", CurrentLanguage.Name), 0, false) != -1;
		}

		// Google 翻訳の URL
		GoogleTranslateUrl = string.Format("http://translate.google.com/translate?js=n&prev=_t&hl=en&ie=UTF-8&layout=2&eotf=1&sl=ja&tl={1}&u={0}",
			HttpUtility.UrlEncode((isSsl ? "https://" : "http://") + host + this.OriginalUrl, Str.Utf8Encoding),
			this.CurrentLanguageCode);

		OriginalFullUrl = (isSsl ? "https://" : "http://") + host + this.OriginalUrl;

		// 表示に使用する言語
		ContentsPrintLanguage = this.CurrentLanguage;
		if (IsCurrentLanguageSupported == false)
		{
			ContentsPrintLanguage = CoreLanguageList.Japanese;
		}

		// 出力フィルタを登録する
		if (fast == false)
		{
			mfs = new MultiLanguageFilterStream(Response.Filter, ContentsPrintLanguage, this.CurrentLanguage, this.BasicHostName, this.ReplaceList);
			mfs.Page = Page;
			Response.Filter = mfs;
		}
	}

	// パスを変換する
	public string ConvertPath(string url)
	{
		return ConvertPath(url, this.CurrentLanguage);
	}
	public string ConvertPath(string url, CoreLanguageClass lang)
	{
		string ja = CoreLanguageList.Japanese.Name;

		if (url.StartsWith("/" + ja, StringComparison.InvariantCultureIgnoreCase))
		{
			url = "/" + lang.Name + url.Substring(ja.Length + 1);
		}

		return url;
	}

	// 特定言語用のパスを取得する
	public string GetPathForLanguage(CoreLanguageClass lang)
	{
		string url = PhysicalUrl;

		return ConvertPath(url, lang);
	}

	public string GetFullUrlForLanguage(CoreLanguageClass lang)
	{
		string url = (IsSSL ? "https://" : "http://") + Host + GetPathForLanguage(lang);

		if (Str.IsEmptyStr(Args) == false)
		{
			url += "?" + Args;
		}

		return url;
	}

	public string ProcStr(string str)
	{
		return ProcStr(str, ContentsPrintLanguage);
	}

	public static string ProcStrDefault(string str)
	{
		return ProcStr(str, CoreLanguageClass.CurrentThreadLanguageClass);
	}

	public static string ProcStr(string str, CoreLanguageClass lang)
	{
		return ProcStr(str, lang, lang);
	}

	public static string ProcStr(string str, CoreLanguageClass lang, CoreLanguageClass langPure)
	{
		MultiLanguageFilterStream st = new MultiLanguageFilterStream(null, lang, langPure, null, null);

		return st.FilterString(str);
	}

	public static string GetCurrentLangCodeFromPath(string str)
	{
		char[] sps =
		{
			'/', '?',
		};
		string[] tokens = str.Split(sps, StringSplitOptions.RemoveEmptyEntries);

		if (tokens.Length >= 1)
		{
			return tokens[0].ToLower();
		}

		return CoreLanguageList.Japanese.Name;
	}
}

// 多言語共通文字列テーブル
public static class MultiString
{
	public const string ChangeLanguage = "[j]Select Language[e]Select Language[/]";
	public const string LanguageNotSupported = "[j]申し訳ございませんが、以下のコンテンツは現在日本語で公開されていません。[e]Unfortunately, following contents are not published in English yet. [/]";
	public const string ThisIsTranslatedByMachine = "Following contents are translated automatically by Google Translate.";
	public const string GoogleTranslate = "[j]Google で翻訳[e]Click here to translate the contents into English by Google Now[/]";
	public const string ShowSrc = "Show the original page";

	public static string GetStr(string srcStr, CoreLanguageClass lang)
	{
		return MultiLang.ProcStr(srcStr, lang);
	}
}

// 言語フィルタ
public class MultiLanguageFilterStream : Stream
{
	public static readonly List<KeyValuePair<string, CoreLanguageClass>> langKeys = new List<KeyValuePair<string, CoreLanguageClass>>();
	public readonly List<KeyValuePair<string, string>> ReplaceList = null;
	public const string TagPure = "<!-- ml:pure -->";
	public const string TagEndPure = "<!-- ml:endpure -->";
	public bool DisableFilter = false;
	public Page Page;

	static MultiLanguageFilterStream()
	{
		langKeys.Add(new KeyValuePair<string, CoreLanguageClass>("[j]", CoreLanguageList.Japanese));
		langKeys.Add(new KeyValuePair<string, CoreLanguageClass>("[e]", CoreLanguageList.English));
		langKeys.Add(new KeyValuePair<string, CoreLanguageClass>("[/]", null));
	}

	Stack<CoreLanguageClass> stack = new Stack<CoreLanguageClass>();
	CoreLanguageClass currentBodyLanguage
	{
		get
		{
			if (stack.Count == 0)
			{
				return null;
			}
			else
			{
				return stack.ToArray()[0];
			}
		}
	}
	bool isLang(CoreLanguageClass lang)
	{
		CoreLanguageClass[] langList = stack.ToArray();

		foreach (CoreLanguageClass c in langList)
		{
			if (c != lang)
			{
				return false;
			}
		}

		return true;
	}
	CoreLanguageClass lastBodyLanguage
	{
		get
		{
			if (stack.Count == 0)
			{
				return null;
			}
			else
			{
				return stack.Peek();
			}
		}
	}

	public string FilterString(string src)
	{
		string[] strList = Str.DivideStringMulti(src, true,
			TagPure, TagEndPure);

		bool b = false;

		StringBuilder ret = new StringBuilder();

		foreach (string str in strList)
		{
			if (str == TagPure)
			{
				b = true;
			}
			else if (str == TagEndPure)
			{
				b = false;
			}

			ret.Append(filterStringInner(str, b ? this.currentLanguagePure : this.currentLanguage, this.currentLanguagePure));
		}

		return ret.ToString();
	}

	string filterStringInner(string src, CoreLanguageClass useLang, CoreLanguageClass useLangPure)
	{
		int i;
		string ret = src;

		if (Str.IsEmptyStr(basicHostName) == false)
		{
			ret = Str.ReplaceStr(ret, "=\"/\"", "=\"http://" + basicHostName + "/\"", false);
			ret = Str.ReplaceStr(ret, "=\'/\'", "=\'http://" + basicHostName + "/\'", false);

			ret = Str.ReplaceStr(ret, "=\"/" + CoreLanguageList.Japanese.Name + "/\"", "=\"http://" + basicHostName + "/" + useLangPure.Name + "/\"", false);
			ret = Str.ReplaceStr(ret, "=\'/" + CoreLanguageList.Japanese.Name + "/\'", "=\'http://" + basicHostName + "/" + useLangPure.Name + "/\'", false);
		}

		ret = Str.ReplaceStr(ret, "=\"/" + CoreLanguageList.Japanese.Name + "/", "=\"/" + useLangPure.Name + "/", false);
		ret = Str.ReplaceStr(ret, "=\'/" + CoreLanguageList.Japanese.Name + "/", "=\'/" + useLangPure.Name + "/", false);

		ret = Str.ReplaceStr(ret, "_lm_" + CoreLanguageList.Japanese.Name, "_lm_" + useLang.Name, false);

		if (this.ReplaceList != null)
		{
			foreach (KeyValuePair<string, string> p in this.ReplaceList)
			{
				ret = Str.ReplaceStr(ret, p.Key, p.Value, false);
			}
		}

		StringBuilder ret2 = new StringBuilder();

		int next = 0;
		while (true)
		{
			int min = int.MaxValue;
			int j = -1;
			for (i = 0; i < langKeys.Count; i++)
			{
				int r = Str.SearchStr(ret, langKeys[i].Key, next, false);
				if (r != -1)
				{
					if (r < min)
					{
						j  = i;
						min = r;
					}
				}
			}

			if (j != -1)
			{
				KeyValuePair<string, CoreLanguageClass> v = langKeys[j];

				if (currentBodyLanguage == null || isLang(useLang))
				{
					ret2.Append(ret.Substring(next, min - next));
				}

				if (v.Value != null)
				{
					if (lastBodyLanguage == null || v.Value.Id <= lastBodyLanguage.Id)
					{
						stack.Push(v.Value);
					}
					else
					{
						stack.Pop();
						stack.Push(v.Value);
					}
				}
				else
				{
					stack.Pop();
				}

				next = min + v.Key.Length;
			}
			else
			{
				if (currentBodyLanguage == null || isLang(useLang))
				{
					ret2.Append(ret.Substring(next, ret.Length - next));
				}
				break;
			}
		}

		ret = ret2.ToString();

		string lang = useLangPure != CoreLanguageList.Japanese ? useLangPure.Name : "ja";

		if (useLangPure != CoreLanguageList.Japanese)
		{
			ret = Str.ReplaceStr(ret, "<meta http-equiv=\"Content-Language\" content=\"ja\" />",
				string.Format("<meta http-equiv=\"Content-Language\" content=\"{0}\" />", lang), false);
		}

		ret = Str.ReplaceStr(ret, "<html>", string.Format("<html lang=\"{0}\">", lang), false);

		// スタイルシートファイル名挿入
		next = 0;
		while (true)
		{
			i = Str.SearchStr(ret, "<link rel=\"stylesheet\" href=\"", next, false);
			if (i == -1)
			{
				break;
			}
			next = i + 1;
			int j = Str.SearchStr(ret, "/>", next, false);
			if (j == -1)
			{
				break;
			}
			string linkStr = ret.Substring(i, j - i + 2 - 1);
			int k = Str.SearchStr(linkStr, "href=\"", 0, false);
			if (k != -1)
			{
				int m = Str.SearchStr(linkStr, "\"", k + 6, false);
				if (m != -1)
				{
					string fileName = linkStr.Substring(k + 6, m - k - 6);
					fileName = Str.ReplaceStr(fileName, ".css", "_" + lang + ".css", false);
					string linkStr2 = string.Format("<link rel=\"stylesheet\" href=\"{0}\" type=\"text/css\" />", fileName);

					ret = ret.Substring(0, j + 2) + linkStr2 + ret.Substring(j + 2);
					next = j + 2 + linkStr2.Length;
				}
			}
		}

		return ret;
	}

	Stream baseStream;
	long position;
	CoreLanguageClass currentLanguage;
	CoreLanguageClass currentLanguagePure;
	string basicHostName;

	public override bool CanRead
	{
		get { return true; }
	}

	public override bool CanSeek
	{
		get { return true; }
	}

	public override bool CanWrite
	{
		get { return true; }
	}

	public override void Flush()
	{
		baseStream.Flush();
	}

	public override long Length
	{
		get { return 0; }
	}

	public override long Position
	{
		get
		{
			return position;
		}
		set
		{
			position = value;
		}
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		return baseStream.Seek(offset, origin);
	}

	public override void SetLength(long value)
	{
		baseStream.SetLength(value);
	}

	public MultiLanguageFilterStream(Stream baseStream, CoreLanguageClass currentLanguage, CoreLanguageClass currentLanguagePure, string basicHostName, List<KeyValuePair<string, string>> replaceList)
	{
		this.baseStream = baseStream;
		this.currentLanguage = currentLanguage;
		this.currentLanguagePure = currentLanguagePure;
		this.basicHostName = basicHostName;
		this.ReplaceList = replaceList;
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		return baseStream.Read(buffer, offset, count);
	}

	string savedString = "";

	public override void Write(byte[] buffer, int offset, int count)
	{
		if (DisableFilter)
		{
			baseStream.Write(buffer, offset, count);
			return;
		}
		byte[] data = new byte[count];
		Buffer.BlockCopy(buffer, offset, data, 0, count);

		string inSrc = savedString + ByteDataToString(data);// Str.Utf8Encoding.GetString(data);

		savedString = "";

		if (inSrc.Length >= 2)
		{
			int len = inSrc.Length;
			string last2 = inSrc.Substring(len - 2, 2);
			string last1 = inSrc.Substring(len - 1, 1);

			if (last1 == "[")
			{
				inSrc = inSrc.Substring(0, len - 1);

				savedString = last1;
			}
			else if (Str.InStr(last2, "["))
			{
				inSrc = inSrc.Substring(0, len - 2);

				savedString = last2;
			}
		}

		string inStr = FilterString(inSrc);

		data = StringToByteData(inStr);// Str.Utf8Encoding.GetBytes(inStr);

		if (data.Length >= 1)
		{
			baseStream.Write(data, 0, data.Length);
			//byte[] t = Str.Utf8Encoding.GetBytes("" + count.ToString() + "");
			//baseStream.Write(t, 0, t.Length);
		}
	}

	public static string ByteDataToString(byte[] data)
	{
		StringBuilder sb = new StringBuilder();

		foreach (byte b in data)
		{
			if (b <= 0x7f && b != (byte)('\\'))
			{
				sb.Append((char)b);
			}
			else
			{
				sb.Append("\\" + ((uint)b).ToString("X2"));
			}
		}

		return sb.ToString();
	}

	public byte[] StringToByteData(string str)
	{
		int i, len;

		len = str.Length;
		Buf b = new Buf();

		for (i = 0; i < len; i++)
		{
			char c = str[i];
			if (c == '\\')
			{
				string tmp = "";

				//try
				{
					tmp = "" + str[i + 1] + str[i + 2];
				}
				/*catch (Exception ex)
				{
					tmp += "|err=" + ex.Message + ",len=" + len + ",i=" + i + "|src=" + str + "|";
					byte[] aa = Str.Utf8Encoding.GetBytes(tmp);
					b.Write(aa);
				}*/

				i += 2;

				//try
				{
					b.WriteByte(byte.Parse(tmp, System.Globalization.NumberStyles.HexNumber));
				}
				//catch
				{
				}
			}
			else
			{
				b.WriteByte((byte)c);
			}
		}

		return b.ByteData;
	}
}

