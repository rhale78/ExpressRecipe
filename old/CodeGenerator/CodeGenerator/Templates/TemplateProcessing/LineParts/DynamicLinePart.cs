using CodeGenerator.Core.Templates.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.Templates
{
	public class DynamicLinePart : LinePartBase
	{
		protected static SurroundParser SurroundParser { get; set; }

		static DynamicLinePart()
		{
			SurroundParser = new SurroundParser("!@", "@!");
		}

		public DynamicLinePart(string linePart)
			:base(linePart)
		{
			if(IsDynamicLine(linePart))
			{
				base.LinePart = linePart.Replace(GetParserPrefix(), "").Replace(GetParserSuffix(), "").Trim();
			}
		}

		public static bool IsDynamicLine(string line)
		{
			if (!string.IsNullOrEmpty(line))
			{
				if (line.Contains(GetParserPrefix()) && line.Contains(GetParserSuffix()))
				{
					if (line.IndexOf(GetParserPrefix()) < line.IndexOf(GetParserSuffix(), line.IndexOf(GetParserPrefix())))
					{
						return true;
					}
				}
			}
			return false;
		}

		public static string GetParserSuffix()
		{
			return SurroundParser.Suffix;
		}

		public static string GetParserPrefix()
		{
			return SurroundParser.Prefix;
		}

		public override string ToString()
		{
			return SurroundParser.Prefix + base.ToString() + SurroundParser.Suffix;
		}
	}
}
