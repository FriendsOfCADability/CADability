using System;
using System.Collections;
using System.IO;

namespace CdlToCSharp
{
	class SyntaxError: System.ApplicationException
	{
		public Input input;
		public SyntaxError(Input input) 
		{
			this.input = input;
		}
	}

	/// <summary>
	/// Tokenizer for CDL files (the class definition language of Open CASCADE), a leftover of the
	/// CdlToCSharp tool that converted CDL class definitions to C#; not used by CADability itself. It reads
	/// a file into a list of tokens (identifiers, numbers, string literals, special characters and "--"
	/// comments) and offers a cursor (<see cref="Index"/>) with helpers to consume tokens, to recognize
	/// keywords and to collect the "---Purpose" comments in <see cref="Comment"/> so that they can be
	/// turned into C# documentation comments.
	/// </summary>
	public class Input
	{
		public string [] Token;
		public int Index;
		private string AlphaNum;
		private string Alpha;
		private string Num;
		private string SpecialChar;
		public string Comment;
		private void Parse(string line,ArrayList tokens)
		{
// System.Diagnostics.Debug.WriteLine("--> "+line);
			// line = line.Replace("\\\"","\"");
			// 1. Vortest aud Kommentare
			int ind = line.IndexOf("--");
			if (ind>=0)
			{
				Parse(line.Substring(0,ind),tokens);
				tokens.Add(line.Substring(ind));
				return;
			}
			// 2. "strings" herauslösen
			ind = line.IndexOf('"');
			if (ind>=0)
			{
				int ind1 = line.IndexOf('"',ind+1);
				if (ind1>0)
				{
					Parse(line.Substring(0,ind),tokens);
					tokens.Add(line.Substring(ind,ind1+1-ind));
					Parse(line.Substring(ind1+1),tokens);
					return;
				}
			}
			ind = line.IndexOf('\'');
			if (ind>=0)
			{
				int ind1 = line.IndexOf('\'',ind+1);
				if (ind1>0)
				{
					Parse(line.Substring(0,ind),tokens);
					tokens.Add(line.Substring(ind,ind1+1-ind));
					Parse(line.Substring(ind1+1),tokens);
					return;
				}
			}
			// 3. Normalfall: keine Kommentare, keine "strings"
			string [] parts = line.Split(null);
			for (int i=0; i<parts.Length; ++i)
			{
				string ToTest = parts[i];
				while (ToTest.Length>0)
				{
					if (ToTest.IndexOfAny(AlphaNum.ToCharArray())==0)
					{
						int ind1 = ToTest.IndexOfAny(SpecialChar.ToCharArray());
						if (ind1<0)
						{
							tokens.Add(ToTest);
							ToTest = "";
						} 
						else
						{
							string ToAdd = ToTest.Substring(0,ind1);
							tokens.Add(ToAdd);
							ToTest = ToTest.Substring(ind1);
						}
					} 
					else
					{	// ein Sonderzeichen
						string ToAdd = ToTest.Substring(0,1);
						tokens.Add(ToAdd);
						ToTest = ToTest.Substring(1);
					}
				}
			}
		}
		
		public Input(string filename)
		{
			System.IO.TextReader tr = new StreamReader(filename);
			AlphaNum = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890_+-.";
			Alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
			Num = "1234567890+-";
			SpecialChar = "!”#$%&’()*,/:;<=>?@[\\\"]^‘{|}~"; // + und - und . gehören nicht dazu, damit Zahlen ganz bleiben
			string line = tr.ReadLine();
			ArrayList tokens = new ArrayList();
			while (line!=null)
			{
				Parse(line,tokens);
				line = tr.ReadLine();
			}
			Token = (string[])tokens.ToArray(typeof(string));
			for (int i=0; i<Token.Length; ++i)
			{
				// System.Diagnostics.Debug.WriteLine(Token[i]);
			}
			Index = 0;
			Comment = "";
		}
		public bool IsIdentifier
		{
			get
			{
				return Token[Index].IndexOfAny(Alpha.ToCharArray())==0 && !IsKeyWord;
			}
		}
		public bool IsNumeric
		{
			get
			{
				return Token[Index].IndexOfAny(Num.ToCharArray())==0;
			}
		}
		public bool IsLiteral
		{
			get
			{
				return Token[Index].IndexOfAny(new char [] {'"','\''})==0;
			}
		}
		public bool IsKeyWord
		{
			get
			{
				switch (Token[Index])
				{
					case "alias":
					case "any":
					case "as":
					case "asynchronous":
					case "class":
					case "client":
					case "deferred": 
					case "end":
					case "enumeration":
					case "exception":
					case "executable":
					case "external":
					case "fields":
					case "friends":
					case "from":
					case "generic":
					case "immutable": 
					case "imported":
					case "inherits":
					case "instantiates":
					case "is":
					case "in":
					case "library":
					case "like":
					case "me":
					case "mutable":
					case "myclass":
					case "out":
					case "package":
					case "pointer":
					case "primitive":
					case "private":
					case "protected":
					case "raises":
					case "redefined":
					case "returns":
					case "schema":
					case "static":
					case "to":
					case "uses": 
					case "virtual":
						return true;
					default: return false;
				}
			}
		}

		public string NextIdentifier()
		{
			if (!IsIdentifier) throw new SyntaxError(this);
			return Next();
		}
		public string Next()
		{
			string res = Token[Index];
			++Index;
			EatComment();
			return res;
		}
		public void Assert(string ToCheck)
		{
			if (Token[Index]!=ToCheck) throw new SyntaxError(this);
			++Index;
			EatComment();
		}
		public bool Eat(string ToCheck)
		{
			if (Token[Index]==ToCheck)
			{
				++Index;
				EatComment();
				return true;
			}
			return false;
		}
		public void EatComment()
		{
			while (Index<Token.Length && Token[Index].StartsWith("--")) 
			{
                if (Token[Index].StartsWith("---Purpose"))
                {   // hier fängt ein neuer Kommentar an
                    int subindex;
                    if (Token[Index].StartsWith("---Purpose: ")) subindex = 12;
                    else if (Token[Index].StartsWith("---Purpose:")) subindex = 11;
                    else if (Token[Index].StartsWith("---Purpose :")) subindex = 12;
                    else subindex = 10;
                    Comment = "\n///" + Token[Index].Substring(subindex);
                }
                else
                {
                    if (!Token[Index].StartsWith("---C++: "))
                    {
                        Comment = Comment + "\n///" + Token[Index].Substring(2);
                    }
                }
				++Index;
			}
		}
        public string GetComment()
        {
            string res = Comment;
            Comment = "";
            return res;
        }
	}
}
