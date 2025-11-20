using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.CodeFiles
{
    public class SQLCodeFile : TextualFileBase
    {
        public override CodeFileBase CreateInstance()
        {
            return new SQLCodeFile();
        }
    }
}
