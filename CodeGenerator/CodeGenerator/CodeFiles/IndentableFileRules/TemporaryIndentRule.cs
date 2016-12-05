using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
    public class TemporaryIndentRule : IndentRule
    {
        public TemporaryIndentRule(IndentableCodeFile codeFile)
            : base(codeFile)
        {
        }

        public override void AddLine(string line)
        {
            CodeFile.Indent();
            CodeFile.AddLine(line);
            CodeFile.DeIndent();
        }
        public override string ToString()
        {
            return "TempIndent " + CompareRule.ToString();
        }
    }
}
