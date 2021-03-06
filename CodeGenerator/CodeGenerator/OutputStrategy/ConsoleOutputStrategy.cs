﻿using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.CodeFiles;

namespace CodeGenerator.Core.OutputStrategy
{
    public class ConsoleOutputStrategy : NoInputStrategyBase
    {
        public override void Write(CodeFileBase codeFile)
        {
            List<string> lines = codeFile.GetLines();
            foreach (string line in lines)
            {
                System.Console.WriteLine(line);
            }
        }
    }
}
