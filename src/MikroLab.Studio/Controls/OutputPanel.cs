using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Emulator.Core.Assembler;

namespace MikroLab.Studio.Controls
{
    internal enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    internal sealed class OutputPanel : UserControl
    {
        private readonly RichTextBox _log;
        private readonly ListBox _errors;
        private readonly Label _errorsHeader;
        private readonly List<ErrorEntry> _errorItems = new List<ErrorEntry>();

        public event Action<int> ErrorActivated;

        public OutputPanel()
        {
            BackColor = Theme.Panel;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Theme.PanelAlt
            };
            var title = new Label
            {
                Text = "ВЫВОД · ЖУРНАЛ СБОРКИ И ОШИБКИ",
                ForeColor = Theme.Muted,
                Font = Theme.HeaderFont,
                AutoSize = true,
                Location = new Point(10, 8)
            };
            var clearButton = Theme.MakeButton("Очистить", Theme.PanelAlt, Theme.Muted, 76, (sender, args) => Clear());
            clearButton.Height = 22;
            clearButton.Font = Theme.UiFont;
            clearButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            clearButton.Location = new Point(Width - 86, 3);
            header.Resize += (sender, args) => clearButton.Left = header.Width - clearButton.Width - 8;
            header.Controls.Add(title);
            header.Controls.Add(clearButton);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Theme.Panel,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _log = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Window,
                ForeColor = Theme.Text,
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false,
                HideSelection = false,
                Margin = new Padding(0)
            };

            var errorsHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Panel,
                Padding = new Padding(1, 0, 0, 0)
            };
            _errorsHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "ОШИБКИ (0)",
                ForeColor = Theme.Muted,
                Font = Theme.HeaderFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                BackColor = Theme.PanelAlt
            };
            _errors = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Window,
                ForeColor = Theme.Text,
                Font = Theme.MonoSmall,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                IntegralHeight = false
            };
            _errors.DrawItem += OnDrawError;
            _errors.DoubleClick += OnErrorDoubleClick;

            errorsHost.Controls.Add(_errors);
            errorsHost.Controls.Add(_errorsHeader);

            content.Controls.Add(_log, 0, 0);
            content.Controls.Add(errorsHost, 1, 0);

            Controls.Add(content);
            Controls.Add(header);
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (_log.TextLength > 200000)
            {
                _log.Clear();
            }

            Color color;
            switch (level)
            {
                case LogLevel.Success: color = Theme.Green; break;
                case LogLevel.Warning: color = Theme.Yellow; break;
                case LogLevel.Error: color = Theme.Red; break;
                default: color = Theme.Text; break;
            }

            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "]  " + message + "\n";
            _log.SelectionStart = _log.TextLength;
            _log.SelectionLength = 0;
            _log.SelectionColor = color;
            _log.AppendText(line);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        public void SetErrors(IReadOnlyList<AssemblerError> errors)
        {
            _errorItems.Clear();
            _errors.Items.Clear();

            if (errors != null)
            {
                foreach (AssemblerError error in errors)
                {
                    _errorItems.Add(new ErrorEntry(error.LineNumber, error.Message));
                }
            }

            foreach (ErrorEntry item in _errorItems)
            {
                _errors.Items.Add(item);
            }
            _errorsHeader.Text = "ОШИБКИ (" + _errorItems.Count + ")";
        }

        public void ClearErrors()
        {
            SetErrors(null);
        }

        public void Clear()
        {
            _log.Clear();
        }

        private void OnDrawError(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _errors.Items.Count) return;
            var item = (ErrorEntry)_errors.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var background = new SolidBrush(selected ? Theme.AccentDark : Theme.Window))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
            }

            string lineText = "Строка " + item.Line + ":";
            Rectangle lineBounds = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, 66, e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                lineText,
                Theme.MonoSmall,
                lineBounds,
                selected ? Color.White : Theme.Accent,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            Rectangle messageBounds = new Rectangle(e.Bounds.Left + 72, e.Bounds.Top, Math.Max(10, e.Bounds.Width - 76), e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                item.Message,
                Theme.MonoSmall,
                messageBounds,
                selected ? Color.White : Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void OnErrorDoubleClick(object sender, EventArgs e)
        {
            if (_errors.SelectedIndex < 0 || _errors.SelectedIndex >= _errorItems.Count) return;
            ErrorActivated?.Invoke(_errorItems[_errors.SelectedIndex].Line);
        }

        private sealed class ErrorEntry
        {
            public int Line { get; }
            public string Message { get; }

            public ErrorEntry(int line, string message)
            {
                Line = line;
                Message = message;
            }

            public override string ToString() => "Строка " + Line + ": " + Message;
        }
    }
}
