using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeGenerator.Core.CodeFiles;

namespace CodeGenerator.Core.OverwriteStrategies
{
    public class FileDefaultStrategy : OverwriteStrategyBase
    {
        public override void Write(CodeFileBase codeFile)
        {
            codeFile.WriteUsingOverwriteStrategy();
        }
    }
}
