using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Interfaces
{
    public interface IConfigDriven
    {
        void Save(string filename);
        void Load(string filename);
        void LoadFromString(string config);
        string SaveToString();
    }
}
