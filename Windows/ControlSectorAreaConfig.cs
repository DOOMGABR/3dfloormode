﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CodeImp.DoomBuilder.ThreeDFloorHelper
{
	public partial class ControlSectorAreaConfig : Form
	{
		private ControlSectorArea csa;

		public ControlSectorAreaConfig(ControlSectorArea csa)
		{
			this.csa = csa;

			InitializeComponent();

			useTagRange.Checked = csa.UseCustomTagRnage;
			firstTag.Text = csa.FirstTag.ToString();
			lastTag.Text = csa.LastTag.ToString();
		}

		private void useTagRange_CheckedChanged(object sender, EventArgs e)
		{
			if (useTagRange.Checked)
			{
				firstTag.Enabled = true;
				lastTag.Enabled = true;
			}
			else
			{
				firstTag.Enabled = false;
				lastTag.Enabled = false;
			}
		}

		private void cancelButton_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void okButton_Click(object sender, EventArgs e)
		{
			csa.UseCustomTagRnage = useTagRange.Checked;

			if (useTagRange.Checked)
			{
				csa.FirstTag = int.Parse(firstTag.Text);
				csa.LastTag = int.Parse(lastTag.Text);
			}

			this.Close();
		}
	}
}
