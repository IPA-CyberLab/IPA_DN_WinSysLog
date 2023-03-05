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
	// クリックの種類
	public enum ClickType
	{
		Ok,		// OK ボタン
		Cancel,	// キャンセルボタン
		Add,	// 追加ボタン
		Delete,	// 削除ボタン
		Edit,	// 編集ボタン
		Custom,	// その他
	}

	// クリックデータ
	public class ClickData
	{
		public ClickType Type;
		public Button Button;
		public int Index;
		public object TargetData;
		public IList TargetList;
		public object Data;
		public string DataName;
		public string ActionName;
		public CdfForm CdfForm;

		public ClickData(Button b)
		{
			string id = b.ID;

			string dataName;
			string actionName;

			ParseButtonIDStr(id, out dataName, out actionName);

			int index = -1;

			if (actionName == "edit" || actionName == "delete")
			{
				int i;
				int j = -1;
				for (i = 0; i < dataName.Length; i++)
				{
					if (dataName[i] == '_')
					{
						j = i;
					}
				}

				if (j == -1)
				{
					throw new Exception("(j == -1)");
				}

				index = Str.StrToInt(dataName.Substring(j + 1));
				dataName = dataName.Substring(0, j);
			}

			switch (actionName)
			{
				case "ok":
					Type = ClickType.Ok;
					break;

				case "cancel":
					Type = ClickType.Cancel;
					break;

				case "add":
					Type = ClickType.Add;
					break;

				case "delete":
					Type = ClickType.Delete;
					break;

				case "edit":
					Type = ClickType.Edit;
					break;

				default:
					Type = ClickType.Custom;
					break;
			}

			this.Button = b;
			this.Index = index;
			this.ActionName = actionName;
			this.DataName = dataName;
		}

		public static void ParseButtonIDStr(string id, out string dataName, out string actionName)
		{
			dataName = actionName = "";

			if (id == "ok")
			{
				actionName = "ok";
			}
			else if (id == "cancel")
			{
				actionName = "cancel";
			}
			else if (id.IndexOf("_") != -1)
			{
				int i;
				int j = -1;
				for (i = 0; i < id.Length; i++)
				{
					if (id[i] == '_')
					{
						j = i;
					}
				}

				if (j == -1)
				{
					throw new Exception("(j == -1)");
				}

				actionName = id.Substring(j + 1);
				dataName = id.Substring(0, j);
			}
			else
			{
				actionName = id;
			}
		}
	}

	// フォーム行
	public class CdfFormRow
	{
		public int Index = 0;
		public Table Table = null;
		public CdfMetaField MetaField = null;
		public TableRow TableRow = null;
		public TableCell FriendlyNameCell = null;
		public TableCell ValueCell = null;
		public TableCell NoteCell = null;
		public WebControl MainControl = null;
		public Label FriendlyNameLabel = null;
		public Label NoteLabel = null;
		public Label ErrorLabel = null;
		public Label RequireLabel = null;
		public string ErrorString = null;
	}

	// RawHtml コントロール
	public class RawHtmlControl : WebControl
	{
		public readonly string Body;

		public RawHtmlControl(string body)
			: base(HtmlTextWriterTag.Unknown)
		{
			this.Body = body;
		}

		public override void RenderBeginTag(HtmlTextWriter writer)
		{
		}

		public override void RenderEndTag(HtmlTextWriter writer)
		{
		}

		protected override void RenderContents(HtmlTextWriter writer)
		{
			if (Str.IsEmptyStr(Body))
			{
				return;
			}

			writer.Write(Body);
		}
	}

	// JavaScript コントロール
	public class JavaScriptControl : WebControl
	{
		public readonly string Body;

		public JavaScriptControl(string body)
			: base(HtmlTextWriterTag.Unknown)
		{
			this.Body = body;
		}

		public override void RenderBeginTag(HtmlTextWriter writer)
		{
		}

		public override void RenderEndTag(HtmlTextWriter writer)
		{
		}

		protected override void RenderContents(HtmlTextWriter writer)
		{
			if (Str.IsEmptyStr(Body))
			{
				return;
			}

			writer.Write("<script type=\"text/javascript\">\n<!--\n" +
				this.Body + "\n// -->\n</script>");
		}
	}

	// フォームの種類
	public enum CdfFormMode
	{
		Edit,		// 編集用
		Print,		// 表示用
		GridPrint,	// グリッド表示用
	}

	// フォーム
	public class CdfForm
	{
		public static string StandardFont = "Arial";//CoreStr.CDF_GOTHIC;
		public readonly CdfFormMode Mode;
		public readonly object Data;
		public readonly Cdf Cdf;
		public readonly string BaseControlName;
		public readonly WebControl ParentControl;
		public const string SpaceBetweenButtons = Str.HtmlSpacing + Str.HtmlSpacing;
		public string History_FontName = StandardFont;
		public bool History_FontBold = true;
		public Color History_FontColor = Color.FromName("#6666ff");
		public FontUnit History_FontSize = new FontUnit(FontSize.XSmall);
		public int History_MaxPrintLength = 31;
		public string HistoryLabel_FontName = StandardFont;
		public bool HistoryLabel_FontBold = false;
		public Color HistoryLabel_FontColor = Color.FromName("#6666ff");
		public FontUnit HistoryLabel_FontSize = new FontUnit(FontSize.Smaller);
		public string Edit_FontName = StandardFont;//CoreStr.CDF_GOTHIC;
		public string EditDropdown_FontName = StandardFont;
		public Color Edit_FontColor = Color.FromName("#0000FF");
		public string Print_FontName = StandardFont;
		public string Paging_FontName = StandardFont;
		public Color Paging_FontColor = Color.BlueViolet;
		public bool Paging_FontBold = true;
		public string Sort_FontName = StandardFont;
		public Color Sort_FontColor = Color.White;
		public Color Sort_BackColor = Color.FromName("#669966");
		public bool Sort_FontBold = true;

		public bool Search_FontBold = true;
		public string Search_FontName = StandardFont;
		public Color Search_FontColor = Color.Black;
		public Color Search_BackColor = Color.White;
		public Unit Search_Width = new Unit(200);

		public Color Print_FontColor = Color.Black;
		public Color FieldName_FontColor = Color.FromName("#007700");
		public bool FieldName_FontBold = true;
		public Color ListLabel_FontColor = Color.FromName("#55559f");
		public bool ListLabel_FontBold = false;
		public Color Error_FontColor = Color.Red;
		public Color Confirm_FontColor = Color.FromName("#6600FF");
		public Color Complete_FontColor = Color.FromName("#000099");
		public Color ErrorRow_BackColor = Color.FromName("#FFFF99");
		public Color HeaderRow_BackColor = Color.FromName("#F1EDF1");
		public Color GridHeaderRow_BackColor = Color.FromName("#97C913");
		public Color GridHeaderRow_FontColor = Color.FromName("#FFFFFF");
		public Color Header_FontColor = Color.FromName("#333399");
		public Color EditReadOnly_FontColor = Color.FromName("#5555ff");
		public Color EditReadOnly_BackColor = Color.FromName("#eeeeee");
		public readonly Page Page;
		public bool DrawTitle = true;
		public bool TopLevel = true;
		public Color Title_BackColor = Color.FromName("#669966");
		public Color Row1_BackColor = Color.FromName("#EFFAD2");
		public Color Row2_BackColor = Color.FromName("#F9FDEE");
		public Color ClusterTitle_BackColor = Color.FromName("#009933");
		public Color ClusterTitle_FontColor = Color.White;
		public bool ClusterTitle_FontBold = true;
		public Color Row_MouneColor = Color.White;
		public Color Row1Confirm_BackColor = Color.FromName("#E8E8FF");
		public Color Row2Confirm_BackColor = Color.FromName("#F4F4FF");
		public string AddButton_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_BUTTON_ADD);
		public string EditButton_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_BUTTON_EDIT);
		public string DeleteButton_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_BUTTON_DELETE);
		public string OkButton_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_BUTTON_OK);
		public string CompleteButton_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_BUTTON_COMPLETE);
		public string OkSendingButton_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_BUTTON_SENDING);
		public string CancelButton_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_BUTTON_CANCEL);
		public string ModifyButton_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_BUTTON_MODIFY);
		public string OkLabel_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_TEXT_OK);
		public string ConfirmLabel_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_TEXT_CONFIRM);
		public string ConfirmErrorLabel_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_TEXT_CONFIRM_ERROR);
		public Unit TableSize = new Unit("99.5%");
		public Unit MainButtonSizeX = new Unit("110px");
		public Unit MainButtonSizeY = new Unit("28px");
		public bool ShowOKButton = true;
		public HorizontalAlign OKButtonAlign = HorizontalAlign.Center;
		public delegate void ButtonClickEvent(CdfForm cdfForm, Button button, string id, ClickData cd);
		public event ButtonClickEvent ButtonClickEvents;
		public delegate void ValidateEvent(CdfForm cdfForm, object data, Dictionary<string, CdfFormRow> rows);
		public event ValidateEvent ValidateEvents;
		public delegate void AfterControlCreatedEvent(CdfForm cdfForm, Table table);
		public event AfterControlCreatedEvent AfterControlCreatedEvents;
		public delegate void SubFormCreatedEvent(CdfForm cdfForm, CdfForm subForm, string fieldName, IList list, object data, int index);
		public event SubFormCreatedEvent SubFormCreatedEvents;
		public delegate void GridRowRenderingEvent(CdfForm cdfForm, object data, int rowIndex, int columnIndex, IList list,
			WebControl mainControl, TableCell cell, CdfMetaField f, string fieldName);
		public event GridRowRenderingEvent GridRowRenderingEvents;
		public bool DebugMode = false;
		public CdfFormManager FormManager;
		public readonly bool ConfirmMode = false;
		public readonly bool CompleteMode = false;
		public List<string> HiddenFields = new List<string>();
		public bool ShowButtonOnPrintMode = false;
		public bool GridHideButtonIfEmpty = true;
		public bool GridUseMouseColorChange = true;
		public string HeaderText = "";
		public readonly bool GridMode = false;
		public readonly int GridFieldsCount = 0;
		public bool GridUsePaging = false;
		public readonly int GridNumRowsOfCurrentData = 0;
		public readonly int GridNumRowsOfCurrentDataOriginal = 0;
		public string MasterKeyFieldName = "";
		public bool GridPagingShowLatest = true;
		public bool GridPagingShowAll = true;
		public string GridPagingDropdownStringFormat = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_PAGING_FORMAT_1);
		public string GridPagingDropdownLatestStringFormat = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_PAGING_FORMAT_2);
		public string GridPagingDropdownAllStringFormat = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DL_ALL);
		public string GridEmptyString = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_ROW_EMPTY);
		public bool ShowDownloadButton = true;
		public string DownloadButton_Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_DOWNLOAD);
		public bool GridShowEmptyStringWhenEmpty = true;
		public Button ButtonOK, ButtonCancel;
		public bool UseConfirm = true;
		public bool HideNote = false;
		List<string> checkedList = new List<string>();
		public bool IsGridEmpty = false;
		public HorizontalAlign TableAlign = HorizontalAlign.Center;
		public readonly string GridPagingControlId;
		public bool GridShowSort = true;
		public bool GridShowSearch = false;
		public bool GridPrintNumCountOnTitle = true;
		public string CurrentSearchText = "";
		public bool HideEmptyField = false;
		public bool DoNotFocus = false;
		public bool NoHyperLink = false;
		public bool IgnoreCluster = false;

		// 指定された項目がチェックされているかどうか取得
		public bool IsChecked(string fieldName, string keyName, object keyValue)
		{
			string name = GenerateCheckBoxName(fieldName, keyName, keyValue);

			return checkedList.Contains(name);
		}

		// チェックされているチェックボックスの一覧の取得
		void getCheckedCheckBoxes()
		{
			checkedList = new List<string>();

			foreach (string name in Page.Request.Form.Keys)
			{
				if (IsCheckBoxName(name))
				{
					string value = Page.Request.Form[name];

					if (Str.IsEmptyStr(value) == false)
					{
						checkedList.Add(name);
					}
				}
			}
		}

		// チェックボックス名かどうか検査
		public static bool IsCheckBoxName(string name)
		{
			return name.StartsWith("cdfcheckbox_");
		}

		// チェックボックス名の生成
		public static string GenerateCheckBoxName(string fieldName, string keyName, object keyValue)
		{
			return string.Format("cdfcheckbox_{0}_{1}_{2}",
							fieldName,
							keyName,
							keyValue);
		}

		// フッタ部へのボタンの追加
		public Button AddButtonToFooter(string id, string text)
		{
			return AddButtonToFooter(id, text, null);
		}
		public Button AddButtonToFooter(string id, string text, string msgBox)
		{
			Label a = new Label();
			a.Text = CdfForm.SpaceBetweenButtons;
			this.ButtonCancel.Parent.Controls.Add(a);

			Button b = this.NewButton(id, text, true);
			this.ButtonCancel.Parent.Controls.Add(b);

			if (Str.IsEmptyStr(msgBox) == false)
			{
				AddConfirnMessageToButton(b, msgBox);
			}

			return b;
		}

		// フッタ部への削除ボタンの追加
		public Button AddRemoveButtonToFooter()
		{
			return AddButtonToFooter("remove", CdfGlobalLangSettings.ProcStr(CoreStr.CDF_REMOVE),
				CdfGlobalLangSettings.ProcStr(CoreStr.CDF_REMOVEMSG));
		}

		// ボタンへの確認メッセージの追加
		public static void AddConfirnMessageToButton(Button b, string str)
		{
			try
			{
				b.Attributes["onclick"] = "return confirm('" + Str.Unescape(str) + "');";
			}
			catch
			{
			}
		}

		// 1 ページあたりの行数
		int gridRowsPerPage = 20;
		public int GridRowsPerPage
		{
			get
			{
				if (this.GridUsePaging)
				{
					return gridRowsPerPage;
				}
				else
				{
					return int.MaxValue;
				}
			}
			set
			{
				if (value >= 1)
				{
					gridRowsPerPage = value;
				}
				else
				{
					gridRowsPerPage = 1;
				}
			}
		}
		// 合計のページ数
		public int GridNumPages
		{
			get
			{
				if ((this.GridNumRowsOfCurrentData % GridRowsPerPage) == 0)
				{
					return this.GridNumRowsOfCurrentData / GridRowsPerPage;
				}
				else
				{
					return this.GridNumRowsOfCurrentData / GridRowsPerPage + 1;
				}
			}
		}
		// 現在のページ番号
		int gridCurrentPage = int.MaxValue;
		public int GridCurrentPage
		{
			get
			{
				if (this.GridUsePaging == false)
				{
					return 0;
				}
				if (this.gridCurrentPage == int.MaxValue)
				{
					if (this.GridPagingShowLatest == false)
					{
						return 0;
					}
				}
				return this.gridCurrentPage;
			}
			set
			{
				int i = value;
				if (i < 0)
				{
					i = 0;
				}
				if (i != int.MaxValue && i != (int.MaxValue - 1))
				{
					if (i >= (GridNumPages - 1))
					{
						i = GridNumPages - 1;
					}
				}

				this.gridCurrentPage = i;
			}
		}
		// 最初の行番号
		public int GridPagingFirstRow
		{
			get
			{
				if (this.GridUsePaging == false)
				{
					return 0;
				}
				else
				{
					return CalcFirstRow(this.GridCurrentPage);
				}
			}
		}
		public int CalcFirstRow(int page)
		{
			if (this.GridUsePaging == false)
			{
				return 0;
			}
			if (page == int.MaxValue)
			{
				return Math.Max(this.GridNumRowsOfCurrentData - this.GridRowsPerPage, 0);
			}
			if (page == int.MaxValue - 1)
			{
				return 0;
			}
			return this.GridRowsPerPage * page;
		}
		// 最後の行番号
		public int GridPagingLastRow
		{
			get
			{
				if (this.GridUsePaging == false)
				{
					return Math.Max(this.GridNumRowsOfCurrentData - 1, 0);
				}
				else
				{
					return CalcLastRow(this.GridCurrentPage);
				}
			}
		}
		public int CalcLastRow(int page)
		{
			int i = Math.Max(this.GridRowsPerPage * (page + 1) - 1, 0);

			if (page == int.MaxValue || page == int.MaxValue - 1)
			{
				i = int.MaxValue;
			}

			return Math.Max(Math.Min(this.GridNumRowsOfCurrentData - 1, i), 0);
		}
		// 指定された行が範囲内かどうか取得
		public bool IsRowInPagingRange(int i)
		{
			if (GridPagingFirstRow <= i && GridPagingLastRow >= i)
			{
				return true;
			}

			return false;
		}

		// ソートコントロールの作成
		DropDownList createSortControl()
		{
			string id = BaseControlName + "sort";

			DropDownList dl = new DropDownList();
			dl.Font.Name = this.Sort_FontName;
			dl.ForeColor = this.Sort_FontColor;
			dl.BackColor = this.Sort_BackColor;
			dl.Font.Bold = this.Sort_FontBold;
			dl.ID = id;

			dl.EnableViewState = false;

			dl.Items.Add(new ListItem(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_SORT_DEF1), ""));
			dl.Items.Add(new ListItem(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_SORT_DEF2), "0:0"));

			CdfMetaClass mc = this.Cdf.MetaClass.Fields[0].ListMetaClass;

			foreach (CdfMetaField f in mc.Fields)
			{
				if (f.CdfFieldAttribute.GridHide == false)
				{
					string str = f.CdfFieldAttribute.FriendlyName;
					str = Str.ReplaceStr(str, "<BR>", "");
					dl.Items.Add(new ListItem(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_SORT_1), str),
						string.Format("{0}:{1}", f.Name, 1)));

					dl.Items.Add(new ListItem(string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_SORT_2), str),
						string.Format("{0}:{1}", f.Name, -1)));
				}
			}

			dl.AutoPostBack = true;

			return dl;
		}

		// ページングコントロールの作成
		DropDownList createPagingControl()
		{
			string id = GridPagingControlId;

			DropDownList dl = new DropDownList();
			int num = 0;

			int i;
			for (i = 0; i < this.GridNumPages; i++)
			{
				string str = string.Format(this.GridPagingDropdownStringFormat,
					i + 1);

				int startRow = CalcFirstRow(i);
				int lastRow = CalcLastRow(i);
				str = getPagingHelpString(str, startRow, lastRow);

				ListItem li = new ListItem(str, i.ToString());

				dl.Items.Add(li);

				num++;
			}

			if (this.GridPagingShowLatest && this.GridNumRowsOfCurrentData >= 1)
			{
				int startRow = CalcFirstRow(int.MaxValue);
				int lastRow = CalcLastRow(int.MaxValue);

				int numRows = (lastRow - startRow) + 1;

				string str = string.Format(this.GridPagingDropdownLatestStringFormat,
					numRows);

				str = getPagingHelpString(str, startRow, lastRow);

				ListItem li = new ListItem(str, int.MaxValue.ToString());

				dl.Items.Add(li);
			}

			if (this.GridPagingShowAll && this.GridNumRowsOfCurrentData >= 1)
			{
				int startRow = 0;
				int lastRow = CalcLastRow(int.MaxValue);

				int numRows = (lastRow - startRow) + 1;

				string str = string.Format(this.GridPagingDropdownAllStringFormat,
					numRows);

				str = getPagingHelpString(str, startRow, lastRow);

				ListItem li = new ListItem(str, (int.MaxValue - 1).ToString());

				dl.Items.Add(li);
			}

			dl.Font.Name = this.Paging_FontName;
			dl.ForeColor = this.Paging_FontColor;
			dl.Font.Bold = this.Paging_FontBold;
			dl.ID = id;

			if (num <= 1)
			{
				//				dl.Visible = false;
			}

			if (num == 1)
			{
				dl.Items.RemoveAt(0);
			}

			if (dl.Items.Count == 0)
			{
				dl.Items.Add(new ListItem(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_NOITEM), int.MaxValue.ToString()));
			}

			dl.EnableViewState = false;

			dl.AutoPostBack = true;

			if (this.Page.IsPostBack == false)
			{
				try
				{
					dl.SelectedValue = this.GridCurrentPage.ToString();
				}
				catch
				{
				}
			}

			return dl;
		}

		string getPagingHelpString(string str, int startRow, int lastRow)
		{
			// 主キー
			if (Str.IsEmptyStr(this.MasterKeyFieldName) == false)
			{
				object masterData = this.Data.GetType().GetField(this.MasterKeyFieldName);

				IList list = (IList)this.gridFields.DotNetFieldInfo.GetValue(this.Data);

				string startString = gridFields.ListMetaClass.FindField(this.MasterKeyFieldName).GetString(
					list[startRow], false);

				string endString = gridFields.ListMetaClass.FindField(this.MasterKeyFieldName).GetString(
					list[lastRow], false);

				str += string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_PAGING_HELPSTR_FORMAT),
					startString,
					endString,
					gridFields.ListMetaClass.FindField(this.MasterKeyFieldName).CdfFieldAttribute.FriendlyName);
			}
			return str;
		}

		CdfMetaField gridFields = null;
		string buttonOkOnClickFunctionName = "OnOkButtonClick" + Str.GenRandStr().Substring(0, 8);
		Button buttonOk = null;
		string completeRedirectUrl = "";
		string completeMessage = "";
		string rurl_skey = "";
		bool IsSorted = false;

		public string CompleteExtData = "";

		public string XmlDataString
		{
			get
			{
				return Str.ObjectToXMLSimple(this.Data, this.Data.GetType());
			}
		}

		void sortMain()
		{
			string id = BaseControlName + "sort";

			string str = this.Page.Request.Form[id];

			if (Str.IsEmptyStr(str) == false)
			{
				string[] tokens = str.Split(':');

				if (tokens.Length == 2)
				{
					bool reverse = false;
					string name = tokens[0];
					int direction = Str.StrToInt(tokens[1]);

					if (Str.IsEmptyStr(name) == false)
					{
						if (name == "0" && direction == 0)
						{
							reverse = true;
						}

						object listData = gridFields.GetData(this.Data);
						CdfMetaClass mc = Cdf.MetaClass.Fields[0].ListMetaClass;
						Type valueType = mc.Type;
						Type listType = listData.GetType();

						FieldInfo fi = listType.GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);
						object[] a = (object[])fi.GetValue(listData);

						if (reverse == false)
						{
							Array.Sort<object>(a, 0, ((IList)listData).Count, new CompClass(name, valueType, direction));
						}
						else
						{
							Array.Reverse(a, 0, ((IList)listData).Count);
						}

						IsSorted = true;
					}
				}
			}
		}

		class CompClass : IComparer<Object>
		{
			string keyName;
			Type type;
			FieldInfo fi;
			int direction;

			public CompClass(string keyName, Type type, int dir)
			{
				this.keyName = keyName;
				this.type = type;
				this.fi = this.type.GetField(keyName);
				this.direction = dir;
			}

			public int Compare(object x, object y)
			{
				IComparable vx = (IComparable)fi.GetValue(x);
				IComparable vy = (IComparable)fi.GetValue(y);

				return vx.CompareTo(vy) * direction;
			}
		}

		public CdfForm(Page page, CdfFormMode mode, object data, Cdf cdf, string baseControlName, CdfFormManager fm, WebControl parentControl)
		{
			this.Mode = mode;
			this.Data = data;
			this.Cdf = cdf;
			this.Page = page;
			this.FormManager = fm;
			this.ParentControl = parentControl;

			getCheckedCheckBoxes();

			rurl_skey = "redirect_" + Str.MakeSafeFileName(Path.GetFileName(this.Page.Request.FilePath));

			string rurl = (string)this.Page.Session[rurl_skey];
			if (Str.IsEmptyStr(rurl) == false &&
				Str.IsEmptyStr((string)this.Page.Request.QueryString["completed"]) == false)
			{
				AspUtil.Redirect(Page, rurl);
			}

			ConfirmMode = isConfirmMode();
			CompleteMode = isCompleteMode();

			if (CompleteMode)
			{
				ConfirmMode = false;
			}

			if (ConfirmMode)
			{
				this.Mode = CdfFormMode.Print;
			}

			if (this.Mode == CdfFormMode.Print)
			{
				this.HideNote = true;
			}

			if (Str.IsEmptyStr(baseControlName))
			{
				baseControlName = "";
			}
			else
			{
				baseControlName += "_";
			}
			this.BaseControlName = baseControlName;

			if (this.Mode == CdfFormMode.GridPrint)
			{
				this.Mode = CdfFormMode.Print;
				this.GridMode = true;

				// 検索処理
				string eventTarget = (string)this.Page.Request.Form["__EVENTTARGET"];
				if (Str.IsEmptyStr(eventTarget) == false)
				{
					string dataName;
					string actionName;
					ClickData.ParseButtonIDStr(eventTarget, out dataName, out actionName);

					// 検索テキスト
					this.CurrentSearchText = this.Page.Request.Form[this.BaseControlName + "searchtext"];
					Str.NormalizeStringStandard(ref this.CurrentSearchText);
					this.CurrentSearchText = this.CurrentSearchText.Replace(",", "");
				}

				// GridMode の場合はカラム数を取得
				if (cdf.MetaClass.Fields.Count != 1)
				{
					throw new Exception(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_GRID_ERROR_1));
				}

				gridFields = cdf.MetaClass.Fields[0];

				if (gridFields.Type != CdfMetaFieldType.List)
				{
					throw new Exception(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_GRID_ERROR_2));
				}

				int count = 0;
				foreach (CdfMetaField mf in gridFields.ListMetaClass.Fields)
				{
					if (mf.CdfFieldAttribute.GridHide == false)
					{
						count++;
					}
				}

				GridFieldsCount = count;

				// 検索の実行
				if (Str.IsEmptyStr(CurrentSearchText) == false)
				{
					IList srcData = (IList)gridFields.GetData(this.Data);
					List<object> dstData = new List<object>();

					GridNumRowsOfCurrentDataOriginal = srcData.Count;

					foreach (object row in srcData)
					{
						bool b = true;
						StringBuilder sb = new StringBuilder();

						foreach (CdfMetaField mf in gridFields.ListMetaClass.Fields)
						{
							string str = mf.GetString(row).Replace(",", "");
							Str.NormalizeStringStandard(ref str);

							sb.Append(str + " ");
						}

						FieldInfo[] fList = row.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
						foreach (FieldInfo f in fList)
						{
							object o = f.GetValue(row);
							if (o != null && o is string)
							{
								sb.Append(o + " ");
							}
						}

						string sbs = sb.ToString();

						string[] tokens = Str.SplitStringForSearch(this.CurrentSearchText);
						foreach (string token in tokens)
						{
							if (token.StartsWith("-") == false)
							{
								if (Str.InStr(sbs, token, false) == false)
								{
									b = false;
								}
							}
							else
							{
								if (Str.InStr(sbs, token.Substring(1)))
								{
									b = false;
								}
							}
						}

						if (b)
						{
							dstData.Add(row);
						}
					}

					CdfBasicList bl = new CdfBasicList();
					bl.ListData = dstData;

					this.Data = bl;

					FieldInfo fi = bl.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)[0];
					gridFields.DotNetFieldInfo = fi;

					GridNumRowsOfCurrentData = ((IList)gridFields.GetData(this.Data)).Count;
				}
				else
				{
					GridNumRowsOfCurrentDataOriginal = GridNumRowsOfCurrentData = ((IList)gridFields.GetData(this.Data)).Count;
				}

				if (this.GridNumRowsOfCurrentData <= 0)
				{
					IsGridEmpty = true;
				}
			}

			if (CompleteMode)
			{
				this.Mode = CdfFormMode.Print;
				completeRedirectUrl = this.FormManager.GetString("complete_url");
				this.FormManager.SetString("complete", "0");
				this.FormManager.SetString("comfirm", "0");
				bool clear = (Str.StrToInt(this.FormManager.GetString("complete_clear")) == 0 ? false : true);
				completeMessage = this.FormManager.GetString("complete_message");
				this.CompleteExtData = this.FormManager.GetString("complete_ext_data");
				this.Page.Session[rurl_skey] =
					completeRedirectUrl;

				if (clear)
				{
					this.FormManager.ClearData();
				}
			}

			this.GridPagingControlId = baseControlName + "paging";

			if (this.GridMode)
			{
				if (this.GridShowSort)
				{
					try
					{
						sortMain();
					}
					catch
					{
					}
				}
			}
		}

		// データ更新
		public void RefreshData()
		{
			if (this.FormManager != null)
			{
				this.FormManager.SetData(this.Data);
			}

			this.Render();
		}

		// フォーカス設定
		string focusId = null;
		void focus(Control c)
		{
			if (this.DoNotFocus)
			{
				return;
			}
			try
			{
				c.Focus();
			}
			catch
			{
			}

			try
			{
				focusId = c.ID;
			}
			catch
			{
			}
		}

		// 現在これが完了モードかどうか
		bool isCompleteMode()
		{
			if (this.FormManager == null)
			{
				return false;
			}

			int i = Str.StrToInt(this.FormManager.GetString("complete"));

			return i != 0;
		}

		// 現在これが確認モードかどうか
		bool isConfirmMode()
		{
			if (Str.StrToInt((string)this.Page.Request.QueryString["confirm"]) == 0)
			{
				return false;
			}

			if (this.FormManager == null)
			{
				return false;
			}

			int i = Str.StrToInt(this.FormManager.GetString("confirm"));

			return i != 0;
		}

		// 確認モードを解除する
		public void CancelConfirm()
		{
			if (this.FormManager == null)
			{
				return;
			}

			if (ConfirmMode == false)
			{
				return;
			}

			this.FormManager.SetString("confirm", "0");
			this.FormManager.SetString("complete", "0");
			AspUtil.Redirect(this.Page, Path.GetFileName(this.Page.Request.FilePath) + "?rand=" + Str.GenRandStr().Substring(0, 8), true);
		}

		// 確認モードに飛ぶ
		public bool Confirm()
		{
			if (this.FormManager == null)
			{
				return false;
			}

			if (ConfirmMode)
			{
				return true;
			}
			else
			{
				this.FormManager.SetString("confirm", "1");
				this.FormManager.SetString("complete", "0");
				AspUtil.Redirect(this.Page, Path.GetFileName(this.Page.Request.FilePath) + "?confirm=1&rand=" + Str.GenRandStr().Substring(0, 8), true);
				return false;
			}
		}

		// 完了
		public void Complete(string message, string redirectUrl)
		{
			Complete(message, redirectUrl, false);
		}
		public void Complete(string message, string redirectUrl, bool clearData)
		{
			Complete(message, redirectUrl, clearData, null);
		}
		public void Complete(string message, string redirectUrl, bool clearData, string ext_data)
		{
			Str.NormalizeString(ref ext_data);

			this.Page.Session[rurl_skey] = null;

			this.FormManager.SetString("confirm", "0");
			this.FormManager.SetString("complete", "1");
			this.FormManager.SetString("complete_message", message);
			this.FormManager.SetString("complete_url", redirectUrl);
			this.FormManager.SetString("complete_ext_data", ext_data);
			this.FormManager.SetString("complete_clear", clearData ? "1" : "0");

			AspUtil.Redirect(this.Page, Path.GetFileName(this.Page.Request.FilePath) + "?completed=1&rand=" + Str.GenRandStr().Substring(0, 8), true);
		}

		// ボタンの作成
		public Button NewButton(string id, string text)
		{
			return NewButton(id, text, false);
		}
		public Button NewButton(string id, string text, bool mainButton)
		{
			Button b = new Button();

			b.ID = id;
			b.Text = text;
			b.ForeColor = Color.Blue;
			b.Font.Bold = true;

			if (DebugMode)
			{
				b.ToolTip = b.ID;
			}
			else
			{
				string s = b.Text;
				Str.NormalizeString(ref s, true, false, false, true);
				b.ToolTip = s;
			}

			if (mainButton)
			{
				b.Width = this.MainButtonSizeX;
				b.Height = this.MainButtonSizeY;
			}

			b.Click += new EventHandler(buttonClicked);

			return b;
		}

		public bool HasError
		{
			get
			{
				if (firstErrorControl == null && genericError == null)
				{
					return false;
				}

				return true;
			}
		}

		void buttonClicked(object sender, EventArgs e)
		{
			if (sender is Button)
			{
				Button b = (Button)sender;
				string id = b.ID;

				ClickData cd = new ClickData(b);
				cd.CdfForm = this;
				cd.Data = this.Data;

				if (cd.Type == ClickType.Edit || cd.Type == ClickType.Delete)
				{
					cd.TargetList = (IList)this.Cdf.MetaClass.FindField(cd.DataName).GetData(this.Data);

					cd.TargetData = cd.TargetList[cd.Index];
				}

				if (this.HasError == false || cd.Type != ClickType.Ok)
				{
					// エラーが発生していない場合はイベント発生
					if (ButtonClickEvents != null)
					{
						if (cd.Type == ClickType.Cancel && this.ConfirmMode)
						{
							// 確認モードで修正がクリックされた
							CancelConfirm();
						}
						else if (cd.Type == ClickType.Ok && this.CompleteMode)
						{
							// 何もしない
						}
						else
						{
							if (this.FormManager != null)
							{
								if (!(cd.Type == ClickType.Ok || cd.Type == ClickType.Cancel))
								{
									this.FormManager.SetString("lastfocus", id);
								}
								else
								{
									this.FormManager.SetString("lastfocus", null);
								}
							}

							try
							{
								bool f = true;
								if (cd.Type == ClickType.Ok && this.UseConfirm && this.GridMode == false
									&& this.ConfirmMode == false && this.Mode == CdfFormMode.Edit)
								{
									if (this.Confirm() == false)
									{
										f = false;
									}
								}

								if (f)
								{
									ButtonClickEvents(this, b, id, cd);
								}
							}
							catch (Exception ex)
							{
								string msg = ex.Message;

								if (this.DebugMode)
								{
									if (!(ex is ApplicationException))
									{
										msg = Str.ToHtml(ex.ToString());
									}
								}
								else
								{
									try
									{
										string current_language = CdfGlobalLangSettings.ResourceLanguage;
										SerializedError[] serialized_errors = Str.DeserializeErrorStr(msg);
										if (serialized_errors != null)
										{
											foreach (SerializedError se in serialized_errors)
											{
												if (Str.StrCmpi(se.Language, current_language))
												{
													msg = Str.ToHtml(se.ErrorMsg);
													if (Str.IsEmptyStr(se.ErrorMsg))
													{
														msg = string.Format("エラー \"{0}\" が発生しました。", se.Code);
													}
													break;
												}
											}
										}
									}
									catch
									{
									}
								}

								this.GenericError(msg);
							}
						}

						RefreshData();
					}
				}
				else
				{
					if (this.FormManager != null)
					{
						this.FormManager.SetString("lastfocus", null);
					}

					// エラーが発生している場合はイベント発生させずエラーコントロールに
					// フォーカスを移動
					if (genericError != null)
					{
						focus(genericErrorLabel);
					}

					if (firstErrorControl != null)
					{
						focus(firstErrorControl);
					}
				}
			}
		}

		// 一般的なエラーが発生
		public bool GenericError(string str)
		{
			try
			{
				if (this.genericError == null)
				{
					this.genericError = str;
					return true;
				}

				return false;
			}
			finally
			{
				printGenericError();
			}
		}

		// GenericError 行を表紙
		void printGenericError()
		{
			if (this.genericError != null)
			{
				if (this.genericErrorRow != null)
				{
					this.genericErrorRow.Visible = true;
				}

				if (this.genericErrorLabel != null)
				{
					this.genericErrorLabel.Text = this.genericError;
				}
			}
		}


		public class TextData
		{
			public int Depth;
			public string Text;

			public TextData(int depth, string text)
			{
				this.Depth = depth;
				this.Text = text;
			}
		}

		public int TextFormSpacingPerDepth = 2;

		// テキストフォームの作成
		public string GenerateTextForm()
		{
			return GenerateTextForm(0, 80);
		}
		public string GenerateTextForm(int depth, int width)
		{
			List<TextData> d = new List<TextData>();

			// 行データの作成
			RenderTextForm(d, depth);

			StringWriter w = new StringWriter();

			// 行データをテキストに変換
			foreach (TextData t in d)
			{
				string[] lines = ConsoleService.SeparateStringByWidth(t.Text, width - t.Depth * TextFormSpacingPerDepth);

				foreach (string line in lines)
				{
					string tmp = Str.MakeCharArray(' ', t.Depth * TextFormSpacingPerDepth) + line;


					if (Str.IsEmptyStr(tmp))
					{
						tmp = "";
					}

					w.WriteLine(tmp);
				}
			}

			return w.ToString();
		}
		public void RenderTextForm(List<TextData> d, int depth)
		{
			int i = 0;
			string[] hiddens = HiddenFields.ToArray();
			List<CdfMetaField> fields = Cdf.MetaClass.Fields;
			object data = this.Data;

			// 通常モード
			foreach (CdfMetaField f in fields)
			{
				if (Str.IsStrInList(f.Name, true, hiddens) == false)
				{
					renderTextForm(depth, d, f, i++, data);
				}
			}
		}
		void renderTextForm(int depth, List<TextData> d, CdfMetaField f, int index, object Data)
		{
			CdfFieldAttribute fa = f.CdfFieldAttribute;
			string controlName = this.BaseControlName + f.Name;
			string idEdit = controlName + "_edit";
			string idPrint = controlName + "_print";
			
			// 項目名
			d.Add(new TextData(depth, string.Format("[{0}]", f.CdfFieldAttribute.FriendlyName)));

			// 値
			string value = "";

			if (Str.IsEmptyStr(f.CdfFieldAttribute.StringBefore) == false)
			{
				value += f.CdfFieldAttribute.StringBefore + " ";
			}

			string print_value = f.GetString(Data);

			switch (f.Type)
			{
				case CdfMetaFieldType.DateTime:
					DateTime dt = (DateTime)f.GetData(Data);

					if (dt == Cdf.ZeroDateTimeValue || dt.Ticks == 0)
					{
						print_value = "";
					}
					break;
			}

			value += print_value;

			if (Str.IsEmptyStr(f.CdfFieldAttribute.StringAfter) == false)
			{
				value += " " + f.CdfFieldAttribute.StringAfter;
			}

			if (f.Type == CdfMetaFieldType.List)	// リストの次の行以降 (データ本体) を描画
			{
				object data = f.GetData(Data);
				IList list = (IList)data;

				int cn = 0;
				int totalnum = list.Count;

				foreach (object listData in list)
				{
					Cdf cdf2 = new Cdf(listData.GetType());
					CdfForm f2 = new CdfForm(this.Page, CdfFormMode.Print, listData,
	cdf2, controlName + "_" + cn + "_subform", this.FormManager, null);

					d.Add(new TextData(depth + 1, string.Format("{2} ({0} / {1})", cn + 1, totalnum, Str.ToHtml(cdf2.MetaClass.CdfClass.FriendlyName))));

					f2.RenderTextForm(d, depth + 1);

					cn++;
				}
			}

			d.Add(new TextData(depth + 1, value));

			d.Add(new TextData(depth + 1, ""));
		}

		// レンダリング
		Table renderedTable = null;
		WebControl firstErrorControl;
		Dictionary<string, CdfFormRow> formRowList = new Dictionary<string, CdfFormRow>();
		string genericError = null;
		bool afceFlag = false;
		public void Render()
		{
			if (IsGridEmpty)
			{
				if (this.GridShowEmptyStringWhenEmpty)
				{
					this.HeaderText = this.GridEmptyString;
				}
			}

			string eventTarget = (string)this.Page.Request.Form["__EVENTTARGET"];
			if (Str.IsEmptyStr(eventTarget) == false)
			{
				string dataName;
				string actionName;
				ClickData.ParseButtonIDStr(eventTarget, out dataName, out actionName);

				// ページングの発生
				string s = (string)this.Page.Request.Form[GridPagingControlId];

				if (Str.IsEmptyStr(s) == false)
				{
					this.GridCurrentPage = Str.StrToInt(s);
				}

				focusId = eventTarget;
			}

			WebControl parent = this.ParentControl;

			Table t;

			firstErrorControl = null;
			formRowList = new Dictionary<string, CdfFormRow>();

			if (renderedTable != null)
			{
				t = renderedTable;

				int i;
				for (i = t.Rows.Count - 1; i >= 3; i--)
				{
					t.Rows.RemoveAt(i);
				}
			}
			else
			{
				t = createTable();
			}
			t.EnableViewState = false;

			if (this.Mode != CdfFormMode.GridPrint)
			{
				if (this.NoHyperLink == false)
				{
					// GridMode 以外ではフィールドで GridLinkUrlFormat が指定されている
					// 項目は非表示にする
					List<CdfMetaField> fields = Cdf.MetaClass.Fields;

					foreach (CdfMetaField mf in fields)
					{
						if (Str.IsEmptyStr(mf.CdfFieldAttribute.GridLinkUrlFormat) == false)
						{
							this.HiddenFields.Add(mf.Name);
						}
					}
				}
			}

			// 行のレンダリング
			if (CompleteMode == false)
			{
				renderAllRows(t);
			}

			// 内容検証イベント
			if (/*this.Mode == CdfFormMode.Edit && */ValidateEvents != null)
			{
				try
				{
					ValidateEvents(this, this.Data, formRowList);
				}
				catch (Exception ex)
				{
					string msg = ex.Message;

					if (this.DebugMode)
					{
						if (!(ex is ApplicationException))
						{
							msg = Str.ToHtml(ex.ToString());
						}
					}

					this.GenericError(msg);
				}
			}

			// エラーコントロール表示
			if (this.Page.IsPostBack)
			{
				int minIndex = int.MaxValue;
				foreach (string name in formRowList.Keys)
				{
					CdfFormRow fr = formRowList[name];

					if (fr.ErrorString != null)
					{
						if (fr.Index <= minIndex)
						{
							firstErrorControl = fr.MainControl;

							minIndex = fr.Index;
						}

						fr.ErrorLabel.Text = (fr.MainControl is RadioButtonList ? "" : "<BR>")
							+ fr.ErrorString;
						fr.ErrorLabel.Font.Bold = true;
						fr.ErrorLabel.ForeColor = Error_FontColor;

						fr.TableRow.BackColor = ErrorRow_BackColor;

						foreach (WebControl wc in fr.ValueCell.Controls)
						{
							if (wc is RadioButtonList)
							{
								wc.BackColor = ErrorRow_BackColor;
							}
						}
					}
				}

				printGenericError();
			}

			// ボタンの追加
			if (this.TopLevel && (((this.Mode == CdfFormMode.Edit && this.ShowOKButton) || (this.ConfirmMode) || (this.CompleteMode)) || (this.Mode == CdfFormMode.Print && ShowButtonOnPrintMode && !(this.GridMode && this.IsGridEmpty && this.GridHideButtonIfEmpty))))
			{
				TableRow r = new TableRow();
				TableCell c;
				Label a;
				Button b;

				c = new TableCell();
				c.Wrap = true;
				c.BorderStyle = BorderStyle.Solid;
				c.BorderWidth = new Unit(1);
				c.BorderColor = Color.LightGray;
				c.HorizontalAlign = this.OKButtonAlign;
				c.ColumnSpan = 3;
				if (this.GridMode)
				{
					c.ColumnSpan = GridFieldsCount;
				}
				else
				{
					if (HideNote)
					{
						c.ColumnSpan--;
					}
				}

				r.CssClass = "tableStandard";

				r.BackColor = getNextRowColor();

				a = new Label();
				a.Text = (ConfirmMode ? (this.genericError == null ? ConfirmLabel_Text : ConfirmErrorLabel_Text) : OkLabel_Text) + Str.HtmlCrlf;

				c.Controls.Add(a);

				if (CompleteMode)
				{
					a.Visible = false;
				}

				b = NewButton(this.BaseControlName + "ok", this.OkButton_Text, true);

				if (ConfirmMode && this.genericError != null)
				{
					b.Visible = false;
				}

				if (CompleteMode)
				{
					b.Text = CompleteButton_Text;
				}

				if (CompleteMode == false)
				{
					if ((this.ConfirmMode == false && this.UseConfirm == false && this.GridMode == false)
						|| ((this.ConfirmMode == true && this.GridMode == false)))
					{
						// 二重送信防止 JavaScript
						c.Controls.Add(new JavaScriptControl(
							"function " + buttonOkOnClickFunctionName + "(b)\n{\n\tb.disabled = true;\n\tb.value = \"" + this.OkSendingButton_Text +
							"\";\n}\n"
							));

						b.OnClientClick = buttonOkOnClickFunctionName + "(this);";
						this.Page.PreRenderComplete += new EventHandler(Page_PreRenderComplete);
					}

					buttonOk = b;
				}
				else
				{
					b.ID = "redirect";
				}
				this.Page.Form.DefaultButton = b.ID;

				c.Controls.Add(b);

				this.ButtonOK = b;

				a = new Label();
				a.Text = SpaceBetweenButtons;

				c.Controls.Add(a);

				b = NewButton(this.BaseControlName + "cancel", ConfirmMode ? ModifyButton_Text : CancelButton_Text, true);

				if (CompleteMode)
				{
					b.Visible = false;
				}

				this.ButtonCancel = b;

				c.Controls.Add(b);

				r.Cells.Add(c);

				t.Rows.Add(r);
			}

			// テーブルを追加
			parent.Controls.Add(t);

			if (AfterControlCreatedEvents != null)
			{
				if (afceFlag == false)
				{
					afceFlag = true;

					AfterControlCreatedEvents(this, t);
				}
			}

			renderedTable = t;

			if (this.FormManager != null)
			{
				if (this.TopLevel && this.Mode == CdfFormMode.Edit)
				{
					this.FormManager.SetData(this.Data);
				}
			}

			if (ConfirmMode && this.TopLevel)
			{
				// 確認モード
				if (this.genericErrorRow.Visible == false)
				{
					this.genericErrorRow.Visible = true;
					this.genericErrorRow.Attributes.Add("confirm", "1");
					this.genericErrorLabel.Text = ConfirmLabel_Text;
					this.genericErrorLabel.Font.Size = new FontUnit(FontSize.Larger);
					this.genericErrorLabel.Font.Bold = false;
					this.genericErrorLabel.ForeColor = Confirm_FontColor;

					this.genericErrorCell.BackColor = Color.White;
				}
				else
				{
					// 確認モードでかつエラー発生
					Label a = this.genericErrorLabel;
					TableCell c1 = this.genericErrorCell;

					a.ForeColor = Error_FontColor;
					a.Font.Bold = true;
					c1.BackColor = ErrorRow_BackColor;
					a.Font.Size = new FontUnit(FontSize.Larger);
				}
			}

			if (CompleteMode && this.TopLevel)
			{
				// 完了モード
				this.genericErrorRow.Visible = true;
				string msg = completeMessage;
				bool raw = false;
				if (msg.StartsWith("!"))
				{
					msg = msg.Substring(1);
					raw = true;
				}

				this.genericErrorLabel.Text = msg;

				if (raw == false)
				{
					this.genericErrorLabel.Font.Size = new FontUnit(FontSize.Larger);
					this.genericErrorLabel.Font.Bold = true;
					this.genericErrorLabel.ForeColor = Complete_FontColor;
				}
				else
				{
					RawHtmlControl r = new RawHtmlControl(msg);
					this.genericErrorLabel.Visible = false;
					this.genericErrorLabel.Parent.Controls.Add(r);
				}

				this.genericErrorCell.BackColor = Color.White;
			}

			// 最後のコントロールにフォーカスを戻す
			if (FormManager != null)
			{
				string lastFocusID = FormManager.GetString("lastfocus");

				if (Str.IsEmptyStr(lastFocusID) == false)
				{
					Control c = Page.FindControl(lastFocusID);

					if (c != null)
					{
						focus(c);
					}
				}
			}

			if (Str.IsEmptyStr(focusId) == false)
			{
				this.Page.Form.DefaultFocus = focusId;
			}
		}

		void Page_PreRenderComplete(object sender, EventArgs e)
		{
			ClientScriptManager sm = this.Page.ClientScript;

			buttonOk.Attributes["OnClick"] = sm.GetPostBackEventReference(buttonOk, "");
		}

		// すべての行をレンダリング
		void renderAllRows(Table t)
		{
			int i = 0;
			string[] hiddens = HiddenFields.ToArray();
			List<CdfMetaField> fields = Cdf.MetaClass.Fields;
			object data = this.Data;

			if (GridMode == false)
			{
				// 通常モード
				foreach (CdfMetaField f in fields)
				{
					if (Str.IsStrInList(f.Name, true, hiddens) == false)
					{
						renderRow(t, f, i++, data);
					}
				}
			}
			else
			{
				// グリッドモード
				fields = this.gridFields.ListMetaClass.Fields;
				IList list = (IList)this.gridFields.DotNetFieldInfo.GetValue(this.Data);

				TableRow headerRow = new TableRow();
				headerRow.BackColor = GridHeaderRow_BackColor;
				foreach (CdfMetaField f in fields)
				{
					if (f.CdfFieldAttribute.GridHide == false)
					{
						TableCell c = new TableCell();
						Label a;

						c.Wrap = false;
						c.BorderStyle = BorderStyle.Solid;
						c.BorderWidth = new Unit(1);
						c.BorderColor = Color.LightGray;
						c.HorizontalAlign = HorizontalAlign.Left;
						c.ColumnSpan = 1;
						c.Font.Bold = FieldName_FontBold;
						c.ForeColor = GridHeaderRow_FontColor;

						a = new Label();
						a.Text = f.CdfFieldAttribute.FriendlyName;

						c.Controls.Add(a);

						headerRow.Controls.Add(c);
					}
				}

				if (this.GridNumRowsOfCurrentData <= 0 && this.GridShowEmptyStringWhenEmpty)
				{
					headerRow.Visible = false;
				}

				t.Rows.Add(headerRow);
				int index = 0;

				int first = this.GridPagingFirstRow;
				int last = this.GridPagingLastRow;

				string last_cluster_title = "";

				if (list.Count >= 1)
				{
					for (index = first; index <= last; index++)
					{
						object d = list[index];
						if (this.IsRowInPagingRange(index))
						{
							if (IgnoreCluster == false && d is ICdfRowExtension && this.IsSorted == false)
							{
								ICdfRowExtension ext = (ICdfRowExtension)d;

								if (Str.IsEmptyStr(ext.ClusterTitleString) == false)
								{
									if (Str.StrCmpi(last_cluster_title, ext.ClusterTitleString) == false)
									{
										// クラスタタイトルの変化
										last_cluster_title = ext.ClusterTitleString;

										TableRow r = new TableRow();
										TableCell c;
										Label a;

										c = new TableCell();
										c.Wrap = false;
										c.BorderStyle = BorderStyle.Solid;
										c.BorderWidth = new Unit(1);
										c.BorderColor = Color.LightGray;
										c.HorizontalAlign = HorizontalAlign.Left;
										c.ColumnSpan = GridFieldsCount;

										r.CssClass = "tableStandard";

										if (ext.ClusterTitleColor == Color.AliceBlue)
										{
											r.BackColor = ClusterTitle_BackColor;
										}
										else
										{
											r.BackColor = ext.ClusterTitleColor;
										}

										a = new Label();
										a.Text = Str.ToHtml(ext.ClusterTitleString);
										a.ForeColor = ClusterTitle_FontColor;
										a.Font.Bold = ClusterTitle_FontBold;
										c.Controls.Add(a);

										r.Cells.Add(c);

										t.Rows.Add(r);
									}
								}
							}

							TableRow row = null;
							int columnIndex = 0;
							foreach (CdfMetaField f in fields)
							{
								if (f.CdfFieldAttribute.GridHide == false)
								{
									row = renderRow(t, f, columnIndex++, d, row, index, list);
								}
							}
						}
					}
				}
			}
		}

		// 次の行の色を取得
		int nColor = 0;
		Color getNextRowColor()
		{
			Color ret;

			ret = getNextRowColor(nColor);

			nColor++;

			return ret;
		}
		Color getNextRowColor(int id)
		{
			Color ret;
			uint uid = (uint)id;

			if ((uid % 2) == 1)
			{
				ret = ConfirmMode ? Row1Confirm_BackColor : Row1_BackColor;
			}
			else
			{
				ret = ConfirmMode ? Row2Confirm_BackColor : Row2_BackColor;
			}

			return ret;
		}

		// 行をレンダリング
		TableRow renderRow(Table t, CdfMetaField f, int index, object Data)
		{
			return renderRow(t, f, index, Data, null, -1, null);
		}
		TableRow renderRow(Table t, CdfMetaField f, int index, object Data, TableRow targetRow, int gridIndex, IList gridListObj)
		{
			CdfFieldAttribute fa = f.CdfFieldAttribute;
			string controlName = this.BaseControlName + f.Name;
			if (gridIndex != -1)
			{
				controlName += "_" + gridIndex.ToString();
			}
			string idEdit = controlName + "_edit";
			string idPrint = controlName + "_print";
			bool allowEmpty = true;

			string Edit_FontName = this.Edit_FontName;
			if (f.Type == CdfMetaFieldType.CandidateList || f.Type == CdfMetaFieldType.CandidateStrList || f.Type == CdfMetaFieldType.Enum)
			{
				Edit_FontName = this.EditDropdown_FontName;
			}
			bool Edit_FontBold = fa.EditFontBold;
			Color Edit_FontColor = this.Edit_FontColor;

			if (Str.IsEmptyStr(fa.EditFontName) == false)
			{
				Edit_FontName = fa.EditFontName;
			}
			if (Str.IsEmptyStr(fa.EditFontColor) == false)
			{
				Edit_FontColor = Color.FromName(fa.EditFontColor);
			}

			string Print_FontName = this.Print_FontName;
			bool Print_FontBold = fa.PrintFontBold;
			Color Print_FontColor = this.Print_FontColor;
			string Print_FontSize = "";
			if (Str.IsEmptyStr(fa.PrintFontSize) == false)
			{
				Print_FontSize = fa.PrintFontSize;
			}

			if (Str.IsEmptyStr(fa.PrintFontName) == false)
			{
				Print_FontName = fa.PrintFontName;
			}
			if (Str.IsEmptyStr(fa.PrintFontColor) == false)
			{
				Print_FontColor = Color.FromName(fa.PrintFontColor);
			}

			TableRow r;

			if (targetRow == null)
			{
				r = new TableRow();

				if (IgnoreCluster == false && Data is ICdfRowExtension)
				{
					int id = ((ICdfRowExtension)Data).BackColorId;

					if (id != -1)
					{
						r.BackColor = getNextRowColor(((ICdfRowExtension)Data).BackColorId);
					}
					else
					{
						r.BackColor = getNextRowColor();
					}
				}
				else
				{
					r.BackColor = getNextRowColor();
				}

				if (this.GridMode && this.GridUseMouseColorChange && (this.GridPagingLastRow - this.GridPagingFirstRow) <= 300)
				{
					r.Attributes["onMouseOver"] = string.Format("this.style.background='{0}'", ColorTranslator.ToHtml(this.Row_MouneColor));
					r.Attributes["onMouseOut"] = string.Format("this.style.background='{0}'", ColorTranslator.ToHtml(r.BackColor));
				}
			}
			else
			{
				r = targetRow;
			}

			TableCell c;
			Label a;

			CdfFormRow fr = new CdfFormRow();

			fr.Index = index;
			fr.TableRow = r;
			fr.MetaField = f;
			fr.Table = t;

			r.CssClass = "tableStandard";

			// 項目名
			c = new TableCell();
			fr.FriendlyNameCell = c;
			c.Wrap = false;
			c.BorderStyle = BorderStyle.Solid;
			c.BorderWidth = new Unit(1);
			c.BorderColor = Color.LightGray;
			c.HorizontalAlign = HorizontalAlign.Left;
			c.ColumnSpan = 1;
			c.Font.Bold = FieldName_FontBold;
			c.ForeColor = FieldName_FontColor;

			a = new Label();
			a.Text = f.CdfFieldAttribute.FriendlyName;
			a.ID = controlName + "_name";

			c.Controls.Add(a);

			c.Width = new Unit("20%");

			if (this.GridMode == false)
			{
				r.Cells.Add(c);
			}

			fr.FriendlyNameLabel = a;

			// 値
			c = new TableCell();
			fr.ValueCell = c;
			c.Wrap = !f.CdfFieldAttribute.NoWrap;
			c.BorderStyle = BorderStyle.Solid;
			c.BorderWidth = new Unit(1);
			c.BorderColor = Color.LightGray;

			c.HorizontalAlign = HorizontalAlign.Left;

			if (this.GridMode)
			{
				c.HorizontalAlign = f.CdfFieldAttribute.GridAlign;
			}

			Label beforeLabel = null;
			Label afterLabel = null;
			bool hideBeforeAfter = false;

			if (Str.IsEmptyStr(f.CdfFieldAttribute.StringBefore) == false)
			{
				a = new Label();
				a.Text = f.CdfFieldAttribute.StringBefore + Str.HtmlSpacing;
				a.ID = controlName + "_before";

				c.Controls.Add(a);

				beforeLabel = a;
			}

			a = new Label();

			Label printLabel = null;
			string formString = Page.Request.Form[idEdit];
			string errorString = null;

			if (Mode == CdfFormMode.Edit && this.Page.IsPostBack)
			{
				Str.NormalizeString(ref formString);
				bool ignoreError = false;

				if (f.Type == CdfMetaFieldType.String)
				{
					CdfStringAttribute sa = (CdfStringAttribute)f.CdfFieldAttribute;

					if (sa.NormalizeStandard == false)
					{
						Str.NormalizeString(ref formString, sa.NormalizeSpace,
							sa.NormalizeToHankaku, sa.NormalizeToZenkaku, sa.NormalizeToZenkakuKana);
					}
					else
					{
						formString = Str.NormalizeStrSoftEther(formString, true);
					}
				}

				if (f.Type == CdfMetaFieldType.CandidateList)
				{
					CdfCandidateListAttribute ca = (CdfCandidateListAttribute)f.CdfFieldAttribute;

					if (ca.AllowNull)
					{
						if (Str.IsEmptyStr(formString))
						{
							ignoreError = true;
						}
					}
				}

				if (f.Type == CdfMetaFieldType.CandidateStrList)
				{
					CdfCandidateStrListAttribute ca = (CdfCandidateStrListAttribute)f.CdfFieldAttribute;

					if (ca.AllowNull)
					{
						if (Str.IsEmptyStr(formString))
						{
							ignoreError = true;
						}
					}
				}

				if (f.Type == CdfMetaFieldType.Enum)
				{
					if (Str.IsEmptyStr(formString))
					{
						formString = "0";
					}
				}

				if (f.CdfFieldAttribute.ReadOnly == false)
				{
					try
					{
						f.SetString(Data, formString);
					}
					catch (FormatException fe)
					{
						if (ignoreError == false)
						{
							errorString = fe.Message;
						}
					}
				}
			}

			WebControl mainControl = null;

			switch (f.Type)
			{
				case CdfMetaFieldType.Bool:		// ブール型
					if (this.GridMode)
					{
						CdfBoolAttribute ba = (CdfBoolAttribute)f.CdfFieldAttribute;
						CheckBox cb = new CheckBox();
						mainControl = cb;

						cb.Text = ba.CheckBoxText;
						string boolStr = formString != null ? formString : f.GetString(Data);
						bool boolValue = !Str.IsEmptyStr(boolStr);

						cb.Font.Name = Edit_FontName;
						cb.Font.Bold = Edit_FontBold;
						cb.ForeColor = Edit_FontColor;

						string key;
						string keyName;

						if (Str.IsEmptyStr(ba.CheckBoxIdName) == false)
						{
							key = Data.GetType().GetField(ba.CheckBoxIdName).GetValue(Data).ToString();
							keyName = ba.CheckBoxIdName;
						}
						else
						{
							key = Str.GenRandStr();
							keyName = Str.GenRandStr();
						}

						cb.ID = GenerateCheckBoxName(f.Name, keyName, key);

						cb.Checked = boolValue;

						c.Controls.Add(cb);
					}
					break;

				case CdfMetaFieldType.String:	// 文字列
					if (Mode == CdfFormMode.Edit)
					{
						TextBox e1 = new TextBox();
						mainControl = e1;

						CdfStringAttribute sa = (CdfStringAttribute)f.CdfFieldAttribute;

						if (sa.MinLength >= 1)
						{
							allowEmpty = false;
						}

						if (Str.IsEmptyStr(sa.Width))
						{
							e1.Width = new Unit("70%");
						}
						else
						{
							e1.Width = new Unit(sa.Width);
						}

						if (Str.IsEmptyStr(sa.Height))
						{
							if (sa.MultiLine)
							{
								e1.Height = new Unit("150px");
							}
						}
						else
						{
							e1.Height = new Unit(sa.Height);
						}

						if (sa.MultiLine)
						{
							e1.TextMode = TextBoxMode.MultiLine;
						}

						string textData = (formString != null ? formString : f.GetString(Data));

						if (sa.Password)
						{
							e1.TextMode = TextBoxMode.Password;
						}
						e1.Text = textData;

						e1.Font.Name = Edit_FontName;
						e1.Font.Bold = Edit_FontBold;
						e1.ForeColor = Edit_FontColor;
						e1.ID = idEdit;
						e1.AutoCompleteType = f.CdfFieldAttribute.AutoCompletionType;

						if (sa.MaxLength >= 1)
						{
							e1.MaxLength = sa.MaxLength;
						}

						c.Controls.Add(e1);

						if (((CdfStringAttribute)f.CdfFieldAttribute).NoJavaScript == false)
						{
							if (Env.IsNET4OrGreater == false)
							{
								c.Controls.Add(new JavaScriptControl(
									string.Format("\tdocument.{0}.{1}.value = \"{2}\";",
									Page.Form.Name,
									e1.ID,
									Str.Unescape(e1.Text))));
							}
							else
							{
								c.Controls.Add(new JavaScriptControl(
									string.Format("\tdocument.getElementById(\"{0}\").{1}.value = \"{2}\";",
									Page.Form.Name,
									e1.ID,
									Str.Unescape(e1.Text))));
							}
						}

						if (sa.HasHistory)
						{
							Label br = new Label();
							br.Text = Str.HtmlBr;
							c.Controls.Add(br);

							br = new Label();
							br.Text = CdfGlobalLangSettings.ProcStr(CoreStr.CDF_CANDIDATES);

							br.Font.Size = this.HistoryLabel_FontSize;
							if (Str.IsEmptyStr(this.HistoryLabel_FontName) == false)
							{
								br.Font.Name = this.HistoryLabel_FontName;
							}
							br.Font.Bold = this.HistoryLabel_FontBold;
							br.ForeColor = this.HistoryLabel_FontColor;
							c.Controls.Add(br);

							DropDownList hd = new DropDownList();
							hd.ID = idEdit + "_history";
							hd.EnableViewState = false;

							hd.Items.Add(new ListItem(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_SELECTABLE), ""));

							hd.Font.Size = this.History_FontSize;
							if (Str.IsEmptyStr(this.History_FontName) == false)
							{
								hd.Font.Name = this.History_FontName;
							}
							hd.Font.Bold = this.History_FontBold;
							hd.ForeColor = this.History_FontColor;
							c.Controls.Add(br);

							int num = 0;

							foreach (string str in sa.History)
							{
								if (Str.IsEmptyStr(str) == false)
								{
									string printStr = str.Trim();

									if (printStr.Length > this.History_MaxPrintLength)
									{
										printStr = printStr.Substring(0, this.History_MaxPrintLength) + "...";
									}

									hd.Items.Add(new ListItem(printStr, str.Trim()));

									num++;
								}
							}

							if (num >= 1)
							{
								c.Controls.Add(hd);

								string code;

								if (Env.IsNET4OrGreater == false)
								{
									code =
										string.Format("document.{0}.{1}.value = this[this.selectedIndex].value",
											Page.Form.Name,
											e1.ID
										);
								}
								else
								{
									code =
										string.Format("document.getElementById(\"{0}\").{1}.value = this[this.selectedIndex].value",
											Page.Form.Name,
											e1.ID
										);
								}

								hd.Attributes["onChange"] = code;
							}
						}
					}
					else
					{
						CdfStringAttribute sa = (CdfStringAttribute)f.CdfFieldAttribute;
						printLabel = new Label();
						mainControl = printLabel;
						string text = sa.Password == false ? f.GetString(Data) :
							Str.MakeCharArray('*', f.GetString(Data).Length);

						if (sa.NoAutoHtml == false)
						{
							text = Str.ToHtml(text);
						}

						if (sa.NoAutoHyperLink == false)
						{
							text = Str.LinkUrlOnText(text, "_blank");
						}

						if (((CdfStringAttribute)f.CdfFieldAttribute).PrintAsList)
						{
							string listPrintText = f.GetString(Data);
							BulletedList listControl = new BulletedList();
							PrintStringToList(listPrintText, listControl);
							listControl.ID = idPrint;
							listControl.Font.Name = Print_FontName;
							listControl.Font.Bold = Print_FontBold;
							listControl.ForeColor = Print_FontColor;
							if (Str.IsEmptyStr(Print_FontSize) == false)
							{
								listControl.Font.Size = new FontUnit(Print_FontSize);
							}
							c.Controls.Add(listControl);
							printLabel.Text = text;
						}
						else
						{
							printLabel.Text = text;
							printLabel.Font.Name = Print_FontName;
							printLabel.Font.Bold = Print_FontBold;
							printLabel.ForeColor = Print_FontColor;
							if (Str.IsEmptyStr(Print_FontSize) == false)
							{
								printLabel.Font.Size = new FontUnit(Print_FontSize);
							}
							printLabel.ID = idPrint;

							if (Str.IsEmptyStr(printLabel.Text))
							{
								hideBeforeAfter = true;
							}

							c.Controls.Add(printLabel);
						}
					}
					break;

				case CdfMetaFieldType.Int:		// 32 ビット整数
					if (Mode == CdfFormMode.Edit)
					{
						TextBox e2 = new TextBox();
						mainControl = e2;
						e2.Text = formString != null ? formString : f.GetString(Data);
						e2.Width = new Unit("100px");
						e2.Font.Name = Edit_FontName;
						e2.Font.Bold = Edit_FontBold;
						e2.ForeColor = Edit_FontColor;
						e2.ID = idEdit;
						e2.AutoCompleteType = f.CdfFieldAttribute.AutoCompletionType;

						if (((CdfIntAttribute)f.CdfFieldAttribute).EmptyAsDefault == false)
						{
							allowEmpty = false;
						}
						else
						{
							if (((CdfIntAttribute)f.CdfFieldAttribute).MinValue != int.MinValue && ((CdfIntAttribute)f.CdfFieldAttribute).MinValue != 0)
							{
								allowEmpty = false;
							}
						}

						c.Controls.Add(e2);
					}
					else
					{
						if (((CdfIntAttribute)f.CdfFieldAttribute).Akaji)
						{
							if ((int)f.GetData(Data) < 0)
							{
								if (Str.IsEmptyStr(((CdfIntAttribute)f.CdfFieldAttribute).AkajiColor) == false)
								{
									Print_FontColor = Color.FromName(((CdfIntAttribute)f.CdfFieldAttribute).AkajiColor);
								}
								else
								{
									Print_FontColor = Color.Red;
								}
							}
						}

						printLabel = new Label();
						mainControl = printLabel;
						printLabel.Text = f.GetString(Data);
						printLabel.Font.Name = Print_FontName;
						printLabel.Font.Bold = Print_FontBold;
						printLabel.ForeColor = Print_FontColor;
						if (Str.IsEmptyStr(Print_FontSize) == false)
						{
							printLabel.Font.Size = new FontUnit(Print_FontSize);
						}
						printLabel.ID = idPrint;

						if (Str.IsEmptyStr(printLabel.Text))
						{
							hideBeforeAfter = true;
						}

						c.Controls.Add(printLabel);
					}
					break;

				case CdfMetaFieldType.Long:		// 64 ビット整数
					if (Mode == CdfFormMode.Edit)
					{
						TextBox e3 = new TextBox();
						mainControl = e3;
						e3.Text = formString != null ? formString : f.GetString(Data);
						e3.Width = new Unit("100px");
						e3.Font.Name = Edit_FontName;
						e3.Font.Bold = Edit_FontBold;
						e3.ForeColor = Edit_FontColor;
						e3.ID = idEdit;
						e3.AutoCompleteType = f.CdfFieldAttribute.AutoCompletionType;

						if (((CdfLongAttribute)f.CdfFieldAttribute).EmptyAsDefault == false)
						{
							allowEmpty = false;
						}
						else
						{
							if (((CdfLongAttribute)f.CdfFieldAttribute).MinValue != long.MinValue && ((CdfLongAttribute)f.CdfFieldAttribute).MinValue != 0)
							{
								allowEmpty = false;
							}
						}

						c.Controls.Add(e3);
					}
					else
					{
						if (((CdfLongAttribute)f.CdfFieldAttribute).Akaji)
						{
							if ((long)f.GetData(Data) < 0)
							{
								if (Str.IsEmptyStr(((CdfLongAttribute)f.CdfFieldAttribute).AkajiColor) == false)
								{
									Print_FontColor = Color.FromName(((CdfLongAttribute)f.CdfFieldAttribute).AkajiColor);
								}
								else
								{
									Print_FontColor = Color.Red;
								}
							}
						}

						printLabel = new Label();
						mainControl = printLabel;
						printLabel.Text = f.GetString(Data);
						printLabel.Font.Name = Print_FontName;
						printLabel.Font.Bold = Print_FontBold;
						printLabel.ForeColor = Print_FontColor;
						if (Str.IsEmptyStr(Print_FontSize) == false)
						{
							printLabel.Font.Size = new FontUnit(Print_FontSize);
						}
						printLabel.ID = idPrint;

						if (Str.IsEmptyStr(printLabel.Text))
						{
							hideBeforeAfter = true;
						}

						c.Controls.Add(printLabel);
					}
					break;

				case CdfMetaFieldType.Double:	// 小数点
					if (Mode == CdfFormMode.Edit)
					{
						TextBox e5 = new TextBox();
						mainControl = e5;
						e5.Text = formString != null ? formString : f.GetString(Data);
						e5.Width = new Unit("200px");
						e5.Font.Name = Edit_FontName;
						e5.Font.Bold = Edit_FontBold;
						e5.ForeColor = Edit_FontColor;
						e5.ID = idEdit;
						e5.AutoCompleteType = f.CdfFieldAttribute.AutoCompletionType;

						if (((CdfDoubleAttribute)f.CdfFieldAttribute).EmptyAsDefault == false)
						{
							allowEmpty = false;
						}
						else
						{
							if (((CdfDoubleAttribute)f.CdfFieldAttribute).MinValue != 0)
							{
								allowEmpty = false;
							}
						}

						c.Controls.Add(e5);
					}
					else
					{
						if (((CdfDoubleAttribute)f.CdfFieldAttribute).Akaji)
						{
							if ((double)f.GetData(Data) < 0)
							{
								if (Str.IsEmptyStr(((CdfDoubleAttribute)f.CdfFieldAttribute).AkajiColor) == false)
								{
									Print_FontColor = Color.FromName(((CdfDoubleAttribute)f.CdfFieldAttribute).AkajiColor);
								}
								else
								{
									Print_FontColor = Color.Red;
								}
							}
						}

						printLabel = new Label();
						mainControl = printLabel;
						printLabel.Text = f.GetString(Data);
						printLabel.Font.Name = Print_FontName;
						printLabel.Font.Bold = Print_FontBold;
						if (Str.IsEmptyStr(Print_FontSize) == false)
						{
							printLabel.Font.Size = new FontUnit(Print_FontSize);
						}
						printLabel.ForeColor = Print_FontColor;
						printLabel.ID = idPrint;

						if (Str.IsEmptyStr(printLabel.Text))
						{
							hideBeforeAfter = true;
						}

						c.Controls.Add(printLabel);
					}
					break;

				case CdfMetaFieldType.DateTime:	// 日時
					if (Mode == CdfFormMode.Edit)
					{
						TextBox e4 = new TextBox();
						mainControl = e4;
						DateTime dt = (DateTime)f.GetData(Data);

						if (formString == null)
						{
							if (dt == Cdf.ZeroDateTimeValue || dt.Ticks == 0)
							{
								e4.Text = "";
							}
							else
							{
								e4.Text = f.GetString(Data);
							}
						}
						else
						{
							e4.Text = formString;
						}

						e4.Width = new Unit("110px");

						if (((CdfDateTimeAttribute)f.CdfFieldAttribute).Type == CdfDateTimeType.Both)
						{
							e4.Width = new Unit("220px");
						}

						e4.Font.Name = Edit_FontName;
						e4.Font.Bold = Edit_FontBold;
						e4.ForeColor = Edit_FontColor;
						e4.ID = idEdit;
						e4.AutoCompleteType = f.CdfFieldAttribute.AutoCompletionType;

						if (((CdfDateTimeAttribute)f.CdfFieldAttribute).AllowZero == false)
						{
							allowEmpty = false;
						}

						c.Controls.Add(e4);
					}
					else
					{
						printLabel = new Label();
						mainControl = printLabel;

						DateTime dt = (DateTime)f.GetData(Data);

						if (dt == Cdf.ZeroDateTimeValue || dt.Ticks == 0)
						{
							printLabel.Text = "";
						}
						else
						{
							printLabel.Text = f.GetString(Data, !((CdfDateTimeAttribute)f.CdfFieldAttribute).PrintSimple);
						}

						printLabel.Font.Name = Print_FontName;
						printLabel.Font.Bold = Print_FontBold;
						printLabel.ForeColor = Print_FontColor;
						if (Str.IsEmptyStr(Print_FontSize) == false)
						{
							printLabel.Font.Size = new FontUnit(Print_FontSize);
						}
						printLabel.ID = idPrint;

						if (Str.IsEmptyStr(printLabel.Text))
						{
							hideBeforeAfter = true;
						}

						c.Controls.Add(printLabel);
					}
					break;

				case CdfMetaFieldType.CandidateList: // 候補リスト
					if (Mode == CdfFormMode.Edit)
					{
						CdfCandidateListAttribute aa = (CdfCandidateListAttribute)f.CdfFieldAttribute;

						ListControl dl = null;

						switch (aa.CandidateListControlType)
						{
							case CdfCandidateListControlType.DropDown:
								dl = new DropDownList();
								break;

							case CdfCandidateListControlType.ListBox:
								dl = new ListBox();
								ListBox lb = (ListBox)dl;
								if (Str.IsEmptyStr(aa.CandidateListListBoxHeight) == false)
								{
									lb.Height = new Unit(aa.CandidateListListBoxHeight);
								}
								break;

							case CdfCandidateListControlType.RadioButton:
								dl = new RadioButtonList();
								((RadioButtonList)dl).BackColor = r.BackColor;
								break;
						}

						mainControl = dl;
						int selIndex = -1;

						CandidateList cl = (CandidateList)f.GetData(Data);
						int currentSel = cl.Value;
						bool ok = false;

						if (aa.ListDefaultEmpty)
						{
							if (!(dl is RadioButtonList))
							{
								dl.Items.Add(new ListItem(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_PLEASE_SELECT), ""));
							}
						}

						foreach (int intValue in cl.CandidateValues)
						{
							ListItem li = new ListItem(cl.GetString(intValue), intValue.ToString());
							dl.Items.Add(li);

							if (currentSel == intValue)
							{
								selIndex = dl.Items.Count - 1;

								ok = true;
							}
						}

						dl.SelectedIndex = selIndex;

						if (ok == false && Str.IsEmptyStr(cl.FriendlyString) == false)
						{
							ListItem li = new ListItem(cl.FriendlyString, cl.Value.ToString());
							dl.Items.Add(li);
							li.Selected = true;
						}

						dl.Font.Name = Edit_FontName;
						if (!(dl is RadioButtonList))
						{
							dl.Font.Bold = Edit_FontBold;
						}
						dl.ForeColor = Edit_FontColor;
						dl.ID = idEdit;

						c.Controls.Add(dl);

						allowEmpty = false;
					}
					else
					{
						printLabel = new Label();
						mainControl = printLabel;
						printLabel.Text = f.GetString(Data, true);
						printLabel.Font.Name = Print_FontName;
						printLabel.Font.Bold = Print_FontBold;
						if (Str.IsEmptyStr(Print_FontSize) == false)
						{
							printLabel.Font.Size = new FontUnit(Print_FontSize);
						}
						printLabel.ForeColor = Print_FontColor;
						printLabel.ID = idPrint;

						c.Controls.Add(printLabel);
					}
					break;

				case CdfMetaFieldType.CandidateStrList: // 候補リスト 2
					if (Mode == CdfFormMode.Edit)
					{
						CdfCandidateStrListAttribute aa = (CdfCandidateStrListAttribute)f.CdfFieldAttribute;

						ListControl dl = null;

						switch (aa.CandidateListControlType)
						{
							case CdfCandidateListControlType.DropDown:
								dl = new DropDownList();
								break;

							case CdfCandidateListControlType.ListBox:
								dl = new ListBox();
								ListBox lb = (ListBox)dl;
								if (Str.IsEmptyStr(aa.CandidateListListBoxHeight) == false)
								{
									lb.Height = new Unit(aa.CandidateListListBoxHeight);
								}
								break;

							case CdfCandidateListControlType.RadioButton:
								dl = new RadioButtonList();
								((RadioButtonList)dl).BackColor = r.BackColor;
								break;
						}

						mainControl = dl;
						int selIndex = -1;

						CandidateStrList cl = (CandidateStrList)f.GetData(Data);
						string currentSel = cl.Value;
						bool ok = false;

						if (aa.ListDefaultEmpty)
						{
							if (!(dl is RadioButtonList))
							{
								dl.Items.Add(new ListItem(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_PLEASE_SELECT), ""));
							}
						}

						foreach (string strValue in cl.CandidateValues)
						{
							string text = cl.GetString(strValue);

							if (aa.MaxPrintStrLen != 0)
							{
								text = Str.TruncStrEx(text, aa.MaxPrintStrLen, null);
							}

							ListItem li = new ListItem(text, strValue.ToString());
							dl.Items.Add(li);

							if (Str.StrCmpi(currentSel, strValue))
							{
								selIndex = dl.Items.Count - 1;

								ok = true;
							}
						}

						dl.SelectedIndex = selIndex;

						if (ok == false && Str.IsEmptyStr(cl.FriendlyString) == false)
						{
							ListItem li = new ListItem(cl.FriendlyString, cl.Value.ToString());
							dl.Items.Add(li);
							li.Selected = true;
						}

						dl.Font.Name = Edit_FontName;
						if (!(dl is RadioButtonList))
						{
							dl.Font.Bold = Edit_FontBold;
						}
						dl.ForeColor = Edit_FontColor;
						dl.ID = idEdit;

						c.Controls.Add(dl);

						allowEmpty = false;
					}
					else
					{
						printLabel = new Label();
						mainControl = printLabel;
						printLabel.Text = f.GetString(Data, true);
						printLabel.Font.Name = Print_FontName;
						printLabel.Font.Bold = Print_FontBold;
						if (Str.IsEmptyStr(Print_FontSize) == false)
						{
							printLabel.Font.Size = new FontUnit(Print_FontSize);
						}
						printLabel.ForeColor = Print_FontColor;
						printLabel.ID = idPrint;

						c.Controls.Add(printLabel);
					}
					break;

				case CdfMetaFieldType.Enum:	// 列挙
					if (Mode == CdfFormMode.Edit)
					{
						DropDownList dl = new DropDownList();
						mainControl = dl;
						int selIndex = -1;
						int currentSel = (int)f.GetData(Data);

						if (((CdfEnumAttribute)f.CdfFieldAttribute).ListDefaultEmpty)
						{
							dl.Items.Add(new ListItem(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_PLEASE_SELECT), ""));
						}

						foreach (CdfMetaEnumItem mi in f.MetaEnum.Items)
						{
							ListItem li = new ListItem(mi.Attribute.FriendlyName, mi.ValueInt.ToString());
							dl.Items.Add(li);

							if ((int)f.GetData(Data) == mi.ValueInt)
							{
								selIndex = dl.Items.Count - 1;
							}
						}

						dl.SelectedIndex = selIndex;

						dl.Font.Name = Edit_FontName;
						dl.Font.Bold = Edit_FontBold;
						dl.ForeColor = Edit_FontColor;
						dl.ID = idEdit;
						c.Controls.Add(dl);

						allowEmpty = false;
					}
					else
					{
						printLabel = new Label();
						mainControl = printLabel;
						printLabel.Text = f.GetString(Data, true);

						foreach (CdfMetaEnumItem mi in f.MetaEnum.Items)
						{
							if ((int)f.GetData(Data) == mi.ValueInt)
							{
								if (Str.IsEmptyStr(mi.Attribute.PrintFontColor) == false)
								{
									Print_FontColor = Color.FromName(mi.Attribute.PrintFontColor);
								}
							}
						}

						printLabel.Font.Name = Print_FontName;
						printLabel.Font.Bold = Print_FontBold;
						if (Str.IsEmptyStr(Print_FontSize) == false)
						{
							printLabel.Font.Size = new FontUnit(Print_FontSize);
						}
						printLabel.ForeColor = Print_FontColor;
						printLabel.ID = idPrint;

						c.Controls.Add(printLabel);
					}
					break;

				case CdfMetaFieldType.List:	// リスト
					if (Mode == CdfFormMode.Edit)
					{
						// 追加ボタン
						Button addButton = NewButton(controlName + "_add", this.AddButton_Text);

						c.Controls.Add(addButton);

						mainControl = addButton;
					}
					else
					{
					}
					break;
			}

			if (printLabel != null)
			{
				if (Str.IsEmptyStr(printLabel.Text))
				{
					if (this.Mode == CdfFormMode.Print && this.HideEmptyField)
					{
						return null;
					}
					printLabel.Text = fa.PrintDefaultString;
				}
			}

			if (printLabel != null && Str.IsEmptyStr(printLabel.Text) == false)
			{
				string urlStr = fa.GridLinkUrlFormat;
				if (Str.IsEmptyStr(urlStr) == false && this.NoHyperLink == false)
				{
					object urlData = null;
					string id = fa.GridLinkUrlIdName;
					if (Str.IsEmptyStr(id) == false)
					{
						if (Str.StrCmpi(id, "index") == false)
						{
							Type currentDataType = Data.GetType();
							urlData = currentDataType.GetField(id).GetValue(Data);
						}
						else
						{
							urlData = gridIndex.ToString();
						}
					}

					string url = urlStr;
					url = string.Format(url, urlData);

					if (Str.IsEmptyStr(url) == false)
					{
						string linkStart = "<a href=\"" + url + "\"";
						if (Str.IsEmptyStr(fa.GridLinkUrlTarget) == false)
						{
							linkStart += " target=\"" + fa.GridLinkUrlTarget + "\"";
						}
						linkStart += ">";

						string linkEnd = "</a>";

						printLabel.Text = linkStart + printLabel.Text + linkEnd;
					}
				}
			}

			if (Str.IsEmptyStr(f.CdfFieldAttribute.StringAfter) == false)
			{
				a = new Label();
				a.Text = Str.HtmlSpacing + f.CdfFieldAttribute.StringAfter;
				a.ID = controlName + "_after";

				c.Controls.Add(a);

				afterLabel = a;
			}

			if (f.Type == CdfMetaFieldType.DateTime && Mode == CdfFormMode.Edit)
			{
				a = new Label();
				DateTime now = DateTime.Now;
				DateTime example = new DateTime(now.Year, now.Month, now.Day,
					now.Hour, 12, 34);
				switch (((CdfDateTimeAttribute)f.CdfFieldAttribute).Type)
				{
					case CdfDateTimeType.DateOnly:
						a.Text = Str.HtmlCrlf + "(" + CdfGlobalLangSettings.ProcStr(CoreStr.CDF_EXAMPLE) + ": " + example.ToString("yyyy/MM/dd") + ")";
						break;

					case CdfDateTimeType.TimeOnly:
						a.Text = Str.HtmlCrlf + "(" + CdfGlobalLangSettings.ProcStr(CoreStr.CDF_EXAMPLE) + ": " + example.ToString("HH:mm:ss") + ")";
						break;

					default:
						a.Text = Str.HtmlCrlf + "(" + CdfGlobalLangSettings.ProcStr(CoreStr.CDF_EXAMPLE) + ": " + example.ToString("yyyy/MM/dd HH:mm:ss") + ")";
						break;
				}
				a.ID = controlName + "_example";
				a.Font.Name = Edit_FontName;
				a.ForeColor = Color.Gray;

				c.Controls.Add(a);
			}

			if (this.GridMode == false)
			{
				if (this.HideNote)
				{
					c.Width = new Unit("80%");
				}
				else
				{
					c.Width = new Unit("45%");
				}
			}
			else
			{
				if (Str.IsEmptyStr(f.CdfFieldAttribute.GridMinSize) == false)
				{
					c.Style.Add("min-width", f.CdfFieldAttribute.GridMinSize);
				}
				string gs = f.CdfFieldAttribute.GridSize;

				if (Str.IsEmptyStr(gs))
				{
					//gs = f.CdfFieldAttribute.GridMinSize;
				}

				if (Str.IsEmptyStr(gs) == false)
				{
					c.Width = new Unit(gs);
				}
			}

			r.Cells.Add(c);

			Label el = new Label();
			el.ID = controlName + "_error";
			el.Text = "";
			c.Controls.Add(el);

			fr.ErrorLabel = el;

			if (errorString != null)
			{
				fr.ErrorString = errorString;
			}

			// 必須項目のマーク
			Label mark = new Label();
			mark.ID = controlName + "_mark";
			mark.Text = Str.ToHtml(" *");
			mark.ForeColor = Color.Red;
			mark.Font.Bold = true;
			fr.RequireLabel = mark;
			mark.Visible = (!allowEmpty) && (f.CdfFieldAttribute.ReadOnly == false);

			fr.FriendlyNameCell.Controls.Add(mark);

			// 説明
			c = new TableCell();
			fr.NoteCell = c;
			c.Wrap = true;
			c.BorderStyle = BorderStyle.Solid;
			c.BorderWidth = new Unit(1);
			c.BorderColor = Color.LightGray;
			c.HorizontalAlign = HorizontalAlign.Left;
			c.Style.Add("min-width", "75px");

			a = new Label();
			string noteText = f.CdfFieldAttribute.Note;

			if (noteText.IndexOf("!!!") != -1)
			{
				if (this.TopLevel)
				{
					noteText = noteText.Replace("!!!", "");
				}
				else
				{
					noteText = noteText.Substring(0, noteText.IndexOf("!!!"));
				}
			}

			a.Text = noteText;
			a.ID = controlName + "_note";
			c.Controls.Add(a);

			fr.NoteLabel = a;

			c.Width = new Unit("35%");

			if (this.GridMode == false && HideNote == false)
			{
				r.Cells.Add(c);
			}

			t.Rows.Add(r);

			// 読み取り専用
			if (f.CdfFieldAttribute.ReadOnly && this.Mode == CdfFormMode.Edit)
			{
				TextBox tb = mainControl as TextBox;

				if (tb != null)
				{
					tb.ReadOnly = true;
					tb.BackColor = EditReadOnly_BackColor;
					tb.ForeColor = EditReadOnly_FontColor;
				}
				else
				{
					WebControl wc = mainControl as WebControl;

					if (wc != null)
					{
						wc.Enabled = false;
					}
				}
			}

			if (f.Type == CdfMetaFieldType.List)	// リストの次の行以降 (データ本体) を描画
			{
				object data = f.GetData(Data);
				IList list = (IList)data;

				Color backColor = r.BackColor;
				int cn = 0;
				int totalnum = list.Count;

				foreach (object listData in list)
				{
					// データ行の作成
					r = new TableRow();
					r.BackColor = backColor;

					// セル 1: ボタン用セル
					c = new TableCell();
					c.Wrap = false;
					c.BorderStyle = BorderStyle.Solid;
					c.BorderWidth = new Unit(1);
					c.BorderColor = Color.LightGray;
					c.HorizontalAlign = HorizontalAlign.Left;
					c.Text = Str.HtmlSpacing;

					r.Cells.Add(c);

					// セル 2: データ用セル
					c = new TableCell();
					c.Wrap = true;
					c.BorderStyle = BorderStyle.Solid;
					c.BorderWidth = new Unit(1);
					c.BorderColor = Color.LightGray;
					c.HorizontalAlign = HorizontalAlign.Left;
					c.ColumnSpan = 2;

					if (HideNote)
					{
						c.ColumnSpan--;
					}

					// セル 2 用のフォーム
					Cdf cdf2 = new Cdf(listData.GetType());
					CdfForm f2 = new CdfForm(this.Page, CdfFormMode.Print, listData,
						cdf2, controlName + "_" + cn + "_subform", this.FormManager, c);
					f2.Row1_BackColor = f2.Row2_BackColor = backColor;
					f2.Row1Confirm_BackColor = f2.Row2Confirm_BackColor = backColor;
					f2.DrawTitle = false;
					f2.TopLevel = false;
					f2.HideNote = true;

					if (SubFormCreatedEvents != null)
					{
						SubFormCreatedEvents(this, f2, f.Name, list, data, cn);
					}


					printLabel = new Label();
					printLabel.Text = string.Format(CdfGlobalLangSettings.ProcStr(CoreStr.CDF_LIST_FORMAT), cn + 1, totalnum, Str.ToHtml(cdf2.MetaClass.CdfClass.FriendlyName)) + Str.HtmlCrlf;
					printLabel.Font.Bold = ListLabel_FontBold;
					printLabel.ForeColor = ListLabel_FontColor;
					printLabel.ID = controlName + "_" + cn + "_label";
					c.Controls.Add(printLabel);

					if (Mode == CdfFormMode.Edit)
					{
						// 編集および削除ボタン
						Button delButton = NewButton(controlName + "_" + cn + "_delete", this.DeleteButton_Text);
						Button editButton = NewButton(controlName + "_" + cn + "_edit", this.EditButton_Text);

						c.Controls.Add(editButton);

						printLabel = new Label();
						printLabel.Text = Str.HtmlSpacing + Str.HtmlSpacing + Str.HtmlSpacing;
						c.Controls.Add(printLabel);

						c.Controls.Add(delButton);

						if (f.CdfFieldAttribute.ReadOnly)
						{
							delButton.Enabled = editButton.Enabled = false;
						}
					}

					f2.Render();
					r.Cells.Add(c);

					// データ行の追加
					t.Rows.Add(r);

					cn++;
				}
			}

			fr.MainControl = mainControl;

			if (Str.IsEmptyStr(focusId))
			{
				focus(mainControl);
			}

			if (this.GridMode == false)
			{
				formRowList.Add(f.Name, fr);
			}

			if (hideBeforeAfter)
			{
				if (beforeLabel != null)
				{
					beforeLabel.Visible = false;
				}

				if (afterLabel != null)
				{
					afterLabel.Visible = false;
				}
			}

			if (this.GridRowRenderingEvents != null)
			{
				GridRowRenderingEvents(this, Data,
					gridIndex, index, gridListObj, mainControl, (TableCell)mainControl.Parent,
					f, f.Name);
			}

			return r;
		}

		public static void PrintStringToList(string listPrintText, BulletedList listControl)
		{
			listControl.Items.Clear();
			StringReader sr = new StringReader(listPrintText);
			while (true)
			{
				string lineStr = sr.ReadLine();
				if (lineStr == null)
				{
					break;
				}
				if (Str.IsEmptyStr(lineStr) == false)
				{
					listControl.Items.Add(lineStr);
				}
			}
		}

		// テーブルの作成
		TableRow genericErrorRow = null;
		Label genericErrorLabel = null;
		TableCell genericErrorCell = null;

		TableRow headerRow = null;
		Label headerLabel = null;
		TableCell headerCell = null;

		Table createTable()
		{
			Label a;
			Table t = new Table();
			t.BorderWidth = new Unit(1);
			t.BorderColor = Color.LightGray;
			t.CellSpacing = 0;
			t.CellPadding = 3;
			t.Width = TableSize;
			t.HorizontalAlign = TableAlign;

			TableRow r = new TableRow();

			// タイトルバー
			TableCell c1 = new TableCell();
			c1.Width = new Unit("100%");
			c1.Wrap = true;
			c1.CssClass = "tableStandard_Header";
			a = new Label();
			a.Text = this.Cdf.MetaClass.CdfClass.FriendlyName;
			if (this.GridPrintNumCountOnTitle && this.Mode == CdfFormMode.Print && this.GridMode)
			{
				if (this.GridNumRowsOfCurrentDataOriginal != this.GridNumRowsOfCurrentData)
				{
					a.Text += CdfGlobalLangSettings.ProcStr(string.Format(" [j](合計 {0} 件 / {1} 件中)[e](Total: {0} entities / All: {1} entities)[/]", this.GridNumRowsOfCurrentData, this.GridNumRowsOfCurrentDataOriginal));
				}
				else
				{
					a.Text += CdfGlobalLangSettings.ProcStr(string.Format(" [j](合計 {0} 件)[e](Total: {0} entities)[/]", this.GridNumRowsOfCurrentData));
				}
			}
			c1.Controls.Add(a);

			// ダウンロードボタン
			if (this.ShowDownloadButton && this.Mode == CdfFormMode.Print &&
				CompleteMode == false && Str.IsEmptyStr(this.CurrentSearchText))
			{
				Label br = new Label();
				br.Text = Str.HtmlSpacing + Str.HtmlSpacing;
				c1.Controls.Add(br);

				Button b = new Button();
				b.ID = this.BaseControlName + "downloadxml";
				b.Text = this.DownloadButton_Text;
				b.Click += new EventHandler(b_Click);
				b.Font.Name = Print_FontName;
				b.Font.Size = new FontUnit("8pt");
				b.BackColor = Color.LightGreen;
				c1.Controls.Add(b);
			}

			if (this.GridShowSort && this.Mode == CdfFormMode.Print && this.GridMode)
			{
				Label br = new Label();
				br.Text = Str.HtmlSpacing + Str.HtmlSpacing;
				c1.Controls.Add(br);

				DropDownList dl = createSortControl();

				c1.Controls.Add(dl);
			}

			if (this.GridShowSearch && this.Mode == CdfFormMode.Print && this.GridMode)
			{
				Label br = new Label();
				br.Text = Str.HtmlSpacing + Str.HtmlSpacing + CdfGlobalLangSettings.ProcStr("[j]検索[e]Search[/]:") + Str.HtmlSpacing;
				c1.Controls.Add(br);

				string searchTextBoxId = BaseControlName + "searchtext";

				TextBox tb = new TextBox();
				tb.AutoPostBack = false;
				tb.Font.Name = this.Search_FontName;
				tb.ForeColor = this.Search_FontColor;
				tb.BackColor = this.Search_BackColor;
				tb.Font.Bold = this.Search_FontBold;
				tb.Width = this.Search_Width;
				tb.ID = searchTextBoxId;

				c1.Controls.Add(tb);

				Label br2 = new Label();
				br2.Text = Str.HtmlSpacing + Str.HtmlSpacing;
				c1.Controls.Add(br2);

				Button b = new Button();
				b.ID = this.BaseControlName + "searchbutton";
				b.Text = CdfGlobalLangSettings.ProcStr("[j]検索[e]Search[/]");
				b.Font.Name = Print_FontName;
				b.Font.Size = new FontUnit("8pt");
				b.BackColor = Color.LightBlue;
				b.Font.Bold = true;
				c1.Controls.Add(b);
				b.Click += new EventHandler(searchButton_Click);
				b.Attributes["OnClick"] = Page.ClientScript.GetPostBackEventReference(b, "");
				this.Page.Form.DefaultButton = b.ID;
			}

			if (this.GridMode)
			{
				c1.ColumnSpan = GridFieldsCount;
			}
			else
			{
				c1.ColumnSpan = 3;
				if (HideNote)
				{
					c1.ColumnSpan--;
				}
			}
			c1.BackColor = Title_BackColor;
			r.Cells.Add(c1);
			t.Rows.Add(r);

			if (this.GridUsePaging)
			{
				DropDownList dl = createPagingControl();
				Label br = new Label();
				br.Text = "<BR>";

				c1.Controls.Add(br);
				c1.Controls.Add(dl);
			}

			if (this.DrawTitle == false)
			{
				r.Visible = false;
			}

			// 一般的なエラーを表示するフィールド
			r = new TableRow();

			c1 = new TableCell();
			c1.Width = new Unit("100%");
			c1.Wrap = true;
			c1.BorderStyle = BorderStyle.Solid;
			c1.BorderWidth = new Unit(1);
			c1.BorderColor = Color.LightGray;
			c1.HorizontalAlign = HorizontalAlign.Left;
			c1.CssClass = "tableStandard";
			c1.ColumnSpan = 3;
			if (this.GridMode)
			{
				c1.ColumnSpan = GridFieldsCount;
			}
			else
			{
				if (HideNote)
				{
					c1.ColumnSpan--;
				}
			}
			c1.BackColor = ErrorRow_BackColor;
			r.Cells.Add(c1);


			a = new Label();
			a.ID = this.BaseControlName + "genericerror_label";
			a.Text = Str.HtmlSpacing;
			a.ForeColor = Error_FontColor;
			a.Font.Bold = true;

			if (this.DebugMode == false)
			{
				a.Font.Size = new FontUnit(FontSize.Larger);
			}

			c1.Controls.Add(a);

			t.Rows.Add(r);

			r.ID = this.BaseControlName + "genericerror_row";

			if (this.genericError == null)
			{
				r.Visible = false;
			}
			else
			{
				a.Text = this.genericError;
			}

			genericErrorLabel = a;
			genericErrorRow = r;
			genericErrorCell = c1;

			// ヘッダフィールド
			r = new TableRow();

			c1 = new TableCell();
			c1.Width = new Unit("100%");
			c1.Wrap = true;
			c1.BorderStyle = BorderStyle.Solid;
			c1.BorderWidth = new Unit(1);
			c1.BorderColor = Color.LightGray;
			c1.HorizontalAlign = HorizontalAlign.Left;
			c1.CssClass = "tableStandard";
			c1.ColumnSpan = 3;
			if (this.GridMode)
			{
				c1.ColumnSpan = GridFieldsCount;
			}
			else
			{
				if (HideNote)
				{
					c1.ColumnSpan--;
				}
			}
			c1.BackColor = HeaderRow_BackColor;
			r.Cells.Add(c1);


			a = new Label();
			a.ID = this.BaseControlName + "header_label";
			a.Text = Str.HtmlSpacing;
			a.ForeColor = Header_FontColor;
			a.Font.Bold = true;

			a.Font.Size = new FontUnit(FontSize.Larger);

			c1.Controls.Add(a);

			t.Rows.Add(r);

			r.ID = this.BaseControlName + "header_row";

			if (Str.IsEmptyStr(HeaderText))
			{
				r.Visible = false;
			}
			else
			{
				if (HeaderText.StartsWith("!") == false)
				{
					a.Text = HeaderText;
				}
				else
				{
					HeaderText = HeaderText.Substring(1);

					a.Visible = false;
					c1.Controls.Add(new RawHtmlControl(HeaderText));
				}
			}

			headerLabel = a;
			headerRow = r;
			headerCell = c1;

			return t;
		}

		// 検索ボタンのクリック
		void searchButton_Click(object sender, EventArgs e)
		{
		}

		// ダウンロードボタンのクリック
		void b_Click(object sender, EventArgs e)
		{
			if (this.ShowDownloadButton == false)
			{
				return;
			}

			XmlAndXsd xx = Util.GenerateXmlAndXsd(this.Data);
			DateTime now = DateTime.Now;

			string baseFileName = MultiLang.ProcStrDefault(this.Cdf.MetaClass.CdfClass.FriendlyName)
				+ "_" + Str.DateTimeToStrShortWithMilliSecs(now);
			baseFileName = Str.MakeSafeFileName(baseFileName);

			xx.XmlFileName = baseFileName + ".xml";

			ZipPacker p = new ZipPacker();
			p.AddFileSimple(xx.XmlFileName, now, FileAttributes.Normal, xx.XmlData);
			p.AddFileSimple(xx.XsdFileName, now, FileAttributes.Normal, xx.XsdData);
			p.Finish();

			Cdf.DownloadFile(this.Page, "xml_" + baseFileName + ".zip", Str.Utf8Encoding,
				p.GeneratedData.Read(p.GeneratedData.Size));
		}
	}
}

