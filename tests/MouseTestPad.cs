using System;
using System.Drawing;
using System.Windows.Forms;

namespace Bl4ckTapApp
{
    internal sealed class MouseTestPad : Form
    {
        private readonly Label counter = new Label();
        private int leftDown, leftUp, rightDown, rightUp;

        private MouseTestPad()
        {
            Text = "Bl4ckTap - Test souris";
            ClientSize = new Size(580, 400);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 12);
            BackColor = Color.FromArgb(22, 30, 41);
            ForeColor = Color.White;
            Label title = new Label { Text = "Zone locale de test : maintiens un bouton ici.", Bounds = new Rectangle(20, 20, 540, 35) };
            counter.Bounds = new Rectangle(20, 65, 540, 70);
            Panel pad = new Panel { Bounds = new Rectangle(20, 150, 540, 225), BackColor = Color.FromArgb(45, 63, 57), AccessibleName = "Zone de test des clics" };
            pad.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) leftDown++; if (e.Button == MouseButtons.Right) rightDown++; UpdateCounter(); };
            pad.MouseUp += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) leftUp++; if (e.Button == MouseButtons.Right) rightUp++; UpdateCounter(); };
            Controls.Add(title); Controls.Add(counter); Controls.Add(pad);
            UpdateCounter();
        }

        private void UpdateCounter()
        {
            counter.Text = "Gauche : " + leftDown + " appuis / " + leftUp + " relâchements\nDroit : " + rightDown + " appuis / " + rightUp + " relâchements";
        }

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MouseTestPad());
        }
    }
}
