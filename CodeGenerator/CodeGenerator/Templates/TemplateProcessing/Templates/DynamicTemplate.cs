using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.CodeFiles;

namespace CodeGenerator.Core.Templates
{
	public class DynamicTemplate : TemplateBase
	{
		//protected new List<TemplateLineBase> Lines { get; set; }

		public DynamicTemplate()
		: base()
		{ }

		public override CodeFileBase Generate()
		{
			throw new NotImplementedException();
		}

		public override void LoadTemplate(string filename)
		{
			List<string> lines = System.IO.File.ReadAllLines(filename).ToList();
			Lines = new List<TemplateLineBase>();
			foreach (string line in lines)
			{
				LoadSingleLine(line);
			}
		}

		public void LoadSingleLine(string line)
		{
			if (DynamicTemplateLine.IsDynamicLine(line))
			{
				Lines.Add(new DynamicTemplateLine(line));
			}
			else
			{
				Lines.Add(new StaticTemplateLine(line));
			}
		}

		public override void SaveTemplate(string filename)
		{
			throw new NotImplementedException();
		}
	}
}