﻿using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.CodeFiles;

namespace CodeGenerator.Core.OverwriteStrategies
{
    public class AlwaysOverwriteStrategy : OverwriteStrategyBase
    {
        public override void Write(CodeFileBase codeFile)
        {
            codeFile.Write();
        }
    }
}
