#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.RA.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Effects
{
	class GpsDotInfo : ITraitInfo
	{
		public readonly string String = "Infantry";
		public readonly string IndicatorPalettePrefix = "player";

		public object Create(ActorInitializer init)
		{
			return new GpsDot(init.Self, this);
		}
	}

	class GpsDot : IEffect
	{
		readonly Actor self;
		readonly GpsDotInfo info;
		readonly Animation anim;

		readonly Dictionary<Player, DotState> stateByPlayer = new Dictionary<Player, DotState>();
		readonly Lazy<HiddenUnderFog> huf;
		readonly Lazy<FrozenUnderFog> fuf;
		readonly Lazy<Disguise> disguise;
		readonly Lazy<Cloak> cloak;
		readonly Cache<Player, FrozenActorLayer> frozen;

		class DotState
		{
			public readonly GpsWatcher Gps;
			public bool IsVisible;
			public DotState(GpsWatcher gps)
			{
				Gps = gps;
			}
		}

		public GpsDot(Actor self, GpsDotInfo info)
		{
			this.self = self;
			this.info = info;
			anim = new Animation(self.World, "gpsdot");
			anim.PlayRepeating(info.String);

			self.World.AddFrameEndTask(w => w.Add(this));

			huf = Exts.Lazy(() => self.TraitOrDefault<HiddenUnderFog>());
			fuf = Exts.Lazy(() => self.TraitOrDefault<FrozenUnderFog>());
			disguise = Exts.Lazy(() => self.TraitOrDefault<Disguise>());
			cloak = Exts.Lazy(() => self.TraitOrDefault<Cloak>());

			frozen = new Cache<Player, FrozenActorLayer>(p => p.PlayerActor.Trait<FrozenActorLayer>());

			stateByPlayer = self.World.Players.ToDictionary(p => p, p => new DotState(p.PlayerActor.Trait<GpsWatcher>()));
		}

		public bool IsDotVisible(Player toPlayer)
		{
			return stateByPlayer[toPlayer].IsVisible;
		}

		bool ShouldShowIndicator(Player toPlayer)
		{
			if (cloak.Value != null && cloak.Value.Cloaked)
				return false;

			if (disguise.Value != null && disguise.Value.Disguised)
				return false;

			if (huf.Value != null && !huf.Value.IsVisible(self, toPlayer)
				&& toPlayer.Shroud.IsExplored(self.CenterPosition))
				return true;

			if (fuf.Value == null)
				return false;

			var f = frozen[toPlayer].FromID(self.ActorID);
			if (f == null)
				return false;

			if (f.HasRenderables || f.NeedRenderables)
				return false;

			return f.Visible && !f.Shrouded;
		}

		public void Tick(World world)
		{
			if (self.Disposed)
				world.AddFrameEndTask(w => w.Remove(this));

			if (!self.IsInWorld || self.IsDead)
				return;

			foreach (var player in self.World.Players)
			{
				var state = stateByPlayer[player];
				state.IsVisible = (state.Gps.Granted || state.Gps.GrantedAllies) && ShouldShowIndicator(player);
			}
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (self.World.RenderPlayer == null || !IsDotVisible(self.World.RenderPlayer) || self.Disposed)
				return SpriteRenderable.None;

			var palette = wr.Palette(info.IndicatorPalettePrefix + self.Owner.InternalName);
			return anim.Render(self.CenterPosition, palette);
		}
	}
}
