using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.CodeFiles;

namespace CodeGenerator.Core.OutputStrategy
{
    public class DebugOutputStrategy : NoInputStrategyBase
    {
        public override void Write(CodeFileBase codeFile)
        {
            List<string> lines = codeFile.GetLines();
            foreach (string line in lines)
            {
                System.Diagnostics.Debug.WriteLine(line);
            }
        }
    }
}
