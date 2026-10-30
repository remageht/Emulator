using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Emulator.Core.Cpu;
using Emulator.Core.Machine;
using Emulator.Core.Memory;

namespace MikroLab.Studio.Controls
{
    /// <summary>
    /// Visual memory cell tape in Flamingo KR580 style.
    /// Shows linear addressable memory cells with breakpoints, PC/SP tracking,
    /// live 8080 instruction disassembly, and memory region color highlighting.
    /// </summary>
    internal sealed class CellMemoryPanel : UserControl
    {
        private readonly DataGridView _grid;
        private readonly TextBox _addressBox;
        private readonly CheckBox _followPc;
        private readonly ComboBox _scopeBox;
        private readonly ContextMenuStrip _contextMenu;

        private byte[] _memory;
        private ushort _pc = 0x8000;
        private ushort _sp = 0x83FE;
        private readonly HashSet<ushort> _breakpoints = new HashSet<ushort>();
        private readonly Dictionary<ushort, DateTime> _recentWrites = new Dictionary<ushort, DateTime>();

        private int _baseAddress = 0x8000;
        private int _rangeCount = 1024;
        private int _lastFollowRow = -1;

        public event Action<ushort> BreakpointToggled;
        public event Action<ushort, byte> ByteWriteRequested;
        public event Action<ushort> SetPcRequested;
        public event Action<string> Message;

        public CellMemoryPanel()
        {
            BackColor = Theme.Panel;

            // ------------------------------------------------------------- Header
            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Theme.PanelAlt,
                Padding = new Padding(6, 4, 6, 4)
            };

            var titleLabel = new Label
            {
                Text = "ПАМЯТЬ",
                ForeColor = Theme.Muted,
                Font = Theme.HeaderFont,
                AutoSize = true,
                Location = new Point(6, 10)
            };

            _addressBox = new TextBox
            {
                Text = "8000",
                Width = 54,
                BackColor = Theme.Window,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.MonoSmall,
                Location = new Point(62, 7)
            };
            _addressBox.KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Enter)
                {
                    args.SuppressKeyPress = true;
                    GoToAddress();
                }
            };

            var goBtn = Theme.MakeOutlineButton("→", 30, (s, e) => GoToAddress());
            goBtn.Height = 24;
            goBtn.Location = new Point(120, 6);

            _followPc = new CheckBox
            {
                Text = "Следить за PC",
                ForeColor = Theme.Text,
                Font = Theme.UiFont,
                AutoSize = true,
                Checked = true,
                BackColor = Color.Transparent,
                Location = new Point(156, 8)
            };

            _scopeBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.PanelAlt,
                ForeColor = Theme.Text,
                Font = Theme.MonoSmall,
                Width = 120,
                Dock = DockStyle.Right
            };
            _scopeBox.Items.Add("ОЗУ (8000h)");
            _scopeBox.Items.Add("Вся память");
            _scopeBox.Items.Add("ПЗУ (0000h)");
            _scopeBox.Items.Add("Регистры/Стек");
            _scopeBox.SelectedIndex = 0;
            _scopeBox.SelectedIndexChanged += OnScopeChanged;

            topBar.Controls.Add(titleLabel);
            topBar.Controls.Add(_addressBox);
            topBar.Controls.Add(goBtn);
            topBar.Controls.Add(_followPc);
            topBar.Controls.Add(_scopeBox);

            // ------------------------------------------------------------- Grid
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                VirtualMode = true,
                RowCount = _rangeCount,
                ColumnHeadersHeight = 24,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Theme.Panel,
                GridColor = Theme.Border,
                EnableHeadersVisualStyles = false,
                Font = Theme.MonoSmall,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };

            EnableDoubleBuffering(_grid);

            _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.PanelAlt;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
            _grid.ColumnHeadersDefaultCellStyle.Font = Theme.HeaderFont;
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            _grid.DefaultCellStyle.BackColor = Theme.Panel;
            _grid.DefaultCellStyle.ForeColor = Theme.Text;
            _grid.DefaultCellStyle.SelectionBackColor = Theme.Mix(Theme.Panel, Theme.Accent, 0.35);
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;

            var colBp = new DataGridViewTextBoxColumn
            {
                HeaderText = "●",
                Width = 24,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            };

            var colPtr = new DataGridViewTextBoxColumn
            {
                HeaderText = "►",
                Width = 30,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            };

            var colAddr = new DataGridViewTextBoxColumn
            {
                HeaderText = "Адрес",
                Width = 56,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colByte = new DataGridViewTextBoxColumn
            {
                HeaderText = "HEX",
                Width = 42,
                ReadOnly = false,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            };

            var colDisasm = new DataGridViewTextBoxColumn
            {
                HeaderText = "Команда (Мнемоника 8080)",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft }
            };

            _grid.Columns.Add(colBp);
            _grid.Columns.Add(colPtr);
            _grid.Columns.Add(colAddr);
            _grid.Columns.Add(colByte);
            _grid.Columns.Add(colDisasm);

            _grid.CellValueNeeded += OnCellValueNeeded;
            _grid.CellValuePushed += OnCellValuePushed;
            _grid.CellFormatting += OnCellFormatting;
            _grid.CellClick += OnCellClick;
            _grid.CellDoubleClick += OnCellDoubleClick;

            // ------------------------------------------------------------- Context Menu
            _contextMenu = new ContextMenuStrip { Renderer = new DarkMenuRenderer() };
            _contextMenu.Items.Add(new ToolStripMenuItem("Переключить точку останова (F9)", null, (s, e) => ToggleSelectedBreakpoint()));
            _contextMenu.Items.Add(new ToolStripMenuItem("Установить PC сюда", null, (s, e) => SetPcToSelected()));
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(new ToolStripMenuItem("Заполнить 00h", null, (s, e) => FillSelectedZero()));
            _grid.ContextMenuStrip = _contextMenu;

            Controls.Add(_grid);
            Controls.Add(topBar);
        }

        private static void EnableDoubleBuffering(DataGridView grid)
        {
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                grid,
                new object[] { true });
        }

        private void OnScopeChanged(object sender, EventArgs e)
        {
            switch (_scopeBox.SelectedIndex)
            {
                case 0: // ОЗУ (8000h–83FFh)
                    _baseAddress = 0x8000;
                    _rangeCount = 1024;
                    break;
                case 1: // Вся память
                    _baseAddress = 0x0000;
                    _rangeCount = 65536;
                    break;
                case 2: // ПЗУ
                    _baseAddress = 0x0000;
                    _rangeCount = 2048;
                    break;
                case 3: // Регистры и стек
                    _baseAddress = 0x83C0;
                    _rangeCount = 64;
                    break;
            }

            _grid.RowCount = _rangeCount;
            _grid.Invalidate();
        }

        private ushort GetAddressForRow(int rowIndex)
        {
            return (ushort)((_baseAddress + rowIndex) & 0xFFFF);
        }

        private int GetRowForAddress(ushort address)
        {
            int offset = address - _baseAddress;
            if (offset >= 0 && offset < _rangeCount)
                return offset;
            return -1;
        }

        private void OnCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            ushort address = GetAddressForRow(e.RowIndex);
            byte val = _memory != null && address < _memory.Length ? _memory[address] : (byte)0;

            switch (e.ColumnIndex)
            {
                case 0: // BP
                    e.Value = _breakpoints.Contains(address) ? "●" : string.Empty;
                    break;
                case 1: // Ptr
                    bool isPc = address == _pc;
                    bool isSp = address == _sp;
                    if (isPc && isSp)
                        e.Value = "►▼";
                    else if (isPc)
                        e.Value = "►";
                    else if (isSp)
                        e.Value = "▼";
                    else
                        e.Value = string.Empty;
                    break;
                case 2: // Addr
                    e.Value = address.ToString("X4") + "h";
                    break;
                case 3: // Byte
                    e.Value = val.ToString("X2");
                    break;
                case 4: // Disasm
                    if (_memory != null)
                    {
                        var disasm = I8080Disassembler.Disassemble(_memory, address);
                        string comment = GetRegionComment(address);
                        if (!string.IsNullOrEmpty(comment))
                            e.Value = disasm.Mnemonic + "    ;" + comment;
                        else
                            e.Value = disasm.Mnemonic;
                    }
                    else
                    {
                        e.Value = string.Empty;
                    }
                    break;
            }
        }

        private static string GetRegionComment(ushort address)
        {
            if (address == MemoryMap.RegA) return "Reg A";
            if (address == MemoryMap.RegB) return "Reg B";
            if (address == MemoryMap.RegC) return "Reg C";
            if (address == MemoryMap.RegD) return "Reg D";
            if (address == MemoryMap.RegE) return "Reg E";
            if (address == MemoryMap.RegH) return "Reg H";
            if (address == MemoryMap.RegL) return "Reg L";
            if (address == MemoryMap.RegF) return "Flags (PSW)";
            if (address >= MemoryMap.DisplayLatchStart && address <= MemoryMap.DisplayLatchEnd)
                return "Display Latch " + (address - MemoryMap.DisplayLatchStart);
            if (address == MemoryMap.MonitorSeggg) return "Monitor SEGGG entry";
            if (address == MemoryMap.MonitorSound) return "Monitor Sound entry";
            if (address == MemoryMap.MonitorDelay) return "Monitor Delay entry";
            return string.Empty;
        }

        private void OnCellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.ColumnIndex != 3) return;

            ushort address = GetAddressForRow(e.RowIndex);
            string text = e.Value?.ToString()?.Trim();
            if (byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte val))
            {
                if (_memory != null && address < _memory.Length)
                {
                    _memory[address] = val;
                    _recentWrites[address] = DateTime.UtcNow;
                }
                ByteWriteRequested?.Invoke(address, val);
                _grid.InvalidateRow(e.RowIndex);
            }
            else
            {
                Message?.Invoke($"Некорректное hex-значение: '{text}' для адреса {address:X4}h");
            }
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            ushort address = GetAddressForRow(e.RowIndex);

            // Breakpoint column styling
            if (e.ColumnIndex == 0)
            {
                e.CellStyle.ForeColor = Theme.Red;
                e.CellStyle.Font = Theme.UiBold;
            }

            // Pointer column styling
            if (e.ColumnIndex == 1)
            {
                if (address == _pc)
                {
                    e.CellStyle.ForeColor = Theme.Green;
                    e.CellStyle.Font = Theme.UiBold;
                }
                else if (address == _sp)
                {
                    e.CellStyle.ForeColor = Theme.Teal;
                    e.CellStyle.Font = Theme.UiBold;
                }
            }

            // Active PC row styling (Highlighted Flamingo step)
            if (address == _pc)
            {
                e.CellStyle.BackColor = Color.FromArgb(42, 68, 48);
                e.CellStyle.ForeColor = Color.White;
                if (e.ColumnIndex == 2 || e.ColumnIndex == 4)
                {
                    e.CellStyle.Font = Theme.UiBold;
                }
                return;
            }

            // Active SP row styling
            if (address == _sp)
            {
                e.CellStyle.BackColor = Color.FromArgb(32, 54, 68);
                e.CellStyle.ForeColor = Color.White;
                return;
            }

            // Recently written cell glow
            if (_recentWrites.TryGetValue(address, out DateTime writeTime))
            {
                double ageMs = (DateTime.UtcNow - writeTime).TotalMilliseconds;
                if (ageMs < 1200)
                {
                    e.CellStyle.BackColor = Color.FromArgb(70, 56, 26);
                    e.CellStyle.ForeColor = Color.White;
                    return;
                }
            }

            // Region based background tinting
            if (address >= MemoryMap.RegsStart && address <= MemoryMap.RegsEnd)
            {
                e.CellStyle.BackColor = Color.FromArgb(48, 38, 52); // Soft purple for monitor registers
            }
            else if (address >= MemoryMap.DisplayLatchStart && address <= MemoryMap.DisplayLatchEnd)
            {
                e.CellStyle.BackColor = Color.FromArgb(52, 48, 36); // Soft amber for display latches
            }
            else if (address < 0x0800)
            {
                e.CellStyle.BackColor = Color.FromArgb(34, 38, 44); // Subdued ROM area
            }
            else
            {
                e.CellStyle.BackColor = e.RowIndex % 2 == 0 ? Theme.Panel : Theme.Mix(Theme.Panel, Theme.PanelAlt, 0.5);
            }
        }

        private void OnCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            ushort address = GetAddressForRow(e.RowIndex);
            if (e.ColumnIndex == 0) // Breakpoint column
            {
                ToggleBreakpointAt(address);
            }
        }

        private void OnCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (e.ColumnIndex == 0)
            {
                ushort address = GetAddressForRow(e.RowIndex);
                ToggleBreakpointAt(address);
            }
        }

        private void ToggleSelectedBreakpoint()
        {
            if (_grid.CurrentRow != null)
            {
                ushort address = GetAddressForRow(_grid.CurrentRow.Index);
                ToggleBreakpointAt(address);
            }
        }

        private void SetPcToSelected()
        {
            if (_grid.CurrentRow != null)
            {
                ushort address = GetAddressForRow(_grid.CurrentRow.Index);
                SetPcRequested?.Invoke(address);
            }
        }

        private void FillSelectedZero()
        {
            if (_grid.CurrentRow != null)
            {
                ushort address = GetAddressForRow(_grid.CurrentRow.Index);
                if (_memory != null && address < _memory.Length)
                {
                    _memory[address] = 0;
                    _recentWrites[address] = DateTime.UtcNow;
                }
                ByteWriteRequested?.Invoke(address, 0);
                _grid.InvalidateRow(_grid.CurrentRow.Index);
            }
        }

        private void ToggleBreakpointAt(ushort address)
        {
            if (_breakpoints.Contains(address))
                _breakpoints.Remove(address);
            else
                _breakpoints.Add(address);

            BreakpointToggled?.Invoke(address);
            int row = GetRowForAddress(address);
            if (row >= 0) _grid.InvalidateRow(row);
        }

        public void UpdateSnapshot(MachineSnapshot snapshot, IEnumerable<ushort> breakpoints)
        {
            if (snapshot == null) return;

            bool needFullInvalidate = false;
            if (_memory == null || _memory.Length != snapshot.Memory.Length)
            {
                _memory = (byte[])snapshot.Memory.Clone();
                needFullInvalidate = true;
            }
            else
            {
                // Detect byte writes to trigger amber glow
                for (int i = _baseAddress; i < Math.Min(_memory.Length, _baseAddress + _rangeCount); i++)
                {
                    if (_memory[i] != snapshot.Memory[i])
                    {
                        _memory[i] = snapshot.Memory[i];
                        _recentWrites[(ushort)i] = DateTime.UtcNow;
                        int row = GetRowForAddress((ushort)i);
                        if (row >= 0) _grid.InvalidateRow(row);
                    }
                }
            }

            if (_pc != snapshot.PC)
            {
                int oldRow = GetRowForAddress(_pc);
                if (oldRow >= 0) _grid.InvalidateRow(oldRow);

                _pc = snapshot.PC;
                int newRow = GetRowForAddress(_pc);
                if (newRow >= 0) _grid.InvalidateRow(newRow);

                if (_followPc.Checked)
                {
                    FollowAddress(_pc);
                }
            }

            if (_sp != snapshot.SP)
            {
                int oldSp = GetRowForAddress(_sp);
                if (oldSp >= 0) _grid.InvalidateRow(oldSp);

                _sp = snapshot.SP;
                int newSp = GetRowForAddress(_sp);
                if (newSp >= 0) _grid.InvalidateRow(newSp);
            }

            if (breakpoints != null)
            {
                _breakpoints.Clear();
                foreach (var b in breakpoints)
                    _breakpoints.Add(b);
            }

            if (needFullInvalidate)
            {
                _grid.Invalidate();
            }
        }

        public void FollowAddress(ushort address)
        {
            int row = GetRowForAddress(address);
            if (row < 0)
            {
                // Switch scope to full memory if outside current scope
                if (address < _baseAddress || address >= _baseAddress + _rangeCount)
                {
                    _scopeBox.SelectedIndex = 1; // Full memory
                    row = GetRowForAddress(address);
                }
            }

            if (row >= 0 && row < _grid.RowCount && row != _lastFollowRow)
            {
                _lastFollowRow = row;
                try
                {
                    int firstVisible = _grid.FirstDisplayedScrollingRowIndex;
                    int visibleCount = _grid.DisplayedRowCount(false);

                    if (firstVisible < 0 || row < firstVisible || row >= firstVisible + visibleCount)
                    {
                        int targetRow = Math.Max(0, row - (visibleCount / 2));
                        if (targetRow < _grid.RowCount)
                        {
                            _grid.FirstDisplayedScrollingRowIndex = targetRow;
                        }
                    }
                }
                catch
                {
                    // Ignore transient scrolling race conditions
                }
            }
        }

        public void GoToAddress(ushort address)
        {
            _addressBox.Text = address.ToString("X4");
            FollowAddress(address);
            int row = GetRowForAddress(address);
            if (row >= 0 && row < _grid.RowCount)
            {
                _grid.Rows[row].Selected = true;
            }
        }

        private void GoToAddress()
        {
            string text = _addressBox.Text?.Trim()?.TrimEnd('h', 'H');
            if (ushort.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort addr))
            {
                GoToAddress(addr);
            }
            else
            {
                Message?.Invoke($"Некорректный адрес: '{_addressBox.Text}'");
            }
        }
    }
}
