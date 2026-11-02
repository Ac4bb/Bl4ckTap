using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Bl4ckTapApp
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(12, 13, 15);
        public static readonly Color Card = Color.FromArgb(22, 24, 27);
        public static readonly Color Raised = Color.FromArgb(37, 40, 44);
        public static readonly Color Border = Color.FromArgb(47, 50, 55);
        public static readonly Color Text = Color.FromArgb(241, 242, 244);
        public static readonly Color Muted = Color.FromArgb(148, 153, 161);
        public static readonly Color Green = Color.FromArgb(153, 193, 158);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
        public static void DarkTitleBar(IntPtr handle)
        {
            try { int dark = 1; DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)); }
            catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        }
        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.R+(b.R-a.R)*t), (int)(a.G+(b.G-a.G)*t), (int)(a.B+(b.B-a.B)*t));
        }
        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath(); float d = radius * 2;
            p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90);
            p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
        }
        public static void Surface(Graphics g, Rectangle bounds, Color fill, Color border, int radius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Rounded(new RectangleF(bounds.X+.5f,bounds.Y+.5f,bounds.Width-1,bounds.Height-1),radius))
            using (Brush b = new SolidBrush(fill)) using (Pen pen = new Pen(border)) { g.FillPath(b,p); g.DrawPath(pen,p); }
        }
        public static void Dot(Graphics g, float x, float y, float amount)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i=3;i>0;i--) using (Brush glow = new SolidBrush(Color.FromArgb((int)(amount*8),Green)))
                g.FillEllipse(glow,x-4-i*2,y-4-i*2,8+i*4,8+i*4);
            using (Brush b = new SolidBrush(Mix(Muted,Green,amount))) g.FillEllipse(b,x-3,y-3,6,6);
        }
    }

    // Presentation only. Timers run during transitions and stop completely at rest.
    internal sealed class Motion : IDisposable
    {
        private readonly Control owner;
        private readonly Timer timer;
        private readonly float[] values, starts, targets;
        private readonly long[] started;
        public Motion(Control owner, params float[] initial)
        {
            this.owner=owner; values=(float[])initial.Clone(); starts=(float[])initial.Clone(); targets=(float[])initial.Clone(); started=new long[initial.Length];
            timer=new Timer { Interval=15 }; timer.Tick += Tick;
            owner.Disposed += delegate { Dispose(); };
        }
        public float this[int index] { get { return values[index]; } }
        public void To(int index, float value)
        {
            if (targets[index]==value) return;
            starts[index]=values[index]; targets[index]=value; started[index]=Stopwatch.GetTimestamp();
            if (!owner.IsHandleCreated) { values[index]=value; return; }
            timer.Start();
        }
        private void Tick(object sender, EventArgs e)
        {
            bool running=false; long now=Stopwatch.GetTimestamp();
            for (int i=0;i<values.Length;i++)
            {
                if (values[i]==targets[i]) continue;
                float t=(float)Math.Min(1,(now-started[i])/(Stopwatch.Frequency*.19));
                float ease=1-(1-t)*(1-t)*(1-t);
                values[i]=t>=1 ? targets[i] : starts[i]+(targets[i]-starts[i])*ease;
                running |= t<1;
            }
            owner.Invalidate(true); if (!running) timer.Stop();
        }
        public void Dispose() { timer.Dispose(); }
    }

    internal sealed class StatusCard : Panel
    {
        private readonly Motion motion;
        public bool Active { set { motion.To(0,value?1:0); } }
        public StatusCard()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            BackColor=Theme.Background; motion=new Motion(this,0);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Surface(e.Graphics,ClientRectangle,Theme.Mix(Theme.Card,Color.FromArgb(25,31,28),motion[0]),Theme.Mix(Theme.Border,Color.FromArgb(62,80,65),motion[0]),10);
            Theme.Dot(e.Graphics,18,22,motion[0]); base.OnPaint(e);
        }
    }

    internal sealed class ModernButton : Button
    {
        private readonly Motion motion;
        private bool primary;
        public bool Keycap { get; set; }
        public bool Primary { get { return primary; } set { primary=value; motion.To(2,value?1:0); } }
        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; Cursor=Cursors.Hand;
            BackColor=Theme.Background; ForeColor=Theme.Text; Font=new Font("Segoe UI",10,FontStyle.Bold);
            motion=new Motion(this,0,0,0);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
        protected override void OnMouseEnter(EventArgs e) { motion.To(0,1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { motion.To(0,0); motion.To(1,0); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left) motion.To(1,1); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { motion.To(1,0); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space) motion.To(1,1); base.OnKeyDown(e); }
        protected override void OnKeyUp(KeyEventArgs e) { motion.To(1,0); base.OnKeyUp(e); }
        protected override void OnLostFocus(EventArgs e) { motion.To(1,0); base.OnLostFocus(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            float white=Enabled?motion[2]:0;
            Color fill=Theme.Mix(Theme.Mix(Theme.Card,Theme.Raised,motion[0]),Theme.Text,white);
            fill=Theme.Mix(fill,Color.FromArgb(174,179,186),motion[1]*.22f);
            Color border=Theme.Mix(Theme.Border,Theme.Muted,Focused?.7f:motion[0]*.5f);
            if (!Keycap) border=Theme.Mix(Color.FromArgb(66,85,70),fill,white);
            Rectangle rect=ClientRectangle; int inset=(int)Math.Round(motion[1]); rect.Inflate(-inset,-inset);
            Theme.Surface(e.Graphics,rect,fill,border,9);
            if (!Keycap) using (Pen edge=new Pen(Color.FromArgb(35,255,255,255))) e.Graphics.DrawLine(edge,12,rect.Top+1,Width-13,rect.Top+1);
            Color ink=!Enabled?Theme.Muted:Theme.Mix(Theme.Text,Theme.Background,white);
            int separator=Text.IndexOf('·');
            if (Keycap && separator>0)
            {
                string key=Text.Substring(0,separator).Trim();
                int chipWidth=Math.Min(Width/2,TextRenderer.MeasureText(key,Font).Width+22);
                Rectangle chip=new Rectangle(10,7,chipWidth,Height-14);
                Theme.Surface(e.Graphics,chip,Theme.Mix(Theme.Raised,Color.FromArgb(48,51,56),motion[0]),Color.FromArgb(68,72,79),5);
                TextRenderer.DrawText(e.Graphics,key,Font,chip,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                using (Font small=new Font("Segoe UI",9)) TextRenderer.DrawText(e.Graphics,"Modifier le raccourci",small,new Rectangle(chip.Right+12,0,Width-chip.Right-37,Height),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                using(Pen p=new Pen(Theme.Muted,1.5f)) e.Graphics.DrawLines(p,new[]{new Point(Width-23,Height/2-3),new Point(Width-20,Height/2),new Point(Width-23,Height/2+3)});
            }
            else TextRenderer.DrawText(e.Graphics,Text,Font,rect,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed class MouseChoice : CheckBox
    {
        private readonly Motion motion;
        public string Caption { get; set; }
        public bool RightSide { get; set; }
        public MouseChoice()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            Cursor=Cursors.Hand; BackColor=Theme.Background; Font=new Font("Segoe UI",10.5f,FontStyle.Bold);
            motion=new Motion(this,0,0,0);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
        protected override void OnMouseEnter(EventArgs e) { motion.To(0,1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { motion.To(0,0); motion.To(1,0); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left) motion.To(1,1); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { motion.To(1,0); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            float selected=motion[2]; float s=Height/66f;
            Color fill=Theme.Mix(Theme.Card,Theme.Raised,selected*.65f+motion[0]*.25f);
            Rectangle rect=ClientRectangle; int inset=(int)Math.Round(motion[1]); rect.Inflate(-inset,-inset);
            Theme.Surface(e.Graphics,rect,fill,Theme.Mix(Theme.Border,Theme.Muted,Focused?.85f:selected*.55f+motion[0]*.2f),10);
            e.Graphics.ScaleTransform(s,s);
            using (GraphicsPath mouse=Theme.Rounded(new RectangleF(15,20,21,29),9))
            {
                using (Brush b=new SolidBrush(Theme.Mix(Theme.Card,Color.FromArgb(62,66,72),selected))) e.Graphics.FillPath(b,mouse);
                GraphicsState state=e.Graphics.Save(); e.Graphics.SetClip(mouse);
                using (Brush b=new SolidBrush(Theme.Mix(Theme.Border,Theme.Text,selected))) e.Graphics.FillRectangle(b,RightSide?26:15,20,10,13);
                e.Graphics.Restore(state);
                using(Pen p=new Pen(Theme.Mix(Theme.Muted,Theme.Text,selected),1.2f)) e.Graphics.DrawPath(p,mouse);
            }
            using(Pen p=new Pen(fill,1.5f)) { e.Graphics.DrawLine(p,25.5f,20,25.5f,33); e.Graphics.DrawLine(p,15,33,36,33); }
            using(Brush b=new SolidBrush(Theme.Mix(Theme.Border,Theme.Text,selected))) e.Graphics.FillEllipse(b,Width/s-18,12,4,4);
            e.Graphics.ResetTransform();
            TextRenderer.DrawText(e.Graphics,Text,Font,new Point((int)(46*s),(int)(12*s)),Theme.Text);
            using(Font small=new Font("Segoe UI",8.5f)) TextRenderer.DrawText(e.Graphics,Caption,small,new Point((int)(46*s),(int)(37*s)),Theme.Muted);
        }
        protected override void OnCheckedChanged(EventArgs e) { if(motion!=null) motion.To(2,Checked?1:0); base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { motion.To(1,0); base.OnLostFocus(e); Invalidate(); }
    }

    internal sealed class ModeChoice : RadioButton
    {
        private readonly Motion motion;
        public ModeChoice()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            BackColor=Theme.Background; Cursor=Cursors.Hand; Font=new Font("Segoe UI",9.5f,FontStyle.Bold);
            motion=new Motion(this,0,0);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
        protected override void OnMouseEnter(EventArgs e) { motion.To(0,1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { motion.To(0,0); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            float active=motion[1];
            Theme.Surface(e.Graphics,ClientRectangle,Theme.Mix(Theme.Card,Theme.Raised,active*.8f+motion[0]*.2f),Theme.Mix(Theme.Border,Theme.Muted,Focused?.75f:active*.35f),8);
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Theme.Mix(Theme.Muted,Theme.Text,active),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            float half=6+18*active;
            using(Pen p=new Pen(Color.FromArgb((int)(active*180),Theme.Text),2)) { p.StartCap=p.EndCap=LineCap.Round; e.Graphics.DrawLine(p,Width/2f-half,Height-3,Width/2f+half,Height-3); }
        }
        protected override void OnCheckedChanged(EventArgs e) { if(motion!=null) motion.To(1,Checked?1:0); base.OnCheckedChanged(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }

    internal sealed class CpsSlider : Control
    {
        private int value=12;
        private readonly Motion motion;
        public event EventHandler ValueChanged;
        public int Value
        {
            get { return value; }
            set { int next=Math.Max(1,Math.Min(30,value)); if(this.value==next) return; this.value=next; motion.To(0,next); Invalidate(); if(ValueChanged!=null) ValueChanged(this,EventArgs.Empty); }
        }
        public CpsSlider()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
            TabStop=true; Cursor=Cursors.Hand; BackColor=Theme.Background; AccessibleRole=AccessibleRole.Slider;
            motion=new Motion(this,12,0);
        }
        protected override bool IsInputKey(Keys keyData) { return true; }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up) Value++;
            if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down) Value--;
            if(e.KeyCode==Keys.Home) Value=1;
            if(e.KeyCode==Keys.End) Value=30;
            base.OnKeyDown(e);
        }
        private void SetPosition(int x) { Value=1+(int)Math.Round((x-9.0)*29/Math.Max(1,Width-18)); }
        protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left) { Focus(); Capture=true; SetPosition(e.X); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if(Capture) SetPosition(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Capture=false; base.OnMouseUp(e); }
        protected override void OnMouseEnter(EventArgs e) { motion.To(1,1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { motion.To(1,0); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float x=9+(Width-18)*(motion[0]-1)/29f, y=Height/2f;
            using(Pen track=new Pen(Theme.Border,4)) using(Pen active=new Pen(Theme.Text,4))
            {
                track.StartCap=track.EndCap=active.StartCap=active.EndCap=LineCap.Round;
                e.Graphics.DrawLine(track,9,y,Width-9,y); e.Graphics.DrawLine(active,9,y,x,y);
            }
            float halo=9+motion[1]*3;
            using(Brush b=new SolidBrush(Color.FromArgb((int)(12+motion[1]*14),Theme.Text))) e.Graphics.FillEllipse(b,x-halo,y-halo,halo*2,halo*2);
            using(Brush b=new SolidBrush(Theme.Text)) e.Graphics.FillEllipse(b,x-6,y-6,12,12);
            using(Brush b=new SolidBrush(Theme.Raised)) e.Graphics.FillEllipse(b,x-2,y-2,4,4);
            if(Focused) using(Pen p=new Pen(Theme.Muted)) e.Graphics.DrawEllipse(p,x-10,y-10,20,20);
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
}
