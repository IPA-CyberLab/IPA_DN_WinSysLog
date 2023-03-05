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

namespace CoreUtil
{
	// HamCore エントリ
	internal class HamCoreEntry : IComparable
	{
		public string FileName = "";
		public uint Size = 0;
		public uint SizeCompressed = 0;
		public uint Offset = 0;
		public byte[] Buffer = null;
		public long LastAccess = 0;

		public int CompareTo(object obj)
		{
			HamCoreEntry hc1, hc2;
			hc1 = this;
			hc2 = (HamCoreEntry)obj;

			return Str.StrCmpiRetInt(hc1.FileName, hc2.FileName);
		}
	}

	// HamCore ビルダー
	public class HamCoreBuilderFileEntry : IComparable<HamCoreBuilderFileEntry>
	{
		public string Name;
		public Buf RawData;
		public Buf CompressedData;
		public int Offset = 0;

		int IComparable<HamCoreBuilderFileEntry>.CompareTo(HamCoreBuilderFileEntry other)
		{
			return this.Name.CompareTo(other.Name);
		}
	}

	public class HamCoreBuilder
	{
		List<HamCoreBuilderFileEntry> fileList;
		public List<HamCoreBuilderFileEntry> FileList
		{
			get { return fileList; }
		}

		public bool IsFile(string name)
		{
			foreach (HamCoreBuilderFileEntry f in fileList)
			{
				if (f.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		public bool DeleteFile(string name)
		{
			foreach (HamCoreBuilderFileEntry f in fileList)
			{
				if (f.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
				{
					fileList.Remove(f);
					return true;
				}
			}

			return false;
		}

		public HamCoreBuilder()
		{
			fileList = new List<HamCoreBuilderFileEntry>();
		}

		public void AddDir(string dirName)
		{
			dirName = IO.RemoteLastEnMark(dirName);

			DirEntry[] ee = IO.EnumDirEx(dirName);

			foreach (DirEntry e in ee)
			{
				if (e.IsFolder == false)
				{
					AddFile(e.FullPath, dirName);
				}
			}
		}

		public void AddFile(string fileName, string baseDirFileName)
		{
			string name = IO.GetRelativeFileName(fileName, baseDirFileName);

			AddFile(name, File.ReadAllBytes(fileName));
		}

		public void AddFile(string name, byte[] data)
		{
			if (IsFile(name))
			{
				throw new InvalidOperationException("fileName");
			}

			HamCoreBuilderFileEntry f = new HamCoreBuilderFileEntry();

			Console.Write("{0}: ", name);

			f.Name = name;
			f.RawData = new Buf(Util.CloneByteArray(data));
			Console.Write("{0} -> ", f.RawData.Size);
			f.CompressedData = new Buf(ZLib.Compress(f.RawData.ByteData));
			Console.WriteLine("{0}", f.CompressedData.Size);

			this.fileList.Add(f);
		}

		public void Build(string dstFileName)
		{
			Buf b = Build();

			IO.SaveFile(dstFileName, b.ByteData);
		}

		public Buf Build()
		{
			int z;
			Buf b;

			this.fileList.Sort();

			z = 0;

			z += HamCore.HamcoreHeaderSize;

			// ファイルの個数
			z += sizeof(int);

			// ファイルテーブル
			foreach (HamCoreBuilderFileEntry f in this.fileList)
			{
				// ファイル名
				z += Str.ShiftJisEncoding.GetByteCount(f.Name) + sizeof(int);
				// ファイルサイズ
				z += sizeof(int);
				z += sizeof(int);
				// オフセットデータ
				z += sizeof(int);
			}
			// ファイル本体
			foreach (HamCoreBuilderFileEntry f in this.fileList)
			{
				f.Offset = z;
				z += (int)f.CompressedData.Size;
			}

			// 書き込み
			b = new Buf();
			// ヘッダ
			b.Write(Str.ShiftJisEncoding.GetBytes(HamCore.HamcoreHeaderData));
			b.WriteInt((uint)this.fileList.Count);
			foreach (HamCoreBuilderFileEntry f in this.fileList)
			{
				// ファイル名
				b.WriteStr(f.Name, true);
				// ファイルサイズ
				b.WriteInt(f.RawData.Size);
				b.WriteInt(f.CompressedData.Size);
				// オフセット
				b.WriteInt((uint)f.Offset);
			}
			// 本体
			foreach (HamCoreBuilderFileEntry f in this.fileList)
			{
				b.Write(f.CompressedData.ByteData);
			}

			b.SeekToBegin();

			return b;
		}
	}

	// HamCore ファイル
	public class HamCore
	{
		public const string HamcoreDirName = "@hamcore";
		public const string HamcoreHeaderData = "HamCore";
		public const int HamcoreHeaderSize = 7;
		public const long HamcoreCacheExpires = 5 * 60 * 1000;
		bool disableReadRawFile = false;
		public bool DisableReadRawFile
		{
			get { return disableReadRawFile; }
			set { disableReadRawFile = value; }
		}

		Dictionary<string, HamCoreEntry> list;

		IO hamcore_io;

		public HamCore(string filename)
		{
			init(filename);
		}

		public string[] GetFileNames()
		{
			List<string> ret = new List<string>();

			foreach (HamCoreEntry e in list.Values)
			{
				ret.Add(e.FileName);
			}

			return ret.ToArray();
		}

		void init(string filename)
		{
			filename = IO.InnerFilePath(filename);
			string filenameOnly = Path.GetFileName(filename);
			string filenameAlt = Path.Combine(Path.GetDirectoryName(filename), "_" + filenameOnly);

			try
			{
				IO.FileReplaceRename(filenameAlt, filename);
			}
			catch
			{
			}

			list = new Dictionary<string, HamCoreEntry>();

			try
			{
				hamcore_io = IO.FileOpen(filename);
			}
			catch
			{
				return;
			}

			try
			{
				// ファイルヘッダを読み込む
				byte[] header = hamcore_io.Read(HamcoreHeaderSize);
				byte[] header2 = Str.AsciiEncoding.GetBytes(HamcoreHeaderData);
				if (header == null || Util.CompareByte(header, header2) == false)
				{
					throw new SystemException();
				}

				uint num = 0;
				byte[] buf = hamcore_io.Read(Util.SizeOfInt32);
				num = Util.ByteToUInt(buf);
				uint i;
				for (i = 0; i < num; i++)
				{
					// ファイル名
					uint str_size;

					buf = hamcore_io.Read(Util.SizeOfInt32);
					str_size = Util.ByteToUInt(buf);
					if (str_size >= 1)
					{
						str_size--;
					}

					byte[] str_data = hamcore_io.Read((int)str_size);
					string tmp = Str.ShiftJisEncoding.GetString(str_data);

					HamCoreEntry c = new HamCoreEntry();
					c.FileName = tmp;

					buf = hamcore_io.Read(Util.SizeOfInt32);
					c.Size = Util.ByteToUInt(buf);

					buf = hamcore_io.Read(Util.SizeOfInt32);
					c.SizeCompressed = Util.ByteToUInt(buf);

					buf = hamcore_io.Read(Util.SizeOfInt32);
					c.Offset = Util.ByteToUInt(buf);

					list.Add(c.FileName.ToUpper(), c);
				}
			}
			catch
			{
				hamcore_io.Close();
			}
		}

		public Buf ReadHamcore(string name)
		{
			if (name[0] == '|')
			{
				name = name.Substring(1);
			}
			if (name[0] == '/' || name[0] == '\\')
			{
				name = name.Substring(1);
			}

			string filename = name;

			filename = filename.Replace("/", "\\");

			// ローカルディスクの hamcore/ ディレクトリにファイルがあればそれを読み込む
			Buf b;

			if (this.disableReadRawFile == false)
			{
				try
				{
					b = Buf.ReadFromFile(HamcoreDirName + "\\" + filename);

					return b;
				}
				catch
				{
				}
			}

			// 無い場合は hamcore システムで読み込む
			lock (list)
			{
				HamCoreEntry c;
				string key = filename.ToUpper();

				b = null;

				if (list.ContainsKey(key))
				{
					c = list[key];

					if (c.Buffer != null)
					{
						// 既に読み込まれている
						b = new Buf(c.Buffer);
						b.SeekToBegin();
						c.LastAccess = Time.Tick64;
					}
					else
					{
						// 読み込まれていないのでファイルから読み込む
						if (hamcore_io.Seek(SeekOrigin.Begin, (int)c.Offset))
						{
							// 圧縮データの読み込み
							byte[] data = hamcore_io.Read((int)c.SizeCompressed);

							// 展開する
							int dstSize = (int)c.Size;
							byte[] buffer = ZLib.Uncompress(data, dstSize);

							c.Buffer = buffer;
							b = new Buf(buffer);
							b.SeekToBegin();
							c.LastAccess = Time.Tick64;
						}
					}
				}

				// 有効期限の切れたキャッシュを削除する
				long now = Time.Tick64;
				foreach (HamCoreEntry cc in list.Values)
				{
					if (cc.Buffer != null)
					{
						if (((cc.LastAccess + HamcoreCacheExpires) < now) ||
							cc.FileName.StartsWith("Li", StringComparison.CurrentCultureIgnoreCase))
						{
							cc.Buffer = null;
						}
					}
				}
			}

			return b;
		}
	}

	// ディレクトリエントリ
	public class DirEntry : IComparable<DirEntry>
	{
		internal bool folder;
		public bool IsFolder
		{
			get { return folder; }
		}
		internal string fileName;
		public string FileName
		{
			get { return fileName; }
		}
		internal string fullPath;
		public string FullPath
		{
			get { return fullPath; }
		}
		internal string relativePath;
		public string RelativePath
		{
			get { return relativePath; }
		}
		internal long fileSize;
		public long FileSize
		{
			get { return fileSize; }
		}
		internal DateTime createDate;
		public DateTime CreateDate
		{
			get { return createDate; }
		}
		internal DateTime updateDate;
		public DateTime UpdateDate
		{
			get { return updateDate; }
		}

		public int CompareTo(DirEntry other)
		{
			int i;
			i = Str.StrCmpiRetInt(this.fileName, other.fileName);
			if (i == 0)
			{
				i = Str.StrCmpRetInt(this.fileName, other.fileName);
			}

			return i;
		}

		public override string ToString()
		{
			return FileName;
		}
	};

	// ファイル操作
	public class IO
	{
		// ディレクトリのコピー
		public delegate bool CopyDirPreCopyDelegate(FileInfo srcFileInfo);
		public static void CopyDir(string srcDirName, string destDirName, CopyDirPreCopyDelegate preCopy, bool ignoreError, bool printStatus)
		{
			CopyDir(srcDirName, destDirName, preCopy, ignoreError, printStatus, false, false, false);
		}
		public static void CopyDir(string srcDirName, string destDirName, CopyDirPreCopyDelegate preCopy, bool ignoreError, bool printStatus,
			bool skipIfNoChange, bool deleteBom)
		{
			CopyDir(srcDirName, destDirName, preCopy, ignoreError, printStatus, skipIfNoChange, deleteBom, false);
		}
		public static void CopyDir(string srcDirName, string destDirName, CopyDirPreCopyDelegate preCopy, bool ignoreError, bool printStatus,
			bool skipIfNoChange, bool deleteBom, bool useTimeStampToCheckNoChange)
		{
			string[] files = Directory.GetFiles(srcDirName, "*", SearchOption.AllDirectories);

			foreach (string srcFile in files)
			{
				FileInfo info = new FileInfo(srcFile);

				// 宛先ファイル名
				string relativeFileName = IO.GetRelativeFileName(srcFile, srcDirName);
				string destFileName = Path.Combine(destDirName, relativeFileName);
				string destFileDirName = Path.GetDirectoryName(destFileName);

				if (preCopy != null)
				{
					if (preCopy(info) == false)
					{
						continue;
					}
				}

				try
				{
					if (Directory.Exists(destFileDirName) == false)
					{
						Directory.CreateDirectory(destFileDirName);
					}

					FileCopy(srcFile, destFileName, skipIfNoChange, deleteBom, useTimeStampToCheckNoChange);
				}
				catch
				{
					if (ignoreError == false)
					{
						throw;
					}
				}

				if (printStatus)
				{
					Con.WriteLine(relativeFileName);
				}
			}
		}

		// 定数
		public const string DefaultHamcoreFileName = "@hamcore.se2";

		// クラス変数
		static string hamcoreFileName = DefaultHamcoreFileName;
		public static string HamcoreFileName
		{
			get { return IO.hamcoreFileName; }
			set
			{
				lock (hamLockObj)
				{
					if (hamCore != null)
					{
						throw new ApplicationException();
					}

					IO.hamcoreFileName = value;
					tryToUseHamcore = false;
				}
			}
		}

		static bool tryToUseHamcore = true;
		static HamCore hamCore = null;
		static object hamLockObj = new object();
		public static HamCore HamCore
		{
			get
			{
				HamCore ret = null;

				lock (hamLockObj)
				{
					if (hamCore == null)
					{
						if (tryToUseHamcore)
						{
							if (hamCore == null)
							{
								try
								{
									ret = hamCore = new HamCore(hamcoreFileName);
								}
								catch
								{
									tryToUseHamcore = false;
								}
							}
						}
					}
				}

				return ret;
			}
		}

		// フィールド
		string name;
		public string Name
		{
			get { return name; }
		}
		FileStream p;
		public FileStream InnerFileStream
		{
			get { return p; }
		}
		bool writeMode;
		public bool WriteMode
		{
			get { return writeMode; }
		}
		bool hamMode;
		public bool HamMode
		{
			get { return hamMode; }
		}
		Buf hamBuf;

		object lockObj;

		// コンストラクタ
		private IO()
		{
			name = "";
			p = null;
			writeMode = hamMode = false;
			lockObj = new object();
			hamBuf = null;
		}

		// デストラクタ
		~IO()
		{
			Close();
		}

		// ファイルに文字列を書きこむ
		public static void WriteAllTextWithEncoding(string fileName, string str, Encoding encoding)
		{
			WriteAllTextWithEncoding(fileName, str, encoding, false);
		}
		public static void WriteAllTextWithEncoding(string fileName, string str, Encoding encoding, bool appendBom)
		{
			fileName = InnerFilePath(fileName);

			byte[] data = encoding.GetBytes(str);
			byte[] bom = null;
			if (appendBom)
			{
				bom = Str.GetBOM(encoding);
			}

			data = Util.CombineByteArray(bom, data);

			File.WriteAllBytes(fileName, data);
		}

		// ファイルを自動的に文字コードを認識して文字列を読み込む
		public static string ReadAllTextWithAutoGetEncoding(string fileName)
		{
			fileName = InnerFilePath(fileName);

			byte[] data = File.ReadAllBytes(fileName);

			int bomSize;
			Encoding enc = Str.GetEncoding(data, out bomSize);
			if (enc == null)
			{
				enc = Encoding.Default;
			}

			data = Util.RemoveStartByteArray(data, bomSize);

			return enc.GetString(data);
		}

		// 拡張子を元に一時ファイルを作成する
		public static IO CreateTempFileByExt(string ext)
		{
			return IO.FileCreate(CreateTempFileNameByExt(ext));
		}

		// 拡張子を指定するとその拡張子を持つ一時ファイルを作成する
		public static string CreateTempFileNameByExt(string ext)
		{
			if (Str.IsEmptyStr(ext))
			{
				ext = "tmp";
			}
			if (ext[0] == '.')
			{
				ext = ext.Substring(1);
			}

			while (true)
			{
				string newFilename;
				string fullPath;
				string randStr;

				randStr = Str.GenRandStr();
				newFilename = "__" + randStr + "." + ext;

				fullPath = CreateTempFileName(newFilename);

				if (IO.IsFileExists(fullPath) == false)
				{
					return fullPath;
				}
			}
		}

		// 一時ファイルを作成する
		public static IO CreateTempFile(string name)
		{
			return IO.FileCreate(CreateTempFileName(name));
		}

		// 一時ファイル名を作成する
		public static string CreateTempFileName(string name)
		{
			return Path.Combine(Env.MyTempDir, name);
		}

		// サブディレクトリを含んだディレクトリの列挙 (コールバック)
		public delegate bool EnumDirCallbackProc(DirEntry e);

		public static bool EnumDirWithCallback(string dirName, string file_exts, EnumDirCallbackProc cb)
		{
			List<string> ext_list = new List<string>();
			// 指定された拡張子を整理
			if (file_exts != null)
			{
				string[] tokens = file_exts.Split(' ', '\t', ',', ';');
				foreach (string ext in tokens)
				{
					string tmp = ext.ToLowerInvariant().Trim();
					if (Str.IsEmptyStr(tmp) == false)
					{
						if (tmp.StartsWith("*."))
						{
							tmp = tmp.Substring(2);
						}
						if (tmp.StartsWith("."))
						{
							tmp = tmp.Substring(1);
						}
						tmp = "." + tmp;

						ext_list.Add(tmp);
					}
				}
			}

			return enumDirWithCallback(dirName, dirName, cb, ext_list);
		}
		static bool enumDirWithCallback(string dirName, string baseDirName, EnumDirCallbackProc cb, List<string> ext_list)
		{
			string tmp = IO.InnerFilePath(dirName);

			string[] dirs = null;

			try
			{
				dirs = Directory.GetDirectories(tmp);
			}
			catch
			{
			}

			if (dirs != null)
			{
				Array.Sort(dirs);
				foreach (string name in dirs)
				{
					string fullPath = name;

					DirectoryInfo info = null;

					try
					{
						info = new DirectoryInfo(fullPath);
					}
					catch
					{
					}

					if (info != null)
					{
						DirEntry e = new DirEntry();

						e.fileName = Path.GetFileName(name);
						e.fileSize = 0;
						e.createDate = info.CreationTimeUtc;
						e.folder = true;
						e.updateDate = info.LastWriteTimeUtc;
						e.fullPath = fullPath;
						e.relativePath = GetRelativeFileName(fullPath, baseDirName);

						if (cb(e) == false)
						{
							return false;
						}

						enumDirWithCallback(fullPath, baseDirName, cb, ext_list);
					}
				}
			}

			string[] files = null;

			try
			{
				files = Directory.GetFiles(tmp);
			}
			catch
			{
			}

			if (files != null)
			{
				Array.Sort(files);
				foreach (string name in files)
				{
					string fullPath = name;

					bool ok = false;

					if (ext_list.Count == 0)
					{
						ok = true;
					}
					else
					{
						foreach (string ext in ext_list)
						{
							if (name.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase))
							{
								ok = true;
							}
						}
					}

					if (ok)
					{
						FileInfo info = null;

						try
						{
							info = new FileInfo(fullPath);
						}
						catch
						{
						}

						if (info != null)
						{
							DirEntry e = new DirEntry();

							e.fileName = Path.GetFileName(name);
							e.fileSize = info.Length;
							e.createDate = info.CreationTimeUtc;
							e.folder = false;
							e.updateDate = info.LastWriteTimeUtc;
							e.fullPath = fullPath;
							e.relativePath = GetRelativeFileName(fullPath, baseDirName);

							if (cb(e) == false)
							{
								return false;
							}
						}
					}
				}
			}

			return true;
		}

		// サブディレクトリを含んだディレクトリの列挙
		public static DirEntry[] EnumDirEx(string dirName)
		{
			List<DirEntry> list = new List<DirEntry>();

			enumDirEx(dirName, dirName, list);

			return list.ToArray();
		}
		static void enumDirEx(string dirName, string baseDirName, List<DirEntry> list)
		{
			string tmp = IO.InnerFilePath(dirName);

			string[] dirs = Directory.GetDirectories(tmp);
			foreach (string name in dirs)
			{
				string fullPath = name;
				DirectoryInfo info = new DirectoryInfo(fullPath);

				DirEntry e = new DirEntry();

				e.fileName = Path.GetFileName(name);
				e.fileSize = 0;
				e.createDate = info.CreationTimeUtc;
				e.folder = true;
				e.updateDate = info.LastWriteTimeUtc;
				e.fullPath = fullPath;
				e.relativePath = GetRelativeFileName(fullPath, baseDirName);

				list.Add(e);

				enumDirEx(fullPath, baseDirName, list);
			}

			string[] files = Directory.GetFiles(tmp);
			foreach (string name in files)
			{
				string fullPath = name;
				FileInfo info = new FileInfo(fullPath);

				DirEntry e = new DirEntry();

				e.fileName = Path.GetFileName(name);
				e.fileSize = info.Length;
				e.createDate = info.CreationTimeUtc;
				e.folder = false;
				e.updateDate = info.LastWriteTimeUtc;
				e.fullPath = fullPath;
				e.relativePath = GetRelativeFileName(fullPath, baseDirName);

				list.Add(e);
			}
		}

		// ディレクトリの列挙
		public static DirEntry[] EnumDir(string dirName)
		{
			List<DirEntry> list = new List<DirEntry>();
			string tmp = IO.InnerFilePath(dirName);

			string[] dirs = Directory.GetDirectories(tmp);
			foreach (string name in dirs)
			{
				string fullPath = name;
				DirectoryInfo info = new DirectoryInfo(fullPath);

				DirEntry e = new DirEntry();

				e.fileName = Path.GetFileName(name);
				e.fileSize = 0;
				e.createDate = info.CreationTimeUtc;
				e.folder = true;
				e.updateDate = info.LastWriteTimeUtc;
				e.fullPath = fullPath;
				e.relativePath = GetRelativeFileName(fullPath, dirName);

				list.Add(e);
			}

			string[] files = Directory.GetFiles(tmp);
			foreach (string name in files)
			{
				string fullPath = name;
				FileInfo info = new FileInfo(fullPath);

				DirEntry e = new DirEntry();

				e.fileName = Path.GetFileName(name);
				e.fileSize = info.Length;
				e.createDate = info.CreationTimeUtc;
				e.folder = false;
				e.updateDate = info.LastWriteTimeUtc;
				e.fullPath = fullPath;
				e.relativePath = GetRelativeFileName(fullPath, dirName);

				list.Add(e);
			}

			list.Sort();

			return list.ToArray();
		}

		// ファイルを置換してリネームする
		public static void FileReplaceRename(string oldName, string newName)
		{
			try
			{
				FileCopy(oldName, newName);
				FileDelete(oldName);
			}
			catch (Exception e)
			{
				throw e;
			}
		}

		// ファイルをコピーする
		public static void FileCopy(string oldName, string newName)
		{
			FileCopy(oldName, newName, false, false);
		}
		public static void FileCopy(string oldName, string newName, bool skipIfNoChange, bool deleteBom)
		{
			FileCopy(oldName, newName, skipIfNoChange, deleteBom, false);
		}
		public static void FileCopy(string oldName, string newName, bool skipIfNoChange, bool deleteBom, bool useTimeStampToCheckNoChange)
		{
			string tmp1 = InnerFilePath(oldName);
			string tmp2 = InnerFilePath(newName);

			if (useTimeStampToCheckNoChange && skipIfNoChange)
			{
				DateTime dt1, dt2;

				try
				{
					dt1 = Directory.GetLastWriteTimeUtc(tmp1);
					dt2 = Directory.GetLastWriteTimeUtc(tmp2);

					TimeSpan ts = dt2 - dt1;
					if (ts.TotalSeconds >= -5.0)
					{
						return;
					}
				}
				catch
				{
				}
			}

			if (skipIfNoChange || deleteBom)
			{
				byte[] srcData = File.ReadAllBytes(tmp1);
				byte[] destData = new byte[0];
				bool changed = true;
				int bomSize;

				Str.GetEncoding(srcData, out bomSize);
				if (bomSize >= 1)
				{
					srcData = Util.ExtractByteArray(srcData, bomSize, srcData.Length - bomSize);
				}

				if (skipIfNoChange)
				{
					try
					{
						FileStream fs = File.OpenRead(tmp2);
						long size = 0xffffffff;
						try
						{
							size = fs.Length;
						}
						finally
						{
							fs.Close();
						}

						if (size == srcData.Length || srcData.Length == 0)
						{
							destData = File.ReadAllBytes(tmp2);
						}
					}
					catch
					{
					}

					if (Util.CompareByte(srcData, destData))
					{
						changed = false;
					}
				}

				if (changed)
				{
					File.WriteAllBytes(tmp2, srcData);
					CopyFileTimestamp(tmp2, tmp1);
				}
			}
			else
			{
				File.Copy(tmp1, tmp2, true);
			}
		}

		// ファイルの日付をコピーする
		public static void CopyFileTimestamp(string dstFileName, string srcFileName)
		{
			DateTime dt1 = File.GetCreationTimeUtc(srcFileName);
			DateTime dt2 = File.GetLastAccessTimeUtc(srcFileName);
			DateTime dt3 = File.GetLastWriteTimeUtc(srcFileName);

			File.SetCreationTimeUtc(dstFileName, dt1);
			File.SetLastAccessTimeUtc(dstFileName, dt2);
			File.SetLastWriteTimeUtc(dstFileName, dt3);
		}

		// ファイルの日付を上書きする
		public static void SetFileTimestamp(string dstFileName, FileInfo fi)
		{
			File.SetCreationTimeUtc(dstFileName, fi.CreationTimeUtc);
			File.SetLastAccessTimeUtc(dstFileName, fi.LastAccessTimeUtc);
			File.SetLastWriteTimeUtc(dstFileName, fi.LastWriteTimeUtc);
		}

		// ファイルを読み込む
		static public byte[] ReadFile(string name)
		{
			IO io = FileOpen(name);
			try
			{
				int size = io.FileSize;
				byte[] ret = io.Read(size);
				return ret;
			}
			finally
			{
				io.Close();
			}
		}

		// ファイルを保存する
		static public void SaveFile(string name, byte[] data)
		{
			SaveFile(name, data, 0, data.Length);
		}
		static public void SaveFile(string name, byte[] data, int offset, int size)
		{
			IO io = FileCreate(name);
			try
			{
				io.Write(data, offset, size);
			}
			finally
			{
				io.Close();
			}
		}

		// 安全なファイル名を生成する
		static public string MakeSafeFileName(string src)
		{
			return src
				.Replace("..", "__")
				.Replace("/", "_")
				.Replace("\\", "_")
				.Replace("@", "_")
				.Replace("|", "_");
		}

		// ディレクトリが存在するかどうか確認する
		public static bool IsDirExists(string name)
		{
			string tmp = InnerFilePath(name);

			return Directory.Exists(tmp);
		}

		// ファイルが存在するかどうか確認する
		public static bool IsFileExists(string name)
		{
			string tmp = InnerFilePath(name);

			return File.Exists(tmp);
		}

		// ファイルを削除する
		static void fileDeleteInner(string name)
		{
			string name2 = ConvertPath(name);

			File.Delete(name2);
		}
		public static void FileDelete(string name)
		{
			string tmp = InnerFilePath(name);

			fileDeleteInner(tmp);
		}

		// シークする
		public bool Seek(SeekOrigin mode, int offset)
		{
			lock (lockObj)
			{
				if (p != null)
				{
					try
					{
						p.Seek(offset, mode);

						return true;
					}
					catch
					{
						return false;
					}
				}
				else
				{
					return false;
				}
			}
		}

		// ファイルサイズの取得
		public long FileSize64
		{
			get
			{
				lock (lockObj)
				{
					if (p != null)
					{
						return p.Length;
					}
					else
					{
						if (hamMode)
						{
							return (long)hamBuf.Size;
						}
					}

					return 0;
				}
			}
		}
		public int FileSize
		{
			get
			{
				long size64 = this.FileSize64;

				if (size64 >= 2147483647)
				{
					size64 = 2147483647;
				}

				return (int)size64;
			}
		}
		public static int GetFileSize(string name)
		{
			IO io = IO.FileOpen(name, false);
			try
			{
				return io.FileSize;
			}
			finally
			{
				io.Close();
			}
		}

		// ファイルからすべて読み込む
		public byte[] ReadAll()
		{
			this.Seek(SeekOrigin.Begin, 0);
			int size = this.FileSize;

			byte[] data = new byte[size];
			this.Read(data, 0, size);

			this.Seek(SeekOrigin.Begin, 0);

			return data;
		}

		// ファイルから読み込む
		public byte[] Read(int size)
		{
			byte[] buf = new byte[size];
			bool ret = Read(buf, size);
			if (ret == false)
			{
				return null;
			}
			return buf;
		}
		public bool Read(byte[] buf, int size)
		{
			return Read(buf, 0, size);
		}
		public bool Read(byte[] buf, int offset, int size)
		{
			if (size == 0)
			{
				return true;
			}

			lock (lockObj)
			{
				if (this.HamMode)
				{
					byte[] ret = hamBuf.Read((uint)size);

					if (ret.Length != size)
					{
						return false;
					}

					Util.CopyByte(buf, offset, ret, 0, size);

					return true;
				}

				if (p != null)
				{
					try
					{
						int ret = p.Read(buf, offset, size);
						if (ret == size)
						{
							return true;
						}
						else
						{
							return false;
						}
					}
					catch
					{
						return false;
					}
				}
				else
				{
					return false;
				}
			}
		}

		// ファイルに書き込む
		public bool Write(byte[] buf)
		{
			return Write(buf, 0, buf.Length);
		}
		public bool Write(byte[] buf, int size)
		{
			return Write(buf, 0, size);
		}
		public bool Write(byte[] buf, int offset, int size)
		{
			if (writeMode == false)
			{
				return false;
			}
			if (size == 0)
			{
				return true;
			}

			lock (lockObj)
			{
				if (p != null)
				{
					try
					{
						p.Write(buf, offset, size);

						return true;
					}
					catch
					{
						return false;
					}
				}
				else
				{
					return false;
				}
			}
		}

		// 閉じて削除する
		public bool CloseAndDelete()
		{
			string name = this.Name;

			Close();

			try
			{
				FileDelete(name);
				return true;
			}
			catch
			{
				return false;
			}
		}

		// 閉じる
		public void Close()
		{
			Close(false);
		}
		public void Close(bool noFlush)
		{
			lock (this.lockObj)
			{
				if (this.hamMode == false)
				{
					if (this.p != null)
					{
						if (this.writeMode && noFlush == false)
						{
							Flush();
						}

						this.p.Close();
					}

					this.p = null;
				}
			}
		}

		// フラッシュする
		public void Flush()
		{
			try
			{
				lock (this.lockObj)
				{
					if (this.p != null)
					{
						this.p.Flush();
					}
				}
			}
			catch
			{
			}
		}

		static IO fileCreateInner(string name)
		{
			IO o = new IO();

			string name2 = ConvertPath(name);

			lock (o.lockObj)
			{
				o.p = File.Open(name2, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
				o.name = name2;
				o.writeMode = true;
			}

			return o;
		}

		// ファイルを作成する
		public static IO FileCreate(string name)
		{
			name = InnerFilePath(name);

			return fileCreateInner(name);
		}

		static IO fileOpenInner(string name, bool writeMode, bool readLock)
		{
			IO o = new IO();

			string name2 = ConvertPath(name);

			lock (o.lockObj)
			{
				o.p = File.Open(name2, FileMode.Open, (writeMode ? FileAccess.ReadWrite : FileAccess.Read),
					(readLock ? FileShare.None : FileShare.Read));

				o.name = name2;
				o.writeMode = writeMode;
			}

			return o;
		}

		// ファイルを開く
		public static IO FileOpen(string name)
		{
			return FileOpen(name, false);
		}
		public static IO FileOpen(string name, bool writeMode)
		{
			return FileOpen(name, writeMode, false);
		}
		public static IO FileOpen(string name, bool writeMode, bool readLock)
		{
			name = InnerFilePath(name);

			if (name[0] == '|')
			{
				HamCore hc = IO.HamCore;

				Buf b = hc.ReadHamcore(name);
				if (b == null)
				{
					throw new FileNotFoundException();
				}

				IO o = new IO();
				o.name = name.Substring(1);
				o.hamMode = true;
				o.hamBuf = b;

				return o;
			}
			else
			{
				return fileOpenInner(name, writeMode, readLock);
			}
		}

		// ファイルを開くか作成する
		public static IO FileCreateOrAppendOpen(string name)
		{
			if (IsFileExists(name))
			{
				IO io = FileOpen(name, true);
				io.Seek(SeekOrigin.End, 0);
				return io;
			}
			else
			{
				return FileCreate(name);
			}
		}

		// 相対的ファイル名を計算する
		public static string GetRelativeFileName(string fileName, string baseDirName)
		{
			baseDirName = RemoteLastEnMark(baseDirName).Trim() + "\\";
			fileName = fileName.Trim();

			if (fileName.Length <= baseDirName.Length)
			{
				throw new ArgumentException("fileName, baseDirName");
			}

			if (fileName.StartsWith(baseDirName, StringComparison.InvariantCultureIgnoreCase) == false)
			{
				throw new ArgumentException("fileName, baseDirName");
			}

			return fileName.Substring(baseDirName.Length);
		}

		// パスの正規化
		public static string RemoteLastEnMark(string path)
		{
			if (path == null)
			{
				path = "";
			}
			if (path.EndsWith("\\"))
			{
				path = path.Substring(0, path.Length - 1);
			}
			return path;
		}

		// ファイル名の変更
		public static void FileRename(string oldName, string newName)
		{
			string tmp1 = InnerFilePath(oldName);
			string tmp2 = InnerFilePath(newName);

			File.Move(tmp1, tmp2);
		}

		// ディレクトリ内のすべてのファイルの削除
		public static void DeleteFilesAndSubDirsInDir(string dirName)
		{
			DeleteFilesAndSubDirsInDir(dirName, false);
		}
		public static void DeleteFilesAndSubDirsInDir(string dirName, bool ignore_error)
		{
			dirName = InnerFilePath(dirName);

			if (Directory.Exists(dirName) == false)
			{
				Directory.CreateDirectory(dirName);
				return;
			}

			string[] files = Directory.GetFiles(dirName);
			string[] dirs = Directory.GetDirectories(dirName);

			foreach (string file in files)
			{
				if (ignore_error == false)
				{
					File.SetAttributes(file, FileAttributes.Normal);
					File.Delete(file);
				}
				else
				{
					try
					{
						File.SetAttributes(file, FileAttributes.Normal);
					}
					catch
					{
					}

					try
					{
						File.Delete(file);
					}
					catch
					{
					}
				}
			}

			foreach (string dir in dirs)
			{
				if (ignore_error == false)
				{
					Directory.Delete(dir, true);
				}
				else
				{
					try
					{
						Directory.Delete(dir, true);
					}
					catch
					{
					}
				}
			}
		}

		// ディレクトリの削除
		public static bool DeleteDir(string dirName)
		{
			return DeleteDir(dirName, false);
		}
		public static bool DeleteDir(string dirName, bool deleteSubDirs)
		{
			try
			{
				Directory.Delete(InnerFilePath(dirName), deleteSubDirs);
				return true;
			}
			catch
			{
				return false;
			}
		}

		// ディレクトリの作成
		public static bool MakeDir(string dirName)
		{
			try
			{
				Directory.CreateDirectory(InnerFilePath(dirName));
				return true;
			}
			catch
			{
				return false;
			}
		}
		public static bool MakeDirIfNotExists(string dirName)
		{
			string path = InnerFilePath(dirName);

			if (Directory.Exists(path) == false)
			{
				Directory.CreateDirectory(path);

				return true;
			}

			return false;
		}

		// ファイルパスを正規化する
		public static string NormalizePath(string src)
		{
			bool first_double_slash = false;
			bool first_single_slash = false;
			string win32_drive_char = "";
			int i;
			string tmp;

			// パスを変換する (Win32, UNIX 変換)
			tmp = ConvertPath(src).Trim();

			// 先頭が "./" や "../" で始まっている場合はカレントディレクトリに置換する
			if (tmp.StartsWith(".\\") || tmp.StartsWith("..\\") || tmp.StartsWith(".") || tmp.StartsWith(".."))
			{
				if (tmp.StartsWith(".."))
				{
					tmp = Env.CurrentDir + "/../" + tmp.Substring(2);
				}
				else
				{
					tmp = Env.CurrentDir + "/" + tmp;
				}
			}

			// 先頭が "~/" で始まっている場合はホームディレクトリに置換する
			if (tmp.StartsWith("~/") || tmp.StartsWith("~\\"))
			{
				tmp = Env.HomeDir + "/" + tmp.Substring(2);
			}

			if (tmp.StartsWith("//") || tmp.StartsWith("\\\\"))
			{
				// 最初が "//" または "\\" で始まる
				first_double_slash = true;
			}
			else
			{
				if (tmp.StartsWith("/") || tmp.StartsWith("\\"))
				{
					first_single_slash = true;
				}
			}

			if (tmp.Length >= 2)
			{
				if (tmp[1] == ':')
				{
					// Win32 のドライブ文字列表記
					win32_drive_char = "" + tmp[0];
					tmp = tmp.Substring(2);
				}
			}

			if (tmp == "/" || tmp == "\\")
			{
				tmp = "";
			}

			// トークン分割
			char[] splitChars = { '/', '\\' };
			string[] t = tmp.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);

			Stack<string> sk = new Stack<string>();

			for (i = 0; i < t.Length; i++)
			{
				string s = t[i];

				if (Str.StrCmpi(s, "."))
				{
					continue;
				}
				else if (Str.StrCmpi(s, ".."))
				{
					if (sk.Count >= 1 && (first_double_slash == false || sk.Count >= 2))
					{
						sk.Pop();
					}
				}
				else
				{
					sk.Push(s);
				}
			}

			// トークン結合
			tmp = "";

			if (first_double_slash)
			{
				tmp += "//";
			}
			else if (first_single_slash)
			{
				tmp += "/";
			}

			if (Str.IsEmptyStr(win32_drive_char) == false)
			{
				tmp = win32_drive_char + ":/" + tmp;
			}

			string[] sks = sk.ToArray();
			Array.Reverse(sks);
			for (i = 0; i < sks.Length; i++)
			{
				tmp += sks[i];
				if (i != (sks.Length - 1))
				{
					tmp += "/";
				}
			}

			tmp = ConvertPath(tmp);

			return tmp;
		}

		// パスの変換
		public static string ConvertPath(string path)
		{
			return path.Replace('/', '\\');
		}

		// パスの結合
		public static string ConbinePath(string dirname, string filename)
		{
			return CombinePath(dirname, filename);
		}
		public static string CombinePath(string dirname, string filename)
		{
			bool is_full_path;
			string filename_ident = NormalizePath(filename);

			is_full_path = false;

			if (filename_ident.StartsWith("\\") || filename_ident.StartsWith("/"))
			{
				is_full_path = true;
			}

			filename = filename_ident;

			if (filename.Length >= 2)
			{
				char c = filename[0];
				if (('a' <= c && c <= 'z') || ('A' <= c && c <= 'Z'))
				{
					if (filename[1] == ':')
					{
						is_full_path = true;
					}
				}
			}

			string tmp;

			if (is_full_path == false)
			{
				tmp = dirname;
				if (tmp.EndsWith("/") == false && tmp.EndsWith("\\") == false)
				{
					tmp += "/";
				}

				tmp += filename;
			}
			else
			{
				tmp = filename;
			}

			return NormalizePath(tmp);
		}

		// 内部ファイルパスの生成
		public static string InnerFilePath(string src)
		{
			if (src[0] != '@')
			{
				return NormalizePath(src);
			}
			else
			{
				return CombinePath(Env.ExeFileDir, src.Substring(1));
			}
		}

		// 作成日時を取得する
		public static DateTime GetCreationTimeUtc(string filename)
		{
			return File.GetCreationTimeUtc(InnerFilePath(filename));
		}
		public static DateTime GetCreationTimeLocal(string filename)
		{
			return File.GetCreationTime(InnerFilePath(filename));
		}

		// 最終更新日時を取得する
		public static DateTime GetLastWriteTimeUtc(string filename)
		{
			return File.GetLastWriteTimeUtc(InnerFilePath(filename));
		}
		public static DateTime GetLastWriteTimeLocal(string filename)
		{
			return File.GetLastWriteTime(InnerFilePath(filename));
		}

		// 最終アクセス日時を取得する
		public static DateTime GetLastAccessTimeUtc(string filename)
		{
			return File.GetLastAccessTimeUtc(InnerFilePath(filename));
		}
		public static DateTime GetLastAccessTimeLocal(string filename)
		{
			return File.GetLastAccessTime(InnerFilePath(filename));
		}

		// ファイル内容を読みだす
		public static byte[] ReadFileData(string filename)
		{
			FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			try
			{
				long size = fs.Length;
				int size2 = (int)Math.Min(size, int.MaxValue);
				byte[] ret = new byte[size2];
				fs.Read(ret, 0, size2);
				return ret;
			}
			finally
			{
				fs.Close();
			}
		}
	}
}
