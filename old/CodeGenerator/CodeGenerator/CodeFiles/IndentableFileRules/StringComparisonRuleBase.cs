using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles.IndentableFileRules
{
    public abstract class StringComparisonRuleBase
    {
        public string ComparisonValue { get; set; }
        //RSH 11/24/16 - need to eventually use this - for now, everything is case insensitive compare
        public bool CaseInsensitive { get; set; }

        public abstract bool FitsRule(string line);
    }
}
