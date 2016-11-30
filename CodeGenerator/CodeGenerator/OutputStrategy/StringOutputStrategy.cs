using CodeGenerator.Core.CodeFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.OutputStrategy
{
    public class StringOutputStrategy:NoInputStrategyBase
    {
        public string Output { get; set; }

        public override void Write(CodeFileBase codeFile)
        {
            Output = "";
            List<string> lines = codeFile.GetLines();
            Output = string.Join(System.Environment.NewLine, lines);
        }
    }
}
