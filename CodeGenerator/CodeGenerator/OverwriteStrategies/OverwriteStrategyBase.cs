using CodeGenerator.Core.CodeFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.OverwriteStrategies
{
    public abstract class OverwriteStrategyBase
    {
        public abstract void Write(CodeFileBase codeFile);
    }
}
