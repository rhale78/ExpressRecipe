using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
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
}
