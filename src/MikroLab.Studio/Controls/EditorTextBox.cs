using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MikroLab.Studio.Controls
{
    /// <summary>
    /// RichTextBox that reports scrolling so the line-number gutter can stay in sync.
    /// </summary>
    internal sealed class EditorTextBox : RichTextBox
    {
        private const int WM_VSCROLL = 0x0115;
        private const int WM_HSCROLL = 0x0114;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int EM_GETSCROLLPOS = 0x04DD;
        private const int EM_SETSCROLLPOS = 0x04DE;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref POINT lParam);

        public event EventHandler ViewScrolled;

        public EditorTextBox()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Editor;
            ForeColor = Theme.Text;
            Font = Theme.MonoFont;
            WordWrap = false;
            DetectUrls = false;
            AutoWordSelection = false;
            ShortcutsEnabled = true;
            ScrollBars = RichTextBoxScrollBars.Both;
            HideSelection = false;
            AcceptsTab = true;
        }

        public Point GetScrollPosition()
        {
            if (!IsHandleCreated) return Point.Empty;
            var point = new POINT();
            SendMessage(Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref point);
            return new Point(point.X, point.Y);
        }

        public void SetScrollPosition(Point position)
        {
            if (!IsHandleCreated) return;
            var point = new POINT { X = position.X, Y = position.Y };
            SendMessage(Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref point);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL || m.Msg == WM_MOUSEWHEEL)
            {
                ViewScrolled?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
