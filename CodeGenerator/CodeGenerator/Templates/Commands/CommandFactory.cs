using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeGenerator.Core.Templates.Commands
{
    public static class CommandFactory
    {
        public static List<CommandBase> Commands { get; private set; }

        static CommandFactory()
        {
            Commands = new List<CommandBase>();
            LoadAllCommands();
        }

        private static void LoadAllCommands()
        {
        }
    }
}
