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
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class InstallLogic : Widget
	{
		[ObjectCreator.UseCtor]
		public InstallLogic(Widget widget, Action continueLoading, string mirrorListUrl, string modId)
		{
			var panel = widget.Get("INSTALL_PANEL");
			var widgetArgs = new WidgetArgs
			{
				{ "afterInstall", () => { Ui.CloseWindow(); continueLoading(); } },
				{ "continueLoading", continueLoading },
				{ "mirrorListUrl", mirrorListUrl },
				{ "modId", modId }
			};

			var mod = ModMetadata.AllMods[modId];
			var text = "OpenRA requires the original {0} game content.".F(mod.Title);
			panel.Get<LabelWidget>("DESC1").Text = text;

			var downloadButton = panel.Get<ButtonWidget>("DOWNLOAD_BUTTON");
			downloadButton.OnClick = () => Ui.OpenWindow("INSTALL_DOWNLOAD_PANEL", widgetArgs);
			downloadButton.IsDisabled = () => string.IsNullOrEmpty(mod.Content.PackageMirrorList);

			panel.Get<ButtonWidget>("INSTALL_BUTTON").OnClick = () =>
				Ui.OpenWindow("INSTALL_FROMCD_PANEL", widgetArgs);

			panel.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Game.RunAfterTick(() =>
				{
					Game.Settings.Game.PreviousMod = Game.ModData.Manifest.Mod.Id;
					Game.InitializeMod("modchooser", null);
				});
			};
		}
	}
}
