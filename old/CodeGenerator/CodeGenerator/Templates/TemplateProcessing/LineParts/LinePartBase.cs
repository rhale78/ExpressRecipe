using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.Templates
{
	public abstract class LinePartBase
	{
		protected string LinePart { get; set; }

		public LinePartBase(string linePart)
		{
			LinePart = linePart;
		}

		public override string ToString()
		{
			return LinePart;
		}
	}
}