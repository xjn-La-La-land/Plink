using System;
using System.Threading;
using System.Windows.Forms;

namespace Plink
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex instanceMutex = new Mutex(true, "Plink.SingleInstance.v1", out createdNew))
            {
                if (!createdNew)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (TrayApplicationContext context = new TrayApplicationContext())
                {
                    Application.Run(context);
                }
            }
        }
    }
}
