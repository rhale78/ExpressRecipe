using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
    public class PreDeIndentRule : IndentRule
    {
        public PreDeIndentRule(IndentableCodeFile codeFile)
            : base(codeFile)
        {
        }

        public override void AddLine(string line)
        {
            CodeFile.DeIndent();
            CodeFile.AddLine(line);
        }
        public override string ToString()
        {
            return "PreDeIndent " + CompareRule.ToString();
        }
    }
}
