using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using HslCommunication.Robot.EFORT;

namespace HslCommunicationDemo.Robot
{
	public class EfortRobotControl : UserControl
	{
		private IContainer components = null;

		private Label label71;

		private TextBox textBox61;

		private TextBox textBox62;

		private TextBox textBox63;

		private TextBox textBox64;

		private TextBox textBox65;

		private TextBox textBox66;

		private TextBox textBox67;

		private Label label70;

		private TextBox textBox54;

		private TextBox textBox55;

		private TextBox textBox56;

		private TextBox textBox57;

		private TextBox textBox58;

		private TextBox textBox59;

		private TextBox textBox60;

		private Label label69;

		private TextBox textBox47;

		private TextBox textBox48;

		private TextBox textBox49;

		private TextBox textBox50;

		private TextBox textBox51;

		private TextBox textBox52;

		private TextBox textBox53;

		private Label label68;

		private TextBox textBox37;

		private TextBox textBox41;

		private TextBox textBox42;

		private TextBox textBox43;

		private TextBox textBox44;

		private TextBox textBox45;

		private TextBox textBox46;

		private Label label67;

		private Label label66;

		private Label label65;

		private Label label63;

		private TextBox textBox38;

		private TextBox textBox39;

		private TextBox textBox40;

		private Label label64;

		private TextBox textBox33;

		private TextBox textBox34;

		private TextBox textBox35;

		private TextBox textBox36;

		private Label label62;

		private TextBox textBox29;

		private TextBox textBox30;

		private TextBox textBox31;

		private TextBox textBox32;

		private Label label60;

		private TextBox textBox25;

		private TextBox textBox26;

		private TextBox textBox27;

		private TextBox textBox28;

		private Label label58;

		private TextBox textBox21;

		private TextBox textBox22;

		private TextBox textBox23;

		private TextBox textBox24;

		private Label label56;

		private TextBox textBox17;

		private TextBox textBox18;

		private TextBox textBox19;

		private TextBox textBox20;

		private Label label54;

		private TextBox textBox16;

		private TextBox textBox15;

		private TextBox textBox14;

		private TextBox textBox13;

		private Label label51;

		private TextBox textBox12;

		private Label label50;

		private TextBox textBox11;

		private Label label45;

		private TextBox textBox10;

		private Label label44;

		private TextBox textBox9;

		private Label label43;

		private TextBox textBox8;

		private Label label42;

		private GroupBox groupBox1;

		private Label label72;

		private Label label73;

		private Label label48;

		private Label label49;

		private Label label46;

		private Label label47;

		private Label label40;

		private Label label41;

		private Label label39;

		private Label label36;

		private Label label37;

		private Label label38;

		private Label label33;

		private Label label34;

		private Label label35;

		private Label label30;

		private Label label31;

		private Label label32;

		private Label label27;

		private Label label28;

		private Label label29;

		private Label label24;

		private Label label25;

		private Label label26;

		private Label label21;

		private Label label22;

		private Label label23;

		private Label label17;

		private Label label18;

		private Label label19;

		private Label label14;

		private Label label15;

		private Label label16;

		private Label label13;

		private Label label12;

		private Label label11;

		private TextBox textBox7;

		private Label label10;

		private TextBox textBox6;

		private Label label9;

		private TextBox textBox5;

		private Label label8;

		private TextBox textBox4;

		private Label label7;

		private Label label61;

		private Label label59;

		private Label label57;

		private Label label55;

		private Label label53;

		private Label label52;

		public EfortRobotControl()
		{
			InitializeComponent();
		}

		public void RenderRobotData(EfortData efortData)
		{
			if (base.InvokeRequired)
			{
				try
				{
					Invoke(new Action<EfortData>(RenderRobotData), efortData);
					return;
				}
				catch
				{
					return;
				}
			}
			textBox4.Text = efortData.PacketStart;
			textBox6.Text = efortData.PacketOrders.ToString();
			textBox7.Text = efortData.PacketHeartbeat.ToString();
			textBox5.Text = efortData.PacketEnd.ToString();
			if (efortData.ErrorStatus == 1)
			{
				label13.BackColor = Color.Red;
				label12.BackColor = SystemColors.Control;
			}
			else
			{
				label13.BackColor = SystemColors.Control;
				label12.BackColor = Color.Red;
			}
			if (efortData.HstopStatus == 1)
			{
				label15.BackColor = Color.Red;
				label14.BackColor = SystemColors.Control;
			}
			else
			{
				label15.BackColor = SystemColors.Control;
				label14.BackColor = Color.Red;
			}
			if (efortData.AuthorityStatus == 1)
			{
				label18.BackColor = Color.Red;
				label17.BackColor = SystemColors.Control;
			}
			else
			{
				label18.BackColor = SystemColors.Control;
				label17.BackColor = Color.Red;
			}
			if (efortData.ServoStatus == 1)
			{
				label22.BackColor = Color.Red;
				label21.BackColor = SystemColors.Control;
			}
			else
			{
				label22.BackColor = SystemColors.Control;
				label21.BackColor = Color.Red;
			}
			if (efortData.AxisMoveStatus == 1)
			{
				label25.BackColor = Color.Red;
				label24.BackColor = SystemColors.Control;
			}
			else
			{
				label25.BackColor = SystemColors.Control;
				label24.BackColor = Color.Red;
			}
			if (efortData.ProgMoveStatus == 1)
			{
				label28.BackColor = Color.Red;
				label27.BackColor = SystemColors.Control;
			}
			else
			{
				label28.BackColor = SystemColors.Control;
				label27.BackColor = Color.Red;
			}
			if (efortData.ProgLoadStatus == 1)
			{
				label31.BackColor = Color.Red;
				label30.BackColor = SystemColors.Control;
			}
			else
			{
				label31.BackColor = SystemColors.Control;
				label30.BackColor = Color.Red;
			}
			if (efortData.ProgHoldStatus == 1)
			{
				label34.BackColor = Color.Red;
				label33.BackColor = SystemColors.Control;
			}
			else
			{
				label34.BackColor = SystemColors.Control;
				label33.BackColor = Color.Red;
			}
			if (efortData.ModeStatus == 1)
			{
				label39.BackColor = Color.Red;
				label37.BackColor = SystemColors.Control;
				label36.BackColor = SystemColors.Control;
			}
			else if (efortData.ModeStatus == 2)
			{
				label39.BackColor = SystemColors.Control;
				label37.BackColor = Color.Red;
				label36.BackColor = SystemColors.Control;
			}
			else
			{
				label39.BackColor = SystemColors.Control;
				label37.BackColor = SystemColors.Control;
				label36.BackColor = Color.Red;
			}
			label40.Text = efortData.SpeedStatus + " %";
			label46.Text = efortData.ProjectName;
			label48.Text = efortData.ProgramName;
			label72.Text = efortData.DbDeviceTime.ToString();
			textBox8.Text = GetStringFromArray(efortData.IoDOut);
			textBox9.Text = GetStringFromArray(efortData.IoDIn);
			textBox10.Text = GetStringFromArray(efortData.IoIOut);
			textBox11.Text = GetStringFromArray(efortData.IoIIn);
			textBox12.Text = efortData.ErrorText;
			textBox13.Text = efortData.DbAxisPos[0].ToString();
			textBox20.Text = efortData.DbAxisPos[1].ToString();
			textBox24.Text = efortData.DbAxisPos[2].ToString();
			textBox28.Text = efortData.DbAxisPos[3].ToString();
			textBox32.Text = efortData.DbAxisPos[4].ToString();
			textBox36.Text = efortData.DbAxisPos[5].ToString();
			textBox40.Text = efortData.DbAxisPos[6].ToString();
			textBox16.Text = efortData.DbCartPos[0].ToString();
			textBox17.Text = efortData.DbCartPos[1].ToString();
			textBox21.Text = efortData.DbCartPos[2].ToString();
			textBox25.Text = efortData.DbCartPos[3].ToString();
			textBox29.Text = efortData.DbCartPos[4].ToString();
			textBox33.Text = efortData.DbCartPos[5].ToString();
			textBox14.Text = efortData.DbAxisSpeed[0].ToString();
			textBox19.Text = efortData.DbAxisSpeed[1].ToString();
			textBox23.Text = efortData.DbAxisSpeed[2].ToString();
			textBox27.Text = efortData.DbAxisSpeed[3].ToString();
			textBox31.Text = efortData.DbAxisSpeed[4].ToString();
			textBox35.Text = efortData.DbAxisSpeed[5].ToString();
			textBox39.Text = efortData.DbAxisSpeed[6].ToString();
			textBox46.Text = efortData.DbAxisAcc[0].ToString();
			textBox45.Text = efortData.DbAxisAcc[1].ToString();
			textBox44.Text = efortData.DbAxisAcc[2].ToString();
			textBox43.Text = efortData.DbAxisAcc[3].ToString();
			textBox42.Text = efortData.DbAxisAcc[4].ToString();
			textBox41.Text = efortData.DbAxisAcc[5].ToString();
			textBox37.Text = efortData.DbAxisAcc[6].ToString();
			textBox53.Text = efortData.DbAxisAccAcc[0].ToString();
			textBox52.Text = efortData.DbAxisAccAcc[1].ToString();
			textBox51.Text = efortData.DbAxisAccAcc[2].ToString();
			textBox50.Text = efortData.DbAxisAccAcc[3].ToString();
			textBox49.Text = efortData.DbAxisAccAcc[4].ToString();
			textBox48.Text = efortData.DbAxisAccAcc[5].ToString();
			textBox47.Text = efortData.DbAxisAccAcc[6].ToString();
			textBox15.Text = efortData.DbAxisTorque[0].ToString();
			textBox18.Text = efortData.DbAxisTorque[1].ToString();
			textBox22.Text = efortData.DbAxisTorque[2].ToString();
			textBox26.Text = efortData.DbAxisTorque[3].ToString();
			textBox30.Text = efortData.DbAxisTorque[4].ToString();
			textBox34.Text = efortData.DbAxisTorque[5].ToString();
			textBox38.Text = efortData.DbAxisTorque[6].ToString();
			textBox60.Text = efortData.DbAxisDirCnt[0].ToString();
			textBox59.Text = efortData.DbAxisDirCnt[1].ToString();
			textBox58.Text = efortData.DbAxisDirCnt[2].ToString();
			textBox57.Text = efortData.DbAxisDirCnt[3].ToString();
			textBox56.Text = efortData.DbAxisDirCnt[4].ToString();
			textBox55.Text = efortData.DbAxisDirCnt[5].ToString();
			textBox54.Text = efortData.DbAxisDirCnt[6].ToString();
			textBox67.Text = efortData.DbAxisTime[0].ToString();
			textBox66.Text = efortData.DbAxisTime[1].ToString();
			textBox65.Text = efortData.DbAxisTime[2].ToString();
			textBox64.Text = efortData.DbAxisTime[3].ToString();
			textBox63.Text = efortData.DbAxisTime[4].ToString();
			textBox62.Text = efortData.DbAxisTime[5].ToString();
			textBox61.Text = efortData.DbAxisTime[6].ToString();
		}

		private string GetStringFromArray(Array array)
		{
			StringBuilder stringBuilder = new StringBuilder("[");
			foreach (object item in array)
			{
				stringBuilder.Append(item.ToString() + ",");
			}
			stringBuilder.Append("]");
			return stringBuilder.ToString();
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
			this.label71 = new System.Windows.Forms.Label();
			this.textBox61 = new System.Windows.Forms.TextBox();
			this.textBox62 = new System.Windows.Forms.TextBox();
			this.textBox63 = new System.Windows.Forms.TextBox();
			this.textBox64 = new System.Windows.Forms.TextBox();
			this.textBox65 = new System.Windows.Forms.TextBox();
			this.textBox66 = new System.Windows.Forms.TextBox();
			this.textBox67 = new System.Windows.Forms.TextBox();
			this.label70 = new System.Windows.Forms.Label();
			this.textBox54 = new System.Windows.Forms.TextBox();
			this.textBox55 = new System.Windows.Forms.TextBox();
			this.textBox56 = new System.Windows.Forms.TextBox();
			this.textBox57 = new System.Windows.Forms.TextBox();
			this.textBox58 = new System.Windows.Forms.TextBox();
			this.textBox59 = new System.Windows.Forms.TextBox();
			this.textBox60 = new System.Windows.Forms.TextBox();
			this.label69 = new System.Windows.Forms.Label();
			this.textBox47 = new System.Windows.Forms.TextBox();
			this.textBox48 = new System.Windows.Forms.TextBox();
			this.textBox49 = new System.Windows.Forms.TextBox();
			this.textBox50 = new System.Windows.Forms.TextBox();
			this.textBox51 = new System.Windows.Forms.TextBox();
			this.textBox52 = new System.Windows.Forms.TextBox();
			this.textBox53 = new System.Windows.Forms.TextBox();
			this.label68 = new System.Windows.Forms.Label();
			this.textBox37 = new System.Windows.Forms.TextBox();
			this.textBox41 = new System.Windows.Forms.TextBox();
			this.textBox42 = new System.Windows.Forms.TextBox();
			this.textBox43 = new System.Windows.Forms.TextBox();
			this.textBox44 = new System.Windows.Forms.TextBox();
			this.textBox45 = new System.Windows.Forms.TextBox();
			this.textBox46 = new System.Windows.Forms.TextBox();
			this.label67 = new System.Windows.Forms.Label();
			this.label66 = new System.Windows.Forms.Label();
			this.label65 = new System.Windows.Forms.Label();
			this.label63 = new System.Windows.Forms.Label();
			this.textBox38 = new System.Windows.Forms.TextBox();
			this.textBox39 = new System.Windows.Forms.TextBox();
			this.textBox40 = new System.Windows.Forms.TextBox();
			this.label64 = new System.Windows.Forms.Label();
			this.textBox33 = new System.Windows.Forms.TextBox();
			this.textBox34 = new System.Windows.Forms.TextBox();
			this.textBox35 = new System.Windows.Forms.TextBox();
			this.textBox36 = new System.Windows.Forms.TextBox();
			this.label62 = new System.Windows.Forms.Label();
			this.textBox29 = new System.Windows.Forms.TextBox();
			this.textBox30 = new System.Windows.Forms.TextBox();
			this.textBox31 = new System.Windows.Forms.TextBox();
			this.textBox32 = new System.Windows.Forms.TextBox();
			this.label60 = new System.Windows.Forms.Label();
			this.textBox25 = new System.Windows.Forms.TextBox();
			this.textBox26 = new System.Windows.Forms.TextBox();
			this.textBox27 = new System.Windows.Forms.TextBox();
			this.textBox28 = new System.Windows.Forms.TextBox();
			this.label58 = new System.Windows.Forms.Label();
			this.textBox21 = new System.Windows.Forms.TextBox();
			this.textBox22 = new System.Windows.Forms.TextBox();
			this.textBox23 = new System.Windows.Forms.TextBox();
			this.textBox24 = new System.Windows.Forms.TextBox();
			this.label56 = new System.Windows.Forms.Label();
			this.textBox17 = new System.Windows.Forms.TextBox();
			this.textBox18 = new System.Windows.Forms.TextBox();
			this.textBox19 = new System.Windows.Forms.TextBox();
			this.textBox20 = new System.Windows.Forms.TextBox();
			this.label54 = new System.Windows.Forms.Label();
			this.textBox16 = new System.Windows.Forms.TextBox();
			this.textBox15 = new System.Windows.Forms.TextBox();
			this.textBox14 = new System.Windows.Forms.TextBox();
			this.textBox13 = new System.Windows.Forms.TextBox();
			this.label51 = new System.Windows.Forms.Label();
			this.textBox12 = new System.Windows.Forms.TextBox();
			this.label50 = new System.Windows.Forms.Label();
			this.textBox11 = new System.Windows.Forms.TextBox();
			this.label45 = new System.Windows.Forms.Label();
			this.textBox10 = new System.Windows.Forms.TextBox();
			this.label44 = new System.Windows.Forms.Label();
			this.textBox9 = new System.Windows.Forms.TextBox();
			this.label43 = new System.Windows.Forms.Label();
			this.textBox8 = new System.Windows.Forms.TextBox();
			this.label42 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.label72 = new System.Windows.Forms.Label();
			this.label73 = new System.Windows.Forms.Label();
			this.label48 = new System.Windows.Forms.Label();
			this.label49 = new System.Windows.Forms.Label();
			this.label46 = new System.Windows.Forms.Label();
			this.label47 = new System.Windows.Forms.Label();
			this.label40 = new System.Windows.Forms.Label();
			this.label41 = new System.Windows.Forms.Label();
			this.label39 = new System.Windows.Forms.Label();
			this.label36 = new System.Windows.Forms.Label();
			this.label37 = new System.Windows.Forms.Label();
			this.label38 = new System.Windows.Forms.Label();
			this.label33 = new System.Windows.Forms.Label();
			this.label34 = new System.Windows.Forms.Label();
			this.label35 = new System.Windows.Forms.Label();
			this.label30 = new System.Windows.Forms.Label();
			this.label31 = new System.Windows.Forms.Label();
			this.label32 = new System.Windows.Forms.Label();
			this.label27 = new System.Windows.Forms.Label();
			this.label28 = new System.Windows.Forms.Label();
			this.label29 = new System.Windows.Forms.Label();
			this.label24 = new System.Windows.Forms.Label();
			this.label25 = new System.Windows.Forms.Label();
			this.label26 = new System.Windows.Forms.Label();
			this.label21 = new System.Windows.Forms.Label();
			this.label22 = new System.Windows.Forms.Label();
			this.label23 = new System.Windows.Forms.Label();
			this.label17 = new System.Windows.Forms.Label();
			this.label18 = new System.Windows.Forms.Label();
			this.label19 = new System.Windows.Forms.Label();
			this.label14 = new System.Windows.Forms.Label();
			this.label15 = new System.Windows.Forms.Label();
			this.label16 = new System.Windows.Forms.Label();
			this.label13 = new System.Windows.Forms.Label();
			this.label12 = new System.Windows.Forms.Label();
			this.label11 = new System.Windows.Forms.Label();
			this.textBox7 = new System.Windows.Forms.TextBox();
			this.label10 = new System.Windows.Forms.Label();
			this.textBox6 = new System.Windows.Forms.TextBox();
			this.label9 = new System.Windows.Forms.Label();
			this.textBox5 = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.textBox4 = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.label61 = new System.Windows.Forms.Label();
			this.label59 = new System.Windows.Forms.Label();
			this.label57 = new System.Windows.Forms.Label();
			this.label55 = new System.Windows.Forms.Label();
			this.label53 = new System.Windows.Forms.Label();
			this.label52 = new System.Windows.Forms.Label();
			this.groupBox1.SuspendLayout();
			base.SuspendLayout();
			this.label71.AutoSize = true;
			this.label71.Location = new System.Drawing.Point(777, 267);
			this.label71.Name = "label71";
			this.label71.Size = new System.Drawing.Size(80, 17);
			this.label71.TabIndex = 202;
			this.label71.Text = "七轴工作时长";
			this.textBox61.Location = new System.Drawing.Point(786, 450);
			this.textBox61.Name = "textBox61";
			this.textBox61.Size = new System.Drawing.Size(68, 23);
			this.textBox61.TabIndex = 201;
			this.textBox62.Location = new System.Drawing.Point(786, 423);
			this.textBox62.Name = "textBox62";
			this.textBox62.Size = new System.Drawing.Size(68, 23);
			this.textBox62.TabIndex = 200;
			this.textBox63.Location = new System.Drawing.Point(786, 396);
			this.textBox63.Name = "textBox63";
			this.textBox63.Size = new System.Drawing.Size(68, 23);
			this.textBox63.TabIndex = 199;
			this.textBox64.Location = new System.Drawing.Point(786, 369);
			this.textBox64.Name = "textBox64";
			this.textBox64.Size = new System.Drawing.Size(68, 23);
			this.textBox64.TabIndex = 198;
			this.textBox65.Location = new System.Drawing.Point(786, 342);
			this.textBox65.Name = "textBox65";
			this.textBox65.Size = new System.Drawing.Size(68, 23);
			this.textBox65.TabIndex = 197;
			this.textBox66.Location = new System.Drawing.Point(786, 314);
			this.textBox66.Name = "textBox66";
			this.textBox66.Size = new System.Drawing.Size(68, 23);
			this.textBox66.TabIndex = 196;
			this.textBox67.Location = new System.Drawing.Point(786, 287);
			this.textBox67.Name = "textBox67";
			this.textBox67.Size = new System.Drawing.Size(68, 23);
			this.textBox67.TabIndex = 195;
			this.label70.AutoSize = true;
			this.label70.Location = new System.Drawing.Point(701, 267);
			this.label70.Name = "label70";
			this.label70.Size = new System.Drawing.Size(80, 17);
			this.label70.TabIndex = 194;
			this.label70.Text = "七轴反向计数";
			this.textBox54.Location = new System.Drawing.Point(710, 450);
			this.textBox54.Name = "textBox54";
			this.textBox54.Size = new System.Drawing.Size(68, 23);
			this.textBox54.TabIndex = 193;
			this.textBox55.Location = new System.Drawing.Point(710, 423);
			this.textBox55.Name = "textBox55";
			this.textBox55.Size = new System.Drawing.Size(68, 23);
			this.textBox55.TabIndex = 192;
			this.textBox56.Location = new System.Drawing.Point(710, 396);
			this.textBox56.Name = "textBox56";
			this.textBox56.Size = new System.Drawing.Size(68, 23);
			this.textBox56.TabIndex = 191;
			this.textBox57.Location = new System.Drawing.Point(710, 369);
			this.textBox57.Name = "textBox57";
			this.textBox57.Size = new System.Drawing.Size(68, 23);
			this.textBox57.TabIndex = 190;
			this.textBox58.Location = new System.Drawing.Point(710, 342);
			this.textBox58.Name = "textBox58";
			this.textBox58.Size = new System.Drawing.Size(68, 23);
			this.textBox58.TabIndex = 189;
			this.textBox59.Location = new System.Drawing.Point(710, 314);
			this.textBox59.Name = "textBox59";
			this.textBox59.Size = new System.Drawing.Size(68, 23);
			this.textBox59.TabIndex = 188;
			this.textBox60.Location = new System.Drawing.Point(710, 287);
			this.textBox60.Name = "textBox60";
			this.textBox60.Size = new System.Drawing.Size(68, 23);
			this.textBox60.TabIndex = 187;
			this.label69.AutoSize = true;
			this.label69.Location = new System.Drawing.Point(557, 267);
			this.label69.Name = "label69";
			this.label69.Size = new System.Drawing.Size(80, 17);
			this.label69.TabIndex = 186;
			this.label69.Text = "七轴加加速度";
			this.textBox47.Location = new System.Drawing.Point(564, 450);
			this.textBox47.Name = "textBox47";
			this.textBox47.Size = new System.Drawing.Size(68, 23);
			this.textBox47.TabIndex = 185;
			this.textBox48.Location = new System.Drawing.Point(564, 423);
			this.textBox48.Name = "textBox48";
			this.textBox48.Size = new System.Drawing.Size(68, 23);
			this.textBox48.TabIndex = 184;
			this.textBox49.Location = new System.Drawing.Point(564, 396);
			this.textBox49.Name = "textBox49";
			this.textBox49.Size = new System.Drawing.Size(68, 23);
			this.textBox49.TabIndex = 183;
			this.textBox50.Location = new System.Drawing.Point(564, 369);
			this.textBox50.Name = "textBox50";
			this.textBox50.Size = new System.Drawing.Size(68, 23);
			this.textBox50.TabIndex = 182;
			this.textBox51.Location = new System.Drawing.Point(564, 342);
			this.textBox51.Name = "textBox51";
			this.textBox51.Size = new System.Drawing.Size(68, 23);
			this.textBox51.TabIndex = 181;
			this.textBox52.Location = new System.Drawing.Point(564, 314);
			this.textBox52.Name = "textBox52";
			this.textBox52.Size = new System.Drawing.Size(68, 23);
			this.textBox52.TabIndex = 180;
			this.textBox53.Location = new System.Drawing.Point(564, 287);
			this.textBox53.Name = "textBox53";
			this.textBox53.Size = new System.Drawing.Size(68, 23);
			this.textBox53.TabIndex = 179;
			this.label68.AutoSize = true;
			this.label68.Location = new System.Drawing.Point(490, 267);
			this.label68.Name = "label68";
			this.label68.Size = new System.Drawing.Size(68, 17);
			this.label68.TabIndex = 178;
			this.label68.Text = "七轴加速度";
			this.textBox37.Location = new System.Drawing.Point(490, 450);
			this.textBox37.Name = "textBox37";
			this.textBox37.Size = new System.Drawing.Size(68, 23);
			this.textBox37.TabIndex = 177;
			this.textBox41.Location = new System.Drawing.Point(490, 423);
			this.textBox41.Name = "textBox41";
			this.textBox41.Size = new System.Drawing.Size(68, 23);
			this.textBox41.TabIndex = 176;
			this.textBox42.Location = new System.Drawing.Point(490, 396);
			this.textBox42.Name = "textBox42";
			this.textBox42.Size = new System.Drawing.Size(68, 23);
			this.textBox42.TabIndex = 175;
			this.textBox43.Location = new System.Drawing.Point(490, 369);
			this.textBox43.Name = "textBox43";
			this.textBox43.Size = new System.Drawing.Size(68, 23);
			this.textBox43.TabIndex = 174;
			this.textBox44.Location = new System.Drawing.Point(490, 342);
			this.textBox44.Name = "textBox44";
			this.textBox44.Size = new System.Drawing.Size(68, 23);
			this.textBox44.TabIndex = 173;
			this.textBox45.Location = new System.Drawing.Point(490, 314);
			this.textBox45.Name = "textBox45";
			this.textBox45.Size = new System.Drawing.Size(68, 23);
			this.textBox45.TabIndex = 172;
			this.textBox46.Location = new System.Drawing.Point(490, 287);
			this.textBox46.Name = "textBox46";
			this.textBox46.Size = new System.Drawing.Size(68, 23);
			this.textBox46.TabIndex = 171;
			this.label67.AutoSize = true;
			this.label67.Location = new System.Drawing.Point(901, 267);
			this.label67.Name = "label67";
			this.label67.Size = new System.Drawing.Size(32, 17);
			this.label67.TabIndex = 170;
			this.label67.Text = "方向";
			this.label66.AutoSize = true;
			this.label66.Location = new System.Drawing.Point(643, 267);
			this.label66.Name = "label66";
			this.label66.Size = new System.Drawing.Size(56, 17);
			this.label66.TabIndex = 169;
			this.label66.Text = "七轴力矩";
			this.label65.AutoSize = true;
			this.label65.Location = new System.Drawing.Point(420, 267);
			this.label65.Name = "label65";
			this.label65.Size = new System.Drawing.Size(56, 17);
			this.label65.TabIndex = 168;
			this.label65.Text = "七轴速度";
			this.label63.AutoSize = true;
			this.label63.Location = new System.Drawing.Point(348, 267);
			this.label63.Name = "label63";
			this.label63.Size = new System.Drawing.Size(56, 17);
			this.label63.TabIndex = 167;
			this.label63.Text = "七轴角度";
			this.textBox38.Location = new System.Drawing.Point(637, 450);
			this.textBox38.Name = "textBox38";
			this.textBox38.Size = new System.Drawing.Size(68, 23);
			this.textBox38.TabIndex = 166;
			this.textBox39.Location = new System.Drawing.Point(416, 450);
			this.textBox39.Name = "textBox39";
			this.textBox39.Size = new System.Drawing.Size(68, 23);
			this.textBox39.TabIndex = 165;
			this.textBox40.Location = new System.Drawing.Point(342, 450);
			this.textBox40.Name = "textBox40";
			this.textBox40.Size = new System.Drawing.Size(68, 23);
			this.textBox40.TabIndex = 164;
			this.label64.AutoSize = true;
			this.label64.Location = new System.Drawing.Point(266, 453);
			this.label64.Name = "label64";
			this.label64.Size = new System.Drawing.Size(68, 17);
			this.label64.TabIndex = 163;
			this.label64.Text = "七轴数据：";
			this.textBox33.Location = new System.Drawing.Point(881, 423);
			this.textBox33.Name = "textBox33";
			this.textBox33.Size = new System.Drawing.Size(68, 23);
			this.textBox33.TabIndex = 161;
			this.textBox34.Location = new System.Drawing.Point(637, 423);
			this.textBox34.Name = "textBox34";
			this.textBox34.Size = new System.Drawing.Size(68, 23);
			this.textBox34.TabIndex = 160;
			this.textBox35.Location = new System.Drawing.Point(416, 423);
			this.textBox35.Name = "textBox35";
			this.textBox35.Size = new System.Drawing.Size(68, 23);
			this.textBox35.TabIndex = 159;
			this.textBox36.Location = new System.Drawing.Point(342, 423);
			this.textBox36.Name = "textBox36";
			this.textBox36.Size = new System.Drawing.Size(68, 23);
			this.textBox36.TabIndex = 158;
			this.label62.AutoSize = true;
			this.label62.Location = new System.Drawing.Point(266, 426);
			this.label62.Name = "label62";
			this.label62.Size = new System.Drawing.Size(68, 17);
			this.label62.TabIndex = 157;
			this.label62.Text = "六轴数据：";
			this.textBox29.Location = new System.Drawing.Point(881, 396);
			this.textBox29.Name = "textBox29";
			this.textBox29.Size = new System.Drawing.Size(68, 23);
			this.textBox29.TabIndex = 155;
			this.textBox30.Location = new System.Drawing.Point(637, 396);
			this.textBox30.Name = "textBox30";
			this.textBox30.Size = new System.Drawing.Size(68, 23);
			this.textBox30.TabIndex = 154;
			this.textBox31.Location = new System.Drawing.Point(416, 396);
			this.textBox31.Name = "textBox31";
			this.textBox31.Size = new System.Drawing.Size(68, 23);
			this.textBox31.TabIndex = 153;
			this.textBox32.Location = new System.Drawing.Point(342, 396);
			this.textBox32.Name = "textBox32";
			this.textBox32.Size = new System.Drawing.Size(68, 23);
			this.textBox32.TabIndex = 152;
			this.label60.AutoSize = true;
			this.label60.Location = new System.Drawing.Point(266, 399);
			this.label60.Name = "label60";
			this.label60.Size = new System.Drawing.Size(68, 17);
			this.label60.TabIndex = 151;
			this.label60.Text = "五轴数据：";
			this.textBox25.Location = new System.Drawing.Point(881, 369);
			this.textBox25.Name = "textBox25";
			this.textBox25.Size = new System.Drawing.Size(68, 23);
			this.textBox25.TabIndex = 149;
			this.textBox26.Location = new System.Drawing.Point(637, 369);
			this.textBox26.Name = "textBox26";
			this.textBox26.Size = new System.Drawing.Size(68, 23);
			this.textBox26.TabIndex = 148;
			this.textBox27.Location = new System.Drawing.Point(416, 369);
			this.textBox27.Name = "textBox27";
			this.textBox27.Size = new System.Drawing.Size(68, 23);
			this.textBox27.TabIndex = 147;
			this.textBox28.Location = new System.Drawing.Point(342, 369);
			this.textBox28.Name = "textBox28";
			this.textBox28.Size = new System.Drawing.Size(68, 23);
			this.textBox28.TabIndex = 146;
			this.label58.AutoSize = true;
			this.label58.Location = new System.Drawing.Point(266, 372);
			this.label58.Name = "label58";
			this.label58.Size = new System.Drawing.Size(68, 17);
			this.label58.TabIndex = 145;
			this.label58.Text = "四轴数据：";
			this.textBox21.Location = new System.Drawing.Point(881, 342);
			this.textBox21.Name = "textBox21";
			this.textBox21.Size = new System.Drawing.Size(68, 23);
			this.textBox21.TabIndex = 143;
			this.textBox22.Location = new System.Drawing.Point(637, 342);
			this.textBox22.Name = "textBox22";
			this.textBox22.Size = new System.Drawing.Size(68, 23);
			this.textBox22.TabIndex = 142;
			this.textBox23.Location = new System.Drawing.Point(416, 342);
			this.textBox23.Name = "textBox23";
			this.textBox23.Size = new System.Drawing.Size(68, 23);
			this.textBox23.TabIndex = 141;
			this.textBox24.Location = new System.Drawing.Point(342, 342);
			this.textBox24.Name = "textBox24";
			this.textBox24.Size = new System.Drawing.Size(68, 23);
			this.textBox24.TabIndex = 140;
			this.label56.AutoSize = true;
			this.label56.Location = new System.Drawing.Point(266, 345);
			this.label56.Name = "label56";
			this.label56.Size = new System.Drawing.Size(68, 17);
			this.label56.TabIndex = 139;
			this.label56.Text = "三轴数据：";
			this.textBox17.Location = new System.Drawing.Point(881, 314);
			this.textBox17.Name = "textBox17";
			this.textBox17.Size = new System.Drawing.Size(68, 23);
			this.textBox17.TabIndex = 137;
			this.textBox18.Location = new System.Drawing.Point(637, 314);
			this.textBox18.Name = "textBox18";
			this.textBox18.Size = new System.Drawing.Size(68, 23);
			this.textBox18.TabIndex = 136;
			this.textBox19.Location = new System.Drawing.Point(416, 314);
			this.textBox19.Name = "textBox19";
			this.textBox19.Size = new System.Drawing.Size(68, 23);
			this.textBox19.TabIndex = 135;
			this.textBox20.Location = new System.Drawing.Point(342, 314);
			this.textBox20.Name = "textBox20";
			this.textBox20.Size = new System.Drawing.Size(68, 23);
			this.textBox20.TabIndex = 134;
			this.label54.AutoSize = true;
			this.label54.Location = new System.Drawing.Point(266, 317);
			this.label54.Name = "label54";
			this.label54.Size = new System.Drawing.Size(68, 17);
			this.label54.TabIndex = 133;
			this.label54.Text = "二轴数据：";
			this.textBox16.Location = new System.Drawing.Point(881, 287);
			this.textBox16.Name = "textBox16";
			this.textBox16.Size = new System.Drawing.Size(68, 23);
			this.textBox16.TabIndex = 131;
			this.textBox15.Location = new System.Drawing.Point(637, 287);
			this.textBox15.Name = "textBox15";
			this.textBox15.Size = new System.Drawing.Size(68, 23);
			this.textBox15.TabIndex = 130;
			this.textBox14.Location = new System.Drawing.Point(416, 287);
			this.textBox14.Name = "textBox14";
			this.textBox14.Size = new System.Drawing.Size(68, 23);
			this.textBox14.TabIndex = 129;
			this.textBox13.Location = new System.Drawing.Point(342, 287);
			this.textBox13.Name = "textBox13";
			this.textBox13.Size = new System.Drawing.Size(68, 23);
			this.textBox13.TabIndex = 128;
			this.label51.AutoSize = true;
			this.label51.Location = new System.Drawing.Point(266, 290);
			this.label51.Name = "label51";
			this.label51.Size = new System.Drawing.Size(68, 17);
			this.label51.TabIndex = 127;
			this.label51.Text = "一轴数据：";
			this.textBox12.Location = new System.Drawing.Point(342, 219);
			this.textBox12.Multiline = true;
			this.textBox12.Name = "textBox12";
			this.textBox12.Size = new System.Drawing.Size(604, 40);
			this.textBox12.TabIndex = 126;
			this.label50.AutoSize = true;
			this.label50.Location = new System.Drawing.Point(266, 222);
			this.label50.Name = "label50";
			this.label50.Size = new System.Drawing.Size(68, 17);
			this.label50.TabIndex = 125;
			this.label50.Text = "错误信息：";
			this.textBox11.Location = new System.Drawing.Point(342, 172);
			this.textBox11.Multiline = true;
			this.textBox11.Name = "textBox11";
			this.textBox11.Size = new System.Drawing.Size(604, 40);
			this.textBox11.TabIndex = 124;
			this.label45.AutoSize = true;
			this.label45.Location = new System.Drawing.Point(266, 175);
			this.label45.Name = "label45";
			this.label45.Size = new System.Drawing.Size(47, 17);
			this.label45.TabIndex = 123;
			this.label45.Text = "IoIIn：";
			this.textBox10.Location = new System.Drawing.Point(342, 126);
			this.textBox10.Multiline = true;
			this.textBox10.Name = "textBox10";
			this.textBox10.Size = new System.Drawing.Size(604, 40);
			this.textBox10.TabIndex = 122;
			this.label44.AutoSize = true;
			this.label44.Location = new System.Drawing.Point(266, 129);
			this.label44.Name = "label44";
			this.label44.Size = new System.Drawing.Size(57, 17);
			this.label44.TabIndex = 121;
			this.label44.Text = "IoIOut：";
			this.textBox9.Location = new System.Drawing.Point(342, 80);
			this.textBox9.Multiline = true;
			this.textBox9.Name = "textBox9";
			this.textBox9.Size = new System.Drawing.Size(604, 40);
			this.textBox9.TabIndex = 120;
			this.label43.AutoSize = true;
			this.label43.Location = new System.Drawing.Point(266, 83);
			this.label43.Name = "label43";
			this.label43.Size = new System.Drawing.Size(52, 17);
			this.label43.TabIndex = 119;
			this.label43.Text = "IoDIn：";
			this.textBox8.Location = new System.Drawing.Point(342, 34);
			this.textBox8.Multiline = true;
			this.textBox8.Name = "textBox8";
			this.textBox8.Size = new System.Drawing.Size(604, 40);
			this.textBox8.TabIndex = 118;
			this.label42.AutoSize = true;
			this.label42.Location = new System.Drawing.Point(266, 37);
			this.label42.Name = "label42";
			this.label42.Size = new System.Drawing.Size(62, 17);
			this.label42.TabIndex = 117;
			this.label42.Text = "IoDOut：";
			this.groupBox1.Controls.Add(this.label72);
			this.groupBox1.Controls.Add(this.label73);
			this.groupBox1.Controls.Add(this.label48);
			this.groupBox1.Controls.Add(this.label49);
			this.groupBox1.Controls.Add(this.label46);
			this.groupBox1.Controls.Add(this.label47);
			this.groupBox1.Controls.Add(this.label40);
			this.groupBox1.Controls.Add(this.label41);
			this.groupBox1.Controls.Add(this.label39);
			this.groupBox1.Controls.Add(this.label36);
			this.groupBox1.Controls.Add(this.label37);
			this.groupBox1.Controls.Add(this.label38);
			this.groupBox1.Controls.Add(this.label33);
			this.groupBox1.Controls.Add(this.label34);
			this.groupBox1.Controls.Add(this.label35);
			this.groupBox1.Controls.Add(this.label30);
			this.groupBox1.Controls.Add(this.label31);
			this.groupBox1.Controls.Add(this.label32);
			this.groupBox1.Controls.Add(this.label27);
			this.groupBox1.Controls.Add(this.label28);
			this.groupBox1.Controls.Add(this.label29);
			this.groupBox1.Controls.Add(this.label24);
			this.groupBox1.Controls.Add(this.label25);
			this.groupBox1.Controls.Add(this.label26);
			this.groupBox1.Controls.Add(this.label21);
			this.groupBox1.Controls.Add(this.label22);
			this.groupBox1.Controls.Add(this.label23);
			this.groupBox1.Controls.Add(this.label17);
			this.groupBox1.Controls.Add(this.label18);
			this.groupBox1.Controls.Add(this.label19);
			this.groupBox1.Controls.Add(this.label14);
			this.groupBox1.Controls.Add(this.label15);
			this.groupBox1.Controls.Add(this.label16);
			this.groupBox1.Controls.Add(this.label13);
			this.groupBox1.Controls.Add(this.label12);
			this.groupBox1.Controls.Add(this.label11);
			this.groupBox1.Location = new System.Drawing.Point(6, 35);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(243, 440);
			this.groupBox1.TabIndex = 116;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "状态";
			this.label72.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label72.Location = new System.Drawing.Point(101, 384);
			this.label72.Name = "label72";
			this.label72.Size = new System.Drawing.Size(120, 19);
			this.label72.TabIndex = 35;
			this.label72.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label73.AutoSize = true;
			this.label73.Location = new System.Drawing.Point(9, 385);
			this.label73.Name = "label73";
			this.label73.Size = new System.Drawing.Size(92, 17);
			this.label73.TabIndex = 34;
			this.label73.Text = "设备开机总长：";
			this.label48.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label48.Location = new System.Drawing.Point(101, 356);
			this.label48.Name = "label48";
			this.label48.Size = new System.Drawing.Size(120, 19);
			this.label48.TabIndex = 33;
			this.label48.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label49.AutoSize = true;
			this.label49.Location = new System.Drawing.Point(9, 357);
			this.label49.Name = "label49";
			this.label49.Size = new System.Drawing.Size(80, 17);
			this.label49.TabIndex = 32;
			this.label49.Text = "加载程序名：";
			this.label46.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label46.Location = new System.Drawing.Point(101, 327);
			this.label46.Name = "label46";
			this.label46.Size = new System.Drawing.Size(120, 19);
			this.label46.TabIndex = 31;
			this.label46.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label47.AutoSize = true;
			this.label47.Location = new System.Drawing.Point(9, 328);
			this.label47.Name = "label47";
			this.label47.Size = new System.Drawing.Size(80, 17);
			this.label47.TabIndex = 30;
			this.label47.Text = "加载工程名：";
			this.label40.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label40.Location = new System.Drawing.Point(101, 299);
			this.label40.Name = "label40";
			this.label40.Size = new System.Drawing.Size(120, 19);
			this.label40.TabIndex = 29;
			this.label40.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label41.AutoSize = true;
			this.label41.Location = new System.Drawing.Point(9, 300);
			this.label41.Name = "label41";
			this.label41.Size = new System.Drawing.Size(68, 17);
			this.label41.TabIndex = 28;
			this.label41.Text = "速度状态：";
			this.label39.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label39.Location = new System.Drawing.Point(12, 269);
			this.label39.Name = "label39";
			this.label39.Size = new System.Drawing.Size(59, 19);
			this.label39.TabIndex = 27;
			this.label39.Text = "手动";
			this.label39.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label36.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label36.Location = new System.Drawing.Point(162, 269);
			this.label36.Name = "label36";
			this.label36.Size = new System.Drawing.Size(59, 19);
			this.label36.TabIndex = 26;
			this.label36.Text = "远程";
			this.label36.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label37.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label37.Location = new System.Drawing.Point(87, 269);
			this.label37.Name = "label37";
			this.label37.Size = new System.Drawing.Size(59, 19);
			this.label37.TabIndex = 25;
			this.label37.Text = "自动";
			this.label37.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label38.AutoSize = true;
			this.label38.Location = new System.Drawing.Point(9, 243);
			this.label38.Name = "label38";
			this.label38.Size = new System.Drawing.Size(44, 17);
			this.label38.TabIndex = 24;
			this.label38.Text = "模式：";
			this.label33.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label33.Location = new System.Drawing.Point(166, 212);
			this.label33.Name = "label33";
			this.label33.Size = new System.Drawing.Size(59, 19);
			this.label33.TabIndex = 23;
			this.label33.Text = "无暂停";
			this.label33.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label34.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label34.Location = new System.Drawing.Point(101, 212);
			this.label34.Name = "label34";
			this.label34.Size = new System.Drawing.Size(59, 19);
			this.label34.TabIndex = 22;
			this.label34.Text = "暂停中";
			this.label34.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label35.AutoSize = true;
			this.label35.Location = new System.Drawing.Point(9, 213);
			this.label35.Name = "label35";
			this.label35.Size = new System.Drawing.Size(92, 17);
			this.label35.TabIndex = 21;
			this.label35.Text = "程序暂停状态：";
			this.label30.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label30.Location = new System.Drawing.Point(166, 185);
			this.label30.Name = "label30";
			this.label30.Size = new System.Drawing.Size(59, 19);
			this.label30.TabIndex = 20;
			this.label30.Text = "未加载";
			this.label30.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label31.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label31.Location = new System.Drawing.Point(101, 185);
			this.label31.Name = "label31";
			this.label31.Size = new System.Drawing.Size(59, 19);
			this.label31.TabIndex = 19;
			this.label31.Text = "有加载";
			this.label31.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label32.AutoSize = true;
			this.label32.Location = new System.Drawing.Point(9, 186);
			this.label32.Name = "label32";
			this.label32.Size = new System.Drawing.Size(92, 17);
			this.label32.TabIndex = 18;
			this.label32.Text = "程序加载状态：";
			this.label27.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label27.Location = new System.Drawing.Point(166, 158);
			this.label27.Name = "label27";
			this.label27.Size = new System.Drawing.Size(59, 19);
			this.label27.TabIndex = 17;
			this.label27.Text = "未运行";
			this.label27.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label28.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label28.Location = new System.Drawing.Point(101, 158);
			this.label28.Name = "label28";
			this.label28.Size = new System.Drawing.Size(59, 19);
			this.label28.TabIndex = 16;
			this.label28.Text = "有运行";
			this.label28.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label29.AutoSize = true;
			this.label29.Location = new System.Drawing.Point(9, 159);
			this.label29.Name = "label29";
			this.label29.Size = new System.Drawing.Size(92, 17);
			this.label29.TabIndex = 15;
			this.label29.Text = "程序运行状态：";
			this.label24.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label24.Location = new System.Drawing.Point(166, 131);
			this.label24.Name = "label24";
			this.label24.Size = new System.Drawing.Size(59, 19);
			this.label24.TabIndex = 14;
			this.label24.Text = "未运动";
			this.label24.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label25.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label25.Location = new System.Drawing.Point(101, 131);
			this.label25.Name = "label25";
			this.label25.Size = new System.Drawing.Size(59, 19);
			this.label25.TabIndex = 13;
			this.label25.Text = "有运动";
			this.label25.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label26.AutoSize = true;
			this.label26.Location = new System.Drawing.Point(9, 132);
			this.label26.Name = "label26";
			this.label26.Size = new System.Drawing.Size(80, 17);
			this.label26.TabIndex = 12;
			this.label26.Text = "轴运动状态：";
			this.label21.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label21.Location = new System.Drawing.Point(166, 103);
			this.label21.Name = "label21";
			this.label21.Size = new System.Drawing.Size(59, 19);
			this.label21.TabIndex = 11;
			this.label21.Text = "未使能";
			this.label21.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label22.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label22.Location = new System.Drawing.Point(101, 103);
			this.label22.Name = "label22";
			this.label22.Size = new System.Drawing.Size(59, 19);
			this.label22.TabIndex = 10;
			this.label22.Text = "有使能";
			this.label22.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label23.AutoSize = true;
			this.label23.Location = new System.Drawing.Point(9, 104);
			this.label23.Name = "label23";
			this.label23.Size = new System.Drawing.Size(68, 17);
			this.label23.TabIndex = 9;
			this.label23.Text = "伺服状态：";
			this.label17.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label17.Location = new System.Drawing.Point(166, 76);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(59, 19);
			this.label17.TabIndex = 8;
			this.label17.Text = "无权限";
			this.label17.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label18.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label18.Location = new System.Drawing.Point(101, 76);
			this.label18.Name = "label18";
			this.label18.Size = new System.Drawing.Size(59, 19);
			this.label18.TabIndex = 7;
			this.label18.Text = "有权限";
			this.label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label19.AutoSize = true;
			this.label19.Location = new System.Drawing.Point(9, 77);
			this.label19.Name = "label19";
			this.label19.Size = new System.Drawing.Size(68, 17);
			this.label19.TabIndex = 6;
			this.label19.Text = "权限状态：";
			this.label14.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label14.Location = new System.Drawing.Point(166, 48);
			this.label14.Name = "label14";
			this.label14.Size = new System.Drawing.Size(59, 19);
			this.label14.TabIndex = 5;
			this.label14.Text = "有急停";
			this.label14.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label15.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label15.Location = new System.Drawing.Point(101, 48);
			this.label15.Name = "label15";
			this.label15.Size = new System.Drawing.Size(59, 19);
			this.label15.TabIndex = 4;
			this.label15.Text = "无急停";
			this.label15.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label16.AutoSize = true;
			this.label16.Location = new System.Drawing.Point(9, 49);
			this.label16.Name = "label16";
			this.label16.Size = new System.Drawing.Size(68, 17);
			this.label16.TabIndex = 3;
			this.label16.Text = "急停状态：";
			this.label13.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label13.Location = new System.Drawing.Point(166, 21);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(59, 19);
			this.label13.TabIndex = 2;
			this.label13.Text = "报警中";
			this.label13.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label12.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.label12.Location = new System.Drawing.Point(101, 21);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(59, 19);
			this.label12.TabIndex = 1;
			this.label12.Text = "无报警";
			this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(9, 22);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(68, 17);
			this.label11.TabIndex = 0;
			this.label11.Text = "报警状态：";
			this.textBox7.Location = new System.Drawing.Point(565, 4);
			this.textBox7.Name = "textBox7";
			this.textBox7.Size = new System.Drawing.Size(89, 23);
			this.textBox7.TabIndex = 115;
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(489, 7);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(68, 17);
			this.label10.TabIndex = 114;
			this.label10.Text = "数据心跳：";
			this.textBox6.Location = new System.Drawing.Point(381, 3);
			this.textBox6.Name = "textBox6";
			this.textBox6.Size = new System.Drawing.Size(89, 23);
			this.textBox6.TabIndex = 113;
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(305, 6);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(68, 17);
			this.label9.TabIndex = 112;
			this.label9.Text = "数据命令：";
			this.textBox5.Location = new System.Drawing.Point(736, 4);
			this.textBox5.Name = "textBox5";
			this.textBox5.Size = new System.Drawing.Size(197, 23);
			this.textBox5.TabIndex = 111;
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(660, 7);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(68, 17);
			this.label8.TabIndex = 110;
			this.label8.Text = "报文结束：";
			this.textBox4.Location = new System.Drawing.Point(79, 3);
			this.textBox4.Name = "textBox4";
			this.textBox4.Size = new System.Drawing.Size(197, 23);
			this.textBox4.TabIndex = 109;
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(3, 6);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(68, 17);
			this.label7.TabIndex = 108;
			this.label7.Text = "报文开始：";
			this.label61.AutoSize = true;
			this.label61.Location = new System.Drawing.Point(858, 426);
			this.label61.Name = "label61";
			this.label61.Size = new System.Drawing.Size(28, 17);
			this.label61.TabIndex = 162;
			this.label61.Text = "C：";
			this.label59.AutoSize = true;
			this.label59.Location = new System.Drawing.Point(858, 399);
			this.label59.Name = "label59";
			this.label59.Size = new System.Drawing.Size(28, 17);
			this.label59.TabIndex = 156;
			this.label59.Text = "B：";
			this.label57.AutoSize = true;
			this.label57.Location = new System.Drawing.Point(858, 372);
			this.label57.Name = "label57";
			this.label57.Size = new System.Drawing.Size(28, 17);
			this.label57.TabIndex = 150;
			this.label57.Text = "A：";
			this.label55.AutoSize = true;
			this.label55.Location = new System.Drawing.Point(858, 345);
			this.label55.Name = "label55";
			this.label55.Size = new System.Drawing.Size(27, 17);
			this.label55.TabIndex = 144;
			this.label55.Text = "Z：";
			this.label53.AutoSize = true;
			this.label53.Location = new System.Drawing.Point(858, 317);
			this.label53.Name = "label53";
			this.label53.Size = new System.Drawing.Size(27, 17);
			this.label53.TabIndex = 138;
			this.label53.Text = "Y：";
			this.label52.AutoSize = true;
			this.label52.Location = new System.Drawing.Point(858, 290);
			this.label52.Name = "label52";
			this.label52.Size = new System.Drawing.Size(28, 17);
			this.label52.TabIndex = 132;
			this.label52.Text = "X：";
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			base.Controls.Add(this.label71);
			base.Controls.Add(this.textBox61);
			base.Controls.Add(this.textBox62);
			base.Controls.Add(this.textBox63);
			base.Controls.Add(this.textBox64);
			base.Controls.Add(this.textBox65);
			base.Controls.Add(this.textBox66);
			base.Controls.Add(this.textBox67);
			base.Controls.Add(this.label70);
			base.Controls.Add(this.textBox54);
			base.Controls.Add(this.textBox55);
			base.Controls.Add(this.textBox56);
			base.Controls.Add(this.textBox57);
			base.Controls.Add(this.textBox58);
			base.Controls.Add(this.textBox59);
			base.Controls.Add(this.textBox60);
			base.Controls.Add(this.label69);
			base.Controls.Add(this.textBox47);
			base.Controls.Add(this.textBox48);
			base.Controls.Add(this.textBox49);
			base.Controls.Add(this.textBox50);
			base.Controls.Add(this.textBox51);
			base.Controls.Add(this.textBox52);
			base.Controls.Add(this.textBox53);
			base.Controls.Add(this.label68);
			base.Controls.Add(this.textBox37);
			base.Controls.Add(this.textBox41);
			base.Controls.Add(this.textBox42);
			base.Controls.Add(this.textBox43);
			base.Controls.Add(this.textBox44);
			base.Controls.Add(this.textBox45);
			base.Controls.Add(this.textBox46);
			base.Controls.Add(this.label67);
			base.Controls.Add(this.label66);
			base.Controls.Add(this.label65);
			base.Controls.Add(this.label63);
			base.Controls.Add(this.textBox38);
			base.Controls.Add(this.textBox39);
			base.Controls.Add(this.textBox40);
			base.Controls.Add(this.label64);
			base.Controls.Add(this.textBox33);
			base.Controls.Add(this.textBox34);
			base.Controls.Add(this.textBox35);
			base.Controls.Add(this.textBox36);
			base.Controls.Add(this.label62);
			base.Controls.Add(this.textBox29);
			base.Controls.Add(this.textBox30);
			base.Controls.Add(this.textBox31);
			base.Controls.Add(this.textBox32);
			base.Controls.Add(this.label60);
			base.Controls.Add(this.textBox25);
			base.Controls.Add(this.textBox26);
			base.Controls.Add(this.textBox27);
			base.Controls.Add(this.textBox28);
			base.Controls.Add(this.label58);
			base.Controls.Add(this.textBox21);
			base.Controls.Add(this.textBox22);
			base.Controls.Add(this.textBox23);
			base.Controls.Add(this.textBox24);
			base.Controls.Add(this.label56);
			base.Controls.Add(this.textBox17);
			base.Controls.Add(this.textBox18);
			base.Controls.Add(this.textBox19);
			base.Controls.Add(this.textBox20);
			base.Controls.Add(this.label54);
			base.Controls.Add(this.textBox16);
			base.Controls.Add(this.textBox15);
			base.Controls.Add(this.textBox14);
			base.Controls.Add(this.textBox13);
			base.Controls.Add(this.label51);
			base.Controls.Add(this.textBox12);
			base.Controls.Add(this.label50);
			base.Controls.Add(this.textBox11);
			base.Controls.Add(this.label45);
			base.Controls.Add(this.textBox10);
			base.Controls.Add(this.label44);
			base.Controls.Add(this.textBox9);
			base.Controls.Add(this.label43);
			base.Controls.Add(this.textBox8);
			base.Controls.Add(this.label42);
			base.Controls.Add(this.groupBox1);
			base.Controls.Add(this.textBox7);
			base.Controls.Add(this.label10);
			base.Controls.Add(this.textBox6);
			base.Controls.Add(this.label9);
			base.Controls.Add(this.textBox5);
			base.Controls.Add(this.label8);
			base.Controls.Add(this.textBox4);
			base.Controls.Add(this.label7);
			base.Controls.Add(this.label61);
			base.Controls.Add(this.label59);
			base.Controls.Add(this.label57);
			base.Controls.Add(this.label55);
			base.Controls.Add(this.label53);
			base.Controls.Add(this.label52);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Name = "EfortRobotControl";
			base.Size = new System.Drawing.Size(958, 479);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
}
