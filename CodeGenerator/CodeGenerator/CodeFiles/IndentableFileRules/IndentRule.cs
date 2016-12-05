using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
    public abstract class IndentRule
    {
        protected IndentableCodeFile CodeFile { get; set; }
        protected StringComparisonRuleBase CompareRule { get; set; }
        public IndentRule(IndentableCodeFile codeFile)
        {
            CodeFile = codeFile;
        }
        public abstract void AddLine(string line);
        public bool Fits(string line)
        {
            return CompareRule.FitsRule(line);
        }
    }
}
