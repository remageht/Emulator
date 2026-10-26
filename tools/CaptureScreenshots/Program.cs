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
                string outDir = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/screenshots/original"));
                Directory.CreateDirectory(outDir);
                Console.WriteLine("Output directory: " + outDir);

                using (var form = new ProgramLogic())
                {
                    form.Show();
                    Application.DoEvents();

                    var tabControl = form.Controls["TabLabsControl"] as TabControl;
                    if (tabControl == null)
                    {
                        Console.WriteLine("Error: TabLabsControl not found");
                        return 1;
                    }

                    for (int i = 0; i < tabControl.TabCount; i++)
                    {
                        tabControl.SelectedIndex = i;
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(200);
                        Application.DoEvents();

                        using (var bmp = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                            string safeName = $"tab_{i + 1}_lab_{i + 1}.png";
                            string outPath = Path.Combine(outDir, safeName);
                            bmp.Save(outPath, ImageFormat.Png);
                            Console.WriteLine($"Saved screenshot: {outPath}");
                        }
                    }

                    form.Close();
                }

                Console.WriteLine("All screenshots captured successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception capturing screenshots: " + ex);
                return 2;
            }
        }
    }
}
