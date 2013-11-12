#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.FileFormats;

namespace OpenRA.Network
{
	public class HandshakeRequest
	{
		public string[] Mods;
		public string Map;

		public string Serialize()
		{
			var data = new List<MiniYamlNode>();
			data.Add(new MiniYamlNode("Handshake", FieldSaver.Save(this)));
			return data.WriteToString();
		}

		public static HandshakeRequest Deserialize(string data)
		{
			var handshake = new HandshakeRequest();
			FieldLoader.Load(handshake, MiniYaml.FromString(data).First().Value);
			return handshake;
		}
	}

	public class HandshakeResponse
	{
		public string[] Mods;
		public string Password;
		[FieldLoader.Ignore] public Session.Client Client;

		public string Serialize()
		{
			var data = new List<MiniYamlNode>();
			data.Add( new MiniYamlNode( "Handshake", null,
				new string[]{ "Mods", "Password" }.Select( p => FieldSaver.SaveField(this, p) ).ToList() ) );
			data.Add(new MiniYamlNode("Client", FieldSaver.Save(Client)));

			return data.WriteToString();
		}

		public static HandshakeResponse Deserialize(string data)
		{
			var handshake = new HandshakeResponse();
			handshake.Client = new Session.Client();

			var ys = MiniYaml.FromString(data);
			foreach (var y in ys)
				switch (y.Key)
				{
					case "Handshake":
						FieldLoader.Load(handshake, y.Value);
					break;
					case "Client":
						FieldLoader.Load(handshake.Client, y.Value);
					break;
				}
			return handshake;
		}
	}
}
