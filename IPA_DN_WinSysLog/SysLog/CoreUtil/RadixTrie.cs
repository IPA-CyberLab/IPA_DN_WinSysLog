﻿using System;
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
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Xml.Serialization;
using System.Web.UI.WebControls;
using System.Security.AccessControl;
using System.Security.Principal;
using CoreUtil;
using System.Net.Mail;

namespace CoreUtil
{
	/// <summary>
	/// represents a node in the radix tree
	/// stores:
	/// a label - string that the tree holds
	/// a list of the node's subnodes - a list of other objects of this type
	/// </summary>
	public class RadixNode
	{
		public RadixNode(RadixNode parent)
		{
			Label = new byte[0];
			SubNodes = new List<RadixNode>();

			this.Parent = parent;
		}

		public RadixNode(RadixNode parent, byte[] l)
		{
			Label = l;
			SubNodes = new List<RadixNode>();

			this.Parent = parent;
		}
		public byte[] Label;
		public List<RadixNode> SubNodes;
		public RadixNode Parent;
		public object Object;

		public RadixNode TraverseParentNonNull()
		{
			if (this.Object != null)
			{
				return this;
			}

			RadixNode n = this.Parent;

			while (true)
			{
				if (n == null)
				{
					return null;
				}

				if (n.Object != null)
				{
					return n;
				}

				n = n.Parent;
			}
		}
	}

	public class RadixTrie
	{
		/// <summary>
		/// store the tree's root
		/// </summary>
		public RadixNode Root;

		public int Count = 0;

		/// <summary>
		/// construct a new tree with it's root
		/// </summary>
		public RadixTrie()
		{
			Root = new RadixNode(null, new byte[0]);
		}

		public RadixTrie(RadixNode root)
		{
			Root = root;
		}

		/// <summary>
		/// insert a word into the tree
		/// </summary>
		/// <param name="word"></param>
		public RadixNode Insert(byte[] word)
		{
			return InsertRec(word, Root);
		}

		/// <summary>
		/// recursively traverse the tree
		/// carry the word you want inserted until a proper place for it is found and it can be inserted there
		/// if a node already stores a substring of the word(substrnig with the same first letter as the word itself)
		/// then that substring must be removed from the word and it's children checked out next
		/// hence the name wordPart - part of a word
		/// </summary>
		/// <param name="wordPart">the part of the word that is to be inserted that is not already included in any of the tree's nodes</param>
		/// <param name="curNode">the node currently traversed</param>
		private RadixNode InsertRec(byte[] wordPart, RadixNode curNode)
		{
			RadixNode ret = null;

			//get the number of characters that the word part that is to be inserted and the current node's label have
			//in common starting from the first position of both strings
			//matching characters in the two strings = have the same value at the same position in both strings
			int matches = MatchingConsecutiveCharacters(wordPart, 0, curNode);

			//if we are at the root node
			//OR
			//the number of characters from the two strings that match is
			//bigger than 0
			//smaller than the the part of the word that is to be inserted
			//bigger than the the label of the current node
			if ((matches == 0) || (curNode == Root) ||
				((matches > 0) && (matches < wordPart.Length) && (matches >= curNode.Label.Length)))
			{
				//remove the current node's label from the word part
				bool inserted = false;
				byte[] newWordPart = Util.CopyByte(wordPart, matches);
				//search the node's subnodes and if the subnode label's first character matches 
				//the word part's first character then insert the word part after this node(call the
				//current method recursively)
				foreach (RadixNode child in curNode.SubNodes)
					if (child.Label[0] == newWordPart[0])
					{
						inserted = true;
						ret = InsertRec(newWordPart, child);
					}
				if (inserted == false)
				{
					ret = new RadixNode(curNode, newWordPart);
					curNode.SubNodes.Add(ret);
					this.Count++;
				}
			}
			else if (matches < wordPart.Length)
			{
				//in this case we have to nodes that we must add to the tree
				//one is the node that has a label extracted from the current node's label without the string of 
				//matching characters(common characters)
				//the other is the node that has it's label extracted from the current word part minus the string
				//of matching characters
				byte[] commonRoot = Util.CopyByte(wordPart, 0, matches);
				byte[] branchPreviousLabel = Util.CopyByte(curNode.Label, matches);
				byte[] branchNewLabel = Util.CopyByte(wordPart, matches);

				curNode.Label = commonRoot;

				RadixNode newNodePreviousLabel = new RadixNode(curNode, branchPreviousLabel);
				newNodePreviousLabel.Object = curNode.Object;
				newNodePreviousLabel.SubNodes.AddRange(curNode.SubNodes);

				foreach (RadixNode n in curNode.SubNodes)
				{
					n.Parent = newNodePreviousLabel;
				}

				curNode.SubNodes.Clear();
				curNode.SubNodes.Add(newNodePreviousLabel);
				curNode.Object = null;

				RadixNode newNodeNewLabel = new RadixNode(curNode, branchNewLabel);
				curNode.SubNodes.Add(newNodeNewLabel);
				ret = newNodeNewLabel;
				this.Count++;
			}
			else if (matches == curNode.Label.Length)
			{
				//in this case we don't do anything because the word is already added
			}
			else if (matches > curNode.Label.Length)
			{
				//add the current word part minus the common characters after the current node
				byte[] newNodeLabel = Util.CopyByte(curNode.Label, curNode.Label.Length, wordPart.Length);
				RadixNode newNode = new RadixNode(curNode, newNodeLabel);
				curNode.SubNodes.Add(newNode);
				ret = newNode;
				this.Count++;
			}

			return ret;
		}

		/// <summary>
		/// given a string and a node the number of characters that the string and the node's label have
		/// in common starting from the first character in each is returned
		/// </summary>
		/// <param name="word">a string that is to be compared with the node's label</param>
		/// <param name="curNode">a node</param>
		/// <returns></returns>
		private int MatchingConsecutiveCharacters(byte[] word, int word_pos, RadixNode curNode)
		{
			int matches = 0;
			int minLength = 0;

			//see which string is smaller and save it's lenght
			//when cycling throught the two strings we won't go any further than that
			if (curNode.Label.Length >= (word.Length - word_pos))
				minLength = word.Length - word_pos;
			else if (curNode.Label.Length < (word.Length - word_pos))
				minLength = curNode.Label.Length;

			if (minLength > 0)
				//go throught the two streams
				for (int i = 0; i < minLength; i++)
				{
					//if two characters at the same position have the same value we have one more match
					if (word[i + word_pos] == curNode.Label[i])
						matches++;
					else
						//if at any position the two strings have different characters break the cycle
						break;
				}
			//and return the current number of matches
			return matches;
		}

		public RadixNode Lookup(byte[] word)
		{
			return LookupRec(word, 0, Root);
		}

		/// <summary>
		/// look for a word in the tree begining at the current node 
		/// </summary>
		/// <param name="wordPart"></param>
		/// <param name="curNode"></param>
		/// <returns></returns>
		private RadixNode LookupRec(byte[] word, int pos, RadixNode curNode)
		{
			int matches = MatchingConsecutiveCharacters(word, pos, curNode);

			if ((matches == 0) || (curNode == Root) ||
				((matches > 0) && (matches < (word.Length - pos)) && (matches >= curNode.Label.Length)))
			{
				RadixNode ret = null;
				int new_pos = pos + matches;
				foreach (RadixNode child in curNode.SubNodes)
				{
					if (child.Label[0] == word[new_pos])
					{
						ret = LookupRec(word, new_pos, child);
						if (ret != null)
						{
							break;
						}
					}
				}

				if (ret == null)
				{
					ret = curNode;
				}

				return ret;
			}
			else if (matches == curNode.Label.Length)
			{
				return curNode;
			}
			else return null;
		}

		void enum_objects(ArrayList o, RadixNode n)
		{
			if (n.Object != null)
			{
				o.Add(n.Object);
			}

			foreach (RadixNode nc in n.SubNodes)
			{
				enum_objects(o, nc);
			}
		}

		public ArrayList EnumAllObjects()
		{
			ArrayList ret = new ArrayList();

			enum_objects(ret, this.Root);

			return ret;
		}
	}


}
