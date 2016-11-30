﻿using CodeGenerator.Core.CodeFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.OutputStrategy
{
    public abstract class NoInputStrategyBase:OutputStrategyBase
    {
        public override string Contents(CodeFileBase codeFile)
        {
            return string.Empty;
        }

        public override bool Exists(CodeFileBase codeFile)
        {
            return false;
        }
    }
}
