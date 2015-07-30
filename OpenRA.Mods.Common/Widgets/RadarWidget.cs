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
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class RadarWidget : Widget
	{
		public string WorldInteractionController = null;
		public int AnimationLength = 5;
		public string RadarOnlineSound = null;
		public string RadarOfflineSound = null;
		public Func<bool> IsEnabled = () => true;
		public Action AfterOpen = () => { };
		public Action AfterClose = () => { };
		public Action<float> Animating = _ => { };

		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly RadarPings radarPings;

		readonly HashSet<PPos> dirtyShroudCells = new HashSet<PPos>();

		float radarMinimapHeight;
		int frame;
		bool hasRadar;
		bool cachedEnabled;

		float previewScale = 0;
		int2 previewOrigin = int2.Zero;
		Rectangle mapRect = Rectangle.Empty;

		Sheet radarSheet;
		byte[] radarData;

		Sprite terrainSprite;
		Sprite actorSprite;
		Sprite shroudSprite;
		Shroud renderShroud;

		[ObjectCreator.UseCtor]
		public RadarWidget(World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			radarPings = world.WorldActor.TraitOrDefault<RadarPings>();
		}

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);

			// The four layers are stored in a 2x2 grid within a single texture
			var s = world.Map.MapSize;
			radarSheet = new Sheet(new Size(2 * s.X, 2 * s.Y).NextPowerOf2());
			radarSheet.CreateBuffer();
			radarData = radarSheet.GetData();

			MapBoundsChanged();

			// Set initial terrain data
			foreach (var cell in world.Map.AllCells)
				UpdateTerrainCell(cell);

			world.Map.MapTiles.Value.CellEntryChanged += UpdateTerrainCell;
			world.Map.CustomTerrain.CellEntryChanged += UpdateTerrainCell;
		}

		void MapBoundsChanged()
		{
			var b = world.Map.Bounds;
			var rb = RenderBounds;
			previewScale = Math.Min(rb.Width * 1f / b.Width, rb.Height * 1f / b.Height);
			previewOrigin = new int2((int)((rb.Width - previewScale * b.Width) / 2), (int)((rb.Height - previewScale * b.Height) / 2));
			mapRect = new Rectangle(previewOrigin.X, previewOrigin.Y, (int)(previewScale * b.Width), (int)(previewScale * b.Height));

			var s = world.Map.MapSize;
			terrainSprite = new Sprite(radarSheet, b, TextureChannel.Alpha);
			shroudSprite = new Sprite(radarSheet, new Rectangle(b.Location + new Size(s.X, 0), b.Size), TextureChannel.Alpha);
			actorSprite = new Sprite(radarSheet, new Rectangle(b.Location + new Size(0, s.Y), b.Size), TextureChannel.Alpha);
		}

		void UpdateTerrainCell(CPos cell)
		{
			var uv = cell.ToMPos(world.Map);

			if (!world.Map.CustomTerrain.Contains(uv))
				return;

			var custom = world.Map.CustomTerrain[uv];
			Color color;
			if (custom == byte.MaxValue)
			{
				var type = world.TileSet.GetTileInfo(world.Map.MapTiles.Value[uv]);
				color = type != null ? type.LeftColor : Color.Black;
			}
			else
				color = world.TileSet[custom].Color;

			var stride = radarSheet.Size.Width;

			unsafe
			{
				fixed (byte* colorBytes = &radarData[0])
				{
					var colors = (int*)colorBytes;
					colors[uv.V * stride + uv.U] = color.ToArgb();
				}
			}
		}

		void UpdateShroudCell(PPos projectedCell)
		{
			var stride = radarSheet.Size.Width;
			var dx = world.Map.MapSize.X;

			var color = 0;
			var rp = world.RenderPlayer;
			if (rp != null)
			{
				if (!rp.Shroud.IsExplored(projectedCell))
					color = Color.Black.ToArgb();
				else if (!rp.Shroud.IsVisible(projectedCell))
					color = Color.FromArgb(128, Color.Black).ToArgb();
			}

			unsafe
			{
				fixed (byte* colorBytes = &radarData[0])
				{
					var colors = (int*)colorBytes;
					colors[projectedCell.V * stride + projectedCell.U + dx] = color;
				}
			}
		}

		void MarkShroudDirty(IEnumerable<PPos> projectedCellsChanged)
		{
			dirtyShroudCells.UnionWith(projectedCellsChanged);
		}

		public override string GetCursor(int2 pos)
		{
			if (world == null || !hasRadar)
				return null;

			var cell = MinimapPixelToCell(pos);
			var location = worldRenderer.Viewport.WorldToViewPx(worldRenderer.ScreenPxPosition(world.Map.CenterOfCell(cell)));

			var mi = new MouseInput
			{
				Location = location,
				Button = MouseButton.Right,
				Modifiers = Game.GetModifierKeys()
			};

			var cursor = world.OrderGenerator.GetCursor(world, cell, mi);
			if (cursor == null)
				return "default";

			return Game.ModData.CursorProvider.HasCursorSequence(cursor + "-minimap") ? cursor + "-minimap" : cursor;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (!mapRect.Contains(mi.Location))
				return false;

			if (!hasRadar)
				return true;

			var cell = MinimapPixelToCell(mi.Location);
			var pos = world.Map.CenterOfCell(cell);
			if ((mi.Event == MouseInputEvent.Down || mi.Event == MouseInputEvent.Move) && mi.Button == MouseButton.Left)
				worldRenderer.Viewport.Center(pos);

			if (mi.Event == MouseInputEvent.Down && mi.Button == MouseButton.Right)
			{
				// fake a mousedown/mouseup here
				var location = worldRenderer.Viewport.WorldToViewPx(worldRenderer.ScreenPxPosition(pos));
				var fakemi = new MouseInput
				{
					Event = MouseInputEvent.Down,
					Button = MouseButton.Right,
					Modifiers = mi.Modifiers,
					Location = location
				};

				if (WorldInteractionController != null)
				{
					var controller = Ui.Root.Get<WorldInteractionControllerWidget>(WorldInteractionController);
					controller.HandleMouseInput(fakemi);
					fakemi.Event = MouseInputEvent.Up;
					controller.HandleMouseInput(fakemi);
				}
			}

			return true;
		}

		public override void Draw()
		{
			if (world == null)
				return;

			if (renderShroud != null)
			{
				foreach (var cell in dirtyShroudCells)
					UpdateShroudCell(cell);
				dirtyShroudCells.Clear();
			}

			radarSheet.CommitBufferedData();

			var o = new float2(mapRect.Location.X, mapRect.Location.Y + world.Map.Bounds.Height * previewScale * (1 - radarMinimapHeight) / 2);
			var s = new float2(mapRect.Size.Width, mapRect.Size.Height * radarMinimapHeight);

			var rsr = Game.Renderer.RgbaSpriteRenderer;
			rsr.DrawSprite(terrainSprite, o, s);
			rsr.DrawSprite(actorSprite, o, s);

			if (renderShroud != null)
				rsr.DrawSprite(shroudSprite, o, s);

			// Draw viewport rect
			if (hasRadar)
			{
				var tl = CellToMinimapPixel(world.Map.CellContaining(worldRenderer.ProjectedPosition(worldRenderer.Viewport.TopLeft)));
				var br = CellToMinimapPixel(world.Map.CellContaining(worldRenderer.ProjectedPosition(worldRenderer.Viewport.BottomRight)));

				Game.Renderer.EnableScissor(mapRect);
				DrawRadarPings();
				Game.Renderer.LineRenderer.DrawRect(tl, br, Color.White);
				Game.Renderer.DisableScissor();
			}
		}

		void DrawRadarPings()
		{
			if (radarPings == null)
				return;

			var lr = Game.Renderer.LineRenderer;
			var oldWidth = lr.LineWidth;
			lr.LineWidth = 2;

			foreach (var radarPing in radarPings.Pings.Where(e => e.IsVisible()))
			{
				var c = radarPing.Color;
				var pingCell = world.Map.CellContaining(radarPing.Position);
				var points = radarPing.Points(CellToMinimapPixel(pingCell)).ToArray();

				lr.DrawLine(points[0], points[1], c);
				lr.DrawLine(points[1], points[2], c);
				lr.DrawLine(points[2], points[0], c);
			}

			lr.LineWidth = oldWidth;
		}

		public override void Tick()
		{
			// Enable/Disable the radar
			var enabled = IsEnabled();
			if (enabled != cachedEnabled)
				Sound.Play(enabled ? RadarOnlineSound : RadarOfflineSound);
			cachedEnabled = enabled;

			if (enabled)
			{
				var rp = world.RenderPlayer;
				var newRenderShroud = rp != null ? rp.Shroud : null;
				if (newRenderShroud != renderShroud)
				{
					if (renderShroud != null)
						renderShroud.CellsChanged -= MarkShroudDirty;

					if (newRenderShroud != null)
					{
						// Redraw the full shroud sprite
						MarkShroudDirty(world.Map.AllCells.MapCoords.Select(uv => (PPos)uv));

						// Update the notification binding
						newRenderShroud.CellsChanged += MarkShroudDirty;
					}

					renderShroud = newRenderShroud;
				}

				// The actor layer is updated every tick
				var stride = radarSheet.Size.Width;
				var dy = world.Map.MapSize.Y;

				Array.Clear(radarData, 4 * (actorSprite.Bounds.Top * stride + actorSprite.Bounds.Left), 4 * actorSprite.Bounds.Height * stride);

				unsafe
				{
					fixed (byte* colorBytes = &radarData[0])
					{
						var colors = (int*)colorBytes;

						foreach (var t in world.ActorsWithTrait<IRadarSignature>())
						{
							if (!t.Actor.IsInWorld || world.FogObscures(t.Actor))
								continue;

							foreach (var cell in t.Trait.RadarSignatureCells(t.Actor))
							{
								var uv = cell.First.ToMPos(world.Map);

								if (world.Map.Bounds.Contains(uv.U, uv.V))
									colors[(uv.V + dy) * stride + uv.U] = cell.Second.ToArgb();
							}
						}
					}
				}
			}

			var targetFrame = enabled ? AnimationLength : 0;
			hasRadar = enabled && frame == AnimationLength;
			if (frame == targetFrame)
				return;

			frame += enabled ? 1 : -1;
			radarMinimapHeight = float2.Lerp(0, 1, (float)frame / AnimationLength);

			Animating(frame * 1f / AnimationLength);

			// Update map rectangle for event handling
			var ro = RenderOrigin;
			mapRect = new Rectangle(previewOrigin.X + ro.X, previewOrigin.Y + ro.Y, mapRect.Width, mapRect.Height);

			// Animation is complete
			if (frame == targetFrame)
			{
				if (enabled)
					AfterOpen();
				else
					AfterClose();
			}
		}

		int2 CellToMinimapPixel(CPos p)
		{
			var uv = p.ToMPos(world.Map);
			var mapOffset = new float2(uv.U - world.Map.Bounds.Left, uv.V - world.Map.Bounds.Top);
			return new int2(mapRect.X, mapRect.Y) + (previewScale * mapOffset).ToInt2();
		}

		CPos MinimapPixelToCell(int2 p)
		{
			var viewOrigin = new float2(mapRect.X, mapRect.Y);
			var mapOrigin = new float2(world.Map.Bounds.Left, world.Map.Bounds.Top);
			var fcell = mapOrigin + (1f / previewScale) * (p - viewOrigin);
			return new MPos((int)fcell.X, (int)fcell.Y).ToCPos(world.Map);
		}
	}
}
