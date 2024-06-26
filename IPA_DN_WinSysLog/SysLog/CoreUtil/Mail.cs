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
using System.Net.Mail;
using System.Net.Mime;

#pragma warning disable 0618

namespace CoreUtil
{
	public enum SendMailVersion
	{
		Ver1_With_WebMail,
		Ver2_With_NetMail,
	}

	public class SendMail
	{
		string smtpServer;
		public string SmtpServer
		{
			get { return smtpServer; }
			set { smtpServer = value; }
		}

		SendMailVersion version;
		public SendMailVersion Version
		{
			get { return version; }
			set { version = value; }
		}

		public string Username = null;
		public string Password = null;

		public int SmtpPort = 25;

		public SendMailVersion DefaultVersion = SendMailVersion.Ver2_With_NetMail;

		public const string DefaultMailer = "Microsoft Outlook Express 6.00.2900.2869";
		public const string DefaultMimeOLE = "Produced By Microsoft MimeOLE V6.00.2900.2962";
		public const string DefaultPriority = "3";
		public const string DefaultMSMailPriority = "Normal";
		public const string DefaultTransferEncoding = "7bit";
		private static Encoding defaultEncoding = Str.ISO2022JPEncoding;
		public static Encoding DefaultEncoding
		{
			get { return SendMail.defaultEncoding; }
		}

		string header_mailer = DefaultMailer;
		public string Mailer
		{
			get { return header_mailer; }
			set { header_mailer = value; }
		}
		string header_mimeole = DefaultMimeOLE;
		public string MimeOLE
		{
			get { return header_mimeole; }
			set { header_mimeole = value; }
		}
		string header_priority = DefaultPriority;
		public string Priority
		{
			get { return header_priority; }
			set { header_priority = value; }
		}
		string header_msmail_priority = DefaultMSMailPriority;
		public string MsMailPriority
		{
			get { return header_msmail_priority; }
			set { header_msmail_priority = value; }
		}
		string header_transfer_encoding = DefaultTransferEncoding;
		public string TransferEncoding
		{
			get { return header_transfer_encoding; }
			set { header_transfer_encoding = value; }
		}
		Encoding encoding = DefaultEncoding;
		public Encoding Encoding
		{
			get { return encoding; }
			set { encoding = value; }
		}

		public SendMail(string smtpServer)
		{
			init(smtpServer, DefaultVersion, null, null);
		}

		public SendMail(string smtpServer, SendMailVersion version)
		{
			init(smtpServer, version, null, null);
		}

		public SendMail(string smtpServer, SendMailVersion version, string username, string password)
		{
			init(smtpServer, version, username, password);
		}

		void init(string smtpServer, SendMailVersion version, string username, string password)
		{
			this.smtpServer = smtpServer;
			this.version = version;
			this.Username = username;
			this.Password = password;
		}

		public bool Send(string from, string to, string subject, string body)
		{
			return Send(new MailAddress(from), new MailAddress(to), subject, body);
		}

		public bool Send(MailAddress from, MailAddress to, string subject, string body)
		{
			try
			{
				switch (this.version)
				{
					case SendMailVersion.Ver1_With_WebMail:
						send1(from, to, subject, body);
						break;

					case SendMailVersion.Ver2_With_NetMail:
						send2(from, to, subject, body);
						break;
				}
			}
			catch
			{
				return false;
			}

			return true;
		}

		void send1(MailAddress from, MailAddress to, string subject, string body)
		{
			System.Web.Mail.MailMessage mail = new System.Web.Mail.MailMessage();

			mail.From = from.ToString();
			mail.To = to.ToString();
			mail.Subject = subject;
			mail.Body = body;
			mail.Headers.Add("Content-Transfer-Encoding", this.header_transfer_encoding);
			mail.Headers.Add("X-Mailer", this.header_mailer);
			mail.Headers.Add("X-MSMail-Priority", this.header_msmail_priority);
			mail.Headers.Add("X-Priority", this.header_priority);
			mail.Headers.Add("X-MimeOLE", this.header_mimeole);

			SmtpMail.SmtpServer = smtpServer;
			SmtpMail.Send(mail);
		}

		void send2(MailAddress from, MailAddress to, string subject, string body)
		{
			Encoding encoding = this.encoding;
			TransferEncoding tranEnc = System.Net.Mime.TransferEncoding.SevenBit;

			if (Str.IsSuitableEncodingForString(subject, Str.AsciiEncoding) &&
				Str.IsSuitableEncodingForString(body, Str.AsciiEncoding))
			{
				encoding = Str.AsciiEncoding;
				tranEnc = System.Net.Mime.TransferEncoding.SevenBit;
			}
			else
			{
				if (!Str.IsSuitableEncodingForString(subject, encoding) || !Str.IsSuitableEncodingForString(body, encoding))
				{
					encoding = Str.Utf8Encoding;
					tranEnc = System.Net.Mime.TransferEncoding.Base64;
				}
			}

			SmtpClient c = new SmtpClient(this.smtpServer);
			c.DeliveryMethod = SmtpDeliveryMethod.Network;
			c.EnableSsl = false;
			c.Port = this.SmtpPort;

			if (Str.IsEmptyStr(this.Username) == false && Str.IsEmptyStr(this.Password) == false)
			{
				c.UseDefaultCredentials = false;
				c.Credentials = new System.Net.NetworkCredential(this.Username, this.Password);
			}

			System.Net.Mail.MailMessage mail = new System.Net.Mail.MailMessage(from, to);

			byte[] buffer = encoding.GetBytes(body);

			MemoryStream mem = new MemoryStream(buffer);

			AlternateView alt = new AlternateView(mem, new System.Net.Mime.ContentType("text/plain; charset=" + encoding.WebName));

			alt.TransferEncoding = tranEnc;

			mail.AlternateViews.Add(alt);
			mail.Body = "";

			byte[] sub = encoding.GetBytes(subject);
			string subjectText = string.Format("=?{0}?B?{1}?=", encoding.WebName.ToUpper(),
				Convert.ToBase64String(sub, Base64FormattingOptions.None));

			mail.Subject = subjectText;

			mail.Headers.Add("X-Mailer", this.header_mailer);
			mail.Headers.Add("X-MSMail-Priority", this.header_msmail_priority);
			mail.Headers.Add("X-Priority", this.header_priority);
			mail.Headers.Add("X-MimeOLE", this.header_mimeole);

			c.Send(mail);
		}

		public static MailAddress NewMailAddress(string address, string displayName)
		{
			return NewMailAddress(address, displayName, SendMail.DefaultEncoding);
		}

		public static MailAddress NewMailAddress(string address, string displayName, Encoding encoding)
		{
			MailAddress a = new MailAddress(address, displayName, encoding);

			return a;
		}
	}
}

