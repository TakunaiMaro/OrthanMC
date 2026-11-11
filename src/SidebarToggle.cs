using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace McPanel
{
    // Boton compacto de la barra de titulo que esconde o muestra la barra
    // lateral. Se dibuja a mano para integrarse con el tema y para que el
    // icono cambie de direccion segun el estado (izquierda = esconder,
    // derecha = atraer).
    internal sealed class SidebarToggleButton : Control
    {
        private bool _hover;
        private bool _collapsed;

        public SidebarToggleButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = LanguageService.T("Esconder Barra lateral");
        }
        public bool Collapsed
        {
            get { return _collapsed; }
            set
            {
                if (_collapsed == value) return;
                _collapsed = value;
                AccessibleName = LanguageService.T(value ? "Mostrar Barra lateral" : "Esconder Barra lateral");
                Invalidate();
            }
        }

        private int Dp(int value)
        {
            return (int)Math.Round(value * DeviceDpi / 96f);
        }

        private float Dpf(float value)
        {
            return value * DeviceDpi / 96f;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Palette.TitleBar);

            Rectangle area = new Rectangle(Dp(1), Dp(1), Width - Dp(2), Height - Dp(2));
            Color fill = _hover
                ? Palette.Mix(Palette.TitleBar, Color.White, 0.10f)
                : Palette.Mix(Palette.TitleBar, Color.White, 0.035f);
            Color border = _hover
                ? Palette.Mix(Palette.Border, Color.White, 0.20f)
                : Palette.Border;

            Draw.FillRound(g, area, Dp(7), fill);
            Draw.StrokeRound(g, area, Dp(7), border, 1f);
            RectangleF panel = new RectangleF(
                area.X + Dp(6), area.Y + (area.Height - Dp(14)) / 2f,
                area.Width - Dp(12), Dp(14));
            Draw.FillRound(g, panel, Dp(3), Palette.Mix(Palette.TitleBar, Color.Black, 0.18f));
            Draw.StrokeRound(g, panel, Dp(3), Palette.Border, 1f);

            RectangleF strip = new RectangleF(
                panel.X + Dp(2), panel.Y + Dp(2), Dp(4), panel.Height - Dp(4));
            Draw.FillRound(g, strip, Dpf(1.2f), _collapsed ? Palette.Dim : Palette.Accent);

            float cx = panel.X + panel.Width * 0.66f;
            float cy = panel.Y + panel.Height / 2f;
            float size = Dp(3);
            PointF[] points = _collapsed
                ? new PointF[]
                {
                    new PointF(cx - size * 0.5f, cy - size),
                    new PointF(cx + size * 0.9f, cy),
                    new PointF(cx - size * 0.5f, cy + size)
                }
                : new PointF[]
                {
                    new PointF(cx + size * 0.9f, cy - size),
                    new PointF(cx - size * 0.5f, cy),
                    new PointF(cx + size * 0.9f, cy + size)
                };
                
            using (Pen pen = new Pen(_collapsed ? Palette.Muted : Palette.Text, Math.Max(1f, Dpf(1.4f))))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLines(pen, points);
            }

            if (Focused) ControlPaint.DrawFocusRectangle(g, area, Palette.Text, fill);
        }
    }
}
