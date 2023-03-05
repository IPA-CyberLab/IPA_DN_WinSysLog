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
	public interface ICdfRowExtension
	{
		int BackColorId { get; }
		string ClusterTitleString { get; }
		Color ClusterTitleColor { get; }
	}

	public static class CdfGlobalLangSettings
	{
		public static string ResourceLanguage = "";

		public static string ProcStr(string src)
		{
			if (Str.IsEmptyStr(ResourceLanguage))
			{
				return src;
			}

			return MultiLang.ProcStr(src, CoreLanguageList.GetLanguageClassByName(ResourceLanguage));
		}
	}

	[CdfClass("CdfBasicList")]
	public class CdfBasicList
	{
		[CdfList("CdfBasicList")]
		public List<object> ListData = new List<object>();
	}

	public enum CdfMetaFieldType
	{
		String,
		Int,
		Long,
		Double,
		DateTime,
		List,
		Enum,
		CandidateList,
		Bool,
		CandidateStrList,
	}

	public class CdfMetaEnumItem
	{
		public int ValueInt;
		public string ValueString;
		public CdfEnumItemAttribute Attribute;

		public string FriendlyName
		{
			get
			{
				return Attribute.FriendlyName;
			}
		}

		public override string ToString()
		{
			return ValueString;
		}
	}

	public class CdfMetaEnum
	{
		public Type Type;
		public List<CdfMetaEnumItem> Items = new List<CdfMetaEnumItem>();

		public CdfMetaEnumItem GetEnumItem(string name)
		{
			CdfMetaEnumItem ret = null;

			foreach (CdfMetaEnumItem i in Items)
			{
				if (i.ValueString.Equals(name, StringComparison.InvariantCultureIgnoreCase))
				{
					ret = i;
				}
			}

			return ret;
		}

		public CdfMetaEnumItem GetEnumItem(int id)
		{
			CdfMetaEnumItem ret = null;

			foreach (CdfMetaEnumItem i in Items)
			{
				if (i.ValueInt == id)
				{
					ret = i;
				}
			}

			return ret;
		}
	}

	public class CdfMetaField
	{
		public string Name;
		public CdfMetaFieldType Type;
		public CdfFieldAttribute CdfFieldAttribute;
		public CdfMetaClass ListMetaClass;
		public CdfMetaEnum MetaEnum;
		public FieldInfo DotNetFieldInfo;

		public override string ToString()
		{
			return this.Name;
		}

		public object GetData(object data)
		{
			return DotNetFieldInfo.GetValue(data);
		}

		public void SetData(object data, object value)
		{
			DotNetFieldInfo.SetValue(data, value);
		}

		public void SetString(object data, string destString)
		{
			Str.NormalizeString(ref destString);

			switch (this.Type)
			{
				case CdfMetaFieldType.Enum:
					CdfEnumAttribute enumAttribute = (CdfEnumAttribute)this.CdfFieldAttribute;

					if (Str.IsEmptyStr(destString))
					{
						throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_NOT_SELECTED));
					}

					if (Str.IsInt(destString) == false)
					{
						throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_NOT_SELECTED_2));
					}

					int enumValue = Str.StrToInt(destString);

					if (enumValue == -1)
					{
						throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_NOT_SELECTED));
					}

					SetData(data, enumValue);
					
					break;

				case CdfMetaFieldType.CandidateList:
					CdfCandidateListAttribute candidateListAttribute = (CdfCandidateListAttribute)this.CdfFieldAttribute;
					CandidateList candidateList = (CandidateList)this.DotNetFieldInfo.GetValue(data);

					if (Str.IsEmptyStr(destString))
					{
						throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_NOT_SELECTED));
					}

					int newListIntValue = (int)Str.StrToInt(destString);

					if (candidateList.Set(newListIntValue) == false)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INVALID), newListIntValue));
					}

					break;

				case CdfMetaFieldType.CandidateStrList:
					CdfCandidateStrListAttribute candidateStrListAttribute = (CdfCandidateStrListAttribute)this.CdfFieldAttribute;
					CandidateStrList candidateStrList = (CandidateStrList)this.DotNetFieldInfo.GetValue(data);

					if (Str.IsEmptyStr(destString))
					{
						throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_NOT_SELECTED));
					}

					string newListStrValue = destString;

					if (candidateStrList.Set(newListStrValue) == false)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INVALID), newListStrValue));
					}

					break;

				case CdfMetaFieldType.String:
					CdfStringAttribute stringAttribute = (CdfStringAttribute)this.CdfFieldAttribute;

					if (stringAttribute.DefaultValue.Equals(destString, StringComparison.InvariantCultureIgnoreCase))
					{
						destString = "";
					}

					if (stringAttribute.MinLength >= 1 && destString.Length < stringAttribute.MinLength)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_STRING_MIN),
							stringAttribute.MinLength));
					}

					if (stringAttribute.MaxLength >= 1 && destString.Length > stringAttribute.MaxLength)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_STRING_MAX),
							stringAttribute.MaxLength));
					}

					if (stringAttribute.MailAddress)
					{
						if (Str.CheckMailAddress(destString) == false)
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INVALID_EMAIL));
						}
					}

					if (stringAttribute.MultiLine == false)
					{
						if (destString.IndexOf('\r') != -1 || destString.IndexOf('\n') != -1)
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_STRING_NOLINE));
						}
					}

					if (stringAttribute.NoUnsafeString)
					{
						if (Str.IsSafe(destString) == false)
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_STRING_INVALIDCHAR));
						}
					}

					if (stringAttribute.NoPrintableString)
					{
						if (Str.IsPrintable(destString) == false)
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_STRING_NONPRINTABLE));
						}
					}

					if (Str.IsStrOkForXML(destString) == false)
					{
						throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_STRING_INVALIDCHAR));
					}

					SetData(data, destString);
					break;

				case CdfMetaFieldType.Bool:
					CdfBoolAttribute ba = (CdfBoolAttribute)this.CdfFieldAttribute;

					bool boolValue = !Str.IsEmptyStr(destString);

					SetData(data, boolValue);

					break;

				case CdfMetaFieldType.Int:
					CdfIntAttribute intAttribute = (CdfIntAttribute)this.CdfFieldAttribute;

					Str.NormalizeString(ref destString, true, true, false, false);
					destString = destString.Replace(",", "");

					if (intAttribute.EmptyAsDefault == false)
					{
						if (Str.IsEmptyStr(destString))
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_NOTINTEGER));
						}
					}

					if (Str.IsEmptyStr(destString) == false && Str.IsInt(destString) == false)
					{
						throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_NOT_NUMBER_CHAR));
					}

					int intValue = intAttribute.DefaultValue;

					if (intAttribute.EmptyAsDefault == false || Str.IsEmptyStr(destString) == false)
					{
						intValue = Str.StrToInt(destString);
					}

					if (intValue < intAttribute.MinValue)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_MIN), Str.ToStr3(intAttribute.MinValue)));
					}

					if (intValue > intAttribute.MaxValue)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_MAX), Str.ToStr3(intAttribute.MaxValue)));
					}

					SetData(data, intValue);

					break;

				case CdfMetaFieldType.Long:
					CdfLongAttribute longAttribute = (CdfLongAttribute)this.CdfFieldAttribute;

					Str.NormalizeString(ref destString, true, true, false, false);
					destString = destString.Replace(",", "");

					if (longAttribute.EmptyAsDefault == false)
					{
						if (Str.IsEmptyStr(destString))
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_NOTINTEGER));
						}
					}

					if (Str.IsEmptyStr(destString) == false && Str.IsLong(destString) == false)
					{
						throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_NOT_NUMBER_CHAR));
					}

					long longValue = longAttribute.DefaultValue;

					if (longAttribute.EmptyAsDefault == false || Str.IsEmptyStr(destString) == false)
					{
						longValue = Str.StrToLong(destString);
					}

					if (longValue < longAttribute.MinValue)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_MIN),
							Str.ToStr3(longAttribute.MinValue)));
					}

					if (longValue > longAttribute.MaxValue)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_MAX),
							Str.ToStr3(longAttribute.MaxValue)));
					}

					SetData(data, longValue);

					break;

				case CdfMetaFieldType.Double:
					CdfDoubleAttribute doubleAttribute = (CdfDoubleAttribute)this.CdfFieldAttribute;

					Str.NormalizeString(ref destString, true, true, false, false);
					destString = destString.Replace(",", "");

					if (doubleAttribute.EmptyAsDefault == false)
					{
						if (Str.IsEmptyStr(destString))
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_NOTINTEGER));
						}
					}

					if (Str.IsEmptyStr(destString) == false && Str.IsDouble(destString) == false)
					{
						throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_NOT_NUMBER_CHAR));
					}

					double doubleValue = doubleAttribute.DefaultValue;

					if (doubleAttribute.EmptyAsDefault == false || Str.IsEmptyStr(destString) == false)
					{
						doubleValue = double.Parse(destString);
					}

					if (doubleValue < doubleAttribute.MinValue)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_MIN),
							doubleAttribute.MinValue));
					}

					if (doubleValue > doubleAttribute.MaxValue)
					{
						throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_INT_MAX),
							doubleAttribute.MaxValue));
					}

					SetData(data, doubleValue);

					break;

				case CdfMetaFieldType.DateTime:
					CdfDateTimeAttribute dta = (CdfDateTimeAttribute)this.CdfFieldAttribute;

					Str.NormalizeString(ref destString, true, true, false, false);

					if (dta.Type == CdfDateTimeType.DateOnly)
					{
						if (dta.EmptyAsDefault == false)
						{
							if (Str.IsEmptyStr(destString))
							{
								throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DATE_EMPTY));
							}
						}

						if (Str.IsEmptyStr(destString) == false && Str.IsStrDate(destString) == false)
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DATE_NOTDATE));
						}

						DateTime dtValue = Str.StrToDate(dta.DefaultValue);

						if (dta.EmptyAsDefault == false || Str.IsEmptyStr(destString) == false)
						{
							dtValue = Str.StrToDate(destString);
						}

						if (!(dta.AllowZero && dtValue == Cdf.ZeroDateTimeValue))
						{
							if (Str.IsEmptyStr(dta.MinValue) == false && dtValue < Str.StrToDate(dta.MinValue))
							{
								throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DATE_MIN),
									Str.DateToStr(Str.StrToDate(dta.MinValue))));
							}

							if (Str.IsEmptyStr(dta.MaxValue) == false && dtValue > Str.StrToDate(dta.MaxValue))
							{
								throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DATE_MAX),
									Str.DateToStr(Str.StrToDate(dta.MaxValue))));
							}
						}

						SetData(data, dtValue);
					}
					else if (dta.Type == CdfDateTimeType.TimeOnly)
					{
						if (dta.EmptyAsDefault == false)
						{
							if (Str.IsEmptyStr(destString))
							{
								throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_TIME_EMPTY));
							}
						}

						if (Str.IsEmptyStr(destString) == false && Str.IsStrTime(destString) == false)
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_TIME_NOTTIME));
						}

						DateTime dtValue = Str.StrToTime(dta.DefaultValue);

						if (dta.EmptyAsDefault == false || Str.IsEmptyStr(destString) == false)
						{
							dtValue = Str.StrToTime(destString);
						}

						if (!(dta.AllowZero && dtValue == Cdf.ZeroDateTimeValue))
						{
							if (Str.IsEmptyStr(dta.MinValue) == false && dtValue < Str.StrToTime(dta.MinValue))
							{
								throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_TIME_MIN),
									Str.TimeToStr(Str.StrToTime(dta.MinValue))));
							}

							if (Str.IsEmptyStr(dta.MaxValue) == false && dtValue > Str.StrToTime(dta.MaxValue))
							{
								throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_TIME_MAX),
									Str.TimeToStr(Str.StrToTime(dta.MaxValue))));
							}
						}

						SetData(data, dtValue);
					}
					else
					{
						if (dta.EmptyAsDefault == false)
						{
							if (Str.IsEmptyStr(destString))
							{
								throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DT_EMPTY));
							}
						}

						if (Str.IsEmptyStr(destString) == false && Str.IsStrDateTime(destString) == false)
						{
							throw new FormatException(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DT_NOTDT));
						}

						DateTime dtValue = Str.StrToDateTime(dta.DefaultValue);

						if (dta.EmptyAsDefault == false || Str.IsEmptyStr(destString) == false)
						{
							dtValue = Str.StrToDateTime(destString);
						}

						if (!(dta.AllowZero && dtValue == Cdf.ZeroDateTimeValue))
						{
							if (Str.IsEmptyStr(dta.MinValue) == false && dtValue < Str.StrToDateTime(dta.MinValue))
							{
								throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DT_MIN),
									Str.DateTimeToStr(Str.StrToDateTime(dta.MinValue))));
							}

							if (Str.IsEmptyStr(dta.MaxValue) == false && dtValue > Str.StrToDateTime(dta.MaxValue))
							{
								throw new FormatException(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DT_MAX),
									Str.DateTimeToStr(Str.StrToDateTime(dta.MaxValue))));
							}
						}

						SetData(data, dtValue);
					}
					break;
			}
		}

		public string GetString(object data)
		{
			return GetString(data, false);
		}
		public string GetString(object data, bool printMode)
		{
			object d = GetData(data);
			string ret = "";

			switch (this.Type)
			{
				case CdfMetaFieldType.Enum:
					CdfEnumAttribute ea = (CdfEnumAttribute)this.CdfFieldAttribute;

					int intValue = (int)d;

					foreach (CdfMetaEnumItem mi in this.MetaEnum.Items)
					{
						if (mi.ValueInt == intValue)
						{
							ret = mi.Attribute.FriendlyName;
						}
					}
					
					break;

				case CdfMetaFieldType.CandidateList:
					CdfCandidateListAttribute ca = (CdfCandidateListAttribute)this.CdfFieldAttribute;
					CandidateList cl = (CandidateList)this.DotNetFieldInfo.GetValue(data);

					int iValue = cl.Value;

					ret = cl.FriendlyString;
					Str.NormalizeString(ref ret);

					break;

				case CdfMetaFieldType.CandidateStrList:
					CdfCandidateStrListAttribute ca2 = (CdfCandidateStrListAttribute)this.CdfFieldAttribute;
					CandidateStrList cl2 = (CandidateStrList)this.DotNetFieldInfo.GetValue(data);

					string sValue = cl2.Value;

					ret = cl2.FriendlyString;
					Str.NormalizeString(ref ret);

					break;

				case CdfMetaFieldType.String:
					ret = (string)d;
					CdfStringAttribute sa = (CdfStringAttribute)this.CdfFieldAttribute;
					Str.NormalizeString(ref ret);

					if (this.CdfFieldAttribute.EmptyAsDefault && sa.DefaultValue.Equals(ret, StringComparison.InvariantCultureIgnoreCase))
					{
						ret = "";
					}

					break;

				case CdfMetaFieldType.Bool:
					bool boolValue = (bool)d;

					ret = (boolValue ? "1" : "");

					break;

				case CdfMetaFieldType.Int:
					int i = (int)d;
					CdfIntAttribute ia = (CdfIntAttribute)this.CdfFieldAttribute;

					if (Str.IsEmptyStr(CdfFieldAttribute.PrintFormat) == false)
					{
						ret = i.ToString(CdfFieldAttribute.PrintFormat);
					}
					else
					{
						if (ia.Comma == false)
						{
							ret = i.ToString();
						}
						else
						{
							ret = Str.ToStr3(i);
						}

						if (ia.ShowPlus)
						{
							if (i > 0)
							{
								ret = "+" + ret;
							}
						}
					}

					if (ia.EmptyAsDefault)
					{
						if (ia.DefaultValue == i)
						{
							ret = "";
						}
					}

					break;

				case CdfMetaFieldType.Long:
					long i64 = (long)d;
					CdfLongAttribute ia64 = (CdfLongAttribute)this.CdfFieldAttribute;

					if (Str.IsEmptyStr(CdfFieldAttribute.PrintFormat) == false)
					{
						ret = i64.ToString(CdfFieldAttribute.PrintFormat);
					}
					else
					{
						if (ia64.Comma == false)
						{
							ret = i64.ToString();
						}
						else
						{
							ret = Str.ToStr3(i64);
						}

						if (ia64.ShowPlus)
						{
							if (i64 > 0)
							{
								ret = "+" + ret;
							}
						}
					}

					if (ia64.EmptyAsDefault)
					{
						if (ia64.DefaultValue == i64)
						{
							ret = "";
						}
					}

					break;

				case CdfMetaFieldType.Double:
					double dbl = (double)d;
					CdfDoubleAttribute da = (CdfDoubleAttribute)this.CdfFieldAttribute;

					if (Str.IsEmptyStr(CdfFieldAttribute.PrintFormat) == false)
					{
						ret = dbl.ToString(CdfFieldAttribute.PrintFormat);
					}
					else
					{
						ret = dbl.ToString();

						if (da.ShowPlus)
						{
							if (dbl > 0)
							{
								ret = "+" + ret;
							}
						}
					}

					if (da.EmptyAsDefault)
					{
						if (da.DefaultValue == dbl)
						{
							ret = "";
						}
					}

					break;

				case CdfMetaFieldType.DateTime:
					DateTime dt = (DateTime)d;
					CdfDateTimeAttribute dta = (CdfDateTimeAttribute)this.CdfFieldAttribute;

					if (dta.Type == CdfDateTimeType.DateOnly)
					{
						if (printMode == false)
						{
							ret = dt.ToString("yyyy/MM/dd");
						}
						else
						{
							ret = Str.DateToStr(dt);
						}
					}
					else if (dta.Type == CdfDateTimeType.TimeOnly)
					{
						if (printMode == false)
						{
							ret = dt.ToString("HH:mm:ss");
						}
						else
						{
							ret = Str.TimeToStr(dt);
						}
					}
					else
					{
						if (printMode == false)
						{
							ret = dt.ToString("yyyy/MM/dd HH:mm:ss");
						}
						else
						{
							ret = Str.DateTimeToStr(dt);
						}
					}

					break;
			}

			Str.NormalizeString(ref ret);

			return ret;
		}
	}

	public class CdfMetaClass
	{
		public List<CdfMetaField> Fields = new List<CdfMetaField>();
		public CdfClassAttribute CdfClass;
		public Type Type;
		public string ID;

		public CdfMetaField FindField(string name)
		{
			foreach (CdfMetaField f in this.Fields)
			{
				if (f.Name == name)
				{
					return f;
				}
			}

			return null;
		}

		public override string ToString()
		{
			return Type.Name;
		}
	}

	public class Cdf
	{
		public readonly Type BaseClassType;
		public readonly CdfMetaClass MetaClass;
		public static readonly DateTime ZeroDateTimeValue = new DateTime(1800, 1, 1);

		Dictionary<string, CdfMetaClass> clsList = new Dictionary<string, CdfMetaClass>();

		public Cdf(Type type)
		{
			this.BaseClassType = type;

			MetaClass = loadClass(this.BaseClassType);
		}

		public static void DownloadFile(Page page, string filename, Encoding filenameEncoding, byte[] data)
		{
			DownloadFile(page, filename, filenameEncoding, data, "application/octet-stream");
		}
		public static void DownloadFile(Page page, string filename, Encoding filenameEncoding, byte[] data, string mime)
		{
			try
			{
				page.Response.Filter = null;
			}
			catch
			{
			}

			page.Response.AddHeader("Content-Disposition",
				string.Format("attachment; filename=\"{0}\"", Str.ToUrl(filename, filenameEncoding).Replace("+", " ")));
			page.Response.AddHeader("Content-Length", data.Length.ToString());
			page.Response.AddHeader("Content-Type", mime);
			page.Response.Flush();

			page.Response.OutputStream.Write(data, 0, data.Length);
			page.Response.OutputStream.Flush();
			page.Response.Flush();
			page.Response.End();
		}

		bool hasClass(string id)
		{
			return clsList.ContainsKey(id);
		}

		CdfMetaClass getClass(string id)
		{
			if (clsList.ContainsKey(id))
			{
				return clsList[id];
			}

			return null;
		}

		CdfMetaClass loadClass(Type t)
		{
			if (hasClass(t.GUID.ToString()) == false)
			{
				CdfMetaClass c = parseClass(t);

				clsList.Add(c.ID, c);

				Console.WriteLine(c.Type.Name);

				return c;
			}
			else
			{
				return getClass(t.GUID.ToString());
			}
		}

		CdfMetaClass parseClass(Type t)
		{
			CdfClassAttribute[] al = (CdfClassAttribute[])t.GetCustomAttributes(typeof(CdfClassAttribute), true);
			if (al.Length != 1)
			{
				throw new Exception(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_NO_CDFCLASS), t.Name));
			}

			CdfClassAttribute a = al[0];

			XmlIncludeAttribute[] xi = (XmlIncludeAttribute[])t.GetCustomAttributes(typeof(XmlIncludeAttribute), true);
			foreach (XmlIncludeAttribute x in xi)
			{
				loadClass(x.Type);
			}

			CdfMetaClass c = new CdfMetaClass();
			c.CdfClass = a;
			c.Fields = new List<CdfMetaField>();
			c.ID = t.GUID.ToString();
			c.Type = t;

			FieldInfo[] fList = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
			foreach (FieldInfo f in fList)
			{
				CdfMetaField ff = parseField(f);

				if (ff != null)
				{
					c.Fields.Add(ff);
				}
			}

			return c;
		}

		public static string GetEnumFriendlyName(Type t, string name)
		{
			string ret = null;

			CdfMetaEnum me = ParseEnum(t);
			CdfMetaEnumItem ei = me.GetEnumItem(name);
			if (ei != null)
			{
				ret = ei.FriendlyName;
			}

			return ret;
		}

		public static string GetEnumFriendlyName(Type t, int i)
		{
			string ret = null;

			CdfMetaEnum me = ParseEnum(t);
			CdfMetaEnumItem ei = me.GetEnumItem(i);
			if (ei != null)
			{
				ret = ei.FriendlyName;
			}

			return ret;
		}

		public static string GetEnumFriendlyName(object v)
		{
			return GetEnumFriendlyName(v.GetType(), (int)v);
		}

		public static CdfMetaEnum ParseEnum(Type t)
		{
			CdfMetaEnum e = new CdfMetaEnum();

			e.Type = t;
			e.Items = new List<CdfMetaEnumItem>();
			FieldInfo[] fis = t.GetFields();

			foreach (FieldInfo fi in fis)
			{
				if (fi.IsStatic && fi.IsPublic && fi.IsLiteral)
				{
					CdfMetaEnumItem ei = new CdfMetaEnumItem();

					ei.Attribute = (CdfEnumItemAttribute)getSingleAttribute(fi.Name,
						fi.GetCustomAttributes(false),
						typeof(CdfEnumItemAttribute));

					ei.ValueString = fi.Name;

					ei.ValueInt = (int)fi.GetRawConstantValue();

					e.Items.Add(ei);
				}
			}

			return e;
		}

		public CdfMetaField parseField(FieldInfo f)
		{
			if (hasAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfFieldAttribute)) == false)
			{
				return null;
			}

			CdfMetaField ff = new CdfMetaField();
			ff.DotNetFieldInfo = f;

			if (f.FieldType == typeof(string))
			{
				CdfStringAttribute cs = (CdfStringAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfStringAttribute));
				ff.CdfFieldAttribute = cs;
				ff.Type = CdfMetaFieldType.String;
				ff.Name = f.Name;
				ff.ListMetaClass = null;
			}
			else if (f.FieldType == typeof(int))
			{
				CdfIntAttribute ci = (CdfIntAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfIntAttribute));
				ff.CdfFieldAttribute = ci;
				ff.Type = CdfMetaFieldType.Int;
				ff.Name = f.Name;
				ff.ListMetaClass = null;
			}
			else if (f.FieldType == typeof(long))
			{
				CdfLongAttribute cl = (CdfLongAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfLongAttribute));
				ff.CdfFieldAttribute = cl;
				ff.Type = CdfMetaFieldType.Long;
				ff.Name = f.Name;
				ff.ListMetaClass = null;
			}
			else if (f.FieldType == typeof(double))
			{
				CdfDoubleAttribute cd = (CdfDoubleAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfDoubleAttribute));
				ff.CdfFieldAttribute = cd;
				ff.Type = CdfMetaFieldType.Double;
				ff.Name = f.Name;
				ff.ListMetaClass = null;
			}
			else if (f.FieldType == typeof(DateTime))
			{
				CdfDateTimeAttribute cd = (CdfDateTimeAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfDateTimeAttribute));
				ff.CdfFieldAttribute = cd;
				ff.Type = CdfMetaFieldType.DateTime;
				ff.Name = f.Name;
				ff.ListMetaClass = null;
			}
			else if (f.FieldType == typeof(bool))
			{
				CdfBoolAttribute cd = (CdfBoolAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfBoolAttribute));
				ff.CdfFieldAttribute = cd;
				ff.Type = CdfMetaFieldType.Bool;
				ff.Name = f.Name;
				ff.ListMetaClass = null;
			}
			else if (f.FieldType.IsEnum)
			{
				CdfEnumAttribute ce = (CdfEnumAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfEnumAttribute));
				ff.CdfFieldAttribute = ce;
				ff.ListMetaClass = null;
				ff.Name = f.Name;
				ff.Type = CdfMetaFieldType.Enum;
				ff.MetaEnum = ParseEnum(f.FieldType);
			}
			else if (f.FieldType == typeof(CandidateList))
			{
				CdfCandidateListAttribute ca = (CdfCandidateListAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfCandidateListAttribute));
				ff.CdfFieldAttribute = ca;
				ff.ListMetaClass = null;
				ff.Name = f.Name;
				ff.Type = CdfMetaFieldType.CandidateList;
			}
			else if (f.FieldType == typeof(CandidateStrList))
			{
				CdfCandidateStrListAttribute ca = (CdfCandidateStrListAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfCandidateStrListAttribute));
				ff.CdfFieldAttribute = ca;
				ff.ListMetaClass = null;
				ff.Name = f.Name;
				ff.Type = CdfMetaFieldType.CandidateStrList;
			}
			else if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<object>).GetGenericTypeDefinition())
			{
				Type[] genericTypes = f.FieldType.GetGenericArguments();
				if (genericTypes.Length != 1)
				{
					throw new Exception(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_NO_SINGLE_GENERIC_TYPE),
						f.Name));
				}

				ff.CdfFieldAttribute = (CdfListAttribute)getSingleAttribute(f.Name, f.GetCustomAttributes(false), typeof(CdfListAttribute));
				ff.ListMetaClass = loadClass(genericTypes[0]);
				ff.Name = f.Name;
				ff.Type = CdfMetaFieldType.List;
			}
			else
			{
				throw new Exception(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_UNSUPPORTED), f.Name, f.FieldType.Name));
			}

			return ff;
		}

		bool hasAttribute(string name, object[] attributes, Type baseAttributeType)
		{
			List<Attribute> o = new List<Attribute>();

			foreach (object att in attributes)
			{
				if (att.GetType() == baseAttributeType || att.GetType().BaseType == baseAttributeType)
				{
					o.Add((Attribute)att);
				}
			}

			if (o.Count == 0)
			{
				return false;
			}

			return true;
		}

		static Attribute getSingleAttribute(string name, object[] attributes, Type attributeType)
		{
			List<Attribute> o = new List<Attribute>();

			foreach (object att in attributes)
			{
				if (att.GetType() == attributeType)
				{
					o.Add((Attribute)att);
				}
			}

			if (o.Count != 1)
			{
				throw new Exception(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_NO_SINGLE_ATT),
					name, attributeType.Name));
			}

			return o[0];
		}

		public static bool SetError(Dictionary<string, CdfFormRow> rows, string fieldName, string errorString)
		{
			if (rows[fieldName].ErrorString == null)
			{
				rows[fieldName].ErrorString = errorString;
				return true;
			}

			return false;
		}
	}
}


