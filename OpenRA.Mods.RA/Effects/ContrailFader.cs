#region Copyright & License Information
/*
 * Copyright 2007-2013 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Effects;
using OpenRA.Graphics;

namespace OpenRA.Mods.RA.Effects
{
	class ContrailFader : IEffect
	{
		WPos pos;
		ContrailRenderable trail;

		public ContrailFader(WPos pos, ContrailRenderable trail)
		{
			this.pos = pos;
			this.trail = trail;
		}

		public void Tick(World world)
		{
			trail.Update(pos);
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			yield return trail;
		}
	}
}
