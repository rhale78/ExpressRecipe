using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
    public class PostIndentRule : IndentRule
    {
        public PostIndentRule(IndentableCodeFile codeFile)
            : base(codeFile)
        {
        }

        public override void AddLine(string line)
        {
            CodeFile.AddLine(line);
            CodeFile.Indent();
        }
        public override string ToString()
        {
            return "PostIndent " + CompareRule.ToString();
        }
    }
}
