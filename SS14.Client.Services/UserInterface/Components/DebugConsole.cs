﻿using Lidgren.Network;
using SFML.System;
using SFML.Window;
using SS14.Client.Interfaces.GOC;
using SS14.Client.Interfaces.Network;
using SS14.Client.Interfaces.Player;
using SS14.Client.Interfaces.Resource;
using SS14.Client.Interfaces.UserInterface;
using SS14.Client.Interfaces.Console;
using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.GO;
using SS14.Shared.IoC;
using SS14.Shared.Maths;
using SS14.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SS14.Client.Services.UserInterface.Components
{
    public class DebugConsole : ScrollableContainer
    {
        private Textbox input;
        private int last_y = 0;
        private Dictionary<string, IConsoleCommand> Commands = new Dictionary<string, IConsoleCommand>();

        public DebugConsole(string uniqueName, Vector2i size, IResourceManager resourceManager) : base(uniqueName, size, resourceManager)
        {
            input = new Textbox(size.X, resourceManager)
            {
                ClearFocusOnSubmit = false,
                drawColor = new SFML.Graphics.Color(128, 128, 128, 100),
                textColor = new SFML.Graphics.Color(255, 250, 240)
            };
            input.OnSubmit += new Textbox.TextSubmitHandler(input_OnSubmit);
            this.BackgroundColor = new SFML.Graphics.Color(128, 128, 128, 100);
            this.DrawBackground = true;
            this.DrawBorder = true;
            Update(0);

            InitializeCommands();
        }

        private void input_OnSubmit(string text, Textbox sender)
        {
            AddLine(text, new SFML.Graphics.Color(255, 250, 240));
            ProcessCommand(text);
        }

        public void AddLine(string text, SFML.Graphics.Color color)
        {
            bool atBottom = scrollbarV.Value >= scrollbarV.max;
            Label newLabel = new Label(text, "MICROGBE", this._resourceManager)
            {
                Position = new Vector2i(5, last_y),
                TextColor = color
            };
            newLabel.Update(0);
            last_y = newLabel.ClientArea.Bottom();
            components.Add(newLabel);
            if (atBottom)
            {
                Update(0);
                scrollbarV.Value = scrollbarV.max;
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            if (input != null)
            {
                input.Position = new Vector2i(ClientArea.Left, ClientArea.Bottom());
                input.Update(frameTime);
            }
        }

        public override void ToggleVisible()
        {
            var netMgr = IoCManager.Resolve<INetworkManager>();
            var uiMgr = IoCManager.Resolve<IUserInterfaceManager>();
            base.ToggleVisible();
            if (IsVisible())
            {
                // Focus doesn't matter because UserInterfaceManager is hardcoded to go to console when it's visible.
                // uiMgr.SetFocus(input);
                // Though TextBox does like focus for the caret and handling KeyDown.
                input.Focus = true;
                netMgr.MessageArrived += new EventHandler<IncomingNetworkMessageArgs>(netMgr_MessageArrived);
            }
            else
            {
                // uiMgr.RemoveFocus(input);
                input.Focus = true;
                netMgr.MessageArrived -= new EventHandler<IncomingNetworkMessageArgs>(netMgr_MessageArrived);
            }
        }

        private void netMgr_MessageArrived(object sender, IncomingNetworkMessageArgs e)
        {
            //Make sure we reset the position - we might recieve this message after the gamestates.
            if (e.Message.Position > 0) e.Message.Position = 0;
            if (e.Message.MessageType == NetIncomingMessageType.Data && (NetMessage)e.Message.PeekByte() == NetMessage.ConsoleCommandReply)
            {
                e.Message.ReadByte();
                AddLine("Server: " + e.Message.ReadString(), new SFML.Graphics.Color(65, 105, 225));
            }
            //Again, make sure we reset the position - we might get it before the gamestate and then that would break.
            e.Message.Position = 0;
        }

        public override void Render()
        {
            base.Render();
            if (input != null) input.Render();
        }

        public override void Dispose()
        {
            base.Dispose();
            input.Dispose();
        }

        public override bool MouseDown(MouseButtonEventArgs e)
        {
            if (!base.MouseDown(e))
                if (input.MouseDown(e))
                {
                    // Focus doesn't matter because UserInterfaceManager is hardcoded to go to console when it's visible.
                    // IoCManager.Resolve<IUserInterfaceManager>().SetFocus(input);
                    return true;
                }
            return false;
        }

        public override bool MouseUp(MouseButtonEventArgs e)
        {
            if (!base.MouseUp(e))
                return input.MouseUp(e);
            else return false;
        }

        public override void MouseMove(MouseMoveEventArgs e)
        {
            base.MouseMove(e);
            input.MouseMove(e);
        }

        public override bool KeyDown(KeyEventArgs e)
        {
            if (!base.KeyDown(e))
                return input.KeyDown(e);
            else return false;
        }

        public override bool TextEntered(TextEventArgs e)
        {
            if (!base.TextEntered(e))
                return input.TextEntered(e);
            else return false;
        }

        /// <summary>
        /// Processes commands (chat messages starting with /)
        /// </summary>
        /// <param name="text">input text</param>
        private void ProcessCommand(string text)
        {
            //Commands are processed locally and then sent to the server to be processed there again.
            var args = new List<string>();

            CommandParsing.ParseArguments(text, args);

            string command = args[0];

            //Entity player;
            //var entMgr = IoCManager.Resolve<IEntityManager>();
            //var plrMgr = IoCManager.Resolve<IPlayerManager>();
            //player = plrMgr.ControlledEntity;
            //IoCManager.Resolve<INetworkManager>().

            switch (command)
            {
                case "cls":
                    components.Clear();
                    last_y = 0;
                    //this.scrollbarH.Value = 0;
                    this.scrollbarV.Value = 0;
                    break;

                case "quit":
                    Environment.Exit(0);
                    break;

                case "addparticles": //This is only clientside.
                    if (args.Count >= 3)
                    {
                        Entity target = null;
                        if (args[1].ToLowerInvariant() == "player")
                        {
                            var plrMgr = IoCManager.Resolve<IPlayerManager>();
                            if (plrMgr != null)
                                if (plrMgr.ControlledEntity != null) target = plrMgr.ControlledEntity;
                        }
                        else
                        {
                            var entMgr = IoCManager.Resolve<IEntityManagerContainer>();
                            if (entMgr != null)
                            {
                                int entUid = int.Parse(args[1]);
                                target = entMgr.EntityManager.GetEntity(entUid);
                            }
                        }

                        if (target != null)
                        {
                            if (!target.HasComponent(ComponentFamily.Particles))
                            {
                                var entMgr = IoCManager.Resolve<IEntityManagerContainer>();
                                var compo = (IParticleSystemComponent)entMgr.EntityManager.ComponentFactory.GetComponent("ParticleSystemComponent");
                                target.AddComponent(ComponentFamily.Particles, compo);
                            }
                            else
                            {
                                var entMgr = IoCManager.Resolve<IEntityManagerContainer>();
                                var compo = (IParticleSystemComponent)entMgr.EntityManager.ComponentFactory.GetComponent("ParticleSystemComponent");
                                target.AddComponent(ComponentFamily.Particles, compo);
                            }
                        }
                    }
                    SendServerConsoleCommand(text); //Forward to server.
                    break;

                case "removeparticles":
                    if (args.Count >= 3)
                    {
                        Entity target = null;
                        if (args[1].ToLowerInvariant() == "player")
                        {
                            var plrMgr = IoCManager.Resolve<IPlayerManager>();
                            if (plrMgr != null)
                                if (plrMgr.ControlledEntity != null) target = plrMgr.ControlledEntity;
                        }
                        else
                        {
                            var entMgr = IoCManager.Resolve<IEntityManagerContainer>();
                            if (entMgr != null)
                            {
                                int entUid = int.Parse(args[1]);
                                target = entMgr.EntityManager.GetEntity(entUid);
                            }
                        }

                        if (target != null)
                        {
                            if (target.HasComponent(ComponentFamily.Particles))
                            {
                                IParticleSystemComponent compo = (IParticleSystemComponent)target.GetComponent(ComponentFamily.Particles);
                                compo.RemoveParticleSystem(args[2]);
                            }
                        }
                    }
                    SendServerConsoleCommand(text); //Forward to server.
                    break;

                case "sendchat":
                    if (args.Count < 2)
                        return;

                    INetworkManager NetworkManager = IoCManager.Resolve<INetworkManager>();
                    NetOutgoingMessage message = NetworkManager.CreateMessage();
                    message.Write((byte)NetMessage.ChatMessage);
                    message.Write((byte)ChatChannel.Player);
                    message.Write(args[1]);
                    NetworkManager.SendMessage(message, NetDeliveryMethod.ReliableUnordered);

                    break;

                // To debug console scrolling and stuff.
                case "fill":
                    SFML.Graphics.Color[] colors = { SFML.Graphics.Color.Green, SFML.Graphics.Color.Blue, SFML.Graphics.Color.Red };
                    Random random = new Random();
                    for (int x = 0; x < 50; x++)
                    {
                        AddLine("filling...", colors[random.Next(0, colors.Length)]);
                    }
                    break;

                default:
                    SendServerConsoleCommand(text); //Forward to server.
                    break;
            }
        }

        private void InitializeCommands()
        {
            foreach (Type t in Assembly.GetCallingAssembly().GetTypes())
            {
                if (!typeof(IConsoleCommand).IsAssignableFrom(t) || t == typeof(IConsoleCommand))
                    continue;

                var instance = Activator.CreateInstance(t, null) as IConsoleCommand;
                RegisterCommand(instance);

            }
        }

        private void SendServerConsoleCommand(string text)
        {
            var netMgr = IoCManager.Resolve<INetworkManager>();
            if (netMgr != null && netMgr.IsConnected)
            {
                NetOutgoingMessage outMsg = netMgr.CreateMessage();
                outMsg.Write((byte)NetMessage.ConsoleCommand);
                outMsg.Write(text);
                netMgr.SendMessage(outMsg, NetDeliveryMethod.ReliableUnordered);
            }
        }
    }

    internal class ServerConsoleCommand : IConsoleCommand
    {
        private readonly string command;
        private readonly string help;
        private readonly string description;

        public string Command => command;
        public string Help => help;
        public string Description => description;

        public void Execute(params string[] args)
        {

        }
    }
}
