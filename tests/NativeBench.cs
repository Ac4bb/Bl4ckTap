using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Bl4ckTapApp
{
    internal sealed class NativeBench : Form
    {
        private readonly Label result = new Label();
        private readonly Button start = new Button();
        private readonly Panel pad = new Panel();
        private readonly Timer timer = new Timer { Interval = 50 };
        private ClickEngine engine;
        private IntPtr ownWindow, padWindow;
        private int stage, ld, lu, rd, ru;
        private long stoppedLeft, stoppedRight;
        private readonly Stopwatch phase = new Stopwatch();

        private NativeBench()
        {
            Text="Bl4ckTap - Test natif isolé"; ClientSize=new Size(620,460); StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Segoe UI",11); BackColor=Color.FromArgb(22,24,28); ForeColor=Color.White;
            result.Bounds=new Rectangle(20,18,580,92); result.Text="Prêt : 1 000 clics gauche + 1 000 clics droit.\nLes clics restent exclusivement dans la zone de test.";
            start.Text="Démarrer le test"; start.Bounds=new Rectangle(20,116,580,38); start.ForeColor=Color.Black;
            pad.Bounds=new Rectangle(20,175,580,260); pad.BackColor=Color.FromArgb(49,61,52); pad.AccessibleName="Zone de test native sans action";
            Controls.Add(result); Controls.Add(start); Controls.Add(pad);
            pad.MouseDown += delegate(object sender,MouseEventArgs e)
            {
                if ((uint)Native.GetMessageExtraInfo().ToInt64()!=Native.Bl4ckTapMarker) return;
                if(e.Button==MouseButtons.Left)ld++; if(e.Button==MouseButtons.Right)rd++;
            };
            pad.MouseUp += delegate(object sender,MouseEventArgs e)
            {
                if ((uint)Native.GetMessageExtraInfo().ToInt64()!=Native.Bl4ckTapMarker) return;
                if(e.Button==MouseButtons.Left)lu++; if(e.Button==MouseButtons.Right)ru++;
            };
            Shown += delegate
            {
                ownWindow=Handle; padWindow=pad.Handle;
                engine=new ClickEngine(new ClickOptions(true,true,false,30),ShortcutBinding.Mouse(4),null,delegate
                {
                    Native.Point point;
                    return Native.GetForegroundWindow()==ownWindow && Native.GetCursorPos(out point) && Native.WindowFromPoint(point)==padWindow;
                });
                timer.Start();
            };
            start.Click += delegate
            {
                if(engine==null||!engine.Status.Ready||stage!=0)return;
                stage=1; start.Enabled=false; engine.Toggle(); phase.Restart();
            };
            timer.Tick += delegate { Tick(); };
            FormClosing += delegate { timer.Stop(); if(engine!=null)engine.Dispose(); };
        }
        private void Tick()
        {
            if(engine==null)return;
            EngineStatus s=engine.Status;
            if(s.Error!=null) { result.Text="ÉCHEC : "+s.Error; stage=5; engine.Stop(); return; }
            if(stage==1)
            {
                result.Text="Test natif : gauche "+s.LeftCount+" / droit "+s.RightCount+"\nPlace le curseur dans le rectangle vert. F8 arrête.";
                if(s.LeftCount>=1000&&s.RightCount>=1000) { engine.Stop();stage=2;phase.Restart(); }
            }
            else if(stage==2&&phase.ElapsedMilliseconds>300)
            {
                stoppedLeft=s.LeftCount;stoppedRight=s.RightCount;
                if(ld!=lu||rd!=ru||s.LeftHeld||s.RightHeld||s.Enabled) { result.Text="ÉCHEC : état bloqué à l'arrêt.";stage=5;return; }
                engine.Configure(new ClickOptions(true,true,true,30));engine.Toggle();stage=3;phase.Restart();
            }
            else if(stage==3)
            {
                result.Text="Vérification : maintien actif, aucun bouton physique enfoncé…";
                if(phase.ElapsedMilliseconds>1200)
                {
                    engine.Stop();stage=4;
                    bool pass=s.LeftCount==stoppedLeft&&s.RightCount==stoppedRight&&!s.LeftHeld&&!s.RightHeld&&ld==lu&&rd==ru&&ld>=1000&&rd>=1000;
                    result.Text=(pass?"RÉUSSI":"ÉCHEC")+" : "+ld+" clics gauche, "+rd+" clics droit.\nTous relâchés. Aucun maintien fantôme après SendInput.";
                }
            }
        }
        [STAThread] private static void Main() { Application.EnableVisualStyles();Application.Run(new NativeBench()); }
    }
}
