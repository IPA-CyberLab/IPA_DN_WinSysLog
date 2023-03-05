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

namespace CoreUtil
{
	public static class CCNumber
	{
		public static bool CheckCCNumber(string number)
		{
			string digitsOnly = NormalizeCardNumber(number);
			int sum = 0;
			int digit = 0;
			int addend = 0;
			bool timesTwo = false;

			for (int i = digitsOnly.Length - 1; i >= 0; i--)
			{
				digit = int.Parse(digitsOnly.Substring(i, 1));
				if (timesTwo)
				{
					addend = digit * 2;
					if (addend > 9)
					{
						addend -= 9;
					}
				}
				else
				{
					addend = digit;
				}

				sum += addend;
				timesTwo = !timesTwo;
			}

			int modules = sum % 10;
			return (modules == 0);
		}

		public static string NormalizeCardNumber(string cardNumber)
		{
			if (cardNumber == null)
			{
				cardNumber = "";
			}

			cardNumber = cardNumber.Trim();

			StringBuilder b = new StringBuilder();

			foreach (char c in cardNumber)
			{
				if (c >= '0' && c <= '9')
				{
					b.Append(c);
				}
				else if (c == '-' || c == '－')
				{
				}
				else
				{
					switch (c)
					{
						case '０':
							b.Append('0');
							break;
						case '１':
							b.Append('1');
							break;
						case '２':
							b.Append('2');
							break;
						case '３':
							b.Append('3');
							break;
						case '４':
							b.Append('4');
							break;
						case '５':
							b.Append('5');
							break;
						case '６':
							b.Append('6');
							break;
						case '７':
							b.Append('7');
							break;
						case '８':
							b.Append('8');
							break;
						case '９':
							b.Append('9');
							break;
						default:
							break;
					}
				}
			}

			return b.ToString();
		}

	}
}

