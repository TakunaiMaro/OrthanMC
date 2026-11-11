#nullable disable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace McPanel
{
    // Barra de desplazamiento dibujada a mano, con el estilo de la referencia:
    // un riel oscuro y alargado con un pulgar claro y tres lineas de agarre.
    // Se usa tanto en las paginas con AutoScroll como en la grilla de perfiles.
    internal sealed class SlimScrollBar : Control
    {
        private int _maxScroll;
        private int _viewport = 1;
        private int _value;
        private bool _hover;
        private bool _dragging;
        private int _dragOffset;
        private readonly Func<Color> _surface;

        public SlimScrollBar(Func<Color> surface)
        {
            _surface = surface;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            TabStop = false;
            Cursor = Cursors.Hand;
            AccessibleName = "Barra de desplazamiento";
            Width = 20;
        }

        public int Value { get { return _value; } }
        public int MaxScroll { get { return _maxScroll; } }
        public event EventHandler ValueChanged;

        public void SetRange(int maxScroll, int viewport)
        {
            _maxScroll = Math.Max(0, maxScroll);
            _viewport = Math.Max(1, viewport);
            if (_value > _maxScroll) _value = _maxScroll;
            if (_value < 0) _value = 0;
            Invalidate();
        }

        public void SetValue(int value, bool silent)
        {
            value = Math.Max(0, Math.Min(_maxScroll, value));
            if (value == _value) return;
            _value = value;
            Invalidate();
            if (!silent && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        private Rectangle TrackRectangle()
        {
            int trackWidth = Math.Max(4, (int)Math.Round(Width * 0.42f));
            int x = (Width - trackWidth) / 2;
            int top = 3;
            int height = Math.Max(4, Height - 6);
            return new Rectangle(x, top, trackWidth, height);
        }

        private Rectangle ThumbRectangle()
        {
            Rectangle track = TrackRectangle();
            int minHeight = Math.Max((int)Math.Round(Width * 2.3f), 40);
            float ratio = (float)_viewport / Math.Max(1f, _viewport + _maxScroll);
            int thumbHeight = (int)Math.Round(track.Height * ratio);
            if (thumbHeight < minHeight) thumbHeight = minHeight;
            if (thumbHeight > track.Height) thumbHeight = track.Height;

            float fraction = _maxScroll <= 0 ? 0f : (float)_value / _maxScroll;
            int available = track.Height - thumbHeight;
            int top = track.Top + (int)Math.Round(available * fraction);
            return new Rectangle(1, top, Math.Max(4, Width - 2), thumbHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color surface = _surface != null ? _surface() : Palette.Bg;
            g.Clear(surface);
            if (_maxScroll <= 0 || Width < 4 || Height < 8) return;

            Rectangle track = TrackRectangle();
            Color trackColor = Palette.Mix(surface, Color.White, _hover ? 0.17f : 0.10f);
            Draw.FillRound(g, track, track.Width / 2f, trackColor);

            Rectangle thumb = ThumbRectangle();
            Color thumbColor = _dragging
                ? Palette.Text
                : (_hover ? Palette.Mix(Palette.Text, Color.White, 0.10f)
                          : Palette.Mix(Palette.Text, surface, 0.20f));
            Draw.FillRound(g, thumb, thumb.Width / 2f, thumbColor);

            // Tres lineas de agarre, solo si el pulgar tiene altura suficiente.
            if (thumb.Height >= Width * 1.7f)
            {
                float lineWidth = Math.Max(6f, Width * 0.40f);
                float cx = thumb.X + thumb.Width / 2f;
                float cy = thumb.Y + thumb.Height / 2f;
                float gap = Math.Max(2.5f, Width * 0.15f);
                using (Pen pen = new Pen(Palette.Mix(thumbColor, surface, 0.68f), Math.Max(1.2f, Width * 0.075f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    for (int i = -1; i <= 1; i++)
                        g.DrawLine(pen, cx - lineWidth / 2f, cy + i * gap, cx + lineWidth / 2f, cy + i * gap);
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; base.OnMouseLeave(e); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Rectangle thumb = ThumbRectangle();
            if (thumb.Contains(e.Location))
            {
                _dragging = true;
                _dragOffset = e.Y - thumb.Top;
            }
            else
            {
                _dragging = true;
                _dragOffset = thumb.Height / 2;
                SetValueFromThumbTop(e.Y - _dragOffset);
            }
            Capture = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) SetValueFromThumbTop(e.Y - _dragOffset);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            Capture = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int step = Math.Max(30, _viewport / 6);
            SetValue(_value - Math.Sign(e.Delta) * step, false);
            base.OnMouseWheel(e);
        }

        private void SetValueFromThumbTop(int top)
        {
            Rectangle track = TrackRectangle();
            int thumbHeight = ThumbRectangle().Height;
            int available = track.Height - thumbHeight;
            if (available <= 0) { SetValue(0, false); return; }
            float fraction = (top - track.Top) / (float)available;
            if (fraction < 0f) fraction = 0f;
            if (fraction > 1f) fraction = 1f;
            SetValue((int)Math.Round(fraction * _maxScroll), false);
        }
    }

    // Conecta una barra superpuesta con un contenedor que usa AutoScroll.
    // La barra vive en el formulario para que el scroll del contenedor no la
    // mueva, y se reposiciona cuando cambia el layout de la pagina.
    internal sealed class ScrollBarBinder : IDisposable
    {
        private const int SB_VERT = 1;
        private readonly ScrollableControl _host;
        private readonly Form _form;
        private readonly SlimScrollBar _bar;
        private bool _syncing;
        private bool _disposed;
        private bool _active;

        public static ScrollBarBinder Attach(ScrollableControl host, Func<Color> surface)
        {
            if (host == null) return null;
            Form form = host.FindForm();
            if (form == null) return null;
            return new ScrollBarBinder(host, form, surface);
        }

        private ScrollBarBinder(ScrollableControl host, Form form, Func<Color> surface)
        {
            _host = host;
            _form = form;
            _bar = new SlimScrollBar(surface ?? (() => Palette.Bg));
            _bar.Width = 20;
            _bar.Visible = false;
            _form.Controls.Add(_bar);
            _bar.ValueChanged += delegate { ApplyValue(); };
            _host.HandleCreated += delegate { Sync(); };
            _host.Layout += delegate { Sync(); };
            _host.Scroll += delegate { Sync(); };
            _host.Resize += delegate { Sync(); };
            _host.VisibleChanged += delegate { Sync(); };
            _host.MouseWheel += delegate { Sync(); };
            _host.LocationChanged += delegate { PositionBar(); };
            _form.Resize += delegate { PositionBar(); };
            _form.Shown += delegate { Sync(); };
        }

        public SlimScrollBar Bar { get { return _bar; } }

        // Permite apagar la barra cuando su vista deja de estar activa
        // (por ejemplo al volver a Perfiles). Visible solo no basta: los
        // paneles ocultos no siempre disparan VisibleChanged.
        public void SetActive(bool active)
        {
            _active = active;
            Sync();
        }

        public void Sync()
        {
            if (_disposed || _host == null || _host.IsDisposed) return;
            HideNativeBar();
            int maximum = _host.VerticalScroll.Maximum;
            int large = _host.VerticalScroll.LargeChange;
            int maxScroll = Math.Max(0, maximum - large + 1);
            _bar.SetRange(maxScroll, Math.Max(1, large));
            _bar.SetValue(Math.Abs(_host.AutoScrollPosition.Y), true);

            bool visible = _active && maxScroll > 0 && _host.Visible && _form.Visible &&
                _host.Width > 40 && _host.Height > 40;
            if (_bar.Visible != visible) _bar.Visible = visible;
            if (visible)
            {
                PositionBar();
            }
        }

        private void PositionBar()
        {
            if (_disposed || _bar == null || _host == null || _host.IsDisposed) return;
            if (!_bar.Visible) return;
            try
            {
                Point origin = _host.PointToScreen(Point.Empty);
                origin = _form.PointToClient(origin);
                int height = Math.Max(8, _host.Height - 4);
                _bar.SetBounds(origin.X + _host.Width - _bar.Width - 4, origin.Y + 2, _bar.Width, height);
            }
            catch { }
        }

        private void ApplyValue()
        {
            if (_syncing || _disposed) return;
            _syncing = true;
            try
            {
                _host.AutoScrollPosition = new Point(0, _bar.Value);
                HideNativeBar();
            }
            finally
            {
                _syncing = false;
            }
        }

        private void HideNativeBar()
        {
            try
            {
                if (_host != null && _host.IsHandleCreated)
                    ShowScrollBar(_host.Handle, SB_VERT, false);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_bar != null && _bar.Parent != null) _bar.Parent.Controls.Remove(_bar);
                if (_bar != null) _bar.Dispose();
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
    }
}
