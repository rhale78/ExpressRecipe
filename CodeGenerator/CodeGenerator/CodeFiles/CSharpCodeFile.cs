﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles
{
    public class CSharpCodeFile : StructuredFileBase
    {
        public CSharpCodeFile()
        {
            Extension = "cs";
        }

        public override CodeFileBase CreateInstance()
        {
            throw new NotImplementedException();
        }

        public override void LoadRulesFromFile(string filename)
        {
            throw new NotImplementedException();
        }

        public override void SaveRulesToFile(string filename)
        {
            throw new NotImplementedException();
        }
    }
}
