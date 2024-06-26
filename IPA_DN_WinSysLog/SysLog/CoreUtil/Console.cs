﻿// CoreUtil
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

#if ASPNET
using Resources.CoreUtil.Properties;
#else
using CoreUtil.Properties;
#endif

namespace CoreUtil
{
	// コンソール入出力
	public static class Con
	{
		static ConsoleService cs = null;

		public static ConsoleService ConsoleService
		{
			get { return Con.cs; }
		}

		public static void SetConsoleService(ConsoleService svc)
		{
			cs = svc;
		}

		public static void UnsetConsoleService()
		{
			cs = null;
		}

		public static string ReadLine()
		{
			return ReadLine("");
		}
		public static string ReadLine(string prompt)
		{
			return ReadLine(prompt, false);
		}
		public static string ReadLine(string prompt, bool noFile)
		{
			if (cs != null)
			{
				return cs.ReadLine(prompt, noFile);
			}
			else
			{
				Console.Write(prompt);
				return Console.ReadLine();
			}
		}

		public static void WriteLine()
		{
			WriteLine("");
		}

		public static void WriteLine(object arg)
		{
			if (cs != null)
			{
				cs.WriteLine(arg);
			}
			else
			{
				Console.WriteLine(arg);
			}
		}

		public static void WriteLine(string str)
		{
			if (cs != null)
			{
				cs.WriteLine(str);
			}
			else
			{
				Console.WriteLine(str);
			}
		}

		public static void WriteLine(string str, object arg)
		{
			if (cs != null)
			{
				cs.WriteLine(str, arg);
			}
			else
			{
				Console.WriteLine(str, arg);
			}
		}

		public static void WriteLine(string str, params object[] args)
		{
			if (cs != null)
			{
				cs.WriteLine(str, args);
			}
			else
			{
				Console.WriteLine(str, args);
			}
		}
	}

	// ユーザーによるキャンセル例外
	public class ConsoleUserCancelException : Exception
	{
		public ConsoleUserCancelException(string msg)
			: base(msg)
		{
		}
	}

	// パラメータの最小 / 最大値評価
	public class ConsoleEvalMinMaxParam
	{
		public readonly string ErrorMessageString;
		public readonly int MinValue, MaxValue;

		public ConsoleEvalMinMaxParam(string errorMessageString, int minValue, int maxValue)
		{
			this.ErrorMessageString = errorMessageString;
			this.MinValue = minValue;
			this.MaxValue = maxValue;
		}
	}

	// コンソールの種類
	public enum ConsoleType
	{
		Local,		// ローカルコンソール
		Csv,		// CSV 出力モード
	}

	// パラメータ項目
	public class ConsoleParam
	{
		public readonly string Name;				// パラメータ名
		public readonly ConsolePromptProcDelegate PromptProc;	// パラメータが指定されていない場合に自動的に呼び出すプロンプト関数 (NULL の場合は呼ばない)
		public readonly object PromptProcParam;		// プロンプト関数に渡す任意のポインタ
		public readonly ConsoleEvalProcDelegate EvalProc;	// パラメータ文字列検証関数
		public readonly object EvalProcParam;		// 検証関数に渡す任意のポインタ
		internal string Tmp = null;					// 一時変数

		public ConsoleParam(string name)
			: this(name, null, null)
		{
		}
		public ConsoleParam(string name,
			ConsolePromptProcDelegate promptProc,
			object promptProcParam)
			: this(name, promptProc, promptProcParam, null, null)
		{
		}
		public ConsoleParam(string name,
			ConsolePromptProcDelegate promptProc,
			object promptProcParam,
			ConsoleEvalProcDelegate evalProc,
			object evalProcParam)
		{
			this.Name = name;
			this.PromptProc = promptProc;
			this.PromptProcParam = promptProcParam;
			this.EvalProc = evalProc;
			this.EvalProcParam = evalProcParam;
		}
	}

	// デリゲート
	public delegate string ConsolePromptProcDelegate(ConsoleService c, object param);
	public delegate bool ConsoleEvalProcDelegate(ConsoleService c, string str, object param);

	delegate void ConsoleFreeDelegate();
	delegate string ConsoleReadLineDelegate(string prompt, bool nofile);
	delegate string ConsoleReadPasswordDelegate(string prompt);
	delegate bool ConsoleWriteDelegate(string str);
	delegate int ConsoleGetWidthDelegate();

	// パラメータ値リスト
	public class ConsoleParamValueList
	{
		List<ConsoleParamValue> o;

		public ConsoleParamValueList()
		{
			o = new List<ConsoleParamValue>();
		}

		// 一覧
		public IEnumerable<ConsoleParamValue> Values
		{
			get
			{
				int i;
				for (i = 0; i < o.Count; i++)
				{
					yield return o[i];
				}
			}
		}

		// 追加
		public void Add(ConsoleParamValue v)
		{
			if (o.Contains(v) == false)
			{
				o.Add(v);
			}
		}

		// 取得
		public ConsoleParamValue this[string name]
		{
			get
			{
				ConsoleParamValue v = new ConsoleParamValue(name, "", 0);

				int i = o.IndexOf(v);
				if (i == -1)
				{
					return new ConsoleParamValue(name, "", 0);
				}

				return o[i];
			}
		}

		public ConsoleParamValue DefaultParam
		{
			get
			{
				foreach (ConsoleParamValue c in o)
				{
					if (c.IsDefaultParam)
					{
						return c;
					}
				}

				return new ConsoleParamValue("", "", 0, true);
			}
		}

		// 文字列の取得
		public string GetStr(string name)
		{
			ConsoleParamValue v = this[name];
			if (v == null)
			{
				return null;
			}

			return v.StrValue;
		}

		// int の取得
		public int GetInt(string name)
		{
			ConsoleParamValue v = this[name];
			if (v == null)
			{
				return 0;
			}

			return v.IntValue;
		}

		// [はい] か [いいえ] の選択
		public bool GetYes(string name)
		{
			return Str.StrToBool(name);
		}
	}

	// パラメータ値
	public class ConsoleParamValue : IComparable<ConsoleParamValue>, IEquatable<ConsoleParamValue>
	{
		public readonly string Name;			// 名前
		public readonly string StrValue;		// 文字列値
		public readonly int IntValue;			// 整数値
		public readonly bool BoolValue;			// ブール値
		public readonly bool IsEmpty;			// 空白かどうか
		public readonly bool IsDefaultParam;	// デフォルトパラメータかどうか

		public ConsoleParamValue(string name, string strValue, int intValue)
			: this(name, strValue, intValue, false)
		{
		}
		public ConsoleParamValue(string name, string strValue, int intValue, bool isDefaultParam)
		{
			this.Name = name;
			this.IntValue = intValue;
			this.StrValue = strValue;
			this.BoolValue = Str.StrToBool(strValue);
			this.IsDefaultParam = isDefaultParam;

			this.IsEmpty = Str.IsEmptyStr(strValue);
		}

		public int CompareTo(ConsoleParamValue other)
		{
			return Str.StrCmpiRetInt(this.Name, other.Name);
		}

		public bool Equals(ConsoleParamValue other)
		{
			return Str.StrCmpi(this.Name, other.Name);
		}
	}

	// コンソールコマンドパラメータ属性
	public class ConsoleCommandParam : Attribute
	{
	}

	// コンソールコマンドメソッド属性
	public class ConsoleCommandMethod : Attribute
	{
		public readonly string Description;
		public readonly string ArgsHelp;
		public readonly string BodyHelp;
		public readonly SortedList<string, string> ParamHelp;

		internal BindingFlags bindingFlag;
		internal MemberInfo memberInfo;
		internal MethodInfo methodInfo;
		internal string name;

		public ConsoleCommandMethod(string description, string argsHelp, string bodyHelp, params string[] paramHelp)
		{
			this.Description = description;
			this.ArgsHelp = argsHelp;
			this.BodyHelp = bodyHelp;
			this.ParamHelp = new SortedList<string, string>(new StrComparer(false));

			foreach (string s in paramHelp)
			{
				int i = s.IndexOf(":");
				if (i == -1)
				{
					throw new ArgumentException(s);
				}

				this.ParamHelp.Add(s.Substring(0, i), s.Substring(i + 1));
			}
		}
	}

	// コンソールエラーコード
	public static class ConsoleErrorCode
	{
		public const int ERR_BAD_COMMAND_OR_PARAM = -100001;
		public const int ERR_INNER_EXCEPTION = -100002;
		public const int ERR_USER_CANCELED = -100003;

		public static string ErrorCodeToString(int code)
		{
			bool b;

			return ErrorCodeToString(code, out b);
		}
		public static string ErrorCodeToString(int code, out bool unknownError)
		{
			unknownError = false;

			switch (code)
			{
				case ERR_BAD_COMMAND_OR_PARAM:
					return "Bad command or parameters.";

				case ERR_USER_CANCELED:
					return "User canceled.";

				case ERR_INNER_EXCEPTION:
				default:
					unknownError = true;
					return string.Format("Unknown Error {0}", code);
			}
		}
	}

	// コンソールサービス
	public class ConsoleService
	{
		IO inFile;						// 入力ファイル
		Buf inBuf;						// 入力バッファ
		IO outFile;						// 出力ファイル
		int win32_OldConsoleWidth;		// 以前のコンソールサイズ

		// 定数
		public const int MaxPromptStrSize = 65536;
		public const int Win32DefaultConsoleWidth = 100;

		// コンソールの種類
		ConsoleType consoleType;
		public ConsoleType ConsoleType
		{
			get { return consoleType; }
		}

		// 最後の終了コード
		int retCode;
		public int RetCode
		{
			get { return retCode; }
		}

		// 最後のエラーメッセージ
		string retErrorMessage;
		public string RetErrorMessage
		{
			get
			{
				bool b;
				string s = ConsoleErrorCode.ErrorCodeToString(this.RetCode, out b);

				if (b)
				{
					s = this.retErrorMessage;
				}

				Str.NormalizeString(ref s);

				return s;
			}
		}

		// 解放関数
		ConsoleFreeDelegate free;

		// 1 行読み込む関数
		ConsoleReadLineDelegate readLine;

		// パスワードを読み込む関数
		ConsoleReadPasswordDelegate readPassword;

		// 文字列を書き出す関数
		ConsoleWriteDelegate write;

		// 画面の横幅の取得
		ConsoleGetWidthDelegate getWidth;

		// 現在呼び出し中のコマンドリスト
		SortedList<string, ConsoleCommandMethod> currentCmdList = null;


		private ConsoleService()
		{
		}

		// エントリポイント
		public static int EntryPoint(string cmdLine, string programName, Type commandClass)
		{
			string s;
			return EntryPoint(cmdLine, programName, commandClass, out s);
		}
		public static int EntryPoint(string cmdLine, string programName, Type commandClass, out string lastErrorMessage)
		{
			int ret = 0;
			string infile, outfile;
			string csvmode;
			ConsoleService c;

			lastErrorMessage = "";

			// /in と /out の項目だけ先読みする
			infile = ParseCommand(cmdLine, "in");
			outfile = ParseCommand(cmdLine, "out");
			if (Str.IsEmptyStr(infile))
			{
				infile = null;
			}
			if (Str.IsEmptyStr(outfile))
			{
				outfile = null;
			}

			// ローカルコンソールの確保
			c = ConsoleService.NewLocalConsoleService(infile, outfile);

			// CSV モードを先読みしてチェック
			csvmode = ParseCommand(cmdLine, "csv");
			if (csvmode != null)
			{
				c.consoleType = ConsoleType.Csv;
			}

			if (c.DispatchCommand(cmdLine, ">", commandClass) == false)
			{
				ret = ConsoleErrorCode.ERR_BAD_COMMAND_OR_PARAM;
			}
			else
			{
				ret = c.retCode;
			}

			lastErrorMessage = c.RetErrorMessage;

			return ret;
		}

		// 文字列の表示
		public bool WriteLine(object value)
		{
			return WriteLine(value.ToString());
		}
		public bool WriteLine(string str)
		{
			return localWrite(str);
		}
		public bool WriteLine(string format, object arg0)
		{
			return WriteLine(string.Format(format, arg0));
		}
		public bool WriteLine(string format, params object[] arg)
		{
			return WriteLine(string.Format(format, arg));
		}

		// 文字列の取得
		public string ReadLine(string prompt)
		{
			return ReadLine(prompt, false);
		}
		public string ReadLine(string prompt, bool noFile)
		{
			return localReadLine(prompt, noFile);
		}

		// パスワードの取得
		public string ReadPassword(string prompt)
		{
			return localReadPassword(prompt);
		}

		// 文字列入力プロンプト
		public static ConsolePromptProcDelegate Prompt
		{
			get { return new ConsolePromptProcDelegate(prompt); }
		}
		static string prompt(ConsoleService c, object param)
		{
			string p = (param == null) ? CdfGlobalLangSettings.ProcStr(CoreStr.CMD_PROMPT) : (string)param;

			return c.readLine(p, true);
		}

		// 指定されたファイルが存在するかどうか評価
		public static ConsoleEvalProcDelegate EvalIsFile
		{
			get { return new ConsoleEvalProcDelegate(evalIsFile); }
		}
		static bool evalIsFile(ConsoleService c, string str, object param)
		{
			string tmp;
			// 引数チェック
			if (c == null || str == null)
			{
				return false;
			}

			tmp = str;

			if (Str.IsEmptyStr(tmp))
			{
				c.write(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_FILE_NAME_EMPTY));
				return false;
			}

			if (IO.IsFileExists(tmp) == false)
			{
				c.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_FILE_NOT_FOUND), tmp));

				return false;
			}

			return true;
		}

		// 整数の評価
		public static ConsoleEvalProcDelegate EvalInt1
		{
			get { return new ConsoleEvalProcDelegate(evalInt1); }
		}
		static bool evalInt1(ConsoleService c, string str, object param)
		{
			string p = (param == null) ? CdfGlobalLangSettings.ProcStr(CoreStr.CMD_EVAL_INT) : (string)param;

			if (Str.StrToInt(str) == 0)
			{
				c.write(p);

				return false;
			}

			return true;
		}

		// 空白を指定できないパラメータの評価
		public static ConsoleEvalProcDelegate EvalNotEmpty
		{
			get { return new ConsoleEvalProcDelegate(evalNotEmpty); }
		}
		static bool evalNotEmpty(ConsoleService c, string str, object param)
		{
			string p = (param == null) ? CdfGlobalLangSettings.ProcStr(CoreStr.CMD_EVAL_NOT_EMPTY) : (string)param;

			if (Str.IsEmptyStr(str) == false)
			{
				return true;
			}

			c.write(p);

			return false;
		}

		// パラメータの最小 / 最大値評価関数
		public static ConsoleEvalProcDelegate EvalMinMax
		{
			get { return new ConsoleEvalProcDelegate(evalMinMax); }
		}
		static bool evalMinMax(ConsoleService c, string str, object param)
		{
			string tag;
			int v;
			// 引数チェック
			if (param == null)
			{
				return false;
			}

			ConsoleEvalMinMaxParam e = (ConsoleEvalMinMaxParam)param;

			if (Str.IsEmptyStr(e.ErrorMessageString))
			{
				tag = CdfGlobalLangSettings.ProcStr(CoreStr.CMD_EVAL_MIN_MAX);
			}
			else
			{
				tag = e.ErrorMessageString;
			}

			v = Str.StrToInt(str);

			if (v >= e.MinValue && v <= e.MaxValue)
			{
				return true;
			}
			else
			{
				c.write(Str.FormatC(tag, e.MinValue, e.MaxValue));

				return false;
			}
		}

		// コマンドのヘルプを表示する
		public void PrintCmdHelp(string cmdName, List<string> paramList)
		{
			string tmp;
			string buf;
			string description, args, help;
			List<string> t;
			int width;
			int i;
			string space;
			// 引数チェック
			if (cmdName == null || paramList == null)
			{
				return;
			}

			width = GetConsoleWidth() - 2;

			description = this.currentCmdList[cmdName].Description;
			args = this.currentCmdList[cmdName].ArgsHelp;
			help = this.currentCmdList[cmdName].BodyHelp;

			space = Str.MakeCharArray(' ', 2);

			// タイトル
			tmp = Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_HELP_TITLE), cmdName);
			this.write(tmp);
			this.write("");

			// 目的
			this.write(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_HELP_DESCRIPTION));
			t = Str.StrArrayToList(SeparateStringByWidth(description, width - 2));
			for (i = 0; i < t.Count; i++)
			{
				buf = Str.FormatC("%S%s", space, t[i]);
				this.write(buf);
			}
			this.write("");

			// 説明
			this.write(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_HELP_HELP));
			t = Str.StrArrayToList(SeparateStringByWidth(help, width - 2));
			for (i = 0; i < t.Count; i++)
			{
				buf = Str.FormatC("%S%s", space, t[i]);
				this.write(buf);
			}
			this.write("");

			// 使用方法
			this.write(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_HELP_USAGE));
			t = Str.StrArrayToList(SeparateStringByWidth(args, width - 2));
			for (i = 0; i < t.Count; i++)
			{
				buf = Str.FormatC("%S%s", space, t[i]);
				this.write(buf);
			}

			// 引数
			if (paramList.Count >= 1)
			{
				this.write("");
				this.write(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_HELP_ARGS));
				PrintCandidateHelp(cmdName, paramList.ToArray(), 2, this.currentCmdList);
			}
		}

		// 候補一覧のヘルプを表示する
		public void PrintCandidateHelp(string cmdName, string[] candidateList, int leftSpace, SortedList<string, ConsoleCommandMethod> ccList)
		{
			int console_width;
			int max_keyword_width;
			List<string> o;
			int i;
			string tmpbuf;
			string left_space_array;
			string max_space_array;
			// 引数チェック
			if (candidateList == null)
			{
				return;
			}

			// 画面の横幅の取得
			console_width = GetConsoleWidth() - 1;

			left_space_array = Str.MakeCharArray(' ', leftSpace);

			// コマンド名はソートしてリスト化する
			// パラメータ名はソートしない
			o = new List<string>();

			max_keyword_width = 0;

			for (i = 0; i < candidateList.Length; i++)
			{
				int keyword_width;

				o.Add(candidateList[i]);

				keyword_width = Str.GetStrWidth(candidateList[i]);
				if (cmdName != null)
				{
					if (candidateList[i].StartsWith("[", StringComparison.InvariantCultureIgnoreCase) == false)
					{
						keyword_width += 1;
					}
					else
					{
						keyword_width -= 2;
					}
				}

				max_keyword_width = Math.Max(max_keyword_width, keyword_width);
			}

			max_space_array = Str.MakeCharArray(' ', max_keyword_width);

			// 候補を表示する
			for (i = 0; i < o.Count; i++)
			{
				string tmp;
				string name = o[i];
				List<string> t;
				string help;
				int j;
				int keyword_start_width = leftSpace;
				int descript_start_width = leftSpace + max_keyword_width + 1;
				int descript_width;
				string space;

				if (console_width >= (descript_start_width + 5))
				{
					descript_width = console_width - descript_start_width - 3;
				}
				else
				{
					descript_width = 2;
				}

				// 名前を生成する
				if (cmdName != null && name.StartsWith("[", StringComparison.InvariantCultureIgnoreCase) == false)
				{
					// パラメータの場合は先頭に "/" を付ける
					tmp = Str.FormatC("/%s", name);
				}
				else
				{
					// コマンド名の場合はそのままの文字を使用する
					if (cmdName == null)
					{
						tmp = name;
					}
					else
					{
						if (name.Length >= 1)
						{
							tmp = name.Substring(1);
						}
						else
						{
							tmp = "";
						}

						if (tmp.Length >= 1)
						{
							tmp = tmp.Substring(0, tmp.Length - 1);
						}
					}
				}

				// ヘルプ文字を取得する
				if (cmdName == null)
				{
					help = ccList[name].Description;
				}
				else
				{
					if (ccList[cmdName].ParamHelp.ContainsKey(name))
					{
						help = ccList[cmdName].ParamHelp[name];
					}
					else
					{
						help = CdfGlobalLangSettings.ProcStr(CoreStr.CMD_UNKNOWN_PARAM);
					}
				}

				space = Str.MakeCharArray(' ', max_keyword_width - Str.GetStrWidth(name) -
					(cmdName == null ? 0 : (name.StartsWith("[", StringComparison.InvariantCultureIgnoreCase) == false ? 1 : -2)));

				t = Str.StrArrayToList(SeparateStringByWidth(help, descript_width));

				for (j = 0; j < t.Count; j++)
				{
					if (j == 0)
					{
						tmpbuf = Str.FormatC("%S%S%S - %s",
							left_space_array, tmp, space, t[j]);
					}
					else
					{
						tmpbuf = Str.FormatC("%S%S   %s",
							left_space_array, max_space_array, t[j]);
					}

					this.write(tmpbuf);
				}
			}
		}

		// 文字列を指定された横幅で分割する
		public static string[] SeparateStringByWidth(string str, int width)
		{
			// 引数チェック
			if (str == null)
			{
				return new string[0];
			}
			if (width <= 0)
			{
				width = 1;
			}

			StringBuilder tmp = new StringBuilder();
			int len, i;
			List<string> o = new List<string>();

			str += (char)0;
			len = str.Length;

			for (i = 0; i < len; i++)
			{
				char c = str[i];

				switch (c)
				{
					case (char)0:
					case '\r':
					case '\n':
						if (c == '\r')
						{
							if (str[i + 1] == '\n')
							{
								i++;
							}
						}

						o.Add(tmp.ToString());
						tmp = new StringBuilder();
						break;

					default:
						tmp.Append(c);
						if (Str.GetStrWidth(tmp.ToString()) >= width)
						{
							o.Add(tmp.ToString());
							tmp = new StringBuilder();
						}
						break;
				}
			}

			if (o.Count == 0)
			{
				o.Add("");
			}

			return o.ToArray();
		}

		// 指定した文字列が help を示すかどうかをチェック
		public static bool IsHelpStr(string str)
		{
			// 引数チェック
			if (str == null)
			{
				return false;
			}

			if (Str.IsStrInList(str, true,
				"help", "?", "man", "/man", "-man", "--man",
				"/help", "/?", "-help", "-?",
				"/h", "--help", "--?"))
			{
				return true;
			}

			return false;
		}

		// コマンドの実行
		public bool DispatchCommand(string execCommandOrNull, string prompt, Type commandClass)
		{
			return DispatchCommand(execCommandOrNull, prompt, commandClass, null);
		}
		public bool DispatchCommand(string execCommandOrNull, string prompt, Type commandClass, object invokerInstance)
		{
			SortedList<string, ConsoleCommandMethod> cmdList = GetCommandList(commandClass);

			currentCmdList = cmdList;
			try
			{
				string str, tmp, cmd_name;
				bool b_exit = false;
				string cmd_param;
				int ret = 0;
				List<string> t, candidate;
				int i;

				if (Str.IsEmptyStr(execCommandOrNull))
				{
				// プロンプト表示
				RETRY:
					tmp = prompt;
					str = this.readLine(tmp, false);

					if (str != null && Str.IsEmptyStr(str))
					{
						goto RETRY;
					}
				}
				else
				{
					// exec_command を使用
					if (prompt != null)
					{
						if (this.consoleType != ConsoleType.Csv)
						{
							//this.write(prompt + execCommandOrNull);
						}
					}
					str = execCommandOrNull;
				}

				if (str == null)
				{
					// ユーザーキャンセル
					return false;
				}

				str = Str.TrimCrlf(str).Trim();

				if (Str.IsEmptyStr(str))
				{
					// 何もしない
					return true;
				}

				// コマンド名とパラメータに分ける
				if (SeparateCommandAndParam(str, out cmd_name, out cmd_param) == false)
				{
					// 何もしない
					return true;
				}

				if (cmd_name.Length >= 2 && cmd_name[0] == '?' && cmd_name[1] != '?')
				{
					cmd_name = cmd_name.Substring(1);
					cmd_param = "/?";
				}

				if (cmd_name.Length >= 2 && cmd_name.EndsWith("?") && cmd_name[cmd_name.Length - 2] != '?')
				{
					cmd_name = cmd_name.Substring(0, cmd_name.Length - 1);
					cmd_param = "/?";
				}

				// コマンドの候補を取得する
				t = new List<string>();
				for (i = 0; i < cmdList.Count; i++)
				{
					t.Add(cmdList.Keys[i]);
				}

				if (IsHelpStr(cmd_name))
				{
					if (Str.IsEmptyStr(cmd_param))
					{
						// 使用できるコマンド一覧を表示する
						this.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_HELP_1), t.Count));

						string[] candidateList = t.ToArray();

						PrintCandidateHelp(null, candidateList, 1, cmdList);

						this.write("");
						this.write(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_HELP_2));
					}
					else
					{
						// 指定したコマンドのヘルプを表示する
						string tmp2, tmp3;
						if (SeparateCommandAndParam(cmd_param, out tmp2, out tmp3))
						{
							bool b = true;

							if (IsHelpStr(tmp2))
							{
								b = false;
							}

							if (b)
							{
								DispatchCommand(Str.FormatC("%S /help", tmp2), null, commandClass, invokerInstance);
							}
						}
					}
				}
				else if (Str.StrCmpi(cmd_name, "exit") ||
					Str.StrCmpi(cmd_name, "quit"))
				{
					// 終了
					b_exit = true;
				}
				else
				{
					candidate = Str.StrArrayToList(GetRealnameCandidate(cmd_name, t.ToArray()));

					if (candidate == null || candidate.Count == 0)
					{
						// 候補無し
						this.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CON_UNKNOWN_CMD), cmd_name));

						this.retCode = ConsoleErrorCode.ERR_BAD_COMMAND_OR_PARAM;
					}
					else if (candidate.Count >= 2)
					{
						// 候補が複数ある
						this.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CON_AMBIGIOUS_CMD), cmd_name));
						this.write(CdfGlobalLangSettings.ProcStr(CoreStr.CON_AMBIGIOUS_CMD_1));
						string[] candidateArray = candidate.ToArray();

						PrintCandidateHelp(null, candidateArray, 1, cmdList);
						this.write(CdfGlobalLangSettings.ProcStr(CoreStr.CON_AMBIGIOUS_CMD_2));

						this.retCode = ConsoleErrorCode.ERR_BAD_COMMAND_OR_PARAM;
					}
					else
					{
						string real_cmd_name;
						int j;

						// 1 つに定まった
						real_cmd_name = candidate[0];

						for (j = 0; j < cmdList.Count; j++)
						{
							if (Str.Equals(cmdList.Values[j].name, real_cmd_name))
							{
								// CSV モードでなければコマンドの説明を表示する
								if (this.consoleType != ConsoleType.Csv)
								{
									this.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CMD_EXEC_MSG_NAME),
										cmdList.Values[j].name,
										cmdList.Values[j].Description));
								}

								// コマンドのプロシージャを呼び出す
								object srcObject = null;
								if (cmdList.Values[j].methodInfo.IsStatic == false)
								{
									srcObject = invokerInstance;
								}
								object[] paramList =
								{
									this,
									real_cmd_name,
									cmd_param,
								};

								try
								{
									ret = (int)cmdList.Values[j].methodInfo.Invoke(srcObject, paramList);
								}
								catch (TargetInvocationException ex)
								{
									Exception ex2 = ex.GetBaseException();

									if (ex2 is ConsoleUserCancelException)
									{
										// ユーザーによるパラメータ指定キャンセル
										this.write(CdfGlobalLangSettings.ProcStr(CoreStr.CON_USER_CANCELED));
										this.write("");
										this.retCode = ConsoleErrorCode.ERR_USER_CANCELED;
									}
									else
									{
										this.write(ex2.ToString());
										this.write("");

										this.retCode = ConsoleErrorCode.ERR_INNER_EXCEPTION;
										this.retErrorMessage = ex2.Message;
									}

									return true;
								}

								if (ret == -1)
								{
									// 終了コマンド
									b_exit = true;
								}
								else
								{
									this.retCode = ret;
								}
							}
						}
					}
				}

				if (b_exit)
				{
					return false;
				}

				return true;
			}
			finally
			{
				currentCmdList = null;
			}
		}

		// コマンド名の一覧の取得
		public static SortedList<string, ConsoleCommandMethod> GetCommandList(Type commandClass)
		{
			SortedList<string, ConsoleCommandMethod> cmdList = new SortedList<string, ConsoleCommandMethod>(new StrComparer(false));

			// コマンド名の一覧の取得
			BindingFlags[] searchFlags =
			{
				BindingFlags.Static | BindingFlags.NonPublic,
				BindingFlags.Static | BindingFlags.Public,
				BindingFlags.Instance | BindingFlags.NonPublic,
				BindingFlags.Instance | BindingFlags.Public,
			};

			foreach (BindingFlags bFlag in searchFlags)
			{
				MemberInfo[] members = commandClass.GetMembers(bFlag);

				foreach (MemberInfo info in members)
				{
					if ((info.MemberType & MemberTypes.Method) != 0)
					{
						MethodInfo mInfo = commandClass.GetMethod(info.Name, bFlag);

						object[] customAtts = mInfo.GetCustomAttributes(true);

						foreach (object att in customAtts)
						{
							if (att is ConsoleCommandMethod)
							{
								ConsoleCommandMethod cc = (ConsoleCommandMethod)att;
								cc.bindingFlag = bFlag;
								cc.memberInfo = info;
								cc.methodInfo = mInfo;
								cc.name = info.Name;

								cmdList.Add(info.Name, cc);

								break;
							}
						}
					}
				}
			}

			return cmdList;
		}

		// 現在のコンソールの横幅を取得する
		public int GetConsoleWidth()
		{
			int size = this.getWidth();

			if (size == 0)
			{
				size = 80;
			}

			if (size < 32)
			{
				size = 32;
			}

			if (size > 65535)
			{
				size = 65535;
			}

			return size;
		}

		// コマンドラインをコマンドとパラメータの 2 つに分離する
		public static bool SeparateCommandAndParam(string src, out string cmd, out string param)
		{
			int i, len;
			StringBuilder tmp;
			string src_tmp;
			cmd = param = null;
			// 引数チェック
			if (src == null)
			{
				return false;
			}

			src_tmp = Str.TrimCrlf(src).Trim();

			len = src_tmp.Length;
			tmp = new StringBuilder();

			for (i = 0; i < (len + 1); i++)
			{
				char c;

				if (i != len)
				{
					c = src_tmp[i];
				}
				else
				{
					c = (char)0;
				}

				switch (c)
				{
					case (char)0:
					case ' ':
					case '\t':
						if (Str.IsEmptyStr(tmp.ToString()))
						{
							return false;
						}
						cmd = tmp.ToString().Trim();
						goto ESCAPE;

					default:
						tmp.Append(c);
						break;
				}
			}

		ESCAPE:
			param = src_tmp.Substring(tmp.Length).Trim();

			return true;
		}

		// ユーザーが指定したコマンド名の省略形に一致する実在するコマンドの一覧の候補を取得する
		public static string[] GetRealnameCandidate(string inputName, string[] realNameList)
		{
			List<string> o = new List<string>();
			// 引数チェック
			if (inputName == null || realNameList == null)
			{
				return new string[0];
			}

			int i;
			bool ok = false;
			for (i = 0; i < realNameList.Length; i++)
			{
				string name = realNameList[i];

				// まず最優先で完全一致するものを検索する
				if (Str.StrCmpi(name, inputName))
				{
					o.Add(name);
					ok = true;
					break;
				}
			}

			if (ok == false)
			{
				// 完全一致するコマンドが無い場合、省略形コマンドとして一致するかどうかチェックする
				for (i = 0; i < realNameList.Length; i++)
				{
					string name = realNameList[i];

					if (IsOmissionName(inputName, name) ||
						IsNameInRealName(inputName, name))
					{
						// 省略形を発見した
						o.Add(name);
						ok = true;
					}
				}
			}

			if (ok)
			{
				// 1 つ以上の候補が見つかった
				return o.ToArray();
			}
			else
			{
				return new string[0];
			}
		}

		// ユーザーが指定したコマンドが既存のコマンドの省略形かどうかチェックする
		public static bool IsOmissionName(string inputName, string realName)
		{
			string oname;
			// 引数チェック
			if (inputName == null || realName == null)
			{
				return false;
			}

			if (Str.IsAllUpperStr(realName))
			{
				// すべて大文字のコマンドは省略形をとらない
				return false;
			}

			oname = GetOmissionName(realName);

			if (Str.IsEmptyStr(oname))
			{
				return false;
			}

			if (oname.StartsWith(inputName, StringComparison.InvariantCultureIgnoreCase))
			{
				// 例: AccountSecureCertSet の oname は ascs だが
				//     ユーザーが asc と入力した場合は true を返す
				return true;
			}

			if (inputName.StartsWith(oname, StringComparison.InvariantCultureIgnoreCase))
			{
				// 例: AccountConnect と
				//     AccountCreate の 2 つのコマンドが実在する際、
				//     ユーザーが "aconnect" と入力すると、
				//     AccountConnect のみ true になるようにする

				if (realName.EndsWith(inputName.Substring(oname.Length), StringComparison.InvariantCultureIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		// 指定したコマンド名の省略名を取得する
		public static string GetOmissionName(string src)
		{
			int i, len;
			// 引数チェック
			if (src == null)
			{
				return null;
			}

			string dst = "";
			len = src.Length;

			for (i = 0; i < len; i++)
			{
				char c = src[i];

				if ((c >= '0' && c <= '9') ||
					(c >= 'A' && c <= 'Z'))
				{
					dst += c;
				}
			}

			return dst;
		}

		// ユーザーが指定したコマンドが既存のコマンドに一致するかどうかチェックする
		public static bool IsNameInRealName(string inputName, string realName)
		{
			// 引数チェック
			if (inputName == null || realName == null)
			{
				return false;
			}

			if (realName.StartsWith(inputName, StringComparison.InvariantCultureIgnoreCase))
			{
				return true;
			}

			return false;
		}

		// コマンドリストをパースする
		public ConsoleParamValueList ParseCommandList(string cmdName, string command, ConsoleParam[] param)
		{
			ConsoleParamValueList ret = parseCommandLineMain(cmdName, command, param);

			if (ret == null)
			{
				throw new ConsoleUserCancelException("");
			}

			return ret;
		}
		private ConsoleParamValueList parseCommandLineMain(string cmdName, string command, ConsoleParam[] param)
		{
			int i;
			ConsoleParamValueList o;
			List<string> param_list;
			List<string> real_name_list;
			bool help_mode = false;
			string tmp;
			bool ok = true;
			// 引数チェック
			if (command == null || cmdName == null)
			{
				return null;
			}

			// 初期化
			for (i = 0; i < param.Length; i++)
			{
				if (Str.IsEmptyStr(param[i].Name) == false)
				{
					if (param[i].Name.StartsWith("["))
					{
						param[i].Tmp = "";
					}
					else
					{
						param[i].Tmp = null;
					}
				}
				else
				{
					param[i].Tmp = "";
				}
			}

			param_list = Str.StrArrayToList(GetCommandNameList(command));

			real_name_list = new List<string>();

			for (i = 0; i < param.Length; i++)
			{
				real_name_list.Add(param[i].Name);
			}

			for (i = 0; i < param_list.Count; i++)
			{
				string s = param_list[i];

				if (Str.StrCmpi(s, "help") ||
					Str.StrCmpi(s, "?"))
				{
					help_mode = true;
					break;
				}
			}

			tmp = ParseCommand(command, "");
			if (tmp != null)
			{
				if (Str.StrCmpi(tmp, "?"))
				{
					help_mode = true;
				}
			}

			if (help_mode)
			{
				// ヘルプを表示
				PrintCmdHelp(cmdName, real_name_list);
				return null;
			}

			for (i = 0; i < param_list.Count; i++)
			{
				// ユーザーが指定したすべてのパラメータ名について対応するコマンドを取得する
				string[] candidate = GetRealnameCandidate(param_list[i], real_name_list.ToArray());

				if (candidate != null && candidate.Length >= 1)
				{
					if (candidate.Length >= 2)
					{
						this.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CON_AMBIGIOUS_PARAM),
							param_list[i]));

						this.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CON_AMBIGIOUS_PARAM_1),
							cmdName));

						PrintCandidateHelp(cmdName, candidate, 1, this.currentCmdList);
						this.write(CdfGlobalLangSettings.ProcStr(CoreStr.CON_AMBIGIOUS_PARAM_2));

						ok = false;
					}
					else
					{
						int j;
						string real_name = candidate[0];

						// 候補が 1 つだけしか無い
						for (j = 0; j < param.Length; j++)
						{
							if (Str.StrCmpi(param[j].Name, real_name))
							{
								param[j].Tmp = param_list[i];
							}
						}
					}
				}
				else
				{
					this.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CON_INVALID_PARAM),
						param_list[i],
						cmdName,
						cmdName));

					ok = false;
				}
			}

			if (ok == false)
			{
				return null;
			}

			// リストの作成
			o = new ConsoleParamValueList();

			// パラメータ一覧に指定された名前のすべてのパラメータを読み込む
			for (i = 0; i < param.Length; i++)
			{
				ConsoleParam p = param[i];
				bool is_default_value = false;

				if (p.Tmp == "")
				{
					is_default_value = true;
				}

				if (p.Tmp != null || p.PromptProc != null)
				{
					string name = p.Name;
					string tmp2, str;

					if (p.Tmp != null)
					{
						tmp2 = p.Tmp;
					}
					else
					{
						tmp2 = p.Name;
					}

					str = ParseCommand(command, tmp2);

					if (str != null)
					{
						string unistr;
						bool ret;
					EVAL_VALUE:
						// 読み込みに成功した
						unistr = str;

						if (p.EvalProc != null)
						{
							// EvalProc が指定されている場合は値を評価する
							ret = p.EvalProc(this, unistr, p.EvalProcParam);
						}
						else
						{
							// EvalProc が指定されていない場合はどのような値でも受け付ける
							ret = true;
						}

						if (ret == false)
						{
							string tmp3;
							// 指定した値は不正である
							if (p.PromptProc == null)
							{
								// キャンセル
								ok = false;
								break;
							}
							else
							{
								// もう一度入力させる
								str = null;
								// 必須パラメータであるのでプロンプトを表示する
								tmp3 = p.PromptProc(this, p.PromptProcParam);
								if (tmp3 == null)
								{
									// ユーザーがキャンセルした
									ok = false;
									break;
								}
								else
								{
									// ユーザーが入力した
									this.write("");
									str = tmp3;
									goto EVAL_VALUE;
								}
							}
						}
						else
						{
							o.Add(new ConsoleParamValue(p.Name, str, Str.StrToInt(str), is_default_value));
						}
					}
					else
					{
						// 読み込みに失敗した。指定されたパラメータが指定されていない
						if (p.PromptProc != null)
						{
							string tmp4;
							// 必須パラメータであるのでプロンプトを表示する
							tmp4 = p.PromptProc(this, p.PromptProcParam);
							if (tmp4 == null)
							{
								// ユーザーがキャンセルした
								ok = false;
								break;
							}
							else
							{
								// ユーザーが入力した
								this.write("");
								str = tmp4;
								if (true)
								{
									string unistr;
									bool ret;
								EVAL_VALUE:
									// 読み込みに成功した
									unistr = str;

									if (p.EvalProc != null)
									{
										// EvalProc が指定されている場合は値を評価する
										ret = p.EvalProc(this, unistr, p.EvalProcParam);
									}
									else
									{
										// EvalProc が指定されていない場合はどのような値でも受け付ける
										ret = true;
									}

									if (ret == false)
									{
										// 指定した値は不正である
										if (p.PromptProc == null)
										{
											// キャンセル
											ok = false;
											break;
										}
										else
										{
											// もう一度入力させる
											str = null;
											// 必須パラメータであるのでプロンプトを表示する
											tmp4 = p.PromptProc(this, p.PromptProcParam);
											if (tmp4 == null)
											{
												// ユーザーがキャンセルした
												ok = false;
												break;
											}
											else
											{
												// ユーザーが入力した
												this.write("");
												str = tmp4;
												goto EVAL_VALUE;
											}
										}
									}
									else
									{
										o.Add(new ConsoleParamValue(p.Name, str, Str.StrToInt(str), is_default_value));
									}
								}
							}
						}
					}
				}
			}

			if (ok)
			{
				return o;
			}
			else
			{
				return null;
			}
		}

		// 入力されたコマンドに含まれていたパラメータ名のリストを取得する
		public static string[] GetCommandNameList(string str)
		{
			if (str == null)
			{
				return new string[0];
			}

			string[] pl;
			ParseCommand(str, "dummy_str", out pl);

			return pl;
		}

		// コマンドをパースする
		public static string ParseCommand(string str, string name)
		{
			string[] pl;
			return ParseCommand(str, name, out pl);
		}
		public static string ParseCommand(string str, string name, out string[] paramList)
		{
			int i;
			string tmp, ret = null;
			SortedList<string, int> o;
			// 引数チェック
			paramList = null;
			if (str == null)
			{
				return null;
			}
			if (Str.IsEmptyStr(name))
			{
				name = null;
			}

			o = new SortedList<string, int>(new StrComparer(false));

			tmp = str.Trim();

			i = Str.SearchStr(tmp, "/CMD", 0, false);

			if (i >= 1 && tmp[i - 1] == '/')
			{
				i = -1;
			}
			if (i == -1)
			{
				i = Str.SearchStr(tmp, "/CMD\t", 0, false);
				if (i >= 1 && tmp[i - 1] == '/')
				{
					i = -1;
				}
			}
			if (i == -1)
			{
				i = Str.SearchStr(tmp, "/CMD:", 0, false);
				if (i >= 1 && tmp[i - 1] == '/')
				{
					i = -1;
				}
			}
			if (i == -1)
			{
				i = Str.SearchStr(tmp, "/CMD=", 0, false);
				if (i >= 1 && tmp[i - 1] == '/')
				{
					i = -1;
				}
			}
			if (i == -1)
			{
				i = Str.SearchStr(tmp, "-CMD ", 0, false);
				if (i >= 1 && tmp[i - 1] == '-')
				{
					i = -1;
				}
			}
			if (i == -1)
			{
				i = Str.SearchStr(tmp, "-CMD\t", 0, false);
				if (i >= 1 && tmp[i - 1] == '-')
				{
					i = -1;
				}
			}
			if (i == -1)
			{
				i = Str.SearchStr(tmp, "-CMD:", 0, false);
				if (i >= 1 && tmp[i - 1] == '-')
				{
					i = -1;
				}
			}
			if (i == -1)
			{
				i = Str.SearchStr(tmp, "-CMD=", 0, false);
				if (i >= 1 && tmp[i - 1] == '-')
				{
					i = -1;
				}
			}

			if (i != -1)
			{
				string s = "CMD";
				if (o != null)
				{
					if (o.ContainsKey(s) == false)
					{
						o.Add(s, 0);
					}
				}
				if (Str.StrCmpi(name, "CMD"))
				{
					ret = str.Substring(i + 5).Trim();
				}
				else
				{
					tmp = tmp.Substring(0, i);
				}
			}

			if (ret == null)
			{
				string[] t = Str.ParseCmdLine(tmp);

				if (t != null)
				{
					for (i = 0; i < t.Length; i++)
					{
						string token = t[i];

						if ((token[0] == '-' && token[1] != '-') ||
							(Str.StrCmpi(token, "--help")) ||
							(token[0] == '/' && token[1] != '/'))
						{
							int j;
							// 名前付き引数
							// コロン文字があるかどうか調べる
							if (Str.StrCmpi(token, "--help"))
							{
								token = token.Substring(1);
							}

							j = Str.SearchStr(token, ":", 0, false);
							if (j == -1)
							{
								j = Str.SearchStr(token, "=", 0, false);
							}
							if (j != -1)
							{
								string tmp2;
								string a;

								// コロン文字がある
								tmp2 = token;
								if (tmp2.Length >= j)
								{
									tmp2 = tmp2.Substring(0, j);
								}

								a = tmp2.Substring(1);
								if (o != null)
								{
									if (o.ContainsKey(a) == false)
									{
										o.Add(a, 0);
									}
								}

								if (tmp2.Length >= 1 && Str.StrCmpi(name, tmp2.Substring(1)))
								{
									if (ret == null)
									{
										// 内容
										ret = token.Substring(j + 1);
									}
								}
							}
							else
							{
								// コロン文字が無い
								string a = token.Substring(1);

								if (o != null)
								{
									if (o.ContainsKey(a) == false)
									{
										o.Add(a, 0);
									}

									if (Str.StrCmpi(name, token.Substring(1)))
									{
										if (ret == null)
										{
											// 空文字
											ret = "";
										}
									}
								}
							}
						}
						else
						{
							// 名前無し引数
							if (name == null)
							{
								if (ret == null)
								{
									if (token.StartsWith("--"))
									{
										ret = token.Substring(1);
									}
									else if (token.StartsWith("//"))
									{
										ret = token.Substring(1);
									}
									else
									{
										ret = token;
									}
								}
							}
						}
					}
				}
			}

			if (o != null)
			{
				List<string> t = new List<string>();

				int j;
				for (j = 0; j < o.Count; j++)
				{
					t.Add(o.Keys[j]);
				}

				paramList = t.ToArray();
			}

			if (ret != null)
			{
				if (Str.StrCmpi(ret, "none") || Str.StrCmpi(ret, "null"))
				{
					// none と ret は予約語である
					ret = "";
				}
			}

			return ret;
		}

		// 新しいローカルコンソールの作成
		public static ConsoleService NewLocalConsoleService()
		{
			return NewLocalConsoleService(null, null);
		}
		public static ConsoleService NewLocalConsoleService(string outFileName)
		{
			return NewLocalConsoleService(null, outFileName);
		}
		public static ConsoleService NewLocalConsoleService(string inFileName, string outFileName)
		{
			IO in_io = null, out_io = null;

			ConsoleService c = new ConsoleService();
			int old_size = 0;

			c.consoleType = ConsoleType.Local;
			c.free = new ConsoleFreeDelegate(c.localFree);
			c.readLine = new ConsoleReadLineDelegate(c.localReadLine);
			c.readPassword = new ConsoleReadPasswordDelegate(c.localReadPassword);
			c.write = new ConsoleWriteDelegate(c.localWrite);
			c.getWidth = new ConsoleGetWidthDelegate(c.localGetWidth);

			if (Str.IsEmptyStr(inFileName) == false)
			{
				// 入力ファイルが指定されている
				try
				{
					in_io = IO.FileOpen(inFileName, false);
				}
				catch
				{
					c.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CON_INFILE_ERROR), inFileName));
					return null;
				}
				c.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CON_INFILE_START), inFileName));
			}

			if (Str.IsEmptyStr(outFileName) == false)
			{
				// 出力ファイルが指定されている
				try
				{
					out_io = IO.FileCreate(outFileName);
				}
				catch
				{
					c.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CON_OUTFILE_ERROR), outFileName));
					if (in_io != null)
					{
						in_io.Close();
					}

					return null;
				}
				c.write(Str.FormatC(CdfGlobalLangSettings.ProcStr(CoreStr.CON_OUTFILE_START), outFileName));
			}

			c.inFile = in_io;
			c.outFile = out_io;
			c.win32_OldConsoleWidth = old_size;

			if (in_io != null)
			{
				byte[] data = in_io.ReadAll();

				c.inBuf = new Buf(data);
			}

			Con.SetConsoleService(c);

			return c;
		}

		// 解放
		void localFree()
		{
			if (inFile != null)
			{
				inFile.Close();
				inFile = null;
			}

			if (outFile != null)
			{
				outFile.Close();
				outFile = null;
			}
		}

		// 画面の横幅を取得
		int localGetWidth()
		{
			int ret = Console.WindowWidth;

			if (ret <= 0)
			{
				ret = 1;
			}

			return ret;
		}

		// コンソールから 1 行読み込む
		string localReadLine(string prompt, bool noFile)
		{
			string ret;
			if (prompt == null)
			{
				prompt = ">";
			}

			writeOutFile(prompt, false);

			if (noFile == false && inBuf != null)
			{
				// ファイルから次の行を読み込む
				ret = readNextFromInFile();

				if (ret != null)
				{
					// 擬似プロンプトを表示する
					Console.Write(prompt);

					// 画面に描画する
					Console.WriteLine(ret);
				}
			}
			else
			{
				// 画面から次の行を読み込む
				Console.Write(prompt);
				ret = Console.ReadLine();

				if (ret != null)
				{
					if (ret.IndexOf((char)0x04) != -1 || ret.IndexOf((char)0x1a) != -1)
					{
						ret = null;
					}
				}
			}

			if (ret != null)
			{
				writeOutFile(ret, true);
			}
			else
			{
				writeOutFile("[EOF]", true);
			}

			return ret;
		}

		// コンソールからパスワードを読み込む
		string localReadPassword(string prompt)
		{
			if (prompt == null)
			{
				prompt = "Password>";
			}

			Console.Write(prompt);
			writeOutFile(prompt, false);

			string tmp = Str.PasswordPrompt();
			if (tmp != null)
			{
				writeOutFile("********", true);
				return tmp;
			}

			return null;
		}

		// コンソールに文字列を表示する
		bool localWrite(string str)
		{
			// 引数チェック
			Console.Write("{0}{1}",
				str,
				(str.EndsWith("\n") ? "" : "\n"));

			writeOutFile(str, true);

			return true;
		}

		// 入力ファイルから次の 1 行を読み込む
		string readNextFromInFile()
		{
			if (inBuf == null)
			{
				return null;
			}

			while (true)
			{
				string str = inBuf.ReadNextLineAsString();
				if (str == null)
				{
					return null;
				}

				str = str.Trim();

				if (Str.IsEmptyStr(str) == false)
				{
					return str;
				}
			}
		}

		// 出力ファイルが指定されている場合は書き出す
		void writeOutFile(string str, bool addLastCrlf)
		{
			if (outFile != null)
			{
				string tmp = Str.NormalizeCrlf(str);

				outFile.Write(Str.Utf8Encoding.GetBytes(str));

				if (str.EndsWith("\n") == false && addLastCrlf)
				{
					outFile.Write(Str.Utf8Encoding.GetBytes(Env.NewLine));
				}

				outFile.Flush();
			}
		}
	}
}
