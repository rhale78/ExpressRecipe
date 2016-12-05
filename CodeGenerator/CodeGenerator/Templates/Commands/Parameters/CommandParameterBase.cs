using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands.Parameters
{
    public abstract class CommandParameterBase
    {
    }

    public abstract class CommandParameterBase<T, W> : CommandParameterBase
        where T: CommandParameterBase, new()
        where W: ResultCommandBase<W>, new()
    {
        protected W Command { get; set; }

        public T CreateInstance()
        {
            return new T();
        }

        public dynamic GetResult()
        {
            Command.Execute();
            return Command.Result;
        }
    }
}
