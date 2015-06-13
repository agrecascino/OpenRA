#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("The actor stays invisible under the shroud.")]
	public class HiddenUnderShroudInfo : ITraitInfo, IDefaultVisibilityInfo
	{
		[Desc("Players with these stances can always see the actor.")]
		public readonly Stance AlwaysVisibleStances = Stance.Ally;

		public virtual object Create(ActorInitializer init) { return new HiddenUnderShroud(this); }
	}

	public class HiddenUnderShroud : IDefaultVisibility, IRenderModifier
	{
		readonly HiddenUnderShroudInfo info;

		public HiddenUnderShroud(HiddenUnderShroudInfo info)
		{
			this.info = info;
		}

		protected IEnumerable<CPos> VisibilityFootprint(Actor self)
		{
			return Shroud.GetVisOrigins(self);
		}

		protected virtual bool IsVisibleInner(Actor self, Player byPlayer)
		{
			return VisibilityFootprint(self).Any(byPlayer.Shroud.IsExplored);
		}

		public bool IsVisible(Actor self, Player byPlayer)
		{
			if (byPlayer == null)
				return true;

			var stance = self.Owner.Stances[byPlayer];
			return info.AlwaysVisibleStances.HasFlag(stance) || IsVisibleInner(self, byPlayer);
		}

		public IEnumerable<IRenderable> ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> r)
		{
			return IsVisible(self, self.World.RenderPlayer) ? r : SpriteRenderable.None;
		}
	}
}
