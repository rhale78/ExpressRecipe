using CodeGenerator.Core.CodeFiles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.OverwriteStrategies
{
    public abstract class OverwriteStrategyBase
    {
        public abstract void Write(CodeFileBase codeFile);
    }
}
