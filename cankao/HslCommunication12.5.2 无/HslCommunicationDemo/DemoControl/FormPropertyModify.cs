using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace HslCommunicationDemo.DemoControl
{
	public class FormPropertyModify : Form
	{
		private IContainer components = null;

		private PropertyGrid propertyGrid1;

		private Button button1;

		private Label label1;

		public FormPropertyModify()
		{
			InitializeComponent();
		}

		public void SetObject(object obj)
		{
			label1.Text = "Object: " + obj.ToString();
			propertyGrid1.SelectedObject = obj;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			base.DialogResult = DialogResult.OK;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.propertyGrid1 = new System.Windows.Forms.PropertyGrid();
			this.button1 = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			base.SuspendLayout();
			this.propertyGrid1.Location = new System.Drawing.Point(3, 28);
			this.propertyGrid1.Name = "propertyGrid1";
			this.propertyGrid1.Size = new System.Drawing.Size(473, 397);
			this.propertyGrid1.TabIndex = 0;
			this.button1.Location = new System.Drawing.Point(160, 431);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(132, 35);
			this.button1.TabIndex = 1;
			this.button1.Text = "OK";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(button1_Click);
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(7, 7);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(53, 17);
			this.label1.TabIndex = 2;
			this.label1.Text = "Object: ";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.ClientSize = new System.Drawing.Size(478, 476);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.button1);
			base.Controls.Add(this.propertyGrid1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.MaximizeBox = false;
			this.MaximumSize = new System.Drawing.Size(494, 515);
			base.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(494, 515);
			base.Name = "FormPropertyModify";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "PropertyModify";
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
