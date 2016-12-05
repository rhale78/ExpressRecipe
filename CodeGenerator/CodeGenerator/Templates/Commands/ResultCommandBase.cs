using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands
{
    public abstract class ResultCommandBase<T> : CommandBase<T>, IResultCommand
        where T: ResultCommandBase<T>, new()
    {
        public abstract dynamic Result { get; }
        public abstract string ResultType { get; }
    }
}
