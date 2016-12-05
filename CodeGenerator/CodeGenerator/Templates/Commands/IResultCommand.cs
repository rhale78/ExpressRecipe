using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands
{
    public interface IResultCommand
    {
        string ResultType { get; }
        dynamic Result { get; }
    }
}
