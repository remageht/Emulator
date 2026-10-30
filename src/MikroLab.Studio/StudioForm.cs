using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Emulator.Core.Assembler;
using Emulator.Core.Cpu;
using Emulator.Core.Machine;
using MikroLab.Studio.Controls;

namespace MikroLab.Studio
{
    /// <summary>
    /// Standalone MikroLab Studio application. It hosts Emulator.Core directly
    /// instead of inheriting the legacy WinForms emulator shell.
    /// </summary>
    public sealed class StudioForm : Form
    {
        private readonly Machine _machine;
        private readonly MachineSession _session;
        private readonly Timer _uiTimer;
        private readonly ToolTip _toolTip;

        private MenuStrip _menu;
        private Panel _toolbar;
        private Panel _statusBar;
        private SplitContainer _outerSplit;
        private SplitContainer _mainSplit;
        private SplitContainer _editorAndDevicesSplit;
        private SplitContainer _rightSplit;

        private CodeEditor _editor;
        private Label _editorFileLabel;
        private Label _cursorLabel;
        private RegisterPanel _registers;
        private MemoryPanel _memoryPanel;
        private CellMemoryPanel _cellMemoryPanel;
        private DevicesPanel _devicesPanel;
        private OutputPanel _output;
        private DarkTabControl _rightTabs;
        private Label _stackInfo;
        private ListBox _stackList;
        private ComboBox _profileBox;
        private readonly HashSet<ushort> _addressBreakpoints = new HashSet<ushort>();

        private Label _statusState;
        private Label _statusPc;
        private Label _statusCycles;
        private Label _statusBreak;
        private Label _statusCursor;
        private Label _statusMessage;

        private Button _btnRun;
        private Button _btnPause;
        private Button _btnStep;
        private Button _btnReset;
        private Button _btnAssemble;
        private ComboBox _sampleBox;

        private string _filePath;
        private bool _dirty;
        private bool _loadingCode;
        private bool _loadingSample;
        private bool _programLoaded;
        private AssembleResult _lastResult;
        private string _lastAssembledText;
        private readonly SortedSet<int> _breakpointLines = new SortedSet<int>();
        private readonly Dictionary<int, ushort> _lineAddresses = new Dictionary<int, ushort>();
        private readonly List<AddressRange> _addressRanges = new List<AddressRange>();
        private RunState _lastRunState = (RunState)(-1);
        private int _lastBreakpointLogged = -1;
        private string _lastStackSignature = string.Empty;

        public StudioForm()
        {
            Text = "MikroLab Studio — КР580ВМ80А / Intel 8080";
            BackColor = Theme.Window;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1380, 840);
            MinimumSize = new Size(1100, 700);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            DoubleBuffered = true;

            _machine = new Machine(new StudioSound(), new StudioClock(), null, CpuProfile.Extended);
            _session = new MachineSession(_machine)
            {
                InstructionsPerSlice = 4000,
                ThrottleLikeOriginal = true
            };
            _toolTip = new ToolTip
            {
                BackColor = Theme.PanelAlt,
                ForeColor = Theme.Text,
                AutoPopDelay = 8000,
                InitialDelay = 400,
                ReshowDelay = 200
            };

            BuildUi();
            WireSession();
            WireDevices();

            _uiTimer = new Timer { Interval = 45 };
            _uiTimer.Tick += OnUiTick;

            LoadSample(0, false);
            _loadingSample = true;
            _sampleBox.SelectedIndex = 0;
            _loadingSample = false;

            Shown += OnShown;
            FormClosing += OnFormClosing;
        }

        // ------------------------------------------------------------------ layout

        private void BuildUi()
        {
            SuspendLayout();

            _menu = BuildMenu();
            _toolbar = BuildToolbar();
            _statusBar = BuildStatusBar();

            _outerSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2,
                SplitterWidth = 6,
                BackColor = Theme.Window,
                Panel1MinSize = 0,
                Panel2MinSize = 0
            };
            _outerSplit.Panel1.BackColor = Theme.Window;
            _outerSplit.Panel2.BackColor = Theme.Window;

            _mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 6,
                BackColor = Theme.Window,
                Panel1MinSize = 0,
                Panel2MinSize = 0
            };
            _mainSplit.Panel1.BackColor = Theme.Window;
            _mainSplit.Panel2.BackColor = Theme.Window;

            _cellMemoryPanel = new CellMemoryPanel { Dock = DockStyle.Fill };
            _cellMemoryPanel.BreakpointToggled += OnCellMemoryBreakpointToggled;
            _cellMemoryPanel.ByteWriteRequested += OnMemoryWrite;
            _cellMemoryPanel.SetPcRequested += OnSetPcRequested;
            _cellMemoryPanel.Message += msg => _output.Log(msg, LogLevel.Warning);
            _mainSplit.Panel1.Controls.Add(_cellMemoryPanel);

            _editorAndDevicesSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6,
                BackColor = Theme.Window,
                Panel1MinSize = 0,
                Panel2MinSize = 0
            };
            _editorAndDevicesSplit.Panel1.BackColor = Theme.Window;
            _editorAndDevicesSplit.Panel2.BackColor = Theme.Window;

            _editorAndDevicesSplit.Panel1.Controls.Add(BuildEditorHost());
            _editorAndDevicesSplit.Panel2.Controls.Add(BuildRightPanel());
            _mainSplit.Panel2.Controls.Add(_editorAndDevicesSplit);

            _outerSplit.Panel1.Controls.Add(_mainSplit);

            _output = new OutputPanel { Dock = DockStyle.Fill };
            _outerSplit.Panel2.Controls.Add(_output);

            Controls.Add(_outerSplit);
            Controls.Add(_statusBar);
            Controls.Add(_toolbar);
            Controls.Add(_menu);
            MainMenuStrip = _menu;

            ResumeLayout(false);
            PerformLayout();
        }

        private MenuStrip BuildMenu()
        {
            var menu = new MenuStrip
            {
                Dock = DockStyle.Top,
                BackColor = Theme.PanelAlt,
                ForeColor = Theme.Text,
                Font = Theme.UiFont,
                Renderer = new DarkMenuRenderer(),
                Padding = new Padding(6, 2, 0, 2)
            };

            var fileMenu = new ToolStripMenuItem("&Файл");
            fileMenu.DropDownItems.Add(MakeMenuItem("Новый", () => NewDocument(), Keys.Control | Keys.N));
            fileMenu.DropDownItems.Add(MakeMenuItem("Открыть…", () => OpenDocument(), Keys.Control | Keys.O));
            fileMenu.DropDownItems.Add(MakeMenuItem("Сохранить", () => SaveDocument(), Keys.Control | Keys.S));
            fileMenu.DropDownItems.Add(MakeMenuItem("Сохранить как…", () => SaveDocumentAs(), Keys.None));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(MakeMenuItem("Выход", () => Close(), Keys.None));

            var runMenu = new ToolStripMenuItem("&Выполнение");
            runMenu.DropDownItems.Add(MakeMenuItem("Собрать", () => AssembleProgram(), Keys.None, "F7"));
            runMenu.DropDownItems.Add(MakeMenuItem("Пуск", () => RunProgram(), Keys.None, "F5"));
            runMenu.DropDownItems.Add(MakeMenuItem("Пауза", () => PauseProgram(), Keys.None, "F6"));
            runMenu.DropDownItems.Add(MakeMenuItem("Шаг", () => StepProgram(), Keys.None, "F10"));
            runMenu.DropDownItems.Add(MakeMenuItem("Сброс", () => ResetProgram(), Keys.None, "Ctrl+R"));
            runMenu.DropDownItems.Add(new ToolStripSeparator());
            runMenu.DropDownItems.Add(MakeMenuItem("Точка останова", () => ToggleBreakpoint(_editor.CaretLine), Keys.None, "F9"));
            runMenu.DropDownItems.Add(MakeMenuItem("К текущему PC", () => GoToProgramCounter(), Keys.None));

            var viewMenu = new ToolStripMenuItem("&Вид");
            viewMenu.DropDownItems.Add(MakeMenuItem("Память", () => SelectRightTab(0), Keys.None));
            viewMenu.DropDownItems.Add(MakeMenuItem("Устройства", () => SelectRightTab(1), Keys.None));
            viewMenu.DropDownItems.Add(MakeMenuItem("Стек", () => SelectRightTab(2), Keys.None));

            var helpMenu = new ToolStripMenuItem("&Справка");
            helpMenu.DropDownItems.Add(MakeMenuItem("Горячие клавиши", ShowHotkeys, Keys.None));
            helpMenu.DropDownItems.Add(MakeMenuItem("О программе", ShowAbout, Keys.None));

            menu.Items.Add(fileMenu);
            menu.Items.Add(runMenu);
            menu.Items.Add(viewMenu);
            menu.Items.Add(helpMenu);
            return menu;
        }

        private static ToolStripMenuItem MakeMenuItem(string text, Action action, Keys shortcut, string display = null)
        {
            var item = new ToolStripMenuItem(text);
            if (shortcut != Keys.None)
            {
                item.ShortcutKeys = shortcut;
            }
            if (!string.IsNullOrEmpty(display))
            {
                item.ShortcutKeyDisplayString = display;
            }
            item.Click += (sender, args) => action();
            return item;
        }

        private Panel BuildToolbar()
        {
            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Theme.Panel,
                Padding = new Padding(10, 9, 10, 9)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Theme.Panel,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _btnRun = Theme.MakeButton("▶  Пуск", Color.FromArgb(46, 102, 55), Color.White, 100, (sender, args) => RunProgram());
            _btnPause = Theme.MakeOutlineButton("⏸  Пауза", 96, (sender, args) => PauseProgram());
            _btnStep = Theme.MakeOutlineButton("⏭  Шаг", 92, (sender, args) => StepProgram());
            _btnReset = Theme.MakeOutlineButton("⏹  Сброс", 108, (sender, args) => ResetProgram());
            _btnAssemble = Theme.MakeButton("⚙  Собрать", Theme.AccentDark, Color.White, 108, (sender, args) => AssembleProgram());

            _toolTip.SetToolTip(_btnRun, "Пуск (F5)");
            _toolTip.SetToolTip(_btnPause, "Пауза (F6)");
            _toolTip.SetToolTip(_btnStep, "Один шаг (F10)");
            _toolTip.SetToolTip(_btnReset, "Сброс процессора (Ctrl+R)");
            _toolTip.SetToolTip(_btnAssemble, "Собрать и загрузить (F7)");

            var newButton = Theme.MakeOutlineButton("Новый", 76, (sender, args) => NewDocument());
            var openButton = Theme.MakeOutlineButton("Открыть", 88, (sender, args) => OpenDocument());
            var saveButton = Theme.MakeOutlineButton("Сохранить", 88, (sender, args) => SaveDocument());
            _toolTip.SetToolTip(newButton, "Новый файл (Ctrl+N)");
            _toolTip.SetToolTip(openButton, "Открыть .asm (Ctrl+O)");
            _toolTip.SetToolTip(saveButton, "Сохранить .asm (Ctrl+S)");

            var sampleLabel = new Label
            {
                Text = "Пример:",
                ForeColor = Theme.Muted,
                Font = Theme.UiFont,
                AutoSize = false,
                Width = 62,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(8, 0, 4, 0),
                BackColor = Color.Transparent
            };
            _sampleBox = new ComboBox
            {
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.PanelAlt,
                ForeColor = Theme.Text,
                Font = Theme.UiFont,
                Margin = new Padding(0, 5, 0, 0)
            };
            foreach (SampleProgram sample in SamplePrograms.All)
            {
                _sampleBox.Items.Add(sample);
            }
            _sampleBox.SelectedIndexChanged += OnSampleSelected;

            var profileLabel = new Label
            {
                Text = "Профиль:",
                ForeColor = Theme.Muted,
                Font = Theme.UiFont,
                AutoSize = false,
                Width = 60,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(8, 0, 4, 0),
                BackColor = Color.Transparent
            };
            _profileBox = new ComboBox
            {
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.PanelAlt,
                ForeColor = Theme.Text,
                Font = Theme.UiFont,
                Margin = new Padding(0, 5, 0, 0)
            };
            _profileBox.Items.Add("Legacy (Лаб. 1–4)");
            _profileBox.Items.Add("Newest (Лаб. 5+)");
            _profileBox.SelectedIndex = _session.Profile == CpuProfile.Legacy ? 0 : 1;
            _profileBox.SelectedIndexChanged += OnProfileSelected;

            flow.Controls.Add(_btnRun);
            flow.Controls.Add(_btnPause);
            flow.Controls.Add(_btnStep);
            flow.Controls.Add(_btnReset);
            flow.Controls.Add(MakeSeparator());
            flow.Controls.Add(_btnAssemble);
            flow.Controls.Add(MakeSeparator());
            flow.Controls.Add(newButton);
            flow.Controls.Add(openButton);
            flow.Controls.Add(saveButton);
            flow.Controls.Add(MakeSeparator());
            flow.Controls.Add(sampleLabel);
            flow.Controls.Add(_sampleBox);
            flow.Controls.Add(MakeSeparator());
            flow.Controls.Add(profileLabel);
            flow.Controls.Add(_profileBox);

            toolbar.Controls.Add(flow);
            return toolbar;
        }

        private static Panel MakeSeparator()
        {
            return new Panel
            {
                Width = 1,
                Height = 28,
                BackColor = Theme.Border,
                Margin = new Padding(7, 2, 7, 2)
            };
        }

        private Panel BuildStatusBar()
        {
            var status = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                BackColor = Theme.PanelAlt
            };

            _statusState = MakeStatusLabel(10, 150, Theme.UiBold);
            _statusPc = MakeStatusLabel(168, 100, Theme.MonoSmall);
            _statusCycles = MakeStatusLabel(276, 150, Theme.MonoSmall);
            _statusBreak = MakeStatusLabel(434, 180, Theme.UiFont);
            _statusCursor = MakeStatusLabel(622, 140, Theme.MonoSmall);
            _statusMessage = MakeStatusLabel(0, 320, Theme.UiFont);
            _statusMessage.TextAlign = ContentAlignment.MiddleRight;
            _statusMessage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _statusMessage.Text = "Готово";

            status.Controls.Add(_statusState);
            status.Controls.Add(_statusPc);
            status.Controls.Add(_statusCycles);
            status.Controls.Add(_statusBreak);
            status.Controls.Add(_statusCursor);
            status.Controls.Add(_statusMessage);
            status.Resize += (sender, args) =>
            {
                _statusMessage.Left = Math.Max(770, status.Width - _statusMessage.Width - 10);
            };
            return status;
        }

        private static Label MakeStatusLabel(int left, int width, Font font)
        {
            return new Label
            {
                AutoSize = false,
                Left = left,
                Top = 0,
                Width = width,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = font,
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent
            };
        }

        private Control BuildEditorHost()
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Theme.PanelAlt
            };
            _editorFileLabel = new Label
            {
                Text = "ПРОГРАММА",
                Font = Theme.HeaderFont,
                ForeColor = Theme.Muted,
                AutoSize = true,
                Location = new Point(10, 8),
                BackColor = Color.Transparent
            };
            _cursorLabel = new Label
            {
                Text = "Строка 1, кол. 1",
                Font = Theme.MonoSmall,
                ForeColor = Theme.Muted,
                AutoSize = true,
                Location = new Point(300, 8),
                BackColor = Color.Transparent
            };
            header.Resize += (sender, args) =>
            {
                _cursorLabel.Left = Math.Max(10, header.Width - _cursorLabel.Width - 12);
            };
            header.Controls.Add(_editorFileLabel);
            header.Controls.Add(_cursorLabel);

            _editor = new CodeEditor { Dock = DockStyle.Fill };
            _editor.CodeChanged += OnCodeChanged;
            _editor.CaretMoved += (sender, args) => UpdateCursorLabel();
            _editor.BreakpointToggled += ToggleBreakpoint;

            host.Controls.Add(_editor);
            host.Controls.Add(header);
            return host;
        }

        private Control BuildRightPanel()
        {
            _rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 6,
                BackColor = Theme.Window,
                Panel1MinSize = 0,
                Panel2MinSize = 0
            };
            _rightSplit.Panel1.BackColor = Theme.Panel;
            _rightSplit.Panel2.BackColor = Theme.Panel;

            _registers = new RegisterPanel { Dock = DockStyle.Fill };
            _rightSplit.Panel1.Controls.Add(_registers);

            _rightTabs = new DarkTabControl { Dock = DockStyle.Fill };

            var devicesPage = new TabPage("Устройства")
            {
                BackColor = Theme.Panel,
                Padding = new Padding(0)
            };
            _devicesPanel = new DevicesPanel { Dock = DockStyle.Fill };
            devicesPage.Controls.Add(_devicesPanel);

            var memoryPage = new TabPage("Дамп (16 байт)")
            {
                BackColor = Theme.Panel,
                Padding = new Padding(0)
            };
            _memoryPanel = new MemoryPanel { Dock = DockStyle.Fill };
            _memoryPanel.ByteWriteRequested += OnMemoryWrite;
            _memoryPanel.Message += message => _output.Log(message, LogLevel.Warning);
            memoryPage.Controls.Add(_memoryPanel);

            var stackPage = new TabPage("Стек")
            {
                BackColor = Theme.Panel,
                Padding = new Padding(0)
            };
            stackPage.Controls.Add(BuildStackPanel());

            _rightTabs.TabPages.Add(devicesPage);
            _rightTabs.TabPages.Add(memoryPage);
            _rightTabs.TabPages.Add(stackPage);
            _rightTabs.SelectedIndex = 0;
            _rightSplit.Panel2.Controls.Add(_rightTabs);
            return _rightSplit;
        }

        private Control BuildStackPanel()
        {
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel };

            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "ЦЕПОЧКА ВЫЗОВОВ · CALL / RET",
                Font = Theme.HeaderFont,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Theme.PanelAlt
            };

            _stackInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "Глубина: 0",
                Font = Theme.MonoSmall,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Theme.Panel
            };

            _stackList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Window,
                ForeColor = Theme.Text,
                Font = Theme.MonoSmall,
                IntegralHeight = false
            };

            host.Controls.Add(_stackList);
            host.Controls.Add(_stackInfo);
            host.Controls.Add(header);
            return host;
        }

        private void OnShown(object sender, EventArgs e)
        {
            ApplySplitLayout();

            _uiTimer.Start();
            _editor.FocusEditor();
            UpdateButtons();
        }

        /// <summary>
        /// SplitContainer validates Panel*MinSize against the current size, so
        /// constraints can only be applied after the form has been laid out.
        /// </summary>
        private void ApplySplitLayout()
        {
            try
            {
                if (_mainSplit.Width >= 1000)
                {
                    _mainSplit.Panel1MinSize = 240;
                    _mainSplit.Panel2MinSize = 600;
                    _mainSplit.SplitterDistance = 330;
                }
                else
                {
                    _mainSplit.Panel1MinSize = 0;
                    _mainSplit.Panel2MinSize = 0;
                    _mainSplit.SplitterDistance = Math.Max(180, (int)(_mainSplit.Width * 0.28));
                }
            }
            catch (Exception)
            {
                // Splitter constraints differ across DPI settings; defaults are fine.
            }

            try
            {
                if (_editorAndDevicesSplit.Width >= 700)
                {
                    _editorAndDevicesSplit.Panel1MinSize = 340;
                    _editorAndDevicesSplit.Panel2MinSize = 320;
                    _editorAndDevicesSplit.SplitterDistance = Math.Max(340, (int)(_editorAndDevicesSplit.Width * 0.55));
                }
                else
                {
                    _editorAndDevicesSplit.Panel1MinSize = 0;
                    _editorAndDevicesSplit.Panel2MinSize = 0;
                    _editorAndDevicesSplit.SplitterDistance = Math.Max(200, _editorAndDevicesSplit.Width / 2);
                }
            }
            catch (Exception)
            {
                // Splitter constraints differ across DPI settings; defaults are fine.
            }

            try
            {
                if (_rightSplit.Height >= 420)
                {
                    _rightSplit.Panel1MinSize = 150;
                    _rightSplit.Panel2MinSize = 180;
                    _rightSplit.SplitterDistance = 218;
                }
                else
                {
                    _rightSplit.Panel1MinSize = 0;
                    _rightSplit.Panel2MinSize = 0;
                    _rightSplit.SplitterDistance = Math.Max(80, _rightSplit.Height / 2);
                }
            }
            catch (InvalidOperationException)
            {
                // Splitter constraints differ across DPI settings; defaults are fine.
            }
            catch (ArgumentOutOfRangeException)
            {
                // Splitter constraints differ across DPI settings; defaults are fine.
            }

            try
            {
                if (_outerSplit.Height >= 460)
                {
                    _outerSplit.Panel1MinSize = 300;
                    _outerSplit.Panel2MinSize = 120;
                    _outerSplit.SplitterDistance = Math.Max(300, _outerSplit.Height - 160);
                }
            }
            catch (InvalidOperationException)
            {
                // Splitter constraints differ across DPI settings; defaults are fine.
            }
            catch (ArgumentOutOfRangeException)
            {
                // Splitter constraints differ across DPI settings; defaults are fine.
            }
        }

        // ------------------------------------------------------------------ wiring

        private void WireSession()
        {
            _session.ExecutionError += message =>
                SafeUi(() => _output.Log("Ошибка выполнения: " + message, LogLevel.Error));
            _session.UnsupportedCommand += (pc, opcode) =>
                SafeUi(() => _output.Log(
                    "Неподдерживаемая команда " + opcode.ToString("X2") + "h по адресу " + pc.ToString("X4") + "h",
                    LogLevel.Error));
            _output.ErrorActivated += line =>
            {
                _editor.GoToLine(line);
                _editor.FocusEditor();
            };
        }

        private void WireDevices()
        {
            _devicesPanel.TumblerChanged += (index, value) =>
            {
                _session.WithMachine(machine =>
                {
                    if (index == 0) machine.Ppi.Tumbler1 = value;
                    else if (index == 1) machine.Ppi.Tumbler2 = value;
                    else if (index == 2) machine.Ppi.Tumbler3 = value;
                });
            };
            _devicesPanel.AnalogChanged += (channel, volts) =>
            {
                _session.WithMachine(machine => machine.Analog.SetChannel(channel, volts));
            };
        }

        // ------------------------------------------------------------------ UI tick

        private void OnUiTick(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing) return;

            MachineSnapshot snapshot = _session.CaptureSnapshot();
            if (snapshot == null) return;

            _registers.UpdateSnapshot(snapshot);
            _devicesPanel.UpdateSnapshot(snapshot);
            _memoryPanel.AllowEditing = _session.State != RunState.Running;
            _memoryPanel.UpdateSnapshot(snapshot);
            _cellMemoryPanel.UpdateSnapshot(snapshot, _session.GetBreakpoints());

            _statusPc.Text = "PC  " + snapshot.PC.ToString("X4") + "h";
            _statusCycles.Text = "Такты  " + snapshot.Cycles.ToString("N0");

            UpdateExecutionLine(snapshot.PC);
            UpdateRunState();
            UpdateStackPanel(snapshot);
        }

        private void UpdateRunState()
        {
            RunState state = _session.State;

            if (state != _lastRunState)
            {
                if (state == RunState.Running)
                {
                    _output.Log("Выполнение запущено · PC=" + CurrentProgramCounter().ToString("X4") + "h");
                }
                else if (state == RunState.Paused && _lastRunState == RunState.Running && !_session.IsPausedAtBreakpoint)
                {
                    _output.Log("Пауза · PC=" + CurrentProgramCounter().ToString("X4") + "h");
                }
                else if (state == RunState.Halted)
                {
                    _output.Log("HLT · процессор остановлен на " + CurrentProgramCounter().ToString("X4") + "h", LogLevel.Warning);
                }
                _lastRunState = state;
            }

            if (_session.IsPausedAtBreakpoint && _session.BreakpointAddress.HasValue)
            {
                ushort address = _session.BreakpointAddress.Value;
                if (_lastBreakpointLogged != address)
                {
                    _lastBreakpointLogged = address;
                    _output.Log("Точка останова · " + address.ToString("X4") + "h", LogLevel.Warning);
                }
            }
            else if (!_session.IsPausedAtBreakpoint)
            {
                _lastBreakpointLogged = -1;
            }

            string text;
            Color color;
            if (_session.IsPausedAtBreakpoint && _session.BreakpointAddress.HasValue)
            {
                text = "● ТОЧКА ОСТАНОВА " + _session.BreakpointAddress.Value.ToString("X4") + "h";
                color = Theme.Yellow;
            }
            else
            {
                switch (state)
                {
                    case RunState.Running:
                        text = "● ВЫПОЛНЯЕТСЯ";
                        color = Theme.Green;
                        break;
                    case RunState.Paused:
                        text = "● ПАУЗА";
                        color = Theme.Yellow;
                        break;
                    case RunState.Halted:
                        text = "● HLT";
                        color = Theme.Red;
                        break;
                    default:
                        text = "● ОСТАНОВЛЕНО";
                        color = Theme.Muted;
                        break;
                }
            }

            _statusState.Text = text;
            _statusState.ForeColor = color;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            RunState state = _session.State;
            bool running = state == RunState.Running;
            bool halted = state == RunState.Halted;

            _btnRun.Enabled = !running && !halted;
            _btnPause.Enabled = running;
            _btnStep.Enabled = !halted;
            _btnReset.Enabled = true;
            _btnAssemble.Enabled = !running;
        }

        private void UpdateExecutionLine(ushort pc)
        {
            int? line = FindLineForAddress(pc);
            _editor.SetExecutionLine(line);
        }

        private void UpdateStackPanel(MachineSnapshot snapshot)
        {
            _stackInfo.Text = "Глибина: " + snapshot.CallStackDepth;

            if (_session.State == RunState.Running)
            {
                return;
            }

            ushort[] stack = _session.GetCallStack();
            var builder = new StringBuilder();
            for (int i = 0; i < stack.Length; i++)
            {
                builder.Append(stack[i].ToString("X4"));
                if (i < stack.Length - 1) builder.Append(',');
            }

            string signature = builder.ToString();
            if (signature == _lastStackSignature) return;
            _lastStackSignature = signature;

            _stackList.BeginUpdate();
            try
            {
                _stackList.Items.Clear();
                if (stack.Length == 0)
                {
                    _stackList.Items.Add("— стек пуст —");
                }
                else
                {
                    for (int i = 0; i < stack.Length; i++)
                    {
                        _stackList.Items.Add((i + 1) + ".  RET → " + stack[i].ToString("X4") + "h");
                    }
                }
            }
            finally
            {
                _stackList.EndUpdate();
            }
        }

        private ushort CurrentProgramCounter()
        {
            MachineSnapshot snapshot = _session.LastSnapshot;
            return snapshot != null ? snapshot.PC : _machine.Cpu.State.PC;
        }

        // ------------------------------------------------------------------ actions

        private void AssembleProgram()
        {
            if (_session.State == RunState.Running)
            {
                _session.Pause();
            }

            string source = _editor.Code;
            AssembleResult result = I8080Assembler.Assemble(source, 0x8000);
            _output.SetErrors(result.Errors);

            if (!result.Success)
            {
                _statusMessage.ForeColor = Theme.Red;
                _statusMessage.Text = "Ошибок: " + result.Errors.Count;
                _output.Log("Сборка не удалась · ошибок: " + result.Errors.Count, LogLevel.Error);
                if (result.Errors.Count > 0)
                {
                    _editor.GoToLine(result.Errors[0].LineNumber);
                }
                return;
            }

            _lastResult = result;
            _lastAssembledText = source;
            _programLoaded = true;
            _lastBreakpointLogged = -1;
            BuildAddressMap(result);

            _session.WithMachine(machine =>
            {
                machine.Cpu.Reset();
                machine.State = RunState.Stopped;
                machine.Display.Reset();
                machine.Ppi.Lamps = 0;
                machine.Ppi.InputWord = 0xFF;
                machine.Ppi.ControlWord = 0xFF;

                for (int address = 0x8000; address <= 0x83FF; address++)
                {
                    machine.Memory.WriteByte((ushort)address, 0);
                }

                foreach (AssembledChunk chunk in result.Chunks)
                {
                    for (int i = 0; i < chunk.Data.Length; i++)
                    {
                        machine.Memory.WriteByte((ushort)(chunk.Address + i), chunk.Data[i]);
                    }
                }

                machine.Cpu.State.PC = result.StartAddress;
                machine.Cpu.State.SP = 0x83FE;
            });

            ApplyBreakpoints();

            int bytes = 0;
            foreach (AssembledChunk chunk in result.Chunks)
            {
                bytes += chunk.Data.Length;
            }
            string range = result.EndAddress > result.StartAddress
                ? result.StartAddress.ToString("X4") + "h–" + (result.EndAddress - 1).ToString("X4") + "h"
                : "—";

            _output.Log(
                "Собрано: " + bytes + " байт · " + range + " · символов: " + result.Symbols.Count,
                LogLevel.Success);
            _statusMessage.ForeColor = Theme.Muted;
            _statusMessage.Text = "Собрано · " + bytes + " байт";
            _editor.SetExecutionLine(FindLineForAddress(result.StartAddress));
            UpdateTitle();
            UpdateButtons();
        }

        private void BuildAddressMap(AssembleResult result)
        {
            _lineAddresses.Clear();
            _addressRanges.Clear();

            foreach (ListingLine line in result.Listing)
            {
                if (!line.HasAddress || line.Bytes == null || line.Bytes.Length == 0) continue;
                _lineAddresses[line.LineNumber] = line.Address;
                _addressRanges.Add(new AddressRange(line.Address, line.Bytes.Length, line.LineNumber));
            }

            _addressRanges.Sort((a, b) => a.Address.CompareTo(b.Address));
            _lastStackSignature = string.Empty;
        }

        private int? FindLineForAddress(ushort address)
        {
            for (int i = 0; i < _addressRanges.Count; i++)
            {
                AddressRange range = _addressRanges[i];
                if (address >= range.Address && address < range.Address + range.Length)
                {
                    return range.Line;
                }
            }
            return null;
        }

        private void RunProgram()
        {
            if (_session.State == RunState.Halted)
            {
                _output.Log("Процессор остановлен (HLT). Сначала выполните сброс.", LogLevel.Warning);
                return;
            }

            if (!_programLoaded || _lastAssembledText != _editor.Code)
            {
                AssembleProgram();
                if (!_programLoaded || _lastAssembledText != _editor.Code)
                {
                    return;
                }
            }

            if (_session.State == RunState.Running) return;
            _session.Run();
        }

        private void PauseProgram()
        {
            if (_session.State != RunState.Running) return;
            _session.Pause();
        }

        private void StepProgram()
        {
            if (_session.State == RunState.Halted)
            {
                _output.Log("Процессор остановлен (HLT). Сначала выполните сброс.", LogLevel.Warning);
                return;
            }

            if (_session.State == RunState.Running)
            {
                _session.Pause();
            }

            _session.Step();
        }

        private void ResetProgram()
        {
            _session.Reset(ResetKind.Soft);
            ushort start = _lastResult != null ? _lastResult.StartAddress : (ushort)0x8000;
            _session.WithMachine(machine => machine.Cpu.State.PC = start);
            _lastBreakpointLogged = -1;
            _output.Log("Сброс · PC=" + start.ToString("X4") + "h");
            _editor.SetExecutionLine(FindLineForAddress(start));
        }

        private void ToggleBreakpoint(int line)
        {
            if (line <= 0) return;

            if (_breakpointLines.Contains(line))
            {
                _breakpointLines.Remove(line);
                _output.Log("Точка останова снята · строка " + line);
            }
            else
            {
                _breakpointLines.Add(line);
                _output.Log("Точка останова · строка " + line);
            }

            _editor.SetBreakpointLines(_breakpointLines);
            ApplyBreakpoints();
        }

        private void ApplyBreakpoints()
        {
            var addresses = new HashSet<ushort>(_addressBreakpoints);
            foreach (int line in _breakpointLines)
            {
                ushort address;
                if (_lineAddresses.TryGetValue(line, out address))
                {
                    addresses.Add(address);
                }
            }

            _session.SetBreakpoints(addresses);
            _statusBreak.Text = "Точки останова: " + addresses.Count;
            _statusBreak.ForeColor = addresses.Count > 0 ? Theme.Yellow : Theme.Muted;
        }

        private void OnCellMemoryBreakpointToggled(ushort address)
        {
            if (_addressBreakpoints.Contains(address))
            {
                _addressBreakpoints.Remove(address);
                _output.Log("Точка останова снята · адрес " + address.ToString("X4") + "h");
            }
            else
            {
                _addressBreakpoints.Add(address);
                _output.Log("Точка останова · адрес " + address.ToString("X4") + "h");
            }

            int? line = FindLineForAddress(address);
            if (line.HasValue)
            {
                if (_addressBreakpoints.Contains(address))
                    _breakpointLines.Add(line.Value);
                else
                    _breakpointLines.Remove(line.Value);

                _editor.SetBreakpointLines(_breakpointLines);
            }

            ApplyBreakpoints();
        }

        private void OnSetPcRequested(ushort address)
        {
            _session.WithMachine(machine => machine.Cpu.State.PC = address);
            _output.Log("Установлен PC = " + address.ToString("X4") + "h");
            _statusMessage.Text = "PC = " + address.ToString("X4") + "h";
            _editor.SetExecutionLine(FindLineForAddress(address));
        }

        private void OnProfileSelected(object sender, EventArgs e)
        {
            if (_profileBox == null) return;
            var target = _profileBox.SelectedIndex == 0 ? CpuProfile.Legacy : CpuProfile.Extended;
            if (_session.Profile != target)
            {
                _session.SwitchProfile(target);
                string desc = target == CpuProfile.Legacy ? "Legacy (Лабораторные 1-4)" : "Newest / Extended (Лабораторная 5+)";
                _output.Log("Профиль процессора переключен на: " + desc, LogLevel.Info);
                _statusMessage.Text = "Профиль: " + (target == CpuProfile.Legacy ? "Legacy" : "Newest");
            }
        }

        private void GoToProgramCounter()
        {
            ushort pc = CurrentProgramCounter();
            int? line = FindLineForAddress(pc);
            if (line.HasValue)
            {
                _editor.GoToLine(line.Value);
            }
            _memoryPanel.SetAddress(pc);
            _cellMemoryPanel.FollowAddress(pc);
            _statusMessage.Text = "PC = " + pc.ToString("X4") + "h";
        }

        private void OnMemoryWrite(ushort address, byte value)
        {
            if (_session.State == RunState.Running)
            {
                _output.Log("Остановите выполнение, чтобы редактировать память.", LogLevel.Warning);
                return;
            }
            _session.WithMachine(machine => machine.Memory.WriteByte(address, value));
        }

        private void SelectRightTab(int index)
        {
            if (index >= 0 && index < _rightTabs.TabCount)
            {
                _rightTabs.SelectedIndex = index;
            }
        }

        // ------------------------------------------------------------------ files

        private void NewDocument()
        {
            if (!ConfirmDiscard()) return;

            _filePath = null;
            LoadCode(SamplePrograms.NewTemplate, true);
            _loadingSample = true;
            _sampleBox.SelectedIndex = -1;
            _loadingSample = false;
            _output.Log("Новый проект");
            AssembleProgram();
            _dirty = false;
            UpdateTitle();
        }

        private void OpenDocument()
        {
            if (!ConfirmDiscard()) return;

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Открыть программу";
                dialog.Filter = "Ассемблер KR580VM80A (*.asm;*.a80)|*.asm;*.a80|Все файлы (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string text = File.ReadAllText(dialog.FileName);
                    _filePath = dialog.FileName;
                    LoadCode(text, true);
                    _loadingSample = true;
                    _sampleBox.SelectedIndex = -1;
                    _loadingSample = false;
                    _dirty = false;
                    UpdateTitle();
                    _output.Log("Открыто: " + dialog.FileName, LogLevel.Success);
                    AssembleProgram();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось открыть файл:\n" + ex.Message,
                        "MikroLab Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private bool SaveDocument()
        {
            if (string.IsNullOrEmpty(_filePath))
            {
                return SaveDocumentAs();
            }

            try
            {
                File.WriteAllText(_filePath, _editor.Code, new UTF8Encoding(false));
                _dirty = false;
                UpdateTitle();
                _output.Log("Сохранено: " + _filePath, LogLevel.Success);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось сохранить файл:\n" + ex.Message,
                    "MikroLab Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private bool SaveDocumentAs()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить программу";
                dialog.Filter = "Ассемблер KR580VM80A (*.asm)|*.asm|Все файлы (*.*)|*.*";
                dialog.FileName = _filePath != null ? Path.GetFileName(_filePath) : "program.asm";
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;

                _filePath = dialog.FileName;
                return SaveDocument();
            }
        }

        private void LoadSample(int index, bool announce)
        {
            if (index < 0 || index >= SamplePrograms.All.Length) return;

            SampleProgram sample = SamplePrograms.All[index];

            if (_profileBox != null)
            {
                int targetProfileIndex = sample.RecommendedProfile == CpuProfile.Legacy ? 0 : 1;
                if (_profileBox.SelectedIndex != targetProfileIndex)
                {
                    _profileBox.SelectedIndex = targetProfileIndex;
                }
            }

            LoadCode(sample.Code, true);
            _filePath = null;
            _loadingSample = true;
            _sampleBox.SelectedIndex = index;
            _loadingSample = false;
            _output.Log("Пример: " + sample.Name);
            AssembleProgram();
            _dirty = false;
            UpdateTitle();
        }

        private void LoadCode(string code, bool clearBreakpoints)
        {
            _loadingCode = true;
            try
            {
                _editor.Code = code ?? string.Empty;
            }
            finally
            {
                _loadingCode = false;
            }

            if (clearBreakpoints)
            {
                _breakpointLines.Clear();
                _addressBreakpoints.Clear();
                _editor.SetBreakpointLines(_breakpointLines);
            }

            _lastResult = null;
            _lastAssembledText = null;
            _programLoaded = false;
            _lineAddresses.Clear();
            _addressRanges.Clear();
            _editor.SetExecutionLine(null);
            ApplyBreakpoints();
            _dirty = false;
        }

        private void OnCodeChanged(object sender, EventArgs e)
        {
            if (_loadingCode) return;

            _dirty = true;
            UpdateTitle();
            _editor.SetExecutionLine(null);
            _statusMessage.ForeColor = Theme.Muted;
            _statusMessage.Text = "Есть изменения · соберите программу (F7)";
        }

        private void OnSampleSelected(object sender, EventArgs e)
        {
            if (_loadingSample) return;
            int index = _sampleBox.SelectedIndex;
            if (index < 0) return;

            if (!ConfirmDiscard())
            {
                _loadingSample = true;
                _sampleBox.SelectedIndex = -1;
                _loadingSample = false;
                return;
            }

            LoadSample(index, true);
        }

        private bool ConfirmDiscard()
        {
            if (!_dirty) return true;

            DialogResult result = MessageBox.Show(
                this,
                "В программе есть несохраненные изменения. Сохранить их?",
                "MikroLab Studio",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.Yes) return SaveDocument();
            return true;
        }

        private void UpdateTitle()
        {
            string name = _filePath == null ? "Без названия" : Path.GetFileName(_filePath);
            Text = (_dirty ? "● " : string.Empty) + name + " — MikroLab Studio";
            if (_editorFileLabel != null)
            {
                _editorFileLabel.Text = "ПРОГРАММА · " + name + (_dirty ? "  ·  изменено" : string.Empty);
            }
        }

        private void UpdateCursorLabel()
        {
            _cursorLabel.Text = "Строка " + _editor.CaretLine + ", кол. " + _editor.CaretColumn;
            _statusCursor.Text = "Строка " + _editor.CaretLine + ", кол. " + _editor.CaretColumn;
        }

        // ------------------------------------------------------------------ dialogs

        private void ShowHotkeys()
        {
            MessageBox.Show(
                this,
                "F5  ·  Пуск\n" +
                "F6  ·  Пауза\n" +
                "F7  ·  Собрать и загрузить\n" +
                "F9  ·  Точка останова на текущей строке\n" +
                "F10 ·  Один шаг\n" +
                "Ctrl+R · Сброс\n" +
                "Ctrl+N / Ctrl+O / Ctrl+S · Файл\n\n" +
                "Клик по левому полю редактора также ставит точку останова.",
                "Горячие клавиши",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                this,
                "MikroLab Studio 1.0\n" +
                "Самостоятельная среда разработки для КР580ВМ80А / Intel 8080.\n\n" +
                "Ядро: Emulator.Core (headless, без WinForms).\n" +
                "Полный набор команд i8080, двухпроходный ассемблер,\n" +
                "точки останова, инспектор памяти и панель устройств.\n\n" +
                "Лицензия: GNU GPL v3.\n" +
                "Идейное продолжение открытого эмулятора Mikrolab.",
                "О программе",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ------------------------------------------------------------------ host automation

        /// <summary>Used by the screenshot harness to assemble the visible program.</summary>
        public void HostAssemble()
        {
            AssembleProgram();
        }

        /// <summary>Used by the screenshot harness to start execution.</summary>
        public void HostRun()
        {
            RunProgram();
        }

        /// <summary>Used by the screenshot harness to pause execution.</summary>
        public void HostPause()
        {
            PauseProgram();
        }

        /// <summary>Used by the screenshot harness to advance one instruction.</summary>
        public void HostStep()
        {
            StepProgram();
        }

        /// <summary>Used by the screenshot harness to select a sample program.</summary>
        public void HostSelectSample(int index)
        {
            LoadSample(index, false);
            _dirty = false;
            UpdateTitle();
        }

        /// <summary>Used by the screenshot harness to select a right-hand tab.</summary>
        public void HostSelectTab(int index)
        {
            SelectRightTab(index);
        }

        /// <summary>Used by the screenshot harness to toggle a breakpoint.</summary>
        public void HostToggleBreakpoint(int line)
        {
            ToggleBreakpoint(line);
        }

        // ------------------------------------------------------------------ misc

        private void SafeUi(Action action)
        {
            if (action == null || IsDisposed || Disposing) return;

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (InvalidOperationException)
            {
                // Window is closing (ObjectDisposedException derives from this).
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5:
                    RunProgram();
                    return true;
                case Keys.F6:
                    PauseProgram();
                    return true;
                case Keys.F7:
                    AssembleProgram();
                    return true;
                case Keys.F9:
                    ToggleBreakpoint(_editor.CaretLine);
                    return true;
                case Keys.F10:
                    StepProgram();
                    return true;
                case Keys.Control | Keys.R:
                    ResetProgram();
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscard())
            {
                e.Cancel = true;
                return;
            }
            _uiTimer.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _uiTimer?.Stop();
                _uiTimer?.Dispose();
                _toolTip?.Dispose();
                _session?.Dispose();
            }
            base.Dispose(disposing);
        }

        private struct AddressRange
        {
            public readonly ushort Address;
            public readonly int Length;
            public readonly int Line;

            public AddressRange(ushort address, int length, int line)
            {
                Address = address;
                Length = length;
                Line = line;
            }
        }
    }
}
