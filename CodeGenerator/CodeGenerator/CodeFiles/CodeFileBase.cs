using CodeGenerator.Core.OutputStrategy;
using CodeGenerator.Core.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.CodeFiles
{
    public abstract class CodeFileBase
    {
        public static string RootPath { get; set; }
        public string Path { get; protected set; }
        protected string Filename { get; set; }
        protected string Extension { get; set; }

        protected OutputStrategyBase OutputStrategy { get; set; }
        protected OverwriteStrategies.OverwriteStrategyBase OverwriteStrategy { get; set; }

        protected List<string> Lines { get; set; }

        public CodeFileBase()
        {
            Lines = new List<string>();
        }

        public string FullFilename
        {
            get
            {
                return RootPath + System.IO.Path.DirectorySeparatorChar + Path + System.IO.Path.DirectorySeparatorChar + Filename + "." + Extension;
            }
        }

        public List<string> GetLines()
        {
            return Lines;
        }

        internal void AddLine(string line)
        {
            Lines.Add(line);
        }

        public string Contents()
        {
            return OutputStrategy.Contents(this);
        }

        public Boolean Exists()
        {
            return OutputStrategy.Exists(this);
        }

        public void Write()
        {
            OutputStrategy.Write(this);
        }

        public void WriteUsingOverwriteStrategy()
        {
            OverwriteStrategy.Write(this);
        }

        public abstract CodeFileBase CreateInstance();
    }
}
