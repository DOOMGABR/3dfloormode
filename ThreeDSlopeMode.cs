
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * Copyright (c) 2014 Boris Iwanski
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.Collections;
using System.Drawing;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Linq;
using CodeImp.DoomBuilder.Windows;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.BuilderModes;
using CodeImp.DoomBuilder.GZBuilder.Geometry;

#endregion

namespace CodeImp.DoomBuilder.ThreeDFloorMode
{
    public class SlopeObject
    {
        private ThreeDFloor threedfloor;
        private Vector2D position;
		private bool isorigin;

        public ThreeDFloor ThreeDFloor { get { return threedfloor; } set { threedfloor = value; } }
        public Vector2D Position { get { return position; } set { position = value; } }
		public bool IsOrigin { get { return isorigin; } set { isorigin = value; } }
    }
    
    [EditMode(DisplayName = "3D Slope Mode",
              SwitchAction = "threedslopemode",		// Action name used to switch to this mode
              ButtonImage = "ThreeDFloorIcon.png",	// Image resource name for the button
              ButtonOrder = int.MinValue + 501,	// Position of the button (lower is more to the left)
              ButtonGroup = "000_editing",
              UseByDefault = true,
              SafeStartMode = true)]

    public class ThreeDSlopeMode : ClassicMode
	{
		#region ================== Constants

		#endregion

		#region ================== Variables

		// Highlighted item
		private Thing highlighted;
        private SlopeObject highlightedslope;
		private Association[] association = new Association[Thing.NUM_ARGS];
		private Association highlightasso = new Association();

		// Interface
		private bool editpressed;
		private bool thinginserted;

        private List<ThreeDFloor> threedfloors;
        private List<SlopeObject> slopeobjects;
		bool dragging = false;
		
		#endregion

		#region ================== Properties

		public override object HighlightedObject { get { return highlighted; } }

		#endregion

		#region ================== Constructor / Disposer

		#endregion

		#region ================== Methods

		public override void OnHelp()
		{
			General.ShowHelp("e_things.html");
		}

		// Cancel mode
		public override void OnCancel()
		{
			base.OnCancel();

			// Return to this mode
			General.Editing.ChangeMode(new ThingsMode());
		}

		// Mode engages
		public override void OnEngage()
		{
            base.OnEngage();
            renderer.SetPresentation(Presentation.Things);

            // Convert geometry selection to linedefs selection
            General.Map.Map.ConvertSelection(SelectionType.Linedefs);
            General.Map.Map.SelectionType = SelectionType.Things;

            // Get all 3D floors in the map
            threedfloors = BuilderPlug.GetThreeDFloors(General.Map.Map.Sectors.ToList());

			UpdateSlopeObjects();
		}

		// Mode disengages
		public override void OnDisengage()
		{
			base.OnDisengage();
			
			// Going to EditSelectionMode?
			if(General.Editing.NewMode is EditSelectionMode)
			{
				// Not pasting anything?
				EditSelectionMode editmode = (General.Editing.NewMode as EditSelectionMode);
				if(!editmode.Pasting)
				{
					// No selection made? But we have a highlight!
					if((General.Map.Map.GetSelectedThings(true).Count == 0) && (highlighted != null))
					{
						// Make the highlight the selection
						highlighted.Selected = true;
					}
				}
			}

			// Hide highlight info
			General.Interface.HideInfo();
		}

		// This redraws the display
		public override void OnRedrawDisplay()
		{
			renderer.RedrawSurface();

			// Render lines and vertices
			if(renderer.StartPlotter(true))
			{
				renderer.PlotLinedefSet(General.Map.Map.Linedefs);
				renderer.PlotVerticesSet(General.Map.Map.Vertices);

				if (highlightedslope != null)
				{
					foreach(Sector s in highlightedslope.ThreeDFloor.TaggedSectors)
						renderer.PlotSector(s, General.Colors.Highlight);
				}

				//for(int i = 0; i < Thing.NUM_ARGS; i++) BuilderPlug.Me.PlotAssociations(renderer, association[i]);
				//if((highlighted != null) && !highlighted.IsDisposed) BuilderPlug.Me.PlotReverseAssociations(renderer, highlightasso);
				renderer.Finish();
			}

			// Render things
			if(renderer.StartThings(true))
			{
				renderer.RenderThingSet(General.Map.ThingsFilter.HiddenThings, Presentation.THINGS_HIDDEN_ALPHA);
				renderer.RenderThingSet(General.Map.ThingsFilter.VisibleThings, 1.0f);
				//for(int i = 0; i < Thing.NUM_ARGS; i++) BuilderPlug.Me.RenderAssociations(renderer, association[i]);
				if((highlighted != null) && !highlighted.IsDisposed)
				{
					//BuilderPlug.Me.RenderReverseAssociations(renderer, highlightasso);
					renderer.RenderThing(highlighted, General.Colors.Highlight, 1.0f);
				}
				renderer.Finish();
			}

			// Selecting?
			if(selecting)
			{
				// Render selection
				if(renderer.StartOverlay(true))
				{
					RenderMultiSelection();
					renderer.Finish();
				}
			}

            UpdateOverlay();

			renderer.Present();
		}

		private void UpdateSlopeObjects()
		{
			slopeobjects = new List<SlopeObject>();

			foreach (ThreeDFloor tdf in threedfloors)
			{
				if (!tdf.Slope.TopSloped || !tdf.Slope.BottomSloped)
					continue;

				SlopeObject so = new SlopeObject();
				so.ThreeDFloor = tdf;
				so.Position = tdf.Slope.Origin;
				so.IsOrigin = true;
				slopeobjects.Add(so);

				so = new SlopeObject();
				so.ThreeDFloor = tdf;
				so.Position = tdf.Slope.Origin + tdf.Slope.Direction;
				so.IsOrigin = false;
				slopeobjects.Add(so);
			}
		}

		// This updates the overlay
        private void UpdateOverlay()
        {
            float size = 9 / renderer.Scale;
            if (renderer.StartOverlay(true))
            {
                foreach (ThreeDFloor tdf in threedfloors)
                {
                    if (!tdf.Slope.TopSloped || !tdf.Slope.BottomSloped)
                        continue;

                    Vector3D v1 = new Vector3D(tdf.Slope.Origin);
                    Vector3D v2 = new Vector3D(tdf.Slope.Origin + tdf.Slope.Direction);
                    byte a = 64;

                    //if (tdf.TaggedSectors.Contains(highlighted))
                    //a = 192;

                    renderer.RenderArrow(new Line3D(v1, v2), new PixelColor(255, 255, 255, 255));
                }

                foreach (SlopeObject so in slopeobjects)
                {
                    renderer.RenderRectangleFilled(new RectangleF(so.Position.x - size / 2, so.Position.y - size / 2, size, size), General.Colors.Background, true);
					renderer.RenderRectangle(new RectangleF(so.Position.x - size / 2, so.Position.y - size / 2, size, size), 2, General.Colors.Indication, true);
                }

				if (highlightedslope != null)
				{
					Vector3D v1 = new Vector3D(highlightedslope.ThreeDFloor.Slope.Origin);
					Vector3D v2 = new Vector3D(highlightedslope.ThreeDFloor.Slope.Origin + highlightedslope.ThreeDFloor.Slope.Direction);

					renderer.RenderArrow(new Line3D(v1, v2), General.Colors.Indication);

					renderer.RenderRectangleFilled(new RectangleF(highlightedslope.Position.x - size / 2, highlightedslope.Position.y - size / 2, size, size), General.Colors.Background, true);
					renderer.RenderRectangle(new RectangleF(highlightedslope.Position.x - size / 2, highlightedslope.Position.y - size / 2, size, size), 2, General.Colors.Highlight, true);
				}

                renderer.Finish();
            }           
        }
		
		// This highlights a new item
		protected void Highlight(Thing t)
		{
			bool completeredraw = false;
			LinedefActionInfo action = null;

			// Often we can get away by simply undrawing the previous
			// highlight and drawing the new highlight. But if associations
			// are or were drawn we need to redraw the entire display.

			// Previous association highlights something?
			if((highlighted != null) && (highlighted.Tag > 0)) completeredraw = true;
			
			// Set highlight association
			if(t != null)
				highlightasso.Set(t.Position, t.Tag, UniversalType.ThingTag);
			else
                highlightasso.Set(new Vector2D(), 0, 0);

			// New association highlights something?
			if((t != null) && (t.Tag > 0)) completeredraw = true;

			if(t != null)
			{
				// Check if we can find the linedefs action
				if((t.Action > 0) && General.Map.Config.LinedefActions.ContainsKey(t.Action))
					action = General.Map.Config.LinedefActions[t.Action];
			}
			
			// Determine linedef associations
			for(int i = 0; i < Thing.NUM_ARGS; i++)
			{
				// Previous association highlights something?
				if((association[i].type == UniversalType.SectorTag) ||
				   (association[i].type == UniversalType.LinedefTag) ||
				   (association[i].type == UniversalType.ThingTag)) completeredraw = true;
				
				// Make new association
				if(action != null)
					association[i].Set(t.Position, t.Args[i], action.Args[i].Type);
				else
					association[i].Set(new Vector2D(), 0, 0);
				
				// New association highlights something?
				if((association[i].type == UniversalType.SectorTag) ||
				   (association[i].type == UniversalType.LinedefTag) ||
				   (association[i].type == UniversalType.ThingTag)) completeredraw = true;
			}
			
			// If we're changing associations, then we
			// need to redraw the entire display
			if(completeredraw)
			{
				// Set new highlight and redraw completely
				highlighted = t;
				General.Interface.RedrawDisplay();
			}
			else
			{
				// Update display
				if(renderer.StartThings(false))
				{
					// Undraw previous highlight
					if((highlighted != null) && !highlighted.IsDisposed)
						renderer.RenderThing(highlighted, renderer.DetermineThingColor(highlighted), 1.0f);

					// Set new highlight
					highlighted = t;

					// Render highlighted item
					if((highlighted != null) && !highlighted.IsDisposed)
						renderer.RenderThing(highlighted, General.Colors.Highlight, 1.0f);

					// Done
					renderer.Finish();
					renderer.Present();
				}
			}
			
			// Show highlight info
			if((highlighted != null) && !highlighted.IsDisposed)
				General.Interface.ShowThingInfo(highlighted);
			else
				General.Interface.HideInfo();
		}

		// Selection
		protected override void OnSelectBegin()
		{
			// Item highlighted?
			if((highlighted != null) && !highlighted.IsDisposed)
			{
				// Flip selection
				highlighted.Selected = !highlighted.Selected;

				// Update display
				if(renderer.StartThings(false))
				{
					// Redraw highlight to show selection
					renderer.RenderThing(highlighted, renderer.DetermineThingColor(highlighted), 1.0f);
					renderer.Finish();
					renderer.Present();
				}
			}
			else
			{
				// Start making a selection
				StartMultiSelection();
			}

			base.OnSelectBegin();
		}

		// End selection
		protected override void OnSelectEnd()
		{
			// Not ending from a multi-selection?
			if(!selecting)
			{
				// Item highlighted?
				if((highlighted != null) && !highlighted.IsDisposed)
				{
					// Update display
					if(renderer.StartThings(false))
					{
						// Render highlighted item
						renderer.RenderThing(highlighted, General.Colors.Highlight, 1.0f);
						renderer.Finish();
						renderer.Present();
					}
				}
			}

			base.OnSelectEnd();
		}

		// Start editing
		protected override void OnEditBegin()
		{
			base.OnEditBegin();
		}

		// Done editing
		protected override void OnEditEnd()
		{
			base.OnEditEnd();
		}

		// Mouse moves
		public override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			// Not holding any buttons?
			if(e.Button == MouseButtons.None)
			{
				// Find the nearest thing within highlight range
				Thing t = MapSet.NearestThingSquareRange(General.Map.ThingsFilter.VisibleThings, mousemappos, BuilderPlug.Me.HighlightSlopeRange / renderer.Scale);

				// Highlight if not the same
				if(t != highlighted) Highlight(t);

                float distance = float.MaxValue;
                float d;
				SlopeObject hso = null;

                foreach (SlopeObject so in slopeobjects)
                {
                    d = Vector2D.Distance(so.Position, mousemappos);

                    if (d <= BuilderModes.BuilderPlug.Me.HighlightRange / renderer.Scale && d < distance)
                    {
                        distance = d;
                        hso = so;
                    }
                }

				if (hso != highlightedslope)
				{
					highlightedslope = hso;
					UpdateOverlay();
					General.Interface.RedrawDisplay();
				}
			}
			else if (dragging && highlightedslope != null)
			{
				highlightedslope.Position = GridSetup.SnappedToGrid(mousemappos, General.Map.Grid.GridSizeF, 1.0f / General.Map.Grid.GridSizeF);

				UpdateOverlay();
				General.Interface.RedrawDisplay();
			}
		}

		// Mouse leaves
		public override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);

			// Highlight nothing
			Highlight(null);
		}

		// Mouse wants to drag
		protected override void OnDragStart(MouseEventArgs e)
		{
			base.OnDragStart(e);

			if (e.Button == MouseButtons.Right)
				dragging = true;
		}

		// Mouse wants to drag
		protected override void OnDragStop(MouseEventArgs e)
		{
			base.OnDragStop(e);

			if (highlightedslope != null)
			{
				if (highlightedslope.IsOrigin)
				{
					Vector2D v = highlightedslope.ThreeDFloor.Slope.Origin + highlightedslope.ThreeDFloor.Slope.Direction;

					highlightedslope.ThreeDFloor.Slope.Origin = highlightedslope.Position;
					highlightedslope.ThreeDFloor.Slope.Direction = v - highlightedslope.Position;
				}
				else
				{
					highlightedslope.ThreeDFloor.Slope.Direction = highlightedslope.Position - highlightedslope.ThreeDFloor.Slope.Origin;
				}

				highlightedslope.ThreeDFloor.Rebuild = true;

				BuilderPlug.ProcessThreeDFloors(new List<ThreeDFloor> { highlightedslope.ThreeDFloor }, highlightedslope.ThreeDFloor.TaggedSectors);

				UpdateOverlay();
				General.Interface.RedrawDisplay();

	
			}

			dragging = false;
		}


		// This is called wheh selection ends
		protected override void OnEndMultiSelection()
		{
			bool selectionvolume = ((Math.Abs(base.selectionrect.Width) > 0.1f) && (Math.Abs(base.selectionrect.Height) > 0.1f));

			if(BuilderPlug.Me.AutoClearSelection && !selectionvolume)
				General.Map.Map.ClearSelectedThings();

			if(selectionvolume)
			{
				if(General.Interface.ShiftState ^ BuilderPlug.Me.AdditiveSelect)
				{
					// Go for all things
					foreach(Thing t in General.Map.ThingsFilter.VisibleThings)
					{
						t.Selected |= ((t.Position.x >= selectionrect.Left) &&
									   (t.Position.y >= selectionrect.Top) &&
									   (t.Position.x <= selectionrect.Right) &&
									   (t.Position.y <= selectionrect.Bottom));
					}
				}
				else
				{
					// Go for all things
					foreach(Thing t in General.Map.ThingsFilter.VisibleThings)
					{
						t.Selected = ((t.Position.x >= selectionrect.Left) &&
									  (t.Position.y >= selectionrect.Top) &&
									  (t.Position.x <= selectionrect.Right) &&
									  (t.Position.y <= selectionrect.Bottom));
					}
				}
			}
			
			base.OnEndMultiSelection();

			// Clear overlay
			if(renderer.StartOverlay(true)) renderer.Finish();

			// Redraw
			General.Interface.RedrawDisplay();
		}

		// This is called when the selection is updated
		protected override void OnUpdateMultiSelection()
		{
			base.OnUpdateMultiSelection();

			// Render selection
			if(renderer.StartOverlay(true))
			{
				RenderMultiSelection();
				renderer.Finish();
				renderer.Present();
			}
		}

		// When copying
		public override bool OnCopyBegin()
		{
			// No selection made? But we have a highlight!
			if((General.Map.Map.GetSelectedThings(true).Count == 0) && (highlighted != null))
			{
				// Make the highlight the selection
				highlighted.Selected = true;
			}

			return base.OnCopyBegin();
		}

		#endregion

		#region ================== Actions

		[BeginAction("threedflipslope")]
		public void FlipSlope()
		{
			if (highlightedslope == null)
				return;

			Vector2D origin;
			Vector2D direction;

			if (highlightedslope.IsOrigin)
			{
				origin = highlightedslope.ThreeDFloor.Slope.Origin + highlightedslope.ThreeDFloor.Slope.Direction;
				direction = highlightedslope.ThreeDFloor.Slope.Direction * (-1);
			}
			else 
			{
				origin = highlightedslope.ThreeDFloor.Slope.Origin + highlightedslope.ThreeDFloor.Slope.Direction;
				direction = highlightedslope.ThreeDFloor.Slope.Direction * (-1);
			}

			highlightedslope.ThreeDFloor.Slope.Origin = origin;
			highlightedslope.ThreeDFloor.Slope.Direction = direction;

			highlightedslope.ThreeDFloor.Rebuild = true;

			BuilderPlug.ProcessThreeDFloors(new List<ThreeDFloor> { highlightedslope.ThreeDFloor }, highlightedslope.ThreeDFloor.TaggedSectors);

			UpdateSlopeObjects();

			// Redraw
			General.Interface.RedrawDisplay();
		}

		// This clears the selection
		[BeginAction("clearselection", BaseAction = true)]
		public void ClearSelection()
		{
			// Clear selection
			General.Map.Map.ClearAllSelected();

			// Redraw
			General.Interface.RedrawDisplay();
		}
		
		// This creates a new thing
		private Thing InsertThing(Vector2D pos)
		{
			if (pos.x < General.Map.Config.LeftBoundary || pos.x > General.Map.Config.RightBoundary ||
				pos.y > General.Map.Config.TopBoundary || pos.y < General.Map.Config.BottomBoundary)
			{
				General.Interface.DisplayStatus(StatusType.Warning, "Failed to insert thing: outside of map boundaries.");
				return null;
			}

			// Create thing
			Thing t = General.Map.Map.CreateThing();
			if(t != null)
			{
				General.Settings.ApplyDefaultThingSettings(t);
				
				t.Move(pos);
				
				t.UpdateConfiguration();

				// Update things filter so that it includes this thing
				General.Map.ThingsFilter.Update();

				// Snap to grid enabled?
				if(General.Interface.SnapToGrid)
				{
					// Snap to grid
					t.SnapToGrid();
				}
				else
				{
					// Snap to map format accuracy
					t.SnapToAccuracy();
				}
			}
			
			return t;
		}

		[BeginAction("deleteitem", BaseAction = true)]
		public void DeleteItem()
		{
			// Make list of selected things
			List<Thing> selected = new List<Thing>(General.Map.Map.GetSelectedThings(true));
			if((selected.Count == 0) && (highlighted != null) && !highlighted.IsDisposed) selected.Add(highlighted);
			
			// Anything to do?
			if(selected.Count > 0)
			{
				// Make undo
				if(selected.Count > 1)
				{
					General.Map.UndoRedo.CreateUndo("Delete " + selected.Count + " things");
					General.Interface.DisplayStatus(StatusType.Action, "Deleted " + selected.Count + " things.");
				}
				else
				{
					General.Map.UndoRedo.CreateUndo("Delete thing");
					General.Interface.DisplayStatus(StatusType.Action, "Deleted a thing.");
				}

				// Dispose selected things
				foreach(Thing t in selected) t.Dispose();
				
				// Update cache values
				General.Map.IsChanged = true;
				General.Map.ThingsFilter.Update();

				// Invoke a new mousemove so that the highlighted item updates
				MouseEventArgs e = new MouseEventArgs(MouseButtons.None, 0, (int)mousepos.x, (int)mousepos.y, 0);
				OnMouseMove(e);

				// Redraw screen
				General.Interface.RedrawDisplay();
			}
		}
		
		#endregion
	}
}