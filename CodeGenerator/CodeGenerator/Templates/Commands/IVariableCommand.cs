using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands
{
    public interface IVariableCommand
    {
        bool Exists();
        void Create(string type, dynamic value);
        dynamic Get();
        void Set(dynamic value);
    }
}
