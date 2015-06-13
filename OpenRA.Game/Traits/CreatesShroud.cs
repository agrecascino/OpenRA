#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;

namespace OpenRA.Traits
{
	public class CreatesShroudInfo : ITraitInfo
	{
		public readonly WRange Range = WRange.Zero;

		public object Create(ActorInitializer init) { return new CreatesShroud(init.Self, this); }
	}

	public class CreatesShroud : ITick, ISync, INotifyAddedToWorld, INotifyRemovedFromWorld
	{
		readonly CreatesShroudInfo info;
		readonly bool lobbyShroudFogDisabled;
		[Sync] CPos cachedLocation;
		[Sync] bool cachedDisabled;

		public CreatesShroud(Actor self, CreatesShroudInfo info)
		{
			this.info = info;
			lobbyShroudFogDisabled = !self.World.LobbyInfo.GlobalSettings.Shroud && !self.World.LobbyInfo.GlobalSettings.Fog;
		}

		public void Tick(Actor self)
		{
			if (lobbyShroudFogDisabled)
				return;

			var disabled = self.IsDisabled();
			if (cachedLocation != self.Location || cachedDisabled != disabled)
			{
				cachedLocation = self.Location;
				cachedDisabled = disabled;

				CPos[] shrouded = null;
				foreach (var p in self.World.Players)
				{
					p.Shroud.RemoveShroudGeneration(self);
					p.Shroud.AddShroudGeneration(self, ref shrouded);
				}
			}
		}

		public void AddedToWorld(Actor self)
		{
			CPos[] shrouded = null;
			foreach (var p in self.World.Players)
				p.Shroud.AddShroudGeneration(self, ref shrouded);
		}

		public void RemovedFromWorld(Actor self)
		{
			foreach (var p in self.World.Players)
				p.Shroud.RemoveShroudGeneration(self);
		}

		public WRange Range { get { return cachedDisabled ? WRange.Zero : info.Range; } }
	}
}