using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
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
}
