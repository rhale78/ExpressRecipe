using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeGenerator.Core.CodeFiles;

namespace CodeGenerator.Core.OutputStrategy
{
    public class FileDefaultStrategy : OutputStrategyBase
    {
        public override string Contents(CodeFileBase codeFile)
        {
            return codeFile.Contents();
        }

        public override bool Exists(CodeFileBase codeFile)
        {
            return codeFile.Exists();
        }

        public override void Write(CodeFileBase codeFile)
        {
            codeFile.Write();
        }
    }
}
