using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

namespace Bl4ckTapApp
{
    internal sealed class MainForm : Form
    {
        private readonly MouseChoice left, right;
        private readonly ModeChoice hold, continuous;
        private readonly CpsSlider speed;
        private readonly Label speedValue, stateTitle, stateHint, modeHint, shortcutHint, totals;
        private readonly ModernButton toggle, changeShortcut;
        private readonly StatusCard statusCard;
        private readonly Timer refresh;
        private readonly ClickEngine engine;
        private readonly string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Bl4ckTap.settings.xml");
        private readonly SessionSwitchEventHandler sessionChanged;
        private readonly PowerModeChangedEventHandler powerChanged;
        private bool loading = true, choosingShortcut;
        private int captureStartRevision, savedRevision = -1;
        private ShortcutBinding initialShortcut = ShortcutBinding.Mouse(4);

        public MainForm(bool live)
        {
            AutoScaleDimensions = new SizeF(96,96); AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Bl4ckTap 2.0"; ClientSize = new Size(480,654);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Background; ForeColor = Theme.Text; Font = new Font("Segoe UI",10); DoubleBuffered = true;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            AddLabel(this,"BL4CKTAP",24,19,350,34,22,Theme.Text,FontStyle.Bold);
            AddLabel(this,"S I M P L E  /  P R É C I S  /  R A P I D E",26,56,370,18,8,Theme.Muted);
            Label version = AddLabel(this,"2.0",409,27,47,23,10,Theme.Muted); version.TextAlign = ContentAlignment.MiddleRight;

            statusCard = new StatusCard { Bounds = new Rectangle(24,88,432,65) };
            Controls.Add(statusCard);
            stateTitle = AddLabel(statusCard,"DÉSACTIVÉ",33,10,384,23,10.5f,Theme.Text,FontStyle.Bold);
            stateHint = AddLabel(statusCard,"Choisis tes boutons, puis active l'auto clicker.",15,36,402,27,8.5f,Theme.Muted);
            AddLabel(this,"BOUTONS À RÉPÉTER",24,172,432,18,8,Theme.Muted,FontStyle.Bold);
            left = new MouseChoice { Text = "Clic gauche", Caption = "Attaquer / casser", Bounds = new Rectangle(24,195,210,66), Checked = true, AccessibleName = "Répéter le clic gauche" };
            right = new MouseChoice { Text = "Clic droit", Caption = "Placer / utiliser", RightSide = true, Bounds = new Rectangle(246,195,210,66), AccessibleName = "Répéter le clic droit" };
            Controls.Add(left); Controls.Add(right);

            AddLabel(this,"MODE DE CLIC",24,279,432,18,8,Theme.Muted,FontStyle.Bold);
            Panel modes = new Panel { Bounds = new Rectangle(24,302,432,39), BackColor = Theme.Background };
            Controls.Add(modes);
            hold = new ModeChoice { Text = "Maintenir le bouton", Bounds = new Rectangle(0,0,210,39), Checked = true };
            continuous = new ModeChoice { Text = "Automatique continu", Bounds = new Rectangle(222,0,210,39) };
            modes.Controls.Add(hold); modes.Controls.Add(continuous);
            modeHint = AddLabel(this,"",26,350,428,33,8.5f,Theme.Muted);

            AddLabel(this,"VITESSE",24,397,210,18,8,Theme.Muted,FontStyle.Bold);
            speedValue = AddLabel(this,"12 CPS",300,383,156,34,23,Theme.Text,FontStyle.Bold);
            speedValue.TextAlign = ContentAlignment.MiddleRight;
            speed = new CpsSlider { Bounds = new Rectangle(24,422,432,28), AccessibleName = "Vitesse : 12 clics par seconde" };
            Controls.Add(speed);
            AddLabel(this,"1",26,453,30,15,8,Theme.Muted);
            Label unit = AddLabel(this,"clics / seconde / bouton",100,453,280,15,8,Theme.Muted); unit.TextAlign = ContentAlignment.MiddleCenter;
            Label max = AddLabel(this,"30",426,453,30,15,8,Theme.Muted); max.TextAlign = ContentAlignment.MiddleRight;

            AddLabel(this,"RACCOURCI ON / OFF",24,480,432,18,8,Theme.Muted,FontStyle.Bold);
            changeShortcut = new ModernButton { Keycap = true, Text = "Souris 4   ·   Modifier le raccourci", Bounds = new Rectangle(24,503,432,40), AccessibleName = "Modifier le raccourci d'activation" };
            Controls.Add(changeShortcut);
            shortcutHint = AddLabel(this,"Un appui active. Un autre appui désactive.",26,550,428,22,8.5f,Theme.Muted);
            toggle = new ModernButton { Text = "ACTIVER", Primary = true, Bounds = new Rectangle(24,585,432,43) };
            Controls.Add(toggle);
            AddLabel(this,"F8  ·  Arrêt immédiat",26,636,220,16,8,Theme.Muted);
            totals = AddLabel(this,"G 0  ·  D 0",260,636,194,16,8,Theme.Muted); totals.TextAlign = ContentAlignment.MiddleRight;

            if (live) LoadSettings();
            loading = false; UpdateModeText();
            if (live) engine = new ClickEngine(CurrentOptions(),initialShortcut);
            left.CheckedChanged += SettingsChanged; right.CheckedChanged += SettingsChanged;
            hold.CheckedChanged += SettingsChanged; continuous.CheckedChanged += SettingsChanged; speed.ValueChanged += SettingsChanged;
            changeShortcut.Click += delegate
            {
                if (engine == null || !engine.Status.Ready) return;
                if (choosingShortcut) { CancelShortcutSelection(); return; }
                captureStartRevision = engine.Status.ShortcutRevision; choosingShortcut = true;
                engine.BeginShortcutCapture(); UpdateStatus();
            };
            toggle.Click += delegate { if (engine != null) engine.Toggle(); };
            Deactivate += delegate { CancelShortcutSelection(); };
            refresh = new Timer { Interval = 80 }; refresh.Tick += delegate { UpdateStatus(); };
            if (live) refresh.Start();
            sessionChanged = delegate(object sender, SessionSwitchEventArgs e) { if (engine != null && e.Reason == SessionSwitchReason.SessionLock) engine.Stop(); };
            powerChanged = delegate(object sender, PowerModeChangedEventArgs e) { if (engine != null && e.Mode == PowerModes.Suspend) engine.Stop(); };
            if (live) { SystemEvents.SessionSwitch += sessionChanged; SystemEvents.PowerModeChanged += powerChanged; }
            FormClosing += delegate { refresh.Stop(); if (engine != null) engine.Dispose(); if (live) SaveSettings(); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); Theme.DarkTitleBar(Handle);
        }

        private static Label AddLabel(Control parent,string text,int x,int y,int w,int h,float size,Color color,FontStyle style=FontStyle.Regular)
        {
            Label label = new Label { Text=text,Bounds=new Rectangle(x,y,w,h),Font=new Font("Segoe UI",size,style),ForeColor=color,BackColor=Color.Transparent,UseMnemonic=false };
            parent.Controls.Add(label); return label;
        }
        private ClickOptions CurrentOptions() { return new ClickOptions(left.Checked,right.Checked,hold.Checked,speed.Value); }
        private void CancelShortcutSelection()
        {
            if (!choosingShortcut || engine == null) return;
            choosingShortcut = false; engine.CancelShortcutCapture();
        }
        protected override bool ProcessCmdKey(ref Message message,Keys keyData)
        {
            if (!choosingShortcut) return base.ProcessCmdKey(ref message,keyData);
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Escape || key == Keys.F8) CancelShortcutSelection();
            else if ((keyData & Keys.Modifiers) == Keys.None && ShortcutBinding.SupportsKey(key))
            {
                engine.ChangeShortcut(ShortcutBinding.Keyboard(key)); choosingShortcut = false;
            }
            return true;
        }
        private void SettingsChanged(object sender,EventArgs e)
        {
            if (loading) return;
            // Radio buttons fire both transitions; apply only once a mode is selected.
            if (!hold.Checked && !continuous.Checked) return;
            UpdateModeText(); if (engine != null) engine.Configure(CurrentOptions()); SaveSettings();
        }
        private void UpdateModeText()
        {
            speedValue.Text = speed.Value + " CPS"; speed.AccessibleName = "Vitesse : " + speed.Value + " clics par seconde";
            modeHint.Text = hold.Checked ? "Seul le bouton coché que tu maintiens se répète.\nRelâche gauche ou droit : ce bouton s'arrête."
                : "Les boutons cochés cliquent sans maintien.\nTon raccourci ou F8 arrête les clics.";
        }
        private void UpdateStatus()
        {
            EngineStatus s = engine.Status;
            if (choosingShortcut && s.ShortcutRevision > captureStartRevision) choosingShortcut = false;
            bool selected = left.Checked || right.Checked;
            toggle.Enabled = s.Ready && selected && !choosingShortcut && !s.CapturingShortcut;
            toggle.Text = (s.Enabled ? "DÉSACTIVER" : "ACTIVER") + "   ·   " + s.Shortcut.Name;
            toggle.Primary = !s.Enabled; toggle.Invalidate();
            changeShortcut.Enabled = s.Ready;
            changeShortcut.Text = choosingShortcut ? "Appuie sur une touche ou un bouton…" : s.Shortcut.Name + "   ·   Modifier le raccourci";
            shortcutHint.Text = choosingShortcut ? "Souris 4 / 5, clic molette ou touche. Échap : annuler." : "Un appui active. Un autre appui désactive.";
            totals.Text = "G " + s.LeftCount.ToString("N0") + "  ·  D " + s.RightCount.ToString("N0");
            statusCard.Active = s.Enabled;
            stateTitle.ForeColor = s.Enabled ? Theme.Green : Theme.Text;
            if (choosingShortcut) { stateTitle.Text = "CHOIX DU RACCOURCI"; stateHint.Text = "Les clics sont arrêtés pendant le changement."; }
            else if (s.Error != null) { stateTitle.Text = s.Ready ? "ARRÊTÉ" : "INDISPONIBLE"; stateHint.Text = s.Error; stateTitle.ForeColor = Color.FromArgb(212,142,135); }
            else if (!selected) { stateTitle.Text = "AUCUN BOUTON SÉLECTIONNÉ"; stateHint.Text = "Coche gauche, droit ou les deux."; }
            else if (!s.Enabled) { stateTitle.Text = "DÉSACTIVÉ"; stateHint.Text = "Active avec " + s.Shortcut.Name + ", puis maintiens un bouton."; if (!hold.Checked) stateHint.Text = "Active avec " + s.Shortcut.Name + " pour cliquer en continu."; }
            else if (!s.Allowed) { stateTitle.Text = "ACTIVÉ"; stateHint.Text = "En pause ici. Retourne dans ton application."; }
            else
            {
                stateTitle.Text = "ACTIVÉ";
                bool l = left.Checked && (!hold.Checked || s.LeftHeld), r = right.Checked && (!hold.Checked || s.RightHeld);
                stateHint.Text = l || r ? (l && r ? "Gauche + droit" : l ? "Gauche" : "Droit") + " · " + speed.Value + " CPS par bouton"
                    : "Prêt : maintiens gauche, droit ou les deux.";
            }
            if (savedRevision != s.ShortcutRevision && s.Ready) { savedRevision = s.ShortcutRevision; SaveSettings(); }
        }
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsPath)) return;
                using (XmlReader reader = XmlReader.Create(settingsPath,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null }))
                {
                    XmlDocument doc = new XmlDocument { XmlResolver=null }; doc.Load(reader); XmlElement root=doc.DocumentElement;
                    if (root == null) return;
                    left.Checked=root.GetAttribute("left")!="false"; right.Checked=root.GetAttribute("right")=="true";
                    hold.Checked=root.GetAttribute("mode")!="continuous"; continuous.Checked=!hold.Checked;
                    int cps; if (Int32.TryParse(root.GetAttribute("cps"),out cps)) speed.Value=cps;
                    initialShortcut=ShortcutBinding.Parse(root.GetAttribute("shortcut"));
                }
            }
            catch (IOException) { } catch (XmlException) { } catch (UnauthorizedAccessException) { }
        }
        private void SaveSettings()
        {
            if (loading) return;
            try
            {
                string temp=settingsPath+".tmp";
                using (XmlWriter writer=XmlWriter.Create(temp,new XmlWriterSettings { Indent=true }))
                {
                    writer.WriteStartElement("autoclick"); writer.WriteAttributeString("left",left.Checked?"true":"false");
                    writer.WriteAttributeString("right",right.Checked?"true":"false"); writer.WriteAttributeString("mode",hold.Checked?"hold":"continuous");
                    writer.WriteAttributeString("cps",speed.Value.ToString());
                    writer.WriteAttributeString("shortcut",engine!=null?engine.Status.Shortcut.Id:initialShortcut.Id); writer.WriteEndElement();
                }
                if (File.Exists(settingsPath)) File.Replace(temp,settingsPath,null); else File.Move(temp,settingsPath);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.SessionSwitch -= sessionChanged; SystemEvents.PowerModeChanged -= powerChanged;
                if (refresh!=null) refresh.Dispose(); if (engine!=null) engine.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
