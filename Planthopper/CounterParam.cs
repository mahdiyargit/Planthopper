using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Planthopper
{
    public sealed class CounterParam : GH_Param<GH_Integer>
    {
        public int I;
        private GH_Document _doc;
        public CounterParam() : base(new GH_InstanceDescription("Counter", "Counter", "Counter", "Planthopper", "Gear"))
        {
        }
        public bool Play { get; set; }
        protected override void CollectVolatileData_Custom()
        {
            if (_doc == null) _doc = OnPingDocument();
            ClearData();
            m_data.Append(new GH_Integer(I));
            if (!Play) return;
            I++;
            _doc?.ScheduleSolution(_interval, doc => ExpireSolution(false));

        }
        private int _interval = 30;
        public string IntervalString
        {
            get
            {
                string intervalString;
                if (_interval < 1000)
                    intervalString = $"{_interval} ms";
                else
                {
                    var timeSpan = new TimeSpan(10000L * _interval);
                    intervalString = timeSpan.Milliseconds == 0 ? (timeSpan.TotalSeconds != 1.0 ? (timeSpan.TotalMinutes != 1.0 ? (timeSpan.TotalSeconds >= 60.0 ? (timeSpan.TotalMinutes >= 60.0 ? (timeSpan.Minutes != 0 || timeSpan.Seconds != 0 ? string.Format("{0:0}:{1:00}:{2:00}", Math.Floor(timeSpan.TotalHours), timeSpan.Minutes, timeSpan.Seconds) : (timeSpan.TotalHours != 1.0 ? string.Format("{0:0} hours", timeSpan.TotalHours) : "1 hour")) : (timeSpan.Seconds != 0 ? string.Format("{0:0}:{1:0}", timeSpan.Minutes, timeSpan.Seconds) : string.Format("{0:0} minutes", timeSpan.TotalMinutes))) : string.Format("{0:0} seconds", timeSpan.TotalSeconds)) : "1 minute") : "1 second") : string.Format("{0} ms", _interval);
                }
                return intervalString;
            }
        }
        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            var item = Menu_AppendItem(menu, "Interval");
            item.ToolTipText = @"Specify the delay between frame updates.";
            menu = item.DropDown;
            Menu_AppendItem(menu, "20 ms", ManualItemClicked, true, _interval == 20).Tag = 20;
            Menu_AppendItem(menu, "30 ms", ManualItemClicked, true, _interval == 30).Tag = 30;
            Menu_AppendItem(menu, "50 ms", ManualItemClicked, true, _interval == 50).Tag = 50;
            Menu_AppendItem(menu, "100 ms", ManualItemClicked, true, _interval == 100).Tag = 100;
            Menu_AppendItem(menu, "200 ms", ManualItemClicked, true, _interval == 200).Tag = 200;
            Menu_AppendItem(menu, "500 ms", ManualItemClicked, true, _interval == 500).Tag = 500;
            Menu_AppendSeparator(menu);
            Menu_AppendItem(menu, "1 second", ManualItemClicked, true, _interval == 1000).Tag = 1000;
            Menu_AppendItem(menu, "2 seconds", ManualItemClicked, true, _interval == 2000).Tag = 2000;
            Menu_AppendItem(menu, "5 seconds", ManualItemClicked, true, _interval == 5000).Tag = 5000;
            Menu_AppendItem(menu, "10 seconds", ManualItemClicked, true, _interval == 10000).Tag = 10000;
            Menu_AppendItem(menu, "30 seconds", ManualItemClicked, true, _interval == 30000).Tag = 30000;
            Menu_AppendSeparator(menu);
            var stringBuilder = new StringBuilder();
            var timeSpan = TimeSpan.FromMilliseconds(_interval);
            if (timeSpan.TotalHours >= 1.0)
            {
                var int32 = Convert.ToInt32(Math.Floor(timeSpan.TotalHours));
                stringBuilder.Append($"{int32} * (60 * 60 * 1000)");
                timeSpan -= TimeSpan.FromHours(int32);
            }
            if (timeSpan.TotalMinutes >= 1.0)
            {
                var int32 = Convert.ToInt32(Math.Floor(timeSpan.TotalMinutes));
                if (stringBuilder.Length > 0)
                    stringBuilder.Append(" + ");
                stringBuilder.Append($"{int32} * (60 * 1000)");
                timeSpan -= TimeSpan.FromMinutes(int32);
            }
            if (timeSpan.TotalSeconds >= 1.0)
            {
                var int32 = Convert.ToInt32(Math.Floor(timeSpan.TotalSeconds));
                if (stringBuilder.Length > 0)
                    stringBuilder.Append(" + ");
                stringBuilder.Append($"{int32} * 1000");
                timeSpan -= TimeSpan.FromSeconds(int32);
            }
            if (timeSpan.TotalMilliseconds >= 1.0)
            {
                var int32 = Convert.ToInt32(Math.Floor(timeSpan.TotalMilliseconds));
                if (stringBuilder.Length > 0)
                    stringBuilder.Append(" + ");
                stringBuilder.Append($"{int32}");
            }
            Menu_AppendTextItem(menu, stringBuilder.ToString(), Menu_Interval_KeyDown, null, true).ToolTipText = @"Specify a custom interval in milliseconds.";
        }
        private void ManualItemClicked(object sender, EventArgs e)
        {
            _interval = (int)((ToolStripItem)sender).Tag;
            Instances.RedrawCanvas();
        }

        private void Menu_Interval_KeyDown(GH_MenuTextBox sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Return) return;
            if (!GH_Convert.ToInt32(sender.Text, out var d, GH_Conversion.Both)) return;
            _interval = d;
            Instances.RedrawCanvas();
        }
        public override bool Write(GH_IWriter writer)
        {
            writer.SetInt32("Interval", _interval);
            return base.Write(writer);
        }
        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("Interval"))
                _interval = reader.GetInt32("Interval");
            return base.Read(reader);
        }
        public override void CreateAttributes() => Attributes = new CounterAttributes(this);
        public override Guid ComponentGuid => new Guid("C03D41A0-FD2C-4302-A81B-85B27C19E6E7");
        public override GH_ParamKind Kind => GH_ParamKind.floating;
        protected override Bitmap Icon => Properties.Resources.counter;
        public override GH_ParamData DataType => GH_ParamData.local;
        public override GH_Exposure Exposure => GH_Exposure.tertiary;
        public override bool IconCapableUI => false;
    }
}