using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Emulator.Core.Machine;

namespace MikroLab.Studio.Controls
{
    internal sealed class RegisterPanel : Control
    {
        private MachineSnapshot _snapshot;

        public RegisterPanel()
        {
            DoubleBuffered = true;
            BackColor = Theme.Panel;
            Height = 214;
            Dock = DockStyle.Top;
        }

        public void UpdateSnapshot(MachineSnapshot snapshot)
        {
            _snapshot = snapshot;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var background = new SolidBrush(Theme.Panel))
            {
                graphics.FillRectangle(background, ClientRectangle);
            }
            Theme.DrawSectionHeader(graphics, new Rectangle(0, 0, Width, 26), "РЕГИСТРЫ · КР580ВМ80А");

            if (_snapshot == null)
            {
                TextRenderer.DrawText(
                    graphics,
                    "Нет данных",
                    Theme.UiFont,
                    new Rectangle(12, 36, Width - 24, 24),
                    Theme.Muted,
                    TextFormatFlags.Left);
                return;
            }

            const int pad = 10;
            const int gap = 6;
            int top = 34;
            int boxHeight = 42;
            int boxWidth = Math.Max(50, (Width - pad * 2 - gap * 3) / 4);

            DrawRegister(graphics, pad, top, boxWidth, boxHeight, "A", _snapshot.A.ToString("X2"));
            DrawRegister(graphics, pad + (boxWidth + gap), top, boxWidth, boxHeight, "B", _snapshot.B.ToString("X2"));
            DrawRegister(graphics, pad + (boxWidth + gap) * 2, top, boxWidth, boxHeight, "C", _snapshot.C.ToString("X2"));
            DrawRegister(graphics, pad + (boxWidth + gap) * 3, top, boxWidth, boxHeight, "D", _snapshot.D.ToString("X2"));

            top += boxHeight + gap;
            DrawRegister(graphics, pad, top, boxWidth, boxHeight, "E", _snapshot.E.ToString("X2"));
            DrawRegister(graphics, pad + (boxWidth + gap), top, boxWidth, boxHeight, "H", _snapshot.H.ToString("X2"));
            DrawRegister(graphics, pad + (boxWidth + gap) * 2, top, boxWidth, boxHeight, "L", _snapshot.L.ToString("X2"));
            DrawRegister(graphics, pad + (boxWidth + gap) * 3, top, boxWidth, boxHeight, "M", _snapshot.M.ToString("X2"));

            top += boxHeight + gap;
            DrawRegister(graphics, pad, top, boxWidth, boxHeight, "PC", _snapshot.PC.ToString("X4"), Theme.Accent);
            DrawRegister(graphics, pad + (boxWidth + gap), top, boxWidth, boxHeight, "SP", _snapshot.SP.ToString("X4"));
            DrawRegister(graphics, pad + (boxWidth + gap) * 2, top, boxWidth, boxHeight, "Циклы", _snapshot.Cycles.ToString("N0"));
            DrawRegister(graphics, pad + (boxWidth + gap) * 3, top, boxWidth, boxHeight, "Стек", _snapshot.CallStackDepth.ToString());

            top += boxHeight + gap + 6;
            DrawFlags(graphics, pad, top);
        }

        private void DrawRegister(Graphics graphics, int x, int y, int width, int height, string name, string value, Color? valueColor = null)
        {
            Rectangle bounds = new Rectangle(x, y, width, height);
            using (GraphicsPath path = Theme.RoundedRect(bounds, 4))
            using (var fill = new SolidBrush(Theme.PanelAlt))
            using (var border = new Pen(Theme.Border))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }

            TextRenderer.DrawText(
                graphics,
                name,
                Theme.HeaderFont,
                new Rectangle(x + 7, y + 4, width - 14, 14),
                Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top);

            TextRenderer.DrawText(
                graphics,
                value,
                Theme.MonoBold,
                new Rectangle(x + 6, y + 15, width - 12, 24),
                valueColor ?? Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawFlags(Graphics graphics, int x, int y)
        {
            TextRenderer.DrawText(
                graphics,
                "ФЛАГИ",
                Theme.HeaderFont,
                new Rectangle(x, y, 70, 22),
                Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            int chipX = x + 72;
            DrawFlag(graphics, ref chipX, y, "S", _snapshot.Flags.S);
            DrawFlag(graphics, ref chipX, y, "Z", _snapshot.Flags.Z);
            DrawFlag(graphics, ref chipX, y, "AC", _snapshot.Flags.AC);
            DrawFlag(graphics, ref chipX, y, "P", _snapshot.Flags.P);
            DrawFlag(graphics, ref chipX, y, "C", _snapshot.Flags.C);
        }

        private void DrawFlag(Graphics graphics, ref int x, int y, string name, bool value)
        {
            int width = name.Length > 1 ? 42 : 34;
            Rectangle bounds = new Rectangle(x, y, width, 22);
            using (GraphicsPath path = Theme.RoundedRect(bounds, 4))
            using (var fill = new SolidBrush(value ? Color.FromArgb(45, 82, 54) : Theme.PanelAlt))
            using (var border = new Pen(value ? Theme.Green : Theme.Border))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            TextRenderer.DrawText(
                graphics,
                name,
                Theme.UiBold,
                bounds,
                value ? Theme.Green : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            x += width + 5;
        }
    }
}
