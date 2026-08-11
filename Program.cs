using System;
using System.Threading;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            bool firstInstance;
            using (Mutex mutex = new Mutex(true, "HansLaserDateSerialDemo.SingleInstance", out firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show("程序已经在运行，不能启动第二个实例。", "大族激光二开Demo", MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 2;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
        }
    }
}
