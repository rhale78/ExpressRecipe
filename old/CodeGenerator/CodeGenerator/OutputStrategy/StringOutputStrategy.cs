using CodeGenerator.Core.CodeFiles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.OutputStrategy
{
    public class StringOutputStrategy : NoInputStrategyBase
    {
        public string Output { get; set; }

        public override void Write(CodeFileBase codeFile)
        {
            Output = string.Empty;
            List<string> lines = codeFile.GetLines();
            Output = string.Join(System.Environment.NewLine, lines);
        }
    }
}
