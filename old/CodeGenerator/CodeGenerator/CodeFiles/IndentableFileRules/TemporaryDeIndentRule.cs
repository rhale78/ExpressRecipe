using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
    public class TemporaryDeIndentRule : IndentRule
    {
        public TemporaryDeIndentRule(IndentableCodeFile codeFile)
            : base(codeFile)
        {
        }

        public override void AddLine(string line)
        {
            CodeFile.DeIndent();
            CodeFile.AddLine(line);
            CodeFile.Indent();
        }
        public override string ToString()
        {
            return "TempDeIndent " + CompareRule.ToString();
        }
    }
}
