using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Emulator;
using MikroLab.Studio;

namespace CaptureScreenshots
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                string docsRoot = args.Length > 0
                    ? Path.GetFullPath(args[0])
                    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/screenshots"));
                string stage1Dir = Path.Combine(docsRoot, "stage1");
                string stage2Dir = Path.Combine(docsRoot, "stage2");
                Directory.CreateDirectory(stage1Dir);
                Directory.CreateDirectory(stage2Dir);

                CaptureLegacy(stage1Dir);
                CaptureStudio(stage2Dir);

                Console.WriteLine("All screenshots captured successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception capturing screenshots: " + ex);
                return 2;
            }
        }

        private static void CaptureLegacy(string baseDir)
        {
            Console.WriteLine("Capturing legacy MainForm screenshots to: " + baseDir);

            using (var form = new MainForm())
            {
                form.Show();
                Application.DoEvents();
                Thread.Sleep(300);
                Application.DoEvents();

                Console.WriteLine($"MainForm: Size={form.Size}, ClientSize={form.ClientSize}");
                var mainTabControl = form.Controls[0] as TabControl;
                if (mainTabControl == null)
                {
                    Console.WriteLine("Error: mainTabControl not found");
                    return;
                }

                mainTabControl.SelectedIndex = 0;
                Application.DoEvents();
                Thread.Sleep(200);
                Application.DoEvents();

                SaveBitmap(form, Path.Combine(baseDir, "main_classic.png"));
                Console.WriteLine("Saved: main_classic.png");

                var classicPanel = form.ClassicPanel;
                if (classicPanel != null)
                {
                    var labsTabControl = classicPanel.Controls["TabLabsControl"] as TabControl;
                    if (labsTabControl != null)
                    {
                        for (int i = 0; i < labsTabControl.TabCount; i++)
                        {
                            labsTabControl.SelectedIndex = i;
                            Application.DoEvents();
                            Thread.Sleep(150);
                            Application.DoEvents();

                            string name = $"classic_lab_{i + 1}.png";
                            SaveBitmap(form, Path.Combine(baseDir, name));
                            Console.WriteLine("Saved: " + name);
                        }
                    }
                }

                mainTabControl.SelectedIndex = 1;
                Application.DoEvents();
                Thread.Sleep(200);
                Application.DoEvents();

                SaveBitmap(form, Path.Combine(baseDir, "main_modern.png"));
                Console.WriteLine("Saved: main_modern.png");

                form.Close();
            }
        }

        private static void CaptureStudio(string baseDir)
        {
            Console.WriteLine("Capturing MikroLab Studio screenshots to: " + baseDir);

            using (var form = new StudioForm())
            {
                form.Show();
                Application.DoEvents();
                Thread.Sleep(600);
                Application.DoEvents();

                SaveBitmap(form, Path.Combine(baseDir, "studio_main.png"));
                Console.WriteLine("Saved: studio_main.png");

                // Put a breakpoint on the SHIFT: OUT 01h line, run until it
                // stops, then execute one step with the lamp output.
                form.HostToggleBreakpoint(12);
                form.HostRun();
                Application.DoEvents();
                Thread.Sleep(500);
                Application.DoEvents();

                SaveBitmap(form, Path.Combine(baseDir, "studio_breakpoint.png"));
                Console.WriteLine("Saved: studio_breakpoint.png");

                form.HostStep();
                form.HostSelectTab(1);
                Application.DoEvents();
                Thread.Sleep(300);
                Application.DoEvents();

                SaveBitmap(form, Path.Combine(baseDir, "studio_devices.png"));
                Console.WriteLine("Saved: studio_devices.png");

                form.HostPause();
                form.Close();
            }
        }

        private static void SaveBitmap(Form form, string path)
        {
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(path, ImageFormat.Png);
            }
        }
    }
}
