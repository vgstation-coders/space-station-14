﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS14.Client.Interfaces.Console;

namespace SS14.Client.Services.Console
{
    class QuitCommand : IConsoleCommand
    {
        public string Command => "quit";
        public string Description => "Kills the game client instantly.";
        public string Help => "Kills the game client instantly, leaving no traces. No telling the server goodbye";

        public bool Execute(IDebugConsole console, params string[] args)
        {
            Environment.Exit(0);
            return false;
        }
    }
}
