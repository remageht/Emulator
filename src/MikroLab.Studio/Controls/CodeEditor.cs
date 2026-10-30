using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MikroLab.Studio.Controls
{
    internal sealed class CodeEditor : UserControl
    {
        private readonly TableLayoutPanel _layout;
        private readonly EditorGutter _gutter;
        private readonly EditorTextBox _box;
        private readonly Timer _highlightTimer;
        private readonly HashSet<int> _breakpoints = new HashSet<int>();
        private int? _executionLine;

        private static readonly Regex TokenRegex = new Regex(
            @"(?<comment>;[^\n]*)" +
            @"|(?<string>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*')" +
            @"|(?<label>^[ \t]*[A-Za-z_.@?][\w.@?]*[ \t]*:)" +
            @"|(?<directive>\b(?:ORG|EQU|DB|DW|DS|END)\b)" +
            @"|(?<mnemonic>\b(?:NOP|HLT|EI|DI|MOV|MVI|LXI|LDA|STA|LHLD|SHLD|LDAX|STAX|XCHG|XTHL|SPHL|PCHL|ADD|ADC|SUB|SBB|ANA|XRA|ORA|CMP|ADI|ACI|SUI|SBI|ANI|XRI|ORI|CPI|INR|DCR|INX|DCX|DAD|RLC|RRC|RAL|RAR|DAA|CMA|STC|CMC|JMP|JNZ|JZ|JNC|JC|JPO|JPE|JP|JM|CALL|CNZ|CZ|CNC|CC|CPO|CPE|CP|CM|RET|RNZ|RZ|RNC|RC|RPO|RPE|RP|RM|RST|PUSH|POP|IN|OUT)\b)" +
            @"|(?<register>\b(?:A|B|C|D|E|H|L|M|SP|PSW)\b)" +
            @"|(?<number>\b(?:0x[0-9A-Fa-f]+|\$[0-9A-Fa-f]+|[0-9][0-9A-Fa-f]*[hH]|[01]+[bB]|[0-9]+)\b)",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public CodeEditor()
        {
            BackColor = Theme.Editor;
            Padding = new Padding(0);

            _gutter = new EditorGutter(this) { Dock = DockStyle.Fill };
            _box = new EditorTextBox { Dock = DockStyle.Fill };

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Theme.Editor
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _layout.Controls.Add(_gutter, 0, 0);
            _layout.Controls.Add(_box, 1, 0);
            Controls.Add(_layout);

            _highlightTimer = new Timer { Interval = 120 };
            _highlightTimer.Tick += OnHighlightTimerTick;

            _box.TextChanged += OnBoxTextChanged;
            _box.SelectionChanged += OnBoxSelectionChanged;
            _box.ViewScrolled += OnBoxViewScrolled;
            _box.KeyDown += OnBoxKeyDown;
            _box.Resize += (sender, args) => _gutter.Invalidate();

            UpdateGutterWidth();
        }

        public string Code
        {
            get => _box.Text;
            set
            {
                _box.Text = value ?? string.Empty;
                _box.SelectionStart = 0;
                _box.SelectionLength = 0;
                UpdateGutterWidth();
                HighlightNow();
                _gutter.Invalidate();
            }
        }

        public int CaretLine
        {
            get
            {
                int index = _box.SelectionStart;
                return _box.GetLineFromCharIndex(index) + 1;
            }
        }

        public int CaretColumn
        {
            get
            {
                int lineIndex = _box.GetLineFromCharIndex(_box.SelectionStart);
                int lineStart = _box.GetFirstCharIndexFromLine(lineIndex);
                if (lineStart < 0) lineStart = 0;
                return _box.SelectionStart - lineStart + 1;
            }
        }

        public int? ExecutionLine => _executionLine;
        public IReadOnlyCollection<int> Breakpoints => _breakpoints;

        public event EventHandler CodeChanged;
        public event EventHandler CaretMoved;
        public event Action<int> BreakpointToggled;

        public void FocusEditor()
        {
            _box.Focus();
        }

        public void SetBreakpointLines(IEnumerable<int> lines)
        {
            _breakpoints.Clear();
            if (lines != null)
            {
                foreach (int line in lines)
                {
                    if (line > 0) _breakpoints.Add(line);
                }
            }
            _gutter.Invalidate();
        }

        public bool HasBreakpoint(int line) => _breakpoints.Contains(line);

        public void SetExecutionLine(int? line)
        {
            if (_executionLine == line) return;
            _executionLine = line;
            _gutter.Invalidate();
        }

        public void GoToLine(int line)
        {
            int lineCount = _box.Lines.Length;
            if (lineCount <= 0) return;
            int target = Math.Max(1, Math.Min(lineCount, line));
            int charIndex = _box.GetFirstCharIndexFromLine(target - 1);
            if (charIndex < 0) charIndex = 0;
            _box.SelectionStart = charIndex;
            _box.SelectionLength = 0;
            _box.ScrollToCaret();
            _box.Focus();
            _gutter.Invalidate();
        }

        public void HighlightNow()
        {
            if (_box.TextLength == 0 || _box.TextLength > 200000)
            {
                return;
            }

            Point scroll = _box.GetScrollPosition();
            int caret = _box.SelectionStart;
            int selectionLength = _box.SelectionLength;

            _highlightTimer.Stop();
            _box.SuspendLayout();

            try
            {
                _box.SelectAll();
                _box.SelectionColor = Theme.Text;
                _box.SelectionFont = Theme.MonoFont;

                string text = _box.Text;
                foreach (Match match in TokenRegex.Matches(text))
                {
                    Color color;
                    if (match.Groups["comment"].Success) color = Theme.Comment;
                    else if (match.Groups["string"].Success) color = Theme.StringColor;
                    else if (match.Groups["label"].Success) color = Theme.Label;
                    else if (match.Groups["directive"].Success) color = Theme.Purple;
                    else if (match.Groups["mnemonic"].Success) color = Color.FromArgb(86, 156, 214);
                    else if (match.Groups["register"].Success) color = Theme.Teal;
                    else if (match.Groups["number"].Success) color = Theme.Number;
                    else continue;

                    _box.Select(match.Index, match.Length);
                    _box.SelectionColor = color;
                }

                _box.SelectionStart = Math.Min(caret, _box.TextLength);
                _box.SelectionLength = Math.Min(selectionLength, Math.Max(0, _box.TextLength - _box.SelectionStart));
                _box.SetScrollPosition(scroll);
            }
            finally
            {
                _box.ResumeLayout();
            }
        }

        private void OnHighlightTimerTick(object sender, EventArgs e)
        {
            _highlightTimer.Stop();
            HighlightNow();
        }

        private void OnBoxTextChanged(object sender, EventArgs e)
        {
            UpdateGutterWidth();
            _gutter.Invalidate();
            if (!_highlightTimer.Enabled)
            {
                _highlightTimer.Start();
            }
            CodeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnBoxSelectionChanged(object sender, EventArgs e)
        {
            _gutter.Invalidate();
            CaretMoved?.Invoke(this, EventArgs.Empty);
        }

        private void OnBoxViewScrolled(object sender, EventArgs e)
        {
            _gutter.Invalidate();
        }

        private void OnBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                if (e.Shift)
                {
                    UnindentSelection();
                }
                else
                {
                    _box.SelectedText = "    ";
                }
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F9)
            {
                // Let the host handle breakpoints through the form-level shortcut.
                e.SuppressKeyPress = true;
                e.Handled = false;
            }
        }

        private void UnindentSelection()
        {
            int lineIndex = _box.GetLineFromCharIndex(_box.SelectionStart);
            int lineStart = _box.GetFirstCharIndexFromLine(lineIndex);
            if (lineStart < 0) return;

            int remove = 0;
            while (remove < 4 && lineStart + remove < _box.TextLength)
            {
                if (_box.Text[lineStart + remove] != ' ') break;
                remove++;
            }
            if (remove > 0)
            {
                int caret = _box.SelectionStart;
                _box.Select(lineStart, remove);
                _box.SelectedText = string.Empty;
                _box.SelectionStart = Math.Max(lineStart, caret - remove);
            }
        }

        private void UpdateGutterWidth()
        {
            int lineCount = Math.Max(1, _box.Lines.Length);
            int digits = Math.Max(2, lineCount.ToString().Length);
            int width = 18 + digits * 8;
            if (width < 52) width = 52;
            if (Math.Abs(_layout.ColumnStyles[0].Width - width) > 0.5f)
            {
                _layout.ColumnStyles[0].Width = width;
                _gutter.Width = width;
            }
        }

        internal EditorTextBox Box => _box;
        internal int? ExecutionLineInternal => _executionLine;
        internal bool HasExecutionLine => _executionLine.HasValue;
        internal bool IsBreakpoint(int line) => _breakpoints.Contains(line);

        internal void DrawGutter(Graphics graphics, Rectangle bounds)
        {
            using (var background = new SolidBrush(Theme.Gutter))
            {
                graphics.FillRectangle(background, bounds);
            }
            using (var border = new Pen(Theme.Border))
            {
                graphics.DrawLine(border, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom);
            }

            int lineCount = _box.Lines.Length;
            if (lineCount == 0) lineCount = 1;

            int firstVisible = _box.GetLineFromCharIndex(_box.GetCharIndexFromPosition(new Point(2, 2)));
            int lastVisible = _box.GetLineFromCharIndex(_box.GetCharIndexFromPosition(new Point(2, Math.Max(2, _box.ClientSize.Height - 2))));
            if (lastVisible < firstVisible) lastVisible = firstVisible;

            for (int line = Math.Max(0, firstVisible - 1); line <= Math.Min(lineCount - 1, lastVisible + 1); line++)
            {
                int charIndex = _box.GetFirstCharIndexFromLine(line);
                if (charIndex < 0) continue;
                Point position = _box.GetPositionFromCharIndex(charIndex);
                int y = position.Y;
                if (y < -_box.Font.Height || y > bounds.Height) continue;

                int displayLine = line + 1;
                bool isExecution = _executionLine == displayLine;
                bool isBreakpoint = _breakpoints.Contains(displayLine);

                if (isExecution)
                {
                    using (var brush = new SolidBrush(Theme.Accent))
                    {
                        graphics.FillRectangle(brush, 0, y, 3, _box.Font.Height + 2);
                    }
                }

                if (isBreakpoint)
                {
                    using (var brush = new SolidBrush(Theme.Red))
                    {
                        int cx = isExecution ? 16 : 12;
                        graphics.FillEllipse(brush, cx - 4, y + (_box.Font.Height / 2) - 4, 9, 9);
                    }
                }

                Rectangle numberBounds = new Rectangle(4, y - 1, bounds.Width - 20, _box.Font.Height + 2);
                TextRenderer.DrawText(
                    graphics,
                    displayLine.ToString(),
                    Theme.MonoSmall,
                    numberBounds,
                    isExecution ? Theme.Accent : Theme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.Top);
            }
        }

        private sealed class EditorGutter : Control
        {
            private readonly CodeEditor _owner;

            public EditorGutter(CodeEditor owner)
            {
                _owner = owner;
                DoubleBuffered = true;
                BackColor = Theme.Gutter;
                Cursor = Cursors.Hand;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                _owner.DrawGutter(e.Graphics, ClientRectangle);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                int charIndex = _owner.Box.GetCharIndexFromPosition(new Point(2, Math.Max(1, e.Y)));
                int line = _owner.Box.GetLineFromCharIndex(charIndex) + 1;
                _owner.BreakpointToggled?.Invoke(line);
            }
        }
    }
}
