using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
