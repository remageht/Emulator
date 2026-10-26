using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Emulator;

namespace CaptureScreenshots
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                string baseDir = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/screenshots/stage1"));
                string artifactDir = @"C:\Users\Эмир\.gemini\antigravity\brain\543fa310-12f9-4b8e-bc9b-df193e14fc10\screenshots\stage1";
                Directory.CreateDirectory(baseDir);
                Directory.CreateDirectory(artifactDir);

                Console.WriteLine("Capturing MainForm screenshots to: " + baseDir);

                using (var form = new MainForm())
                {
                    form.Show();
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(300);
                    Application.DoEvents();

                    Console.WriteLine($"MainForm: Size={form.Size}, ClientSize={form.ClientSize}");
                    var mainTabControl = form.Controls[0] as TabControl;
                    if (mainTabControl == null)
                    {
                        Console.WriteLine("Error: mainTabControl not found");
                        return 1;
                    }
                    Console.WriteLine($"mainTabControl: Size={mainTabControl.Size}, ClientSize={mainTabControl.ClientSize}");
                    Console.WriteLine($"ClassicPanel: Size={form.ClassicPanel?.Size}, ClientSize={form.ClassicPanel?.ClientSize}");

                    // 1. Capture Main Classic Tab
                    mainTabControl.SelectedIndex = 0;
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(200);
                    Application.DoEvents();

                    SaveBitmap(form, Path.Combine(baseDir, "main_classic.png"));
                    SaveBitmap(form, Path.Combine(artifactDir, "main_classic.png"));
                    Console.WriteLine("Saved: main_classic.png");

                    // 2. Capture all 6 lab tabs of classic panel
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
                                System.Threading.Thread.Sleep(150);
                                Application.DoEvents();

                                string name = $"classic_lab_{i + 1}.png";
                                SaveBitmap(form, Path.Combine(baseDir, name));
                                SaveBitmap(form, Path.Combine(artifactDir, name));
                                Console.WriteLine($"Saved: {name}");
                            }
                        }
                    }

                    // 3. Capture Modern Tab
                    mainTabControl.SelectedIndex = 1;
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(200);
                    Application.DoEvents();

                    SaveBitmap(form, Path.Combine(baseDir, "main_modern.png"));
                    SaveBitmap(form, Path.Combine(artifactDir, "main_modern.png"));
                    Console.WriteLine("Saved: main_modern.png");

                    form.Close();
                }

                Console.WriteLine("All Stage 1 screenshots captured successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception capturing screenshots: " + ex);
                return 2;
            }
        }

        private static void SaveBitmap(Form form, string path)
        {
            using (var bmp = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
