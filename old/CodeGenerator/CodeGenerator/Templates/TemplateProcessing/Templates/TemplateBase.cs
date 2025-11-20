using CodeGenerator.Core.CodeFiles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates
{
    public abstract class TemplateBase
    {
        public string FileType { get; set; }
        public List<TemplateLineBase> Lines { get; set; }

		public TemplateBase()
		{
			Lines = new List<TemplateLineBase>();
		}

		protected List<String> AsLiteral {
            get
            {
                List<string> lines = new List<string>();
                foreach (TemplateLineBase line in Lines)
                {
                    lines.Add(line.Line);
                }
                return lines;
            }
            set
            {
                Lines = new List<TemplateLineBase>();
                foreach (string line in value)
                {
                    Lines.Add(new StaticTemplateLine(line));
                }
            }
        }

        protected string CodeFileType { get; set; }

        public abstract void LoadTemplate(string filename);
        public abstract void SaveTemplate(string filename);
        public abstract CodeFileBase Generate();
    }
}
