using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace McPanel
{
    public class Particle
    {
        public float X,Y,VX,VY,Life,MaxLife,Size,Rot,VR;
        public Color Color;
    }

    public class ParticleLayer : Panel
    {
        private List<Particle> _parts = new List<Particle>();
        private System.Windows.Forms.Timer _timer;
        private Random _rnd = new Random();
        private AppTheme _theme = AppTheme.Default;

        public ParticleLayer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 16;
            _timer.Tick += Tick;
            _timer.Start();
            ThemeManager.ThemeChanged += delegate { _theme = ThemeManager.Current; };
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            for (int i = 0; i < 4; i++) Spawn(e.X, e.Y);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Point p = PointToClient(Cursor.Position);
            for (int i = 0; i < 10; i++) Spawn(p.X, p.Y);
        }

        private void Spawn(float x, float y)
        {
            if (_theme == AppTheme.Default) return;
            Particle pr = new Particle();
            pr.X = x; pr.Y = y;
            pr.VX = (float)(_rnd.NextDouble() - 0.5) * 2.2f;
            pr.VY = (float)(_rnd.NextDouble() - 0.8) * 2.0f;
            pr.MaxLife = 18 + _rnd.Next(14);
            pr.Life = pr.MaxLife;
            pr.Size = 2f + (float)_rnd.NextDouble() * 2.6f;
            pr.Rot = (float)_rnd.NextDouble() * 360f;
            pr.VR = (float)(_rnd.NextDouble() - 0.5) * 6f;
            if (_theme == AppTheme.Overworld) pr.Color = Color.FromArgb(160, 100, 255, 120);
            else if (_theme == AppTheme.Nether) pr.Color = Color.FromArgb(190, 255, 90, 40);
            else if (_theme == AppTheme.End) pr.Color = Color.FromArgb(200, 170, 120, 255);
            else pr.Color = Color.FromArgb(150, 150, 200, 255);
            lock (_parts) _parts.Add(pr);
        }

        private void Tick(object sender, EventArgs e)
        {
            lock (_parts)
            {
                for (int i = _parts.Count - 1; i >= 0; i--)
                {
                    Particle pr = _parts[i];
                    pr.X += pr.VX;
                    pr.Y += pr.VY;
                    pr.VY += 0.02f;
                    pr.Life -= 1;
                    pr.Rot += pr.VR;
                    if (pr.Life <= 0) _parts.RemoveAt(i);
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            lock (_parts)
            {
                for (int i = 0; i < _parts.Count; i++)
                {
                    Particle pr = _parts[i];
                    float alpha = pr.Life / pr.MaxLife;
                    Color c = Color.FromArgb((int)(alpha * pr.Color.A), pr.Color.R, pr.Color.G, pr.Color.B);
                    using (SolidBrush b = new SolidBrush(c))
                    {
                        g.TranslateTransform(pr.X, pr.Y);
                        g.RotateTransform(pr.Rot);
                        g.FillEllipse(b, -pr.Size/2, -pr.Size/2, pr.Size, pr.Size);
                        g.ResetTransform();
                    }
                }
            }
        }
    }

    public static class ThemeApplier
    {
        private static readonly HashSet<Form> _titleHooked = new HashSet<Form>();

        public static void ApplyTo(Form form)
        {
            Color[] oldSurface = SurfaceSnapshot();
            Palette.ApplyTheme(ThemeManager.Current);
            Color[] newSurface = SurfaceSnapshot();
            ApplyForm(form, oldSurface, newSurface);
        }

        // ThemeManager avisa una sola vez; aqui se cambia la paleta y se
        // repintan todas las ventanas abiertas (principal, temas y dialogos).
        public static void ApplyToOpenForms()
        {
            Color[] oldSurface = SurfaceSnapshot();
            Palette.ApplyTheme(ThemeManager.Current);
            Color[] newSurface = SurfaceSnapshot();
            foreach (Form form in Application.OpenForms)
            {
                if (form == null || form.IsDisposed) continue;
                ApplyForm(form, oldSurface, newSurface);
            }
        }

        private static Color[] SurfaceSnapshot()
        {
            return new Color[]
            {
                Palette.Bg, Palette.Sidebar, Palette.Card, Palette.CardAlt,
                Palette.Input, Palette.Border, Palette.BorderSoft,
                Palette.TitleBar, Palette.Accent, Palette.AccentHover,
                Palette.AccentDim
            };
        }

        private static void ApplyForm(Form form, Color[] oldSurface, Color[] newSurface)
        {
            try
            {
                form.BackColor = Remap(form.BackColor, oldSurface, newSurface);
                ApplyRecursive(form, oldSurface, newSurface);
                ApplyTitleBar(form);
                form.Invalidate(true);
                if (form.IsHandleCreated) form.Update();
            }
            catch { }
        }

        // El color de la barra de titulo se fija por handle nativo, por eso
        // se reaplica al cambiar de tema y tambien cuando la ventana recien
        // crea su handle (por ejemplo, un dialogo abierto despues).
        private static void ApplyTitleBar(Form form)
        {
            if (form.IsHandleCreated)
            {
                WindowFx.DarkTitleBar(form.Handle);
                return;
            }
            if (_titleHooked.Add(form))
            {
                form.HandleCreated += delegate { WindowFx.DarkTitleBar(form.Handle); };
                form.FormClosed += delegate { _titleHooked.Remove(form); };
            }
        }

        // Recorre todo el arbol de controles y remapea los fondos que venian
        // de la paleta anterior. Tambien refresca los colores cacheados de
        // botones y tarjetas para que no queden parches del tema default.
        private static void ApplyRecursive(Control parent, Color[] oldSurface, Color[] newSurface)
        {
            foreach (Control c in parent.Controls)
            {
                Color mapped = Remap(c.BackColor, oldSurface, newSurface);
                if (mapped.ToArgb() != c.BackColor.ToArgb()) c.BackColor = mapped;

                Color mappedFore = Remap(c.ForeColor, oldSurface, newSurface);
                if (mappedFore.ToArgb() != c.ForeColor.ToArgb()) c.ForeColor = mappedFore;

                if (c is Card)
                {
                    ((Card)c).BorderColor = Palette.Border;
                }
                else if (c is RoundButton)
                {
                    RoundButton button = (RoundButton)c;
                    Color oldFill = button.FillColor;
                    Color oldHover = button.HoverFill;
                    Color oldPress = button.PressFill;
                    Color newFill = Remap(oldFill, oldSurface, newSurface);
                    Color newHover = Remap(oldHover, oldSurface, newSurface);
                    Color newPress = Remap(oldPress, oldSurface, newSurface);
                    if (newFill.ToArgb() != oldFill.ToArgb())
                    {
                        button.FillColor = newFill;
                        button.HoverFill = newHover.ToArgb() != oldHover.ToArgb()
                            ? newHover : ShiftColor(oldHover, oldFill, newFill);
                        button.PressFill = newPress.ToArgb() != oldPress.ToArgb()
                            ? newPress : ShiftColor(oldPress, oldFill, newFill);
                    }
                    button.BorderColor = Remap(button.BorderColor, oldSurface, newSurface);
                }
                else if (c is UsageBar)
                {
                    UsageBar bar = (UsageBar)c;
                    bar.BarColor = Remap(bar.BarColor, oldSurface, newSurface);
                }
                else if (c is ToggleSwitch)
                {
                    ToggleSwitch toggle = (ToggleSwitch)c;
                    toggle.OnColor = Remap(toggle.OnColor, oldSurface, newSurface);
                }
                else if (c is StatusBadge)
                {
                    StatusBadge badge = (StatusBadge)c;
                    badge.DotColor = Remap(badge.DotColor, oldSurface, newSurface);
                }

                c.Invalidate();
                if (c.HasChildren) ApplyRecursive(c, oldSurface, newSurface);
            }
        }

        private static Color Remap(Color color, Color[] oldSurface, Color[] newSurface)
        {
            if (color.A != 255) return color;
            int argb = color.ToArgb();
            for (int i = 0; i < oldSurface.Length; i++)
            {
                if (oldSurface[i].ToArgb() == argb) return newSurface[i];
            }
            return color;
        }

        private static Color ShiftColor(Color color, Color oldFrom, Color newFrom)
        {
            int r = color.R + (newFrom.R - oldFrom.R);
            int g = color.G + (newFrom.G - oldFrom.G);
            int b = color.B + (newFrom.B - oldFrom.B);
            if (r < 0) r = 0; else if (r > 255) r = 255;
            if (g < 0) g = 0; else if (g > 255) g = 255;
            if (b < 0) b = 0; else if (b > 255) b = 255;
            return Color.FromArgb(color.A, r, g, b);
        }
    }
}
