﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using CodeImp.DoomBuilder.Windows;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Rendering;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Editing;
using CodeImp.DoomBuilder.Plugins;
using CodeImp.DoomBuilder.Actions;
using CodeImp.DoomBuilder.Types;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.Data;

namespace CodeImp.DoomBuilder.ThreeDFloorHelper
{
	public partial class ThreeDFloorHelperControl : UserControl
	{
		private ThreeDFloor threeDFloor;
		public Linedef linedef;
		public Sector sector;
		private bool isnew;

		public ThreeDFloor ThreeDFloor { get { return threeDFloor; } }
		public bool IsNew { get { return isnew; } }

		// Create the control from an existing linedef
		public ThreeDFloorHelperControl(ThreeDFloor threeDFloor)
		{
			InitializeComponent();

			isnew = false;

			this.threeDFloor = threeDFloor;

			sectorBorderTexture.TextureName = threeDFloor.BorderTexture;
			sectorTopFlat.TextureName = threeDFloor.TopFlat;
			sectorBottomFlat.TextureName = threeDFloor.BottomFlat;
			sectorCeilingHeight.Text = threeDFloor.TopHeight.ToString();
			sectorFloorHeight.Text = threeDFloor.BottomHeight.ToString();

			typeArgument.Setup(General.Map.Config.LinedefActions[160].Args[1]);
			flagsArgument.Setup(General.Map.Config.LinedefActions[160].Args[2]);
			alphaArgument.Setup(General.Map.Config.LinedefActions[160].Args[3]);

			typeArgument.SetValue(threeDFloor.Type);
			flagsArgument.SetValue(threeDFloor.Flags);
			alphaArgument.SetValue(threeDFloor.Alpha);

			AddSectorCheckboxes();
		}

		// Create a duplicate of the given control
		public ThreeDFloorHelperControl(ThreeDFloorHelperControl ctrl) : this()
		{
			sectorBorderTexture.TextureName = threeDFloor.BorderTexture = ctrl.threeDFloor.BorderTexture;
			sectorTopFlat.TextureName = threeDFloor.TopFlat = ctrl.threeDFloor.TopFlat;
			sectorBottomFlat.TextureName = threeDFloor.BottomFlat = ctrl.threeDFloor.BottomFlat;
			sectorCeilingHeight.Text = ctrl.threeDFloor.TopHeight.ToString();
			sectorFloorHeight.Text = ctrl.threeDFloor.BottomHeight.ToString();

			threeDFloor.TopHeight = ctrl.threeDFloor.TopHeight;
			threeDFloor.BottomHeight = ctrl.threeDFloor.BottomHeight;

			typeArgument.SetValue(ctrl.threeDFloor.Type);
			flagsArgument.SetValue(ctrl.threeDFloor.Flags);
			alphaArgument.SetValue(ctrl.threeDFloor.Alpha);

			for (int i = 0; i < checkedListBoxSectors.Items.Count; i++)
				checkedListBoxSectors.SetItemChecked(i, ctrl.checkedListBoxSectors.GetItemChecked(i));
		}

		// Create a blank control for a new 3D floor
		public ThreeDFloorHelperControl()
		{
			InitializeComponent();

			isnew = true;

			threeDFloor = new ThreeDFloor();

			this.BackColor = Color.FromArgb(128, Color.Green);

			sectorBorderTexture.TextureName = General.Settings.DefaultTexture;
			sectorTopFlat.TextureName = General.Settings.DefaultCeilingTexture;
			sectorBottomFlat.TextureName = General.Settings.DefaultFloorTexture;
			sectorCeilingHeight.Text = General.Settings.DefaultCeilingHeight.ToString();
			sectorFloorHeight.Text = General.Settings.DefaultFloorHeight.ToString();

			typeArgument.Setup(General.Map.Config.LinedefActions[160].Args[1]);
			flagsArgument.Setup(General.Map.Config.LinedefActions[160].Args[2]);
			alphaArgument.Setup(General.Map.Config.LinedefActions[160].Args[3]);

			typeArgument.SetDefaultValue();
			flagsArgument.SetDefaultValue();
			alphaArgument.SetDefaultValue();

			AddSectorCheckboxes();

			for(int i=0; i < checkedListBoxSectors.Items.Count; i++)
				checkedListBoxSectors.SetItemChecked(i, true);
		}

		public void ApplyToThreeDFloor()
		{
			Regex r = new Regex(@"\d+");

			threeDFloor.TopHeight = sectorCeilingHeight.GetResult(threeDFloor.TopHeight);
			threeDFloor.BottomHeight = sectorFloorHeight.GetResult(threeDFloor.BottomHeight);
			threeDFloor.TopFlat = sectorTopFlat.TextureName;
			threeDFloor.BottomFlat = sectorBottomFlat.TextureName;
			threeDFloor.BorderTexture = sectorBorderTexture.TextureName;

			threeDFloor.Type = int.Parse(typeArgument.Text);
			threeDFloor.Flags = int.Parse(flagsArgument.Text);
			threeDFloor.Alpha = int.Parse(alphaArgument.Text);

			threeDFloor.IsNew = isnew;

			threeDFloor.TaggedSectors = new List<Sector>();

			for (int i = 0; i < checkedListBoxSectors.Items.Count; i++)
			{
				string text = checkedListBoxSectors.Items[i].ToString();
				bool ischecked = checkedListBoxSectors.GetItemChecked(i);

				if (ischecked)
				{
					var matches = r.Matches(text);
					Sector s = General.Map.Map.GetSectorByIndex(int.Parse(matches[0].ToString()));
					threeDFloor.TaggedSectors.Add(s);
				}
			}
		}

		private void AddSectorCheckboxes()
		{
			List<Sector> selectedSectors = new List<Sector>(General.Map.Map.GetSelectedSectors(true));

			if (selectedSectors == null)
				return;

			foreach(Sector s in selectedSectors.OrderBy(o => o.Index).ToList())
				checkedListBoxSectors.Items.Add("Sector " + s.Index.ToString(), ThreeDFloor.TaggedSectors.Contains(s));
		}

		private void buttonDuplicate_Click(object sender, EventArgs e)
		{
			((ThreeDFloorEditorWindow)this.ParentForm).DuplicateThreeDFloor(this);
		}

		private void buttonSplit_Click(object sender, EventArgs e)
		{
			((ThreeDFloorEditorWindow)this.ParentForm).SplitThreeDFloor(this);
		}

		private void buttonCheckAll_Click(object sender, EventArgs e)
		{
			for (int i = 0; i < checkedListBoxSectors.Items.Count; i++)
				checkedListBoxSectors.SetItemChecked(i, true);
		}

		private void buttonUncheckAll_Click(object sender, EventArgs e)
		{
			for (int i = 0; i < checkedListBoxSectors.Items.Count; i++)
				checkedListBoxSectors.SetItemChecked(i, false);
		}
	}
}
