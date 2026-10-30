using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Emulator.Core.Machine;

namespace MikroLab.Studio.Controls
{
    internal sealed class MemoryPanel : UserControl
    {
        private readonly DataGridView _grid;
        private readonly TextBox _addressBox;
        private readonly CheckBox _followPc;
        private readonly Label _positionLabel;
        private byte[] _memory;
        private ushort _pc;
        private int _lastFollowRow = -1;
        private bool _allowEditing;

        public event Action<ushort, byte> ByteWriteRequested;
        public event Action<string> Message;

        public MemoryPanel()
        {
            BackColor = Theme.Panel;

            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = Theme.PanelAlt,
                Padding = new Padding(8, 5, 8, 4)
            };

            var addressLabel = new Label
            {
                Text = "АДРЕСА",
                ForeColor = Theme.Muted,
                Font = Theme.HeaderFont,
                AutoSize = true,
                Location = new Point(8, 12)
            };
            _addressBox = new TextBox
            {
                Text = "8000",
                Width = 70,
                BackColor = Theme.Window,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.MonoSmall,
                Location = new Point(58, 6)
            };
            _addressBox.KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Enter)
                {
                    args.SuppressKeyPress = true;
                    GoToAddress();
                }
            };

            var goButton = Theme.MakeOutlineButton("Перейти", 70, (sender, args) => GoToAddress());
            goButton.Location = new Point(134, 1);
            goButton.Height = 26;
            goButton.Margin = new Padding(0);

            _followPc = new CheckBox
            {
                Text = "Слідкувати за PC",
                ForeColor = Theme.Muted,
                Font = Theme.UiFont,
                AutoSize = true,
                Checked = true,
                BackColor = Color.Transparent,
                Location = new Point(216, 8)
            };

            _positionLabel = new Label
            {
                Text = "8000h–800Fh",
                ForeColor = Theme.Muted,
                Font = Theme.MonoSmall,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                Width = 118
            };

            topBar.Controls.Add(addressLabel);
            topBar.Controls.Add(_addressBox);
            topBar.Controls.Add(goButton);
            topBar.Controls.Add(_followPc);
            topBar.Controls.Add(_positionLabel);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                VirtualMode = true,
                RowCount = 4096,
                ColumnHeadersHeight = 26,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                AllowUserToResizeColumns = true,
                AllowUserToResizeRows = false,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                ScrollBars = ScrollBars.Both
            };
            Theme.StyleGrid(_grid);

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "ADDR",
                Width = 60,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            for (int i = 0; i < 16; i++)
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = i.ToString("X2"),
                    Width = 24,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "ASCII",
                Width = 104,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            _grid.CellValueNeeded += OnCellValueNeeded;
            _grid.CellValuePushed += OnCellValuePushed;
            _grid.CellBeginEdit += (sender, args) =>
            {
                if (args.ColumnIndex == 0 || args.ColumnIndex == 17)
                {
                    args.Cancel = true;
                }
            };
            _grid.CellEndEdit += (sender, args) => _grid.InvalidateCell(args.ColumnIndex, args.RowIndex);
            _grid.Scroll += (sender, args) => UpdatePositionLabel();

            Controls.Add(_grid);
            Controls.Add(topBar);
        }

        public bool AllowEditing
        {
            get => _allowEditing;
            set
            {
                if (_allowEditing == value) return;
                _allowEditing = value;
                _grid.ReadOnly = !value;
            }
        }

        public void UpdateSnapshot(MachineSnapshot snapshot)
        {
            if (snapshot == null) return;
            _memory = snapshot.Memory;
            _pc = snapshot.PC;

            if (_followPc.Checked && _memory != null)
            {
                int row = _pc >> 4;
                if (row != _lastFollowRow)
                {
                    _lastFollowRow = row;
                    ScrollToRow(row);
                }
            }

            _grid.Invalidate();
            UpdatePositionLabel();
        }

        public void SetAddress(ushort address)
        {
            _addressBox.Text = address.ToString("X4");
            ScrollToRow(address >> 4);
        }

        public void GoToAddress()
        {
            ushort address;
            if (!TryParseAddress(_addressBox.Text, out address))
            {
                Message?.Invoke("Некоректна адреса: " + _addressBox.Text);
                return;
            }
            ScrollToRow(address >> 4);
        }

        public static bool TryParseAddress(string text, out ushort address)
        {
            address = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string value = text.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(2);
            }
            else if (value.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - 1);
            }

            int parsed;
            if (!int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
            {
                return false;
            }
            if (parsed < 0 || parsed > 0xFFFF) return false;
            address = (ushort)parsed;
            return true;
        }

        private void ScrollToRow(int row)
        {
            if (!_grid.IsHandleCreated) return;
            if (row < 0 || row >= _grid.RowCount) return;
            try
            {
                _grid.FirstDisplayedScrollingRowIndex = row;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Grid may not be laid out yet; ignore.
            }
        }

        private void UpdatePositionLabel()
        {
            int first = _grid.FirstDisplayedScrollingRowIndex;
            if (first < 0) first = 0;
            int address = first * 16;
            _positionLabel.Text = address.ToString("X4") + "h–" + (address + 15).ToString("X4") + "h";
        }

        private void OnCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.RowCount) return;

            int baseAddress = e.RowIndex * 16;
            if (e.ColumnIndex == 0)
            {
                e.Value = baseAddress.ToString("X4");
                return;
            }

            if (e.ColumnIndex >= 1 && e.ColumnIndex <= 16)
            {
                int address = baseAddress + e.ColumnIndex - 1;
                byte value = _memory != null && address < _memory.Length ? _memory[address] : (byte)0;
                e.Value = value.ToString("X2");
                return;
            }

            if (e.ColumnIndex == 17)
            {
                var chars = new char[16];
                for (int i = 0; i < 16; i++)
                {
                    int address = baseAddress + i;
                    byte value = _memory != null && address < _memory.Length ? _memory[address] : (byte)0;
                    chars[i] = value >= 32 && value < 127 ? (char)value : '.';
                }
                e.Value = new string(chars);
            }
        }

        private void OnCellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            if (!AllowEditing) return;
            if (e.ColumnIndex < 1 || e.ColumnIndex > 16) return;

            string text = e.Value == null ? string.Empty : e.Value.ToString();
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text.Substring(2);
            if (text.EndsWith("h", StringComparison.OrdinalIgnoreCase)) text = text.Substring(0, text.Length - 1);

            byte value;
            if (!byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                Message?.Invoke("Некоректне значення байта: " + e.Value);
                _grid.InvalidateCell(e.ColumnIndex, e.RowIndex);
                return;
            }

            int address = e.RowIndex * 16 + e.ColumnIndex - 1;
            ByteWriteRequested?.Invoke((ushort)address, value);
            _grid.InvalidateCell(e.ColumnIndex, e.RowIndex);
        }
    }
}
