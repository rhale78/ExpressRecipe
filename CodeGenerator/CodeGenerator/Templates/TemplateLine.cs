using System;
using System.Collections.Generic;
using System.Linq;
using CodeGenerator.Core.Templates.Interpreter;

namespace CodeGenerator.Core.Templates
{
    public class TemplateLine
    {
        internal TemplateLine(string line)
        {
            Line = line;
        }

        public string Line { get; set; }
        public CommandInterpreter Interpreter { get; set; }

        public string Result {
            get { return Interpreter.Execute(Line); }
        }
    }
}
