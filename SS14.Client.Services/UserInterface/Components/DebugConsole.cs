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
using SFML.Graphics;

namespace SS14.Client.Services.UserInterface.Components
{
	public class DebugConsole : ScrollableContainer, IDebugConsole
	{
		private Textbox input;
		private int last_y = 0;
		private Dictionary<string, IConsoleCommand> commands = new Dictionary<string, IConsoleCommand>();
		private bool sentCommandRequestToServer = false;

		public IDictionary<string, IConsoleCommand> Commands => commands;

		public DebugConsole(string uniqueName, Vector2i size, IResourceManager resourceManager) : base(uniqueName, size, resourceManager)
		{
			input = new Textbox(size.X, resourceManager)
			{
				ClearFocusOnSubmit = false,
				drawColor = new Color(128, 128, 128, 100),
				textColor = new Color(255, 250, 240)
			};
			input.OnSubmit += input_OnSubmit;
			this.BackgroundColor = new Color(128, 128, 128, 200);
			this.DrawBackground = true;
			this.DrawBorder = true;
			// Update(0);

			InitializeCommands();
		}

		private void input_OnSubmit(string text, Textbox sender)
		{
			AddLine("> " + text, new Color(255, 250, 240));
			ProcessCommand(text);
		}

		public void AddLine(string text, Color color)
		{
			bool atBottom = scrollbarV.Value >= scrollbarV.max;
			Label newLabel = new Label(text, "CALIBRI", this._resourceManager)
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
			// var uiMgr = IoCManager.Resolve<IUserInterfaceManager>();
			base.ToggleVisible();
			if (IsVisible())
			{
				// Focus doesn't matter because UserInterfaceManager is hardcoded to go to console when it's visible.
				// uiMgr.SetFocus(input);
				// Though TextBox does like focus for the caret and passing KeyDown.
				input.Focus = true;
				netMgr.MessageArrived += NetMgr_MessageArrived;
				if (netMgr.IsConnected && !sentCommandRequestToServer)
				{
					SendServerCommandRequest();
				}
			}
			else
			{
				// uiMgr.RemoveFocus(input);
				input.Focus = false;
				netMgr.MessageArrived -= NetMgr_MessageArrived;
			}
		}

		private void NetMgr_MessageArrived(object sender, IncomingNetworkMessageArgs e)
		{
			//Make sure we reset the position - we might recieve this message after the gamestates.
			if (e.Message.Position > 0)
				e.Message.Position = 0;

			if (e.Message.MessageType != NetIncomingMessageType.Data)
				return;

			switch ((NetMessage)e.Message.PeekByte())
			{
				case NetMessage.ConsoleCommandReply:
					e.Message.ReadByte();
					AddLine("< " + e.Message.ReadString(), new Color(65, 105, 225));
					break;

				case NetMessage.ConsoleCommandRegister:
					e.Message.ReadByte();
					for (ushort amount = e.Message.ReadUInt16(); amount > 0; amount--)
					{
						string commandName = e.Message.ReadString();
						// Do not do duplicate commands.
						if (commands.ContainsKey(name))
						{
							System.Console.WriteLine("Server sent console command {0}, but we already have one with the same name. Ignoring.", commandName);
							break;
						}
						string help = e.Message.ReadString();
						string description = e.Message.ReadString();

						var command = new ServerDummyCommand(name, help, description);
						commands[commandName] = command;
					}
					break;
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

			string commandname = args[0];

			//Entity player;
			//var entMgr = IoCManager.Resolve<IEntityManager>();
			//var plrMgr = IoCManager.Resolve<IPlayerManager>();
			//player = plrMgr.ControlledEntity;
			//IoCManager.Resolve<INetworkManager>().

			bool forward = true;
			if (commands.ContainsKey(commandname))
			{
				IConsoleCommand command = commands[commandname];
				args.RemoveAt(0);
				forward = command.Execute(this, args.ToArray());
			}
			else if (!IoCManager.Resolve<INetworkManager>().IsConnected)
			{
				AddLine("Unknown command: " + commandname, Color.Red);
				return;
			}

			if (forward)
			{
				SendServerConsoleCommand(text);
			}
		}

		private void InitializeCommands()
		{
			foreach (Type t in Assembly.GetCallingAssembly().GetTypes())
			{
				if (!typeof(IConsoleCommand).IsAssignableFrom(t) || t == typeof(ServerDummyCommand))
					continue;

				var instance = Activator.CreateInstance(t, null) as IConsoleCommand;
				if (commands.ContainsKey(instance.Command))
					throw new Exception(string.Format("Command already registered: {}", instance.Command));

				commands[instance.Command] = instance;
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

		private void SendServerCommandRequest()
		{
			var netMgr = IoCManager.Resolve<INetworkManager>();
			if (!netMgr.IsConnected)
				return;

			NetOutgoingMessage outMsg = netMgr.CreateMessage();
			outMsg.Write((byte)NetMessage.ConsoleCommandRegister);
			netMgr.SendMessage(outMsg, NetDeliveryMethod.ReliableUnordered);
			sentCommandRequestToServer = true;
		}

		public void Clear()
		{
			components.Clear();
			last_y = 0;
			//this.scrollbarH.Value = 0;
			scrollbarV.Value = 0;
		}
	}

	/// <summary>
	/// These dummies are made purely so list and help can list server-side commands.
	/// </summary>
	class ServerDummyCommand : IConsoleCommand
	{
		readonly string command;
		readonly string help;
		readonly string description;

		public string Command => command;
		public string Help => help;
		public string Description => description;

		internal ServerDummyCommand(string command, string help, string description)
		{
			this.command = command;
			this.help = help;
			this.description = description;
		}

		// Always forward to server.
		public bool Execute(IDebugConsole console, params string[] args)
		{
			return true;
		}
	}
}
