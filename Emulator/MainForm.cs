using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Emulator.Core.Machine;

namespace Emulator
{
    public class MainForm : Form
    {
        private TabControl mainTabControl;
        private TabPage tabPageClassic;
        private TabPage tabPageModern;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public ProgramLogic ClassicPanel => classicPanel;
        private ProgramLogic classicPanel;

        private Machine machine;
        private MachineSession session;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.mainTabControl = new TabControl();
            this.tabPageClassic = new TabPage();
            this.tabPageModern = new TabPage();

            this.SuspendLayout();

            // mainTabControl
            this.mainTabControl.Dock = DockStyle.Fill;
            this.mainTabControl.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            this.mainTabControl.Controls.Add(this.tabPageClassic);
            this.mainTabControl.Controls.Add(this.tabPageModern);
            this.mainTabControl.SelectedIndex = 0;
            this.mainTabControl.SelectedIndexChanged += new EventHandler(this.OnTabChanged);

            // tabPageClassic
            this.tabPageClassic.Text = "Мікролаб (класичний)";
            this.tabPageClassic.BackColor = SystemColors.Control;
            this.tabPageClassic.Padding = new Padding(0);

            // classicPanel
            this.classicPanel = new ProgramLogic();
            this.classicPanel.Dock = DockStyle.Fill;
            this.tabPageClassic.Controls.Add(this.classicPanel);

            // tabPageModern
            this.tabPageModern.Text = "Сучасний режим";
            this.tabPageModern.BackColor = Color.FromArgb(248, 249, 250);
            this.tabPageModern.Padding = new Padding(12);

            SetupModernPlaceholder();

            // MainForm
            this.ClientSize = new Size(928, 556);
            this.MinimumSize = new Size(940, 590);
            this.Text = "Емулятор Мікролаб КР580ИК80";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Controls.Add(this.mainTabControl);

            this.ResumeLayout(false);

            // Initialize headless Machine and Session
            this.machine = new Machine();
            this.session = new MachineSession(this.machine);
        }

        private void SetupModernPlaceholder()
        {
            var header = new Label
            {
                Text = "⚡ Сучасний режим — Інтегроване середовище (IDE)",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new Point(16, 16),
                AutoSize = true
            };

            var subtitle = new Label
            {
                Text = "Єдине спільне машинне середовище (Emulator.Core) з класичним Мікролабом.\n" +
                       "На Етапі 2 тут розгортається повнофункціональне середовище:",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(73, 80, 87),
                Location = new Point(16, 48),
                Size = new Size(880, 40)
            };

            var featuresBox = new GroupBox
            {
                Text = "Компоненти сучасного режиму (Етап 2)",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Location = new Point(16, 96),
                Size = new Size(880, 360)
            };

            var desc = new Label
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(16, 28),
                Size = new Size(848, 310),
                Text = "• [Редактор асемблера]: Редактор коду КР580ВМ80А / i8080 з підсвічуванням синтаксису, номерами рядків та закладками.\n\n" +
                       "• [Двоетапний транслятор]: Підтримка міток, констант (EQU), директив виділення пам'яті (ORG, DB, DW, DS).\n" +
                       "  Повний набір команд i8080, включно з відсутніми в класичному Мікролабі (NOP, CMA, ADC, RRC, RLC, RAR, RZ тощо).\n\n" +
                       "• [Інтерактивний відладчик]: Керування виконанням (Пуск / Пауза / Крок вперед / Скидання) без зависання UI навіть на нескінченних циклах.\n" +
                       "  Точки зупинки (Breakpoints), трасування стек-викликів, інспекція регістрів (A, B, C, D, E, H, L, SP, PC) та прапорців (S, Z, AC, P, C).\n\n" +
                       "• [Інспектор пам'яті та стек]: Таблиця байтів у шістнадцятковому/символьному вигляді (HEX/ASCII) з можливістю прямого редагування та переходу на адресу.\n\n" +
                       "• [Панель пристроїв]: Інтерактивний контролер ВВ55 (порти A, B, C), лінійка світлодіодів, блок перемикачів і 7-сегментні індикатори."
            };
            featuresBox.Controls.Add(desc);

            this.tabPageModern.Controls.Add(header);
            this.tabPageModern.Controls.Add(subtitle);
            this.tabPageModern.Controls.Add(featuresBox);
        }

        private void OnTabChanged(object sender, EventArgs e)
        {
            if (mainTabControl.SelectedTab == tabPageModern)
            {
                // Sync state from Classic panel to headless MachineSession
                classicPanel.SyncToMachine(session.Machine);
            }
            else if (mainTabControl.SelectedTab == tabPageClassic)
            {
                // Sync state from headless MachineSession back to Classic panel
                classicPanel.SyncFromMachine(session.Machine);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                session?.Dispose();
                classicPanel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
