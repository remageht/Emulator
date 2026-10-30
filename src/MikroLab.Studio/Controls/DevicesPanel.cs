using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Emulator.Core.Machine;

namespace MikroLab.Studio.Controls
{
    internal sealed class DevicesPanel : UserControl
    {
        private readonly Label _portA;
        private readonly Label _portB;
        private readonly Label _portC;
        private readonly LedBar _lamps;
        private readonly SevenSegmentDisplay _display;
        private readonly ToggleSwitch[] _tumblers;
        private readonly TrackBar[] _analogSliders;
        private readonly Label[] _analogValues;
        private bool _syncing;

        public event Action<int, bool> TumblerChanged;
        public event Action<int, double> AnalogChanged;

        public DevicesPanel()
        {
            BackColor = Theme.Panel;
            AutoScroll = true;
            Padding = new Padding(10, 0, 10, 10);

            _portA = MakeValueLabel();
            _portB = MakeValueLabel();
            _portC = MakeValueLabel();
            _lamps = new LedBar { Dock = DockStyle.Fill, Height = 46, Margin = new Padding(0, 0, 0, 8) };
            _display = new SevenSegmentDisplay { Dock = DockStyle.Fill, Height = 104, Margin = new Padding(0, 0, 0, 8) };
            _tumblers = new ToggleSwitch[3];
            for (int i = 0; i < _tumblers.Length; i++)
            {
                int index = i;
                _tumblers[i] = new ToggleSwitch
                {
                    Dock = DockStyle.Fill,
                    Height = 30,
                    Text = "SA" + (i + 1) + "  ·  перемикач " + (i + 1),
                    Margin = new Padding(0, 0, 0, 2)
                };
                _tumblers[i].CheckedChanged += (sender, args) =>
                {
                    if (_syncing) return;
                    TumblerChanged?.Invoke(index, _tumblers[index].Checked);
                };
            }
            _analogSliders = new TrackBar[4];
            _analogValues = new Label[4];

            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 0,
                BackColor = Theme.Panel,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;
            AddHeader(stack, ref row, "ПОРТИ ВВ55 · КР580ВВ55");
            AddStackRow(stack, ref row, BuildPortRow("Порт A  ·  лампи", _portA));
            AddStackRow(stack, ref row, BuildPortRow("Порт B  ·  перемикачі", _portB));
            AddStackRow(stack, ref row, BuildPortRow("Порт C  ·  керування", _portC));

            AddHeader(stack, ref row, "СВІТЛОДІОДИ · ПОРТ A");
            AddStackRow(stack, ref row, _lamps);

            AddHeader(stack, ref row, "ПЕРЕМИКАЧІ · ПОРТ B");
            for (int i = 0; i < _tumblers.Length; i++)
            {
                AddStackRow(stack, ref row, _tumblers[i]);
            }

            AddHeader(stack, ref row, "7-СЕГМЕНТНІ ІНДИКАТОРИ");
            AddStackRow(stack, ref row, _display);

            AddHeader(stack, ref row, "АНАЛОГОВИЙ ВХІД · 0–10 В");
            for (int i = 0; i < 4; i++)
            {
                AddStackRow(stack, ref row, BuildAnalogRow(i));
            }

            Controls.Add(stack);
        }

        public void UpdateSnapshot(MachineSnapshot snapshot)
        {
            if (snapshot == null) return;

            _portA.Text = snapshot.PpiLamps.ToString("X2") + "h";
            _portC.Text = snapshot.PpiControlWord.ToString("X2") + "h";

            int tumblers = 1;
            if (snapshot.Tumbler1) tumblers |= 9;
            if (snapshot.Tumbler2) tumblers |= 5;
            if (snapshot.Tumbler3) tumblers |= 3;
            _portB.Text = tumblers.ToString("X2") + "h";

            _lamps.Value = snapshot.PpiLamps;
            _display.SetDigits(snapshot.Digits);

            _syncing = true;
            _tumblers[0].SetCheckedSilently(snapshot.Tumbler1);
            _tumblers[1].SetCheckedSilently(snapshot.Tumbler2);
            _tumblers[2].SetCheckedSilently(snapshot.Tumbler3);
            _syncing = false;

            if (snapshot.AnalogVoltages != null)
            {
                for (int i = 0; i < _analogValues.Length && i < snapshot.AnalogVoltages.Length; i++)
                {
                    double volts = snapshot.AnalogVoltages[i];
                    int adc = (int)Math.Round(volts * 25.5);
                    if (adc < 0) adc = 0;
                    if (adc > 255) adc = 255;
                    _analogValues[i].Text = volts.ToString("0.0") + " В · ADC " + adc;
                }
            }
        }

        private Label MakeValueLabel()
        {
            return new Label
            {
                Text = "00h",
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                ForeColor = Theme.Text,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
        }

        private static Control BuildPortRow(string caption, Label value)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 2),
                BackColor = Theme.Panel
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            var label = new Label
            {
                Text = caption,
                ForeColor = Theme.Muted,
                Font = Theme.UiFont,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                BackColor = Color.Transparent
            };
            row.Controls.Add(label, 0, 0);
            row.Controls.Add(value, 1, 0);
            return row;
        }

        private Control BuildAnalogRow(int channel)
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 30,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 0),
                BackColor = Theme.Panel
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));

            var label = new Label
            {
                Text = "Канал " + channel,
                ForeColor = Theme.Muted,
                Font = Theme.UiFont,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                BackColor = Color.Transparent
            };
            var slider = new TrackBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                TickStyle = TickStyle.None,
                AutoSize = false,
                Height = 24,
                BackColor = Theme.Panel,
                Margin = new Padding(0, 2, 6, 2)
            };
            var value = new Label
            {
                Text = "0.0 В · ADC 0",
                ForeColor = Theme.Muted,
                Font = Theme.MonoSmall,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };

            int index = channel;
            slider.ValueChanged += (sender, args) =>
            {
                double volts = slider.Value / 10.0;
                value.Text = volts.ToString("0.0") + " В · ADC " + (int)Math.Round(volts * 25.5);
                AnalogChanged?.Invoke(index, volts);
            };

            row.Controls.Add(label, 0, 0);
            row.Controls.Add(slider, 1, 0);
            row.Controls.Add(value, 2, 0);

            _analogSliders[channel] = slider;
            _analogValues[channel] = value;
            return row;
        }

        private static void AddStackRow(TableLayoutPanel stack, ref int row, Control control)
        {
            stack.RowCount = row + 1;
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.Fill;
            stack.Controls.Add(control, 0, row);
            row++;
        }

        private static void AddHeader(TableLayoutPanel stack, ref int row, string text)
        {
            var header = new Label
            {
                Text = text,
                Font = Theme.HeaderFont,
                ForeColor = Theme.Muted,
                AutoSize = true,
                Margin = new Padding(0, 12, 0, 6),
                BackColor = Color.Transparent
            };
            AddStackRow(stack, ref row, header);
        }
    }

    internal sealed class LedBar : Control
    {
        private byte _value;

        public LedBar()
        {
            DoubleBuffered = true;
            BackColor = Theme.Panel;
        }

        public byte Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int count = 8;
            int step = Width / count;
            int diameter = Math.Min(22, Math.Max(12, step - 14));
            int top = Math.Max(2, (Height - diameter) / 2 - 5);

            for (int i = 0; i < count; i++)
            {
                int bit = 7 - i;
                bool on = ((_value >> bit) & 1) != 0;
                int cx = i * step + step / 2;
                int x = cx - diameter / 2;

                using (var ring = new SolidBrush(Color.FromArgb(24, 25, 27)))
                {
                    e.Graphics.FillEllipse(ring, x - 2, top - 2, diameter + 4, diameter + 4);
                }
                using (var glow = new SolidBrush(on ? Color.FromArgb(48, 110, 60) : Theme.PanelAlt))
                {
                    e.Graphics.FillEllipse(glow, x - 1, top - 1, diameter + 2, diameter + 2);
                }
                using (var led = new SolidBrush(on ? Theme.Green : Color.FromArgb(58, 61, 65)))
                {
                    e.Graphics.FillEllipse(led, x, top, diameter, diameter);
                }
                if (on)
                {
                    using (var highlight = new SolidBrush(Color.FromArgb(190, 255, 215, 190)))
                    {
                        e.Graphics.FillEllipse(highlight, x + diameter / 3, top + diameter / 4, Math.Max(3, diameter / 5), Math.Max(3, diameter / 5));
                    }
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    "D" + bit,
                    Theme.MonoSmall,
                    new Rectangle(cx - step / 2, top + diameter + 2, step, 14),
                    on ? Theme.Green : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            }
        }
    }

    internal sealed class SevenSegmentDisplay : Control
    {
        private byte[] _digits = new byte[8];

        public SevenSegmentDisplay()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(20, 21, 24);
        }

        public void SetDigits(byte[] digits)
        {
            if (digits == null) return;
            bool changed = _digits.Length != digits.Length;
            if (!changed)
            {
                for (int i = 0; i < digits.Length; i++)
                {
                    if (_digits[i] != digits[i]) { changed = true; break; }
                }
            }
            if (!changed) return;

            _digits = new byte[digits.Length];
            Array.Copy(digits, _digits, digits.Length);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var background = new SolidBrush(Color.FromArgb(20, 21, 24)))
            {
                e.Graphics.FillRectangle(background, ClientRectangle);
            }

            int count = Math.Max(1, _digits.Length);
            int gap = 4;
            int digitWidth = Math.Max(18, (Width - gap * (count - 1)) / count);
            int digitHeight = Math.Min(Height - 18, 78);
            int top = Math.Max(2, (Height - digitHeight - 14) / 2);

            for (int i = 0; i < count; i++)
            {
                int x = i * (digitWidth + gap);
                Rectangle bounds = new Rectangle(x, top, digitWidth, digitHeight);
                DrawDigit(e.Graphics, bounds, _digits[i]);
                TextRenderer.DrawText(
                    e.Graphics,
                    i.ToString(),
                    Theme.MonoSmall,
                    new Rectangle(x, top + digitHeight + 2, digitWidth, 14),
                    Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            }
        }

        private static void DrawDigit(Graphics graphics, Rectangle bounds, byte value)
        {
            int thickness = Math.Max(4, Math.Min(7, bounds.Width / 7));
            int margin = thickness + 3;

            Color onColor = Color.FromArgb(255, 122, 89);
            Color offColor = Color.FromArgb(46, 48, 52);

            var segments = new[]
            {
                (Bit: 0x01, X1: bounds.Left + margin, Y1: bounds.Top + thickness / 2, X2: bounds.Right - margin, Y2: bounds.Top + thickness / 2),
                (Bit: 0x02, X1: bounds.Right - thickness / 2, Y1: bounds.Top + margin, X2: bounds.Right - thickness / 2, Y2: bounds.Top + bounds.Height / 2 - 2),
                (Bit: 0x04, X1: bounds.Right - thickness / 2, Y1: bounds.Top + bounds.Height / 2 + 2, X2: bounds.Right - thickness / 2, Y2: bounds.Bottom - margin),
                (Bit: 0x08, X1: bounds.Left + margin, Y1: bounds.Bottom - thickness / 2, X2: bounds.Right - margin, Y2: bounds.Bottom - thickness / 2),
                (Bit: 0x10, X1: bounds.Left + thickness / 2, Y1: bounds.Top + bounds.Height / 2 + 2, X2: bounds.Left + thickness / 2, Y2: bounds.Bottom - margin),
                (Bit: 0x20, X1: bounds.Left + thickness / 2, Y1: bounds.Top + margin, X2: bounds.Left + thickness / 2, Y2: bounds.Top + bounds.Height / 2 - 2),
                (Bit: 0x40, X1: bounds.Left + margin, Y1: bounds.Top + bounds.Height / 2, X2: bounds.Right - margin, Y2: bounds.Top + bounds.Height / 2)
            };

            foreach (var segment in segments)
            {
                bool on = (value & segment.Bit) != 0;
                using (var pen = new Pen(on ? onColor : offColor, thickness))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    graphics.DrawLine(pen, segment.X1, segment.Y1, segment.X2, segment.Y2);
                }
            }

            if ((value & 0x80) != 0)
            {
                using (var dot = new SolidBrush(onColor))
                {
                    int size = Math.Max(4, thickness);
                    graphics.FillEllipse(dot, bounds.Right - size - 2, bounds.Bottom - size - 2, size, size);
                }
            }
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private bool _checked;

        public ToggleSwitch()
        {
            DoubleBuffered = true;
            BackColor = Theme.Panel;
            Height = 30;
            Cursor = Cursors.Hand;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
        }

        public bool Checked => _checked;

        public event EventHandler CheckedChanged;

        public void SetCheckedSilently(bool value)
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            _checked = !_checked;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(Width - 48, (Height - 20) / 2, 42, 20);
            using (GraphicsPath path = Theme.RoundedRect(track, 10))
            using (var fill = new SolidBrush(_checked ? Theme.AccentDark : Color.FromArgb(52, 55, 59)))
            using (var border = new Pen(_checked ? Theme.Accent : Theme.Border))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

            int knobSize = 14;
            int knobX = _checked ? track.Right - knobSize - 3 : track.Left + 3;
            using (var knob = new SolidBrush(_checked ? Color.White : Theme.Muted))
            {
                e.Graphics.FillEllipse(knob, knobX, track.Top + 3, knobSize, knobSize);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                new Rectangle(0, 0, Math.Max(0, Width - 56), Height),
                _checked ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
