using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
    public class PostDeIndentRule : IndentRule
    {
        public PostDeIndentRule(IndentableCodeFile codeFile)
            : base(codeFile)
        {
        }

        public override void AddLine(string line)
        {
            CodeFile.AddLine(line);
            CodeFile.DeIndent();
        }
        public override string ToString()
        {
            return "PostDeIndent " + CompareRule.ToString();
        }
    }
}
