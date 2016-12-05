using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
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
}
