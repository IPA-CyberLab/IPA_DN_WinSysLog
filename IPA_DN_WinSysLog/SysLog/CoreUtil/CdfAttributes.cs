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

#if ASPNET
using Resources.CoreUtil.Properties;
#else
using CoreUtil.Properties;
#endif

namespace CoreUtil
{
	// フィールド属性
	public class CdfFieldAttribute : Attribute, ICloneable
	{
		public bool GridHide = false;
		public string FriendlyName;
		public bool EmptyAsDefault = false;
		public string Note = "";
		public string PrintFormat = "";
		public string StringBefore = "";
		public string StringAfter = "";
		public AutoCompleteType AutoCompletionType = AutoCompleteType.None;
		public bool ReadOnly = false;
		public bool NoWrap = false;
		public string GridSize = "";
		public string GridMinSize = "";
		public HorizontalAlign GridAlign = HorizontalAlign.Left;
		public string PrintFontName = "";
		public string EditFontName = "";
		public string PrintFontColor = "";
		public string EditFontColor = "";
		public bool PrintFontBold = true;
		public bool EditFontBold = true;
		public string PrintFontSize = "";
		public string GridLinkUrlFormat = "";
		public string GridLinkUrlIdName = "";
		public string GridLinkUrlTarget = "";
		public string PrintDefaultString = "";

		public CdfFieldAttribute(string frientlyName)
		{
			this.FriendlyName = frientlyName;
		}

		public CdfFieldAttribute(string frientlyName, bool emptyAsDefault)
		{
			this.FriendlyName = frientlyName;
			this.EmptyAsDefault = emptyAsDefault;
		}

		public object Clone()
		{
			return MemberwiseClone();
		}
	}

	// 数値フィールド属性
	public class CdfIntAttribute : CdfFieldAttribute
	{
		public int MinValue = int.MinValue;
		public int MaxValue = int.MaxValue;
		public int DefaultValue = 0;
		public bool Akaji = false;
		public string AkajiColor = "";
		public bool ShowPlus = false;
		public bool Comma = false;

		public CdfIntAttribute(string friendlyName)
			: base(friendlyName)
		{
		}
	}
	public class CdfLongAttribute : CdfFieldAttribute
	{
		public long MinValue = long.MinValue;
		public long MaxValue = long.MaxValue;
		public long DefaultValue = 0;
		public bool Akaji = false;
		public string AkajiColor = "";
		public bool ShowPlus = false;
		public bool Comma = false;

		public CdfLongAttribute(string friendlyName)
			: base(friendlyName)
		{
		}
	}
	public class CdfDoubleAttribute : CdfFieldAttribute
	{
		public double MinValue = double.MinValue;
		public double MaxValue = double.MaxValue;
		public double DefaultValue = 0;
		public bool Akaji = false;
		public string AkajiColor = "";
		public bool ShowPlus = false;

		public CdfDoubleAttribute(string friendlyName)
			: base(friendlyName)
		{
		}
	}

	// 選択フィールド属性
	public class CdfBoolAttribute : CdfFieldAttribute
	{
		public string CheckBoxText = "選択";
		public string CheckBoxIdName = "";

		public CdfBoolAttribute(string friendlyName)
			: base(friendlyName)
		{
		}
	}

	// 文字列フィールド属性
	public class CdfStringAttribute : CdfFieldAttribute
	{
		public int MinLength = 0;
		public int MaxLength = 0;
		public string DefaultValue = "";
		public string Width = "";
		public string Height = "";
		public bool MultiLine = false;
		public bool Password = false;
		public bool NoUnsafeString = false;
		public bool NoPrintableString = false;
		public bool NormalizeSpace = false;
		public bool NormalizeToHankaku = false;
		public bool NormalizeToZenkaku = false;
		public bool NormalizeToZenkakuKana = false;
		public bool NormalizeStandard = false;
		public bool MailAddress = false;
		public bool NoJavaScript = false;
		public List<String> History = new List<string>();
		public bool PrintAsList = false;
		public bool NoAutoHtml = false;
		public bool NoAutoHyperLink = false;

		public bool HasHistory
		{
			get
			{
				return History.Count >= 1;
			}
		}

		public CdfStringAttribute(string friendlyName)
			: base(friendlyName)
		{
		}
	}

	// 日時フィールドの種類
	public enum CdfDateTimeType
	{
		Both,
		DateOnly,
		TimeOnly,
	}

	// リスト属性
	public class CdfListAttribute : CdfFieldAttribute
	{
		public CdfListAttribute(string friendlyName)
			: base(friendlyName, true)
		{
		}
	}

	// 日時フィールド属性
	public class CdfDateTimeAttribute : CdfFieldAttribute
	{
		public static readonly DateTime ZeroDateTimeValue = new DateTime(1800, 1, 1);

		public string DefaultValue = "1800/01/01";
		public string MinValue = "1900/01/01";
		public string MaxValue = "2099/12/31";
		public bool AllowZero = false;
		public bool PrintSimple = false;
		public CdfDateTimeType Type = CdfDateTimeType.Both;

		public DateTime DefaultValueDateTime
		{
			get
			{
				return Str.StrToDateTime(this.DefaultValue);
			}
		}

		public DateTime MinValueDateTime
		{
			get
			{
				return Str.StrToDateTime(this.MinValue);
			}
		}

		public DateTime MaxValueDateTime
		{
			get
			{
				return Str.StrToDateTime(this.MaxValue);
			}
		}

		public CdfDateTimeAttribute(string friendlyName)
			: base(friendlyName, true)
		{
		}
	}

	// 候補リストにおけるコントロール形式
	public enum CdfCandidateListControlType
	{
		DropDown,
		ListBox,
		RadioButton,
	}

	// 候補リスト属性
	public class CdfCandidateListAttribute : CdfFieldAttribute
	{
		public CdfCandidateListControlType CandidateListControlType = CdfCandidateListControlType.DropDown;
		public string CandidateListListBoxHeight = "";
		public bool ListDefaultEmpty = true;
		public bool AllowNull = false;

		public CdfCandidateListAttribute(string friendlyName)
			: base(friendlyName, false)
		{
		}
	}

	// 候補リスト 2 属性
	public class CdfCandidateStrListAttribute : CdfFieldAttribute
	{
		public CdfCandidateListControlType CandidateListControlType = CdfCandidateListControlType.DropDown;
		public string CandidateListListBoxHeight = "";
		public bool ListDefaultEmpty = true;
		public bool AllowNull = false;
		public int MaxPrintStrLen = 0;

		public CdfCandidateStrListAttribute(string friendlyName)
			: base(friendlyName, false)
		{
		}
	}

	// 列挙体属性
	public class CdfEnumAttribute : CdfFieldAttribute
	{
		public int DefaultValue = 0;
		public bool ListDefaultEmpty = false;

		public CdfEnumAttribute(string friendlyName)
			: base(friendlyName, false)
		{
		}
	}

	// 列挙体のアイテムの属性
	public class CdfEnumItemAttribute : CdfFieldAttribute
	{
		public CdfEnumItemAttribute(string friendlyName)
			: base(friendlyName)
		{
		}
	}

	// クラス属性
	public class CdfClassAttribute : Attribute
	{
		public string FriendlyName;

		public CdfClassAttribute(string frientlyName)
		{
			this.FriendlyName = frientlyName;
		}
	}

	// 候補リストデータ
	public class CandidateListElement : IComparable<CandidateListElement>
	{
		public int Value;
		public string String;

		public CandidateListElement()
		{
			this.Value = 0;
			this.String = "";
		}

		public CandidateListElement(int value, string str)
		{
			this.Value = value;
			this.String = str;
		}

		public int CompareTo(CandidateListElement other)
		{
			return this.String.CompareTo(other.String);
		}
	}

	// 候補リスト
	public class CandidateList
	{
		public int Value = 0;
		public string FriendlyString = "";

		public int Count
		{
			get
			{
				return list.Count;
			}
		}

		public CandidateList()
			: this(0, "")
		{
		}

		public CandidateList(int value, string friendlyString)
		{
			this.Value = value;
			this.FriendlyString = friendlyString;
		}

		public void AddFrom(CandidateList cl)
		{
			foreach (CandidateListElement e in cl.list)
			{
				this.Add(e.Value, e.String);
			}
		}

		public void CopyFrom(CandidateList cl)
		{
			this.list.Clear();

			AddFrom(cl);
		}

		public void SetKeyValuePairList(List<KeyValuePair<int, string>> list)
		{
			this.list.Clear();

			foreach (KeyValuePair<int, string> p in list)
			{
				this.list.Add(new CandidateListElement(p.Key, p.Value));
			}
		}

		List<CandidateListElement> list = new List<CandidateListElement>();

		public List<CandidateListElement> List
		{
			get { return list; }
		}

		public void Clear()
		{
			list.Clear();
		}

		public bool Set(int value)
		{
			string s = GetString(value);

			if (s == null)
			{
				return false;
			}

			this.Value = value;
			this.FriendlyString = s;

			return true;
		}

		public void Add(int value, string str)
		{
			Str.NormalizeString(ref str);

			foreach (CandidateListElement p in this.list)
			{
				if (p.Value == value)
				{
					this.list.Remove(p);
					break;
				}
			}

			list.Add(new CandidateListElement(value, str));

			if (this.Value == value)
			{
				this.FriendlyString = str;
			}
		}

		public bool Delete(int value)
		{
			foreach (CandidateListElement p in this.list)
			{
				if (p.Value == value)
				{
					this.list.Remove(p);
					return true;
				}
			}

			return false;
		}

		public bool HasValue(int value)
		{
			return GetString(value) == null ? false : true;
		}

		public string GetString(int value)
		{
			foreach (CandidateListElement p in this.list)
			{
				if (p.Value == value)
				{
					return p.String;
				}
			}

			return null;
		}

		public IEnumerable<int> CandidateValues
		{
			get
			{
				foreach (CandidateListElement p in this.list)
				{
					yield return p.Value;
				}
			}
		}

		public IEnumerable<string> CandidateStrings
		{
			get
			{
				foreach (CandidateListElement p in this.list)
				{
					yield return p.String;
				}
			}
		}

		public string this[int index]
		{
			get
			{
				foreach (CandidateListElement p in this.list)
				{
					if (p.Value == index)
					{
						return p.String;
					}
				}

				return null;
			}
		}
	}

	// 候補リスト 2 データ
	public class CandidateStrListElement : IComparable<CandidateStrListElement>
	{
		public string Value;
		public string String;

		public CandidateStrListElement()
		{
			this.Value = "";
			this.String = "";
		}

		public CandidateStrListElement(string value, string str)
		{
			this.Value = value;
			this.String = str;
		}

		public int CompareTo(CandidateStrListElement other)
		{
			return Str.StrCmpiRetInt(this.String, other.String);
		}
	}

	// 候補リスト2
	public class CandidateStrList
	{
		public string Value = "";
		public string FriendlyString = "";

		public int Count
		{
			get
			{
				return list.Count;
			}
		}

		public CandidateStrList()
			: this("", "")
		{
		}

		public CandidateStrList(string value, string friendlyString)
		{
			this.Value = value;
			this.FriendlyString = friendlyString;
		}

		public void AddFrom(CandidateStrList cl)
		{
			foreach (CandidateStrListElement e in cl.list)
			{
				this.Add(e.Value, e.String);
			}
		}

		public void CopyFrom(CandidateStrList cl)
		{
			this.list.Clear();

			AddFrom(cl);
		}

		public void SetKeyValuePairList(List<KeyValuePair<string, string>> list)
		{
			this.list.Clear();

			foreach (KeyValuePair<string, string> p in list)
			{
				this.list.Add(new CandidateStrListElement(p.Key, p.Value));
			}
		}

		List<CandidateStrListElement> list = new List<CandidateStrListElement>();

		public List<CandidateStrListElement> List
		{
			get { return list; }
		}

		public void Clear()
		{
			list.Clear();
		}

		public bool Set(string value)
		{
			string s = GetString(value);

			if (s == null)
			{
				return false;
			}

			this.Value = value;
			this.FriendlyString = s;

			return true;
		}

		public void Add(string value, string str)
		{
			Str.NormalizeString(ref str);

			foreach (CandidateStrListElement p in this.list)
			{
				if (Str.StrCmpi(p.Value, value))
				{
					this.list.Remove(p);
					break;
				}
			}

			list.Add(new CandidateStrListElement(value, str));

			if (this.Value == value)
			{
				this.FriendlyString = str;
			}
		}

		public bool Delete(string value)
		{
			foreach (CandidateStrListElement p in this.list)
			{
				if (Str.StrCmpi(p.Value, value))
				{
					this.list.Remove(p);
					return true;
				}
			}

			return false;
		}

		public bool HasValue(string value)
		{
			return GetString(value) == null ? false : true;
		}

		public string GetString(string value)
		{
			foreach (CandidateStrListElement p in this.list)
			{
				if (Str.StrCmpi(p.Value, value))
				{
					return p.String;
				}
			}

			return null;
		}

		public IEnumerable<string> CandidateValues
		{
			get
			{
				foreach (CandidateStrListElement p in this.list)
				{
					yield return p.Value;
				}
			}
		}

		public IEnumerable<string> CandidateStrings
		{
			get
			{
				foreach (CandidateStrListElement p in this.list)
				{
					yield return p.String;
				}
			}
		}

		public string this[string index]
		{
			get
			{
				foreach (CandidateStrListElement p in this.list)
				{
					if (Str.StrCmpi(p.Value, index))
					{
						return p.String;
					}
				}

				return null;
			}
		}
	}
}

