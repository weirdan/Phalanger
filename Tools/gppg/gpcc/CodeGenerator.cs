using System;
using System.Collections.Generic;
using System.IO;
namespace gpcc
{
	public class CodeGenerator
	{
		public Grammar grammar;
		private TextWriter output;
		public TextWriter Output
		{
			get
			{
				return this.output;
			}
			set
			{
				this.output = value;
			}
		}
		public CodeGenerator(TextWriter output)
		{
			this.output = output;
		}
		public void Generate(List<State> states, Grammar grammar)
		{
			this.grammar = grammar;
			this.GenerateCopyright();
			this.GenerateUsingHeader();
			this.output.WriteLine(grammar.headerCode);
			if (grammar.Namespace != null)
			{
				this.output.WriteLine("namespace {0}", grammar.Namespace);
				this.output.WriteLine("{");
			}
			this.GenerateTokens(grammar.terminals);
			this.GenerateClassHeader(grammar.ParserName);
			this.InsertCode(grammar.prologCode);
			this.GenerateInitializeMethod(grammar.ParserName, states, grammar.productions, grammar.nonTerminals);
			this.GenerateActionMethod(grammar.productions);
			this.GenerateToStringMethod();
			this.InsertCode(grammar.epilogCode);
			this.GenerateClassFooter();
			if (grammar.Namespace != null)
			{
				this.output.WriteLine("}");
			}
		}
		private void GenerateCopyright()
		{
			this.output.WriteLine("// This code was generated by the Gardens Point Parser Generator");
			this.output.WriteLine("// Copyright (c) Wayne Kelly, QUT 2005");
            this.output.WriteLine("// Copyright (c) DEVSENSE, 2012");
			this.output.WriteLine();
			this.output.WriteLine();
		}
		private void GenerateUsingHeader()
		{
			this.output.WriteLine("using System;");
			this.output.WriteLine("using System.Text;");
			this.output.WriteLine("using System.Collections.Generic;");
			this.output.WriteLine();
		}
		private void GenerateTokens(Dictionary<string, Terminal> terminals)
		{
			this.output.Write("{0} enum {1} {{", this.grammar.Visibility, this.grammar.TokenName);
			bool flag = true;
			foreach (Terminal current in terminals.Values)
			{
				if (current.symbolic)
				{
					if (!flag)
					{
						this.output.Write(",");
					}
					this.output.Write("{0}={1}", current.ToString(), current.num);
					flag = false;
				}
			}
			this.output.WriteLine("};");
			this.output.WriteLine();
		}
		private string GenerateValueType()
		{
			if (this.grammar.unionType != null)
			{
				this.output.WriteLine("{0} partial struct {1}", this.grammar.Visibility, this.grammar.ValueTypeName);
				this.InsertCode(this.grammar.unionType);
				return this.grammar.ValueTypeName;
			}
			return "int";
		}
		private void GenerateClassHeader(string name)
		{
			string text = this.GenerateValueType();
			this.output.WriteLine("{0} {1} partial class {2}: ShiftReduceParser<{3},{4}>", new object[]
			{
				this.grammar.Visibility,
				this.grammar.Attributes,
				name,
				text,
				this.grammar.PositionType
			});
			this.output.WriteLine("{");
		}
		private void GenerateClassFooter()
		{
			this.output.WriteLine("}");
		}
		private void GenerateInitializeMethod(string className, List<State> states, List<Production> productions, Dictionary<string, NonTerminal> nonTerminals)
		{
			this.output.WriteLine();
			this.output.WriteLine("  protected override string[] NonTerminals { get { return nonTerminals; } }");
			this.output.WriteLine("  private static string[] nonTerminals;");
			this.output.WriteLine();
			this.output.WriteLine("  protected override State[] States { get { return states; } }");
			this.output.WriteLine("  private static readonly State[] states;");
			this.output.WriteLine();
			this.output.WriteLine("  protected override Rule[] Rules { get { return rules; } }");
			this.output.WriteLine("  private static readonly Rule[] rules;");
			this.output.WriteLine();
			this.output.WriteLine();
			this.output.WriteLine("  #region Construction");
			this.output.WriteLine();
			this.output.WriteLine("  static {0}()", className);
			this.output.WriteLine("  {");
			this.output.WriteLine();
			this.GenerateStates(states);
			this.output.WriteLine();
            this.output.WriteLine("    #region rules");
			this.output.WriteLine("    rules = new Rule[]");
            this.output.WriteLine("    {");
            this.output.WriteLine("    default(Rule),");    // 0th rule
            foreach (Production current in productions)
				this.GenerateRule(current);
            this.output.WriteLine("    };");
            this.output.WriteLine("    #endregion");

			this.output.WriteLine();
			this.output.Write("    nonTerminals = new string[] {\"\", ");
			int num = 37;
			foreach (NonTerminal current2 in nonTerminals.Values)
			{
				string text = string.Format("\"{0}\", ", current2.ToString());
				num += text.Length;
				this.output.Write(text);
				if (num > 70)
				{
					this.output.WriteLine();
					this.output.Write("      ");
					num = 0;
				}
			}
			this.output.WriteLine("};");
			this.output.WriteLine("  }");
			this.output.WriteLine();
			this.output.WriteLine("  #endregion");
			this.output.WriteLine();
		}
		private void GenerateStates(List<State> states)
		{
            this.output.WriteLine("    #region states");
			this.output.WriteLine("    states = new State[]");
			this.output.WriteLine("    {");
			for (int i = 0; i < states.Count; i++)
			{
				this.output.Write("      new State({0}, ", i);
				int defaultAction = this.GetDefaultAction(states[i]);
				if (defaultAction != 0)
				{
					this.output.Write(defaultAction);
				}
				else
				{
					this.output.Write("new int[] {");
					bool flag = true;
					foreach (KeyValuePair<Terminal, ParserAction> current in states[i].parseTable)
					{
						if (!flag)
						{
							this.output.Write(",");
						}
						this.output.Write("{0},{1}", current.Key.num, current.Value.ToNum());
						flag = false;
					}
					this.output.Write("}");
				}
				if (states[i].nonTerminalTransitions.Count > 0)
				{
					this.output.Write(", new int[] {");
					bool flag2 = true;
					foreach (Transition current2 in states[i].nonTerminalTransitions.Values)
					{
						if (!flag2)
						{
							this.output.Write(",");
						}
						this.output.Write("{0},{1}", current2.A.num, current2.next.num);
						flag2 = false;
					}
					this.output.Write("}");
				}
				this.output.WriteLine("),");
			}
			this.output.WriteLine("    };");
            this.output.WriteLine("    #endregion");
		}
		private int GetDefaultAction(State state)
		{
			IEnumerator<ParserAction> enumerator = state.parseTable.Values.GetEnumerator();
			enumerator.MoveNext();
			int num = enumerator.Current.ToNum();
			if (num > 0)
			{
				return 0;
			}
			foreach (KeyValuePair<Terminal, ParserAction> current in state.parseTable)
			{
				if (current.Value.ToNum() != num)
				{
					return 0;
				}
			}
			return num;
		}
		private void GenerateRule(Production production)
		{
            this.output.Write("    new Rule(" + production.lhs.num + ", new int[]{");
			bool flag = true;
			foreach (Symbol current in production.rhs)
			{
				if (!flag)
				{
					this.output.Write(",");
				}
				else
				{
					flag = false;
				}
				this.output.Write("{0}", current.num);
			}
			this.output.WriteLine("}),");
		}
		private void GenerateActionMethod(List<Production> productions)
		{
			this.output.WriteLine("  protected override void DoAction(int action)");
			this.output.WriteLine("  {");
			this.output.WriteLine("    switch (action)");
			this.output.WriteLine("    {");
			foreach (Production current in productions)
			{
				if (current.semanticAction != null)
				{
					this.output.WriteLine("      case {0}: // {1}", current.num, current.ToString());
					current.semanticAction.GenerateCode(this);
					this.output.WriteLine("        return;");
				}
			}
			this.output.WriteLine("    }");
			this.output.WriteLine("  }");
			this.output.WriteLine();
		}
		private void GenerateToStringMethod()
		{
			this.output.WriteLine("  protected override string TerminalToString(int terminal)");
			this.output.WriteLine("  {");
			this.output.WriteLine("    if (((Tokens)terminal).ToString() != terminal.ToString())");
			this.output.WriteLine("      return ((Tokens)terminal).ToString();");
			this.output.WriteLine("    else");
			this.output.WriteLine("      return CharToString((char)terminal);");
			this.output.WriteLine("  }");
			this.output.WriteLine();
		}
		private void InsertCode(string code)
		{
			if (code != null)
			{
				StringReader stringReader = new StringReader(code);
				while (true)
				{
					string text = stringReader.ReadLine();
					if (text == null)
					{
						break;
					}
					this.output.WriteLine("{0}", text);
				}
				return;
			}
		}
	}
}
