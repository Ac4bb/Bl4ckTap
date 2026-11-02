using System;
using System.Threading;
using System.Windows.Forms;

namespace Bl4ckTapApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool first;
            using (Mutex instance = new Mutex(true, "Local\\Bl4ckTapApp.v1", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Bl4ckTap est déjà ouvert. Utilise le raccourci choisi pour activer / désactiver, ou F8 pour arrêter.", "Bl4ckTap", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm(true));
                }
                finally { instance.ReleaseMutex(); }
            }
        }
    }
}
