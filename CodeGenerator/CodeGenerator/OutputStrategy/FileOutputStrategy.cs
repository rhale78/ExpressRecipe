using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeGenerator.Core.CodeFiles;

namespace CodeGenerator.Core.OutputStrategy
{
    public class FileOutputStrategy : OutputStrategyBase
    {
        public override string Contents(CodeFileBase codeFile)
        {
            if(Exists(codeFile))
            {
                return System.IO.File.ReadAllText(codeFile.FullFilename);
            }
            else
            {
                return string.Empty;
            }
        }

        public override bool Exists(CodeFileBase codeFile)
        {
            return System.IO.File.Exists(codeFile.FullFilename);

        }

        public override void Write(CodeFileBase codeFile)
        {
            if (codeFile != null)
            {
                if (!System.IO.Directory.Exists(CodeFileBase.RootPath))
                {
                    System.IO.Directory.CreateDirectory(CodeFileBase.RootPath);
                }
                if (!System.IO.Directory.Exists(codeFile.Path))
                {
                    System.IO.Directory.CreateDirectory(codeFile.Path);
                }

                StringOutputStrategy stringOutput = new StringOutputStrategy();
                stringOutput.Write(codeFile);
                System.IO.File.WriteAllText(codeFile.FullFilename, stringOutput.Output);
            }
        }
    }
}
