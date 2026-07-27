using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using HslCommunication;
using HslCommunication.BasicFramework;
using HslCommunication.Enthernet;
using HslCommunicationDemo.DemoControl;
using HslControls.Advanced;

namespace HslCommunicationDemo
{
	public class FormHslMap : HslFormContent
	{
		private IContainer components = null;

		private UserControlHead userControlHead1;

		private HslChinaMap hslChinaMap1;

		public FormHslMap()
		{
			InitializeComponent();
		}

		private void FormTcpDebug_Load(object sender, EventArgs e)
		{
			hslChinaMap1.Paint += HslChinaMap1_Paint;
		}

		private void HslChinaMap1_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 500, 2, 2));
			e.Graphics.DrawString("1-10 次", Font, Brushes.Gray, new Point(1350, 493));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 520, 4, 4));
			e.Graphics.DrawString("10-100 次", Font, Brushes.Gray, new Point(1350, 513));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 540, 6, 6));
			e.Graphics.DrawString("100-1000 次", Font, Brushes.Gray, new Point(1350, 533));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 560, 8, 8));
			e.Graphics.DrawString("1000-3000 次", Font, Brushes.Gray, new Point(1350, 554));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 580, 10, 10));
			e.Graphics.DrawString("3000-5000 次", Font, Brushes.Gray, new Point(1350, 574));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 600, 12, 12));
			e.Graphics.DrawString("5000-10000 次", Font, Brushes.Gray, new Point(1350, 596));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 620, 14, 14));
			e.Graphics.DrawString("10000-13000 次", Font, Brushes.Gray, new Point(1350, 617));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 642, 16, 16));
			e.Graphics.DrawString("13000-15000 次", Font, Brushes.Gray, new Point(1350, 642));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 664, 18, 18));
			e.Graphics.DrawString("15000-20000 次", Font, Brushes.Gray, new Point(1350, 666));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 692, 20, 20));
			e.Graphics.DrawString("20000-25000 次", Font, Brushes.Gray, new Point(1350, 697));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 722, 22, 22));
			e.Graphics.DrawString("25000-30000 次", Font, Brushes.Gray, new Point(1350, 727));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 756, 24, 24));
			e.Graphics.DrawString("30000-50000 次", Font, Brushes.Gray, new Point(1350, 761));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 790, 26, 26));
			e.Graphics.DrawString("50000-100000 次", Font, Brushes.Gray, new Point(1350, 792));
			e.Graphics.FillEllipse(Brushes.Yellow, new Rectangle(1300, 830, 30, 30));
			e.Graphics.DrawString("100000以上次", Font, Brushes.Gray, new Point(1350, 835));
		}

		private void FormHslMap_Shown(object sender, EventArgs e)
		{
			ThreadPool.QueueUserWorkItem(GetDataFromServer2, null);
		}

		private void GetDataFromServer(object obj)
		{
			OperateResult<NetHandle, string> operateResult = new NetSimplifyClient("118.24.36.220", 18467).ReadCustomerFromServer(1000, SoftBasic.FrameworkVersion.ToString());
			if (!operateResult.IsSuccess)
			{
				return;
			}
			MemoryStream memoryStream = new MemoryStream(Encoding.Unicode.GetBytes(operateResult.Content2));
			StreamReader streamReader = new StreamReader(memoryStream, Encoding.Unicode);
			Dictionary<string, long> dictionary = new Dictionary<string, long>();
			while (true)
			{
				string text = streamReader.ReadLine();
				if (text == null)
				{
					break;
				}
				dictionary.Add(text, long.Parse(streamReader.ReadLine()));
			}
			streamReader.Close();
			memoryStream.Close();
			List<dataMy> list = new List<dataMy>();
			foreach (KeyValuePair<string, long> item in dictionary)
			{
				list.Add(new dataMy(item.Key, item.Value));
			}
			list.Sort();
			list.Reverse();
			string[] array = new string[34]
			{
				"北京", "浙江", "河北", "山西", "内蒙", "辽宁", "吉林", "黑龙江", "江苏", "安徽",
				"福建", "江西", "山东", "河南", "湖北", "湖南", "广东", "广西", "海南", "四川",
				"贵州", "云南", "西藏", "陕西", "甘肃", "青海", "宁夏", "新疆", "台湾", "天津",
				"上海", "重庆", "香港", "澳门"
			};
			List<dataMy> shengfen = new List<dataMy>();
			for (int i = 0; i < list.Count; i++)
			{
				string tmp = string.Empty;
				for (int j = 0; j < array.Length; j++)
				{
					if (list[i].Key.Contains(array[j]))
					{
						tmp = array[j];
					}
				}
				if (!string.IsNullOrEmpty(tmp))
				{
					dataMy dataMy2 = shengfen.Find((dataMy m) => m.Key == tmp);
					if (dataMy2 == null)
					{
						shengfen.Add(new dataMy(tmp, list[i].Value));
					}
					else
					{
						dataMy2.Value += list[i].Value;
					}
				}
			}
			Invoke((Action)delegate
			{
				long num = shengfen.Max((dataMy m) => m.Value);
				for (int k = 0; k < shengfen.Count; k++)
				{
					int num2 = 255 - (int)(shengfen[k].Value * 222 / num);
					if (shengfen[k].Value > 0 && shengfen[k].Value < 10 && num2 < 5)
					{
						num2 = 5;
					}
					hslChinaMap1.SetProvinceBackColor(shengfen[k].Key, Color.FromArgb(num2, 0, 0));
				}
			});
		}

		private void GetDataFromServer2(object obj)
		{
			OperateResult<NetHandle, string> operateResult = new NetSimplifyClient("118.24.36.220", 18467).ReadCustomerFromServer(1000, SoftBasic.FrameworkVersion.ToString());
			if (!operateResult.IsSuccess)
			{
				return;
			}
			MemoryStream memoryStream = new MemoryStream(Encoding.Unicode.GetBytes(operateResult.Content2));
			StreamReader streamReader = new StreamReader(memoryStream, Encoding.Unicode);
			Dictionary<string, long> dictionary = new Dictionary<string, long>();
			while (true)
			{
				string text = streamReader.ReadLine();
				if (text == null)
				{
					break;
				}
				dictionary.Add(text, long.Parse(streamReader.ReadLine()));
			}
			streamReader.Close();
			memoryStream.Close();
			List<dataMy> list = new List<dataMy>();
			foreach (KeyValuePair<string, long> item in dictionary)
			{
				list.Add(new dataMy(item.Key, item.Value));
			}
			list.Sort();
			list.Reverse();
			string[] array = new string[372]
			{
				"北京", "天津", "上海", "重庆", "香港", "澳门", "湖州", "嘉兴", "绍兴", "宁波",
				"舟山", "金华", "台州", "温州", "丽水", "衢州", "杭州", "徐州", "连云港", "宿迁",
				"淮安", "盐城", "扬州", "泰州", "南通", "镇江", "常州", "无锡", "苏州", "南京",
				"聊城", "德州", "滨州", "东营", "淄博", "泰安", "莱芜", "菏泽", "济宁", "枣庄",
				"临沂", "潍坊", "日照", "青岛", "烟台", "威海", "济南", "亳州", "淮北", "宿州",
				"阜阳", "淮南", "蚌埠", "六安", "滁州", "安庆", "巢湖", "马鞍山", "铜陵", "芜湖",
				"池州", "黄山", "宣城", "合肥", "三门峡", "济源", "焦作", "新乡", "鹤壁", "安阳",
				"濮阳", "洛阳", "开封", "商丘", "平顶山", "许昌", "漯河", "周口", "南阳", "驻马店",
				"信阳", "郑州", "张家口", "承德", "秦皇岛", "唐山", "保定", "廊坊", "沧州", "衡水",
				"邢台", "邯郸", "石家庄", "大同", "朔州", "忻州", "吕梁", "晋中", "阳泉", "临汾",
				"长治", "运城", "晋城", "太原", "朝阳", "葫芦岛", "锦州", "阜新", "铁岭", "抚顺",
				"盘锦", "鞍山", "辽阳", "本溪", "营口", "丹东", "大连", "沈阳", "白城", "松原",
				"四平", "吉林", "辽源", "通化", "白山", "延吉", "长春", "黑河", "齐齐哈尔", "大庆",
				"绥化", "伊春", "鹤岗", "佳木斯", "双鸭山", "鸡西", "七台河", "牡丹江", "哈尔滨", "乌海",
				"巴彦淖尔", "包头", "鄂尔多斯", "乌兰察布", "呼伦贝尔", "赤峰", "通辽", "呼和浩特", "榆林", "延安",
				"铜川", "宝鸡", "咸阳", "渭南", "汉中", "安康", "商洛", "西安", "十堰", "襄樊",
				"神农架", "恩施", "宜昌", "随州", "荆门", "荆州", "孝感", "天门", "潜江", "仙桃",
				"黄冈", "鄂州", "黄石", "咸宁", "武汉", "湘西土家", "张家界", "常德", "岳阳", "益阳",
				"怀化", "娄底", "湘潭", "株洲", "邵阳", "衡阳", "永州", "郴州", "长沙", "九江",
				"景德镇", "上饶", "萍乡", "宜春", "新余", "鹰潭", "抚州", "吉安", "赣州", "南昌",
				"南平", "宁德", "三明", "龙岩", "莆田", "泉州", "厦门", "漳州", "福州", "湛江",
				"茂名", "阳江", "云浮", "江门", "肇庆", "佛山", "中山", "珠海", "清远", "惠州",
				"东莞", "深圳", "韶关", "河源", "汕尾", "梅州", "揭阳", "汕头", "潮州", "广州",
				"百色", "河池", "柳州", "桂林", "崇左", "防城港", "钦州", "北海", "玉林", "来宾",
				"贵港", "梧州", "贺州", "南宁", "毕节", "遵义", "铜仁", "六盘水", "安顺", "黔西南",
				"黔南", "黔东南", "贵阳", "甘孜藏", "阿坝藏", "凉山彝", "攀枝花", "雅安", "绵阳", "德阳",
				"广元", "巴中", "达州", "南充", "广安", "遂宁", "资阳", "眉山", "内江", "乐山",
				"自贡", "宜宾", "泸州", "成都", "保山", "丽江", "昭通", "曲靖", "玉溪", "临沧",
				"普洱", "怒江", "德宏", "红河", "楚雄", "大理", "文山", "西双版", "迪庆", "昆明",
				"三亚", "儋州", "五指山", "文昌", "琼海", "万宁", "东方", "白沙黎", "昌江黎", "乐东黎",
				"陵水黎", "保亭黎", "琼中黎", "海口", "基隆", "桃园", "新竹", "宜兰", "苗栗", "台中",
				"花莲", "彰化", "南投", "云林", "嘉义", "台南", "高雄", "屏东", "台东", "澎湖",
				"台北", "日喀则", "山南", "林芝", "昌都", "那曲", "阿里地", "拉萨", "海东", "西宁",
				"海西", "海北", "海南藏族", "黄南", "果洛", "玉树", "克拉玛依", "吐鲁番", "哈密", "阿克苏",
				"博尔塔拉", "巴音", "昌吉", "孜勒苏柯", "伊宁", "阿勒泰", "和田", "石河子", "阿拉尔", "塔城",
				"喀什", "伊犁哈", "乌鲁木齐", "嘉峪关", "酒泉", "张掖", "金昌", "白银", "天水", "武威",
				"平凉", "庆阳", "定西", "陇南", "临夏", "甘南", "兰州", "石嘴山", "吴忠", "固原",
				"中卫", "银川"
			};
			List<dataMy> shengfen = new List<dataMy>();
			for (int i = 0; i < list.Count; i++)
			{
				string tmp = string.Empty;
				for (int j = 0; j < array.Length; j++)
				{
					if (list[i].Key.Contains(array[j]))
					{
						tmp = array[j];
					}
				}
				if (!string.IsNullOrEmpty(tmp))
				{
					dataMy dataMy2 = shengfen.Find((dataMy m) => m.Key == tmp);
					if (dataMy2 == null)
					{
						shengfen.Add(new dataMy(tmp, list[i].Value));
					}
					else
					{
						dataMy2.Value += list[i].Value;
					}
				}
			}
			Invoke((Action)delegate
			{
				for (int k = 0; k < shengfen.Count; k++)
				{
					if (shengfen[k].Value == 0)
					{
						shengfen[k].Value = 0L;
					}
					else if (shengfen[k].Value <= 10)
					{
						shengfen[k].Value = 1L;
					}
					else if (shengfen[k].Value <= 100)
					{
						shengfen[k].Value = 2L;
					}
					else if (shengfen[k].Value <= 1000)
					{
						shengfen[k].Value = 3L;
					}
					else if (shengfen[k].Value <= 3000)
					{
						shengfen[k].Value = 4L;
					}
					else if (shengfen[k].Value <= 5000)
					{
						shengfen[k].Value = 5L;
					}
					else if (shengfen[k].Value <= 10000)
					{
						shengfen[k].Value = 6L;
					}
					else if (shengfen[k].Value <= 13000)
					{
						shengfen[k].Value = 7L;
					}
					else if (shengfen[k].Value <= 15000)
					{
						shengfen[k].Value = 8L;
					}
					else if (shengfen[k].Value <= 20000)
					{
						shengfen[k].Value = 9L;
					}
					else if (shengfen[k].Value <= 25000)
					{
						shengfen[k].Value = 10L;
					}
					else if (shengfen[k].Value <= 30000)
					{
						shengfen[k].Value = 11L;
					}
					else if (shengfen[k].Value <= 50000)
					{
						shengfen[k].Value = 12L;
					}
					else if (shengfen[k].Value <= 100000)
					{
						shengfen[k].Value = 13L;
					}
					else
					{
						shengfen[k].Value = 15L;
					}
				}
				hslChinaMap1.MarkCity(shengfen.Select((dataMy m) => m.Key).ToArray(), shengfen.Select((dataMy m) => (int)m.Value).ToArray(), shengfen.Select((dataMy m) => Color.Yellow).ToArray());
			});
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
			this.userControlHead1 = new HslCommunicationDemo.DemoControl.UserControlHead();
			this.hslChinaMap1 = new HslControls.Advanced.HslChinaMap();
			base.SuspendLayout();
			this.userControlHead1.BackColor = System.Drawing.Color.FromArgb(64, 64, 64);
			this.userControlHead1.Dock = System.Windows.Forms.DockStyle.Top;
			this.userControlHead1.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			this.userControlHead1.Location = new System.Drawing.Point(0, 0);
			this.userControlHead1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.userControlHead1.MinimumSize = new System.Drawing.Size(800, 32);
			this.userControlHead1.Name = "userControlHead1";
			this.userControlHead1.Size = new System.Drawing.Size(1004, 32);
			this.userControlHead1.TabIndex = 21;
			this.hslChinaMap1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
			this.hslChinaMap1.CanMouseMoveHighlight = false;
			this.hslChinaMap1.GraphicColor = System.Drawing.Color.FromArgb(24, 24, 24);
			this.hslChinaMap1.Location = new System.Drawing.Point(0, 32);
			this.hslChinaMap1.Name = "hslChinaMap1";
			this.hslChinaMap1.OffsetX = 0;
			this.hslChinaMap1.OffsetY = 0;
			this.hslChinaMap1.ProvinceColor = System.Drawing.Color.FromArgb(32, 32, 32);
			this.hslChinaMap1.Size = new System.Drawing.Size(1004, 612);
			this.hslChinaMap1.TabIndex = 22;
			base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 17f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.AutoScroll = true;
			base.ClientSize = new System.Drawing.Size(1004, 645);
			base.Controls.Add(this.hslChinaMap1);
			base.Controls.Add(this.userControlHead1);
			this.Font = new System.Drawing.Font("微软雅黑", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 134);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "FormHslMap";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "HSL全国使用分布";
			base.Load += new System.EventHandler(FormTcpDebug_Load);
			base.Shown += new System.EventHandler(FormHslMap_Shown);
			base.ResumeLayout(false);
		}
	}
}
