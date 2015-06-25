﻿#region Copyright & License Information
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

namespace OpenRA.Traits
{
	public static class SelectableExts
	{
		public static int SelectionPriority(this ActorInfo a)
		{
			var selectableInfo = a.Traits.GetOrDefault<SelectableInfo>();
			return selectableInfo != null ? selectableInfo.Priority : int.MinValue;
		}

		const int PriorityRange = 30;

		public static int SelectionPriority(this Actor a)
		{
			var basePriority = a.Info.Traits.Get<SelectableInfo>().Priority;
			var lp = a.World.LocalPlayer;

			if (a.Owner == lp || lp == null)
				return basePriority;

			switch (lp.Stances[a.Owner])
			{
				case Stance.Ally: return basePriority - PriorityRange;
				case Stance.Neutral: return basePriority - 2 * PriorityRange;
				case Stance.Enemy: return basePriority - 3 * PriorityRange;

				default:
					throw new InvalidOperationException();
			}
		}

		public static Actor WithHighestSelectionPriority(this IEnumerable<Actor> actors)
		{
			return actors.MaxByOrDefault(a => a.Info.SelectionPriority());
		}

		public static FrozenActor WithHighestSelectionPriority(this IEnumerable<FrozenActor> actors)
		{
			return actors.MaxByOrDefault(a => a.Info.SelectionPriority());
		}

		static readonly Actor[] NoActors = { };

		public static IEnumerable<Actor> SubsetWithHighestSelectionPriority(this IEnumerable<Actor> actors)
		{
			return actors.GroupBy(x => x.SelectionPriority())
				.OrderByDescending(g => g.Key)
				.Select(g => g.AsEnumerable())
				.DefaultIfEmpty(NoActors)
				.FirstOrDefault();
		}
	}
}
