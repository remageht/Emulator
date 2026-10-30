using System;
using System.Drawing;
using System.Windows.Forms;

namespace MikroLab.Studio
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (sender, args) =>
            {
                MessageBox.Show(
                    args.Exception.ToString(),
                    "MikroLab Studio — непредвиденная ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    MessageBox.Show(
                        ex.ToString(),
                        "MikroLab Studio — критическая ошибка",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };

            Application.Run(new StudioForm());
        }
    }
}
