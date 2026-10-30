using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MikroLab.Studio
{
    internal static class Theme
    {
        public static readonly Color Window = Color.FromArgb(30, 31, 34);
        public static readonly Color Panel = Color.FromArgb(43, 45, 48);
        public static readonly Color PanelAlt = Color.FromArgb(38, 40, 43);
        public static readonly Color Editor = Color.FromArgb(30, 31, 34);
        public static readonly Color Gutter = Color.FromArgb(37, 39, 42);
        public static readonly Color Border = Color.FromArgb(58, 61, 65);
        public static readonly Color Text = Color.FromArgb(223, 225, 229);
        public static readonly Color Muted = Color.FromArgb(157, 160, 168);
        public static readonly Color Accent = Color.FromArgb(76, 154, 255);
        public static readonly Color AccentDark = Color.FromArgb(54, 116, 200);
        public static readonly Color Green = Color.FromArgb(106, 171, 115);
        public static readonly Color Red = Color.FromArgb(241, 109, 107);
        public static readonly Color Yellow = Color.FromArgb(232, 191, 106);
        public static readonly Color Purple = Color.FromArgb(197, 134, 192);
        public static readonly Color Teal = Color.FromArgb(78, 201, 176);
        public static readonly Color Number = Color.FromArgb(181, 206, 168);
        public static readonly Color StringColor = Color.FromArgb(206, 145, 120);
        public static readonly Color Comment = Color.FromArgb(106, 153, 85);
        public static readonly Color Label = Color.FromArgb(220, 220, 170);

        public static readonly Font UiFont = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font UiBold = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font HeaderFont = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font MonoFont = new Font("Consolas", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font MonoSmall = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font MonoBold = new Font("Consolas", 12F, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font IconFont = new Font("Segoe UI Symbol", 11F, FontStyle.Regular, GraphicsUnit.Point);

        public static Color Mix(Color a, Color b, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }

        public static Color Lighten(Color color, double amount) => Mix(color, Color.White, amount);
        public static Color Darken(Color color, double amount) => Mix(color, Color.Black, amount);

        public static Button MakeButton(string text, Color backColor, Color foreColor, int width, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = UiFont,
                Cursor = Cursors.Hand,
                TabStop = false,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0, 0, 6, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Lighten(backColor, 0.12);
            button.FlatAppearance.MouseDownBackColor = Darken(backColor, 0.12);
            if (onClick != null)
            {
                button.Click += onClick;
            }
            return button;
        }

        public static Button MakeOutlineButton(string text, int width, EventHandler onClick)
        {
            var button = MakeButton(text, PanelAlt, Text, width, onClick);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Border;
            return button;
        }

        public static Label MakeHeader(string text)
        {
            return new Label
            {
                Text = text,
                Font = HeaderFont,
                ForeColor = Muted,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            };
        }

        public static Label MakeLabel(string text, Color color, Font font = null)
        {
            return new Label
            {
                Text = text,
                ForeColor = color,
                Font = font ?? UiFont,
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        public static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = Panel;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = Color.FromArgb(50, 52, 56);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = PanelAlt;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = PanelAlt;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Muted;
            grid.ColumnHeadersDefaultCellStyle.Font = MonoSmall;
            grid.ColumnHeadersHeight = 26;
            grid.DefaultCellStyle.BackColor = Panel;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = AccentDark;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = MonoSmall;
            grid.DefaultCellStyle.Padding = new Padding(1, 0, 1, 0);
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowTemplate.Height = 21;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.ShowCellToolTips = false;
            grid.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
        }

        public static void DrawSectionHeader(Graphics g, Rectangle bounds, string text)
        {
            using (var brush = new SolidBrush(PanelAlt))
            {
                g.FillRectangle(brush, bounds);
            }
            using (var pen = new Pen(Border))
            {
                g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }
            TextRenderer.DrawText(
                g,
                text,
                HeaderFont,
                new Rectangle(bounds.Left + 10, bounds.Top, bounds.Width - 20, bounds.Height),
                Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => Theme.PanelAlt;
        public override Color MenuStripGradientEnd => Theme.PanelAlt;
        public override Color MenuItemSelected => Theme.AccentDark;
        public override Color MenuItemSelectedGradientBegin => Theme.AccentDark;
        public override Color MenuItemSelectedGradientEnd => Theme.AccentDark;
        public override Color MenuItemPressedGradientBegin => Theme.Panel;
        public override Color MenuItemPressedGradientEnd => Theme.Panel;
        public override Color MenuItemBorder => Theme.AccentDark;
        public override Color MenuBorder => Theme.Border;
        public override Color ToolStripDropDownBackground => Theme.PanelAlt;
        public override Color ImageMarginGradientBegin => Theme.PanelAlt;
        public override Color ImageMarginGradientMiddle => Theme.PanelAlt;
        public override Color ImageMarginGradientEnd => Theme.PanelAlt;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
        public override Color CheckBackground => Theme.AccentDark;
        public override Color CheckSelectedBackground => Theme.Accent;
        public override Color CheckPressedBackground => Theme.AccentDark;
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                e.TextColor = Color.White;
            }
            else if (!e.Item.Enabled)
            {
                e.TextColor = Theme.Muted;
            }
            else
            {
                e.TextColor = Theme.Text;
            }
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.Text;
            base.OnRenderArrow(e);
        }
    }

    internal sealed class DarkTabControl : TabControl
    {
        public DarkTabControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            DrawMode = TabDrawMode.OwnerDrawFixed;
            ItemSize = new Size(120, 30);
            SizeMode = TabSizeMode.Fixed;
            Padding = new Point(14, 6);
            Font = Theme.UiBold;
            BackColor = Theme.Panel;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= TabPages.Count) return;

            bool selected = SelectedIndex == e.Index;
            Rectangle bounds = e.Bounds;
            using (var background = new SolidBrush(selected ? Theme.Panel : Theme.PanelAlt))
            {
                e.Graphics.FillRectangle(background, bounds);
            }
            if (selected)
            {
                using (var accent = new SolidBrush(Theme.Accent))
                {
                    e.Graphics.FillRectangle(accent, bounds.Left, bounds.Bottom - 2, bounds.Width, 2);
                }
            }
            TextRenderer.DrawText(
                e.Graphics,
                TabPages[e.Index].Text,
                Font,
                bounds,
                selected ? Theme.Text : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            using (var brush = new SolidBrush(Theme.Panel))
            {
                pevent.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }
    }
}
