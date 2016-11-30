using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public abstract class StringComparisonRuleBase
    {
        public string ComparisonValue { get; set; }
        //RSH 11/24/16 - need to eventually use this - for now, everything is case insensitive compare
        public bool CaseInsensitive { get; set; }

        public abstract bool FitsRule(string line);
    }

    public class PrefixStringComparisonRule : StringComparisonRuleBase
    {
        public override bool FitsRule(string line)
        {
            if (line.StartsWith(ComparisonValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return "Prefix(" + ComparisonValue + ")";
        }
    }

    public class PostfixStringComparisonRule : StringComparisonRuleBase
    {
        public override bool FitsRule(string line)
        {
            if (line.EndsWith(ComparisonValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return "Postfix(" + ComparisonValue + ")";
        }
    }
    public class InfixStringComparisonRule : StringComparisonRuleBase
    {
        public override bool FitsRule(string line)
        {
            if (line.IndexOf(ComparisonValue, StringComparison.OrdinalIgnoreCase) > 0)
            {
                return true;
            }
            return false;
        }
        public override string ToString()
        {
            return "Infix(" + ComparisonValue + ")";
        }
    }

    public class EqualsStringComparisonRule : StringComparisonRuleBase
    {
        public override bool FitsRule(string line)
        {
            if (string.Equals(line, ComparisonValue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }
        public override string ToString()
        {
            return "Equals(" + ComparisonValue + ")";
        }
    }

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

    public class PreIndentRule : IndentRule
    {
        public PreIndentRule(IndentableCodeFile codeFile)
            : base(codeFile) { }

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
    public class PostIndentRule : IndentRule
    {
        public PostIndentRule(IndentableCodeFile codeFile)
            : base(codeFile) { }

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
    public class PreDeIndentRule : IndentRule
    {
        public PreDeIndentRule(IndentableCodeFile codeFile)
            : base(codeFile) { }

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
    public class PostDeIndentRule : IndentRule
    {
        public PostDeIndentRule(IndentableCodeFile codeFile)
            : base(codeFile) { }

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

    public class TemporaryIndentRule : IndentRule
    {
        public TemporaryIndentRule(IndentableCodeFile codeFile)
            : base(codeFile) { }

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

    public class TemporaryDeIndentRule : IndentRule
    {
        public TemporaryDeIndentRule(IndentableCodeFile codeFile)
            : base(codeFile) { }

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
