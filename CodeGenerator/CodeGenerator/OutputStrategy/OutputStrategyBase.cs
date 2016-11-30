﻿using CodeGenerator.Core.CodeFiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeGenerator.Core.OutputStrategy
{
    public abstract class OutputStrategyBase
    {
        public abstract bool Exists(CodeFileBase codeFile);
        public abstract void Write(CodeFileBase codeFile);
        public abstract string Contents(CodeFileBase codeFile);
    }
}
