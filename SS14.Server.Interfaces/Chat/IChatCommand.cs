﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS14.Shared.Command;

namespace SS14.Server.Interfaces.Chat
{
    public interface IChatCommand : ICommand
    {
        void Execute(IChatManager manager, IClient client, params string[] args);
    }
}
