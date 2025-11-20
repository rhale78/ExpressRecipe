using CodeGenerator.Core.Templates.Parser;
using System.Collections.Generic;

namespace CodeGenerator.Core.Templates
{
	public class DynamicTemplateLine : TemplateLineBase
	{
		protected List<LinePartBase> LineParts { get; set; }

		internal DynamicTemplateLine(string line)
			: base(line)
		{
			ParseLine(line);
		}

		protected void ParseLine(string line)
		{
			LineParts = new List<LinePartBase>();
			int startIndex = -1;
			int endIndex = -1;
			do
			{
				startIndex = line.IndexOf(DynamicLinePart.GetParserPrefix());
				endIndex = line.IndexOf(DynamicLinePart.GetParserSuffix());
				if (startIndex > -1 && endIndex > -1)
				{
					if (startIndex > 0)
					{
						string startString = line.Substring(0, startIndex - 1).Trim();
						LineParts.Add(new StaticLinePart(startString));
					}
					string parseString = line.Substring(startIndex, endIndex - startIndex + DynamicLinePart.GetParserSuffix().Length);
					LineParts.Add(new DynamicLinePart(parseString));
					line = line.Substring(endIndex + DynamicLinePart.GetParserSuffix().Length);
				}
				else
				{
					LineParts.Add(new StaticLinePart(line.Trim()));
					line = string.Empty;
				}
			} while (line.Length>0);
			int a = 0;
		}

		public override string Result
		{
			get
			{
				string line = string.Empty;
				foreach (LinePartBase linePart in LineParts)
				{
					line = line + Interpreter.Execute(Line);
				}
				if (!(LineParts[LineParts.Count - 1] is DynamicLinePart))
				{
					line = line + System.Environment.NewLine;
				}
				return line;
			}
		}

		public static bool IsDynamicLine(string line)
		{
			return DynamicLinePart.IsDynamicLine(line);
		}

		public override string ToString()
		{
			string line = string.Empty;
			foreach (LinePartBase linePart in LineParts)
			{
				line = line + linePart.ToString();
			}
			return line;
		}
	}
}
