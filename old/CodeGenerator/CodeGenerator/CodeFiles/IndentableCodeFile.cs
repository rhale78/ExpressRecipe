using CodeGenerator.Core.CodeFiles.IndentableFileRules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles
{
    public abstract class IndentableCodeFile : CodeFileBase
    {
        protected int IndentLevel { get; set; }
        protected int IndentAmount { get; set; }

        protected List<IndentRule> IndentRules { get; set; }

        public IndentableCodeFile()
        {
            IndentRules = new List<IndentRule>();
        }

        internal void Indent()
        {
            IndentLevel++;
        }

        internal void DeIndent()
        {
            IndentLevel--;
        }

        internal new void AddLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                base.AddLine(line);
            }

            foreach (IndentRule rule in IndentRules)
            {
                if (rule.Fits(line))
                {
                    rule.AddLine(line);
                    break;
                }
            }
            AddLineInternal(line);
        }

        protected void AddLineInternal(string line)
        {
            base.AddLine(new string(' ', IndentAmount * IndentLevel) + line);
        }

        public abstract void LoadRulesFromFile(string filename);
        public abstract void SaveRulesToFile(string filename);

        public void AddRule(IndentRule rule)
        {
            IndentRules.Add(rule);
        }
    }
}
