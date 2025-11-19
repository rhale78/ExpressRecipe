using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
    public class PreIndentRule : IndentRule
    {
        public PreIndentRule(IndentableCodeFile codeFile)
            : base(codeFile)
        {
        }

        public override void AddLine(string line)
        {
            CodeFile.Indent();
            CodeFile.AddLine(line);
        }
        public override string ToString()
        {
            return "PreIndent " + CompareRule.ToString();
        }
    }
}
