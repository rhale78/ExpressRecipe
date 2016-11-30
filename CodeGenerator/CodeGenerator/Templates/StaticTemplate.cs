using CodeGenerator.Core.CodeFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.Templates
{
    public class StaticTemplate : TemplateBase
    {
        public override CodeFileBase Generate()
        {
            CodeFileBase codeFile = CodeFileFactory.Create(CodeFileType);
            foreach(string line in AsLiteral)
            {
                codeFile.AddLine(line);
            }
            return codeFile;
        }

        public override void LoadTemplate(string filename)
        {
             AsLiteral = System.IO.File.ReadAllLines(filename).ToList();
        }

        public override void SaveTemplate(string filename)
        {
            System.IO.File.WriteAllLines(filename, AsLiteral.ToArray());
        }
    }
}
