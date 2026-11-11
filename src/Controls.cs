using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace McPanel
{
    // Panel con doble buffer para contenedores que se repintan durante
    // animaciones (por ejemplo la barra de titulo). Evita el parpadeo de
    // los controles hijos al cambiar tamanos rapidamente.
    internal sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }
    }

    internal sealed class Card : Panel
    {
        private int _radius = 14;
        private Color _border = Palette.Border;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Palette.Card;
            Padding = new Padding(18);
            // Do not use Region clipping to avoid cutting overlapping rounded containers
        }

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        public Color BorderColor
        {
            get { return _border; }
            set { _border = value; Invalidate(); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            // Limpiar tambien las esquinas: si solo se pinta el path redondeado,
            // quedan restos del tema anterior en forma de triangulos.
            e.Graphics.Clear(Palette.Backdrop(this, Palette.Bg));
            RectangleF r = new RectangleF(0f, 0f, Width - 1f, Height - 1f);
            Draw.FillRound(e.Graphics, r, _radius, BackColor);
            if (_border.A > 0)
            {
                Draw.StrokeRound(e.Graphics, new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), _radius, _border, 1f);
            }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
        }
    }

    internal sealed class RoundButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private Color _fill = Palette.Accent;
        private Color _hoverFill = Palette.AccentHover;
        private Color _pressFill = ColorTranslator.FromHtml("#2E9A40");
        private Color _textColor = ColorTranslator.FromHtml("#06120A");
        private Color _borderColor = Color.Transparent;
        private int _radius = 9;
        private string _glyph = "";
        private float _glyphStroke = 1.8f;

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Color.Transparent;
            Font = Fonts.UI(9.5f, FontStyle.Bold);
            Height = 38;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public Color FillColor { get { return _fill; } set { _fill = value; Invalidate(); } }
        public Color HoverFill { get { return _hoverFill; } set { _hoverFill = value; Invalidate(); } }
        public Color PressFill { get { return _pressFill; } set { _pressFill = value; Invalidate(); } }
        public Color TextColor { get { return _textColor; } set { _textColor = value; Invalidate(); } }
        public Color BorderColor { get { return _borderColor; } set { _borderColor = value; Invalidate(); } }
        public int Radius { get { return _radius; } set { _radius = value; Invalidate(); } }
        public string Glyph { get { return _glyph; } set { _glyph = value; Invalidate(); } }
        public float GlyphStroke { get { return _glyphStroke; } set { _glyphStroke = value; Invalidate(); } }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs mevent) { if (mevent.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(mevent); }
        protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parent = Palette.Backdrop(this, Palette.Bg);
            g.Clear(parent);

            Color fill;
            Color text;
            if (!Enabled)
            {
                fill = Palette.Mix(_fill, parent, 0.62f);
                text = Palette.Dim;
            }
            else if (_pressed) { fill = _pressFill; text = _textColor; }
            else if (_hover) { fill = _hoverFill; text = _textColor; }
            else { fill = _fill; text = _textColor; }

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Draw.FillRound(g, r, _radius, fill);
            if (_borderColor.A > 0)
            {
                Draw.StrokeRound(g, r, _radius, _borderColor, 1f);
            }

            bool hasText = !string.IsNullOrEmpty(Text);
            bool hasGlyph = !string.IsNullOrEmpty(_glyph);
            Size textSize = hasText ? TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix) : Size.Empty;
            float glyphSize = 17f;
            float gap = (hasText && hasGlyph) ? 8f : 0f;
            float total = (hasGlyph ? glyphSize : 0f) + gap + textSize.Width;
            float startX = (Width - total) / 2f;
            float cy = Height / 2f;

            if (hasGlyph)
            {
                Draw.Glyph(g, _glyph, new RectangleF(startX, cy - glyphSize / 2f, glyphSize, glyphSize), text, _glyphStroke);
                startX += glyphSize + gap;
            }
            if (hasText)
            {
                RectangleF tr = new RectangleF(startX, 0f, textSize.Width + 2f, Height);
                TextRenderer.DrawText(g, Text, Font, Rectangle.Round(tr), text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }
    }

    internal sealed class UsageBar : Control
    {
        private double _value;
        private Color _barColor = Palette.Accent;

        public UsageBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Height = 8;
            BackColor = Color.Transparent;
        }

        public double Value
        {
            get { return _value; }
            set
            {
                double v = value;
                if (v < 0) v = 0;
                if (v > 1) v = 1;
                if (Math.Abs(v - _value) > 0.0001)
                {
                    _value = v;
                    Invalidate();
                }
            }
        }

        public Color BarColor { get { return _barColor; } set { _barColor = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parent = Palette.Backdrop(this, Palette.Card);
            g.Clear(parent);
            float radius = Height / 2f;
            RectangleF track = new RectangleF(0, 0, Width, Height);
            Draw.FillRound(g, track, radius, Palette.Mix(parent, Color.Black, 0.22f));
            if (_value > 0.001)
            {
                float w = (float)(Width * _value);
                if (w < Height) w = Height;
                RectangleF fill = new RectangleF(0, 0, w, Height);
                using (LinearGradientBrush b = new LinearGradientBrush(fill, Palette.Mix(_barColor, Color.Black, 0.22f), _barColor, LinearGradientMode.Horizontal))
                {
                    using (GraphicsPath p = Draw.Round(fill, radius)) g.FillPath(b, p);
                }
            }
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private bool _checked;
        private Color _onColor = Palette.Accent;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(44, 24);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public Color OnColor { get { return _onColor; } set { _onColor = value; Invalidate(); } }
        public event EventHandler CheckedChanged;

        protected override void OnClick(EventArgs e)
        {
            if (!Enabled) return;
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parent = Palette.Backdrop(this, Palette.Card);
            g.Clear(parent);
            float h = Math.Min(Height, 24f);
            float w = Math.Min(Width, 44f);
            RectangleF track = new RectangleF((Width - w) / 2f, (Height - h) / 2f, w, h);
            Color off = Palette.Mix(parent, Color.White, 0.14f);
            Color trackColor = _checked ? (Enabled ? _onColor : Palette.Mix(_onColor, parent, 0.55f)) : off;
            Draw.FillRound(g, track, h / 2f, trackColor);
            float knob = h - 6f;
            float kx = _checked ? track.Right - knob - 3f : track.X + 3f;
            RectangleF kr = new RectangleF(kx, track.Y + 3f, knob, knob);
            using (SolidBrush b = new SolidBrush(Enabled ? Palette.White : Palette.Dim)) g.FillEllipse(b, kr);
            if (Focused) Draw.StrokeRound(g, track, h / 2f, Palette.Alpha(Palette.White, 60), 1f);
        }
    }

    internal sealed class NavButton : Control
    {
        private bool _hover;
        private bool _active;
        private string _glyph = "pulse";

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Height = 46;
            Cursor = Cursors.Hand;
            Font = Fonts.UI(10f, FontStyle.Regular);
            ForeColor = Palette.Muted;
            BackColor = Color.Transparent;
            TabStop = true;
        }

        public bool Active { get { return _active; } set { _active = value; Invalidate(); } }
        public string Glyph { get { return _glyph; } set { _glyph = value; Invalidate(); } }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parent = Palette.Backdrop(this, Palette.Sidebar);
            g.Clear(parent);

            if (_active || _hover)
            {
                RectangleF bg = new RectangleF(8f, 4f, Width - 16f, Height - 8f);
                Draw.FillRound(g, bg, 9f, _active ? Palette.Card : Palette.Mix(parent, Color.White, 0.045f));
            }
            if (_active)
            {
                RectangleF bar = new RectangleF(0f, (Height - 22f) / 2f, 3f, 22f);
                Draw.FillRound(g, bar, 1.5f, Palette.Accent);
            }

            Color fg = _active ? Palette.Text : (_hover ? Palette.Text : Palette.Muted);
            RectangleF gr = new RectangleF(22f, (Height - 19f) / 2f, 19f, 19f);
            Draw.Glyph(g, _glyph, gr, _active ? Palette.Accent : fg, 1.7f);
            RectangleF tr = new RectangleF(52f, 0f, Width - 60f, Height);
            TextRenderer.DrawText(g, Text, Font, Rectangle.Round(tr), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
            {
                RectangleF fr = new RectangleF(8f, 4f, Width - 16f, Height - 8f);
                Draw.StrokeRound(g, fr, 9f, Palette.Alpha(Palette.Accent, 110), 1f);
            }
        }
    }

    internal sealed class StatusBadge : Control
    {
        private Color _dot = Palette.Muted;
        private string _label = "Detenido";
        private string _raw = "DETENIDO";
        private bool _autoSizeToText = true;

        public StatusBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Height = 26;
            Width = 110;
            Font = Fonts.UI(9f, FontStyle.Bold);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        public Color DotColor { get { return _dot; } set { _dot = value; Invalidate(); } }
        public bool AutoSizeToText { get { return _autoSizeToText; } set { _autoSizeToText = value; } }

        public void Set(string text, Color dot)
        {
            _raw = text;
            _label = LanguageService.T(text);
            _dot = dot;
            if (_autoSizeToText)
            {
                Size ts = TextRenderer.MeasureText(_label, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                Width = ts.Width + 34;
            }
            Invalidate();
        }

        // Vuelve a traducir el ultimo texto recibido. Se usa al cambiar de
        // idioma sin esperar al siguiente refresco de estado.
        public void RefreshLanguage()
        {
            Set(_raw, _dot);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            _label = Text;
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parent = Palette.Backdrop(this, Palette.Card);
            g.Clear(parent);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color fill = Palette.Mix(parent, _dot, 0.16f);
            Color border = Palette.Mix(parent, _dot, 0.38f);
            Draw.FillRound(g, r, Height / 2f, fill);
            Draw.StrokeRound(g, r, Height / 2f, border, 1f);
            float cy = Height / 2f;
            using (SolidBrush b = new SolidBrush(_dot)) g.FillEllipse(b, 11f, cy - 3.5f, 7f, 7f);
            RectangleF tr = new RectangleF(24f, 0f, Width - 32f, Height);
            TextRenderer.DrawText(g, _label, Font, Rectangle.Round(tr), _dot,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    internal sealed class SliderBar : Control
    {
        private int _min;
        private int _max = 100;
        private int _value = 50;
        private int _step = 1;
        private bool _dragging;

        public SliderBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Height = 30;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public int Min { get { return _min; } set { _min = value; if (_max <= _min) _max = _min + 1; Value = _value; Invalidate(); } }
        public int Max { get { return _max; } set { _max = value; if (_max <= _min) _max = _min + 1; Value = _value; Invalidate(); } }
        public int Step { get { return _step; } set { _step = Math.Max(1, value); } }
        public int Value
        {
            get { return _value; }
            set
            {
                int v = value;
                if (v < _min) v = _min;
                if (v > _max) v = _max;
                if (_step > 1)
                {
                    int rel = v - _min;
                    v = _min + (int)Math.Round((double)rel / _step) * _step;
                    if (v > _max) v = _max;
                }
                if (v != _value)
                {
                    _value = v;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler ValueChanged;

        private void SetFromX(int x)
        {
            float left = 8f;
            float right = Width - 8f;
            float ratio = (x - left) / Math.Max(1f, right - left);
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;
            Value = _min + (int)Math.Round(ratio * (_max - _min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                Focus();
                SetFromX(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) { Value -= _step; e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { Value += _step; e.Handled = true; }
            else if (e.KeyCode == Keys.Home) { Value = _min; e.Handled = true; }
            else if (e.KeyCode == Keys.End) { Value = _max; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parent = Palette.Backdrop(this, Palette.Card);
            g.Clear(parent);
            float left = 8f;
            float right = Width - 8f;
            float cy = Height / 2f;
            RectangleF track = new RectangleF(left, cy - 3f, Math.Max(1f, right - left), 6f);
            Draw.FillRound(g, track, 3f, Palette.Mix(parent, Color.Black, 0.28f));
            float ratio = _max > _min ? (float)(_value - _min) / (_max - _min) : 0f;
            float x = left + (right - left) * ratio;
            RectangleF fill = new RectangleF(left, cy - 3f, Math.Max(0f, x - left), 6f);
            if (fill.Width > 0.5f) Draw.FillRound(g, fill, 3f, Palette.Accent);
            RectangleF knob = new RectangleF(x - 8f, cy - 8f, 16f, 16f);
            using (SolidBrush b = new SolidBrush(Palette.White)) g.FillEllipse(b, knob);
            using (Pen p = new Pen(Palette.Accent, 2f)) g.DrawEllipse(p, knob);
            if (Focused)
            {
                using (Pen p = new Pen(Palette.Alpha(Palette.Accent, 90), 1f))
                    g.DrawEllipse(p, knob.X - 3f, knob.Y - 3f, knob.Width + 6f, knob.Height + 6f);
            }
        }
    }

    // Selector dibujado a mano: el ComboBox nativo ignora el tema oscuro
    // (borde e items blancos), asi que el desplegable se hace con un
    // ContextMenuStrip oscuro y el campo se pinta como el resto del HUD.
    internal sealed class SelectBox : Control
    {
        private string[] _items;
        private int _index = -1;
        private bool _hover;
        private ContextMenuStrip _menu;

        public SelectBox(string[] items, string current)
        {
            _items = items ?? new string[0];
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Palette.Input;
            ForeColor = Palette.Text;
            Font = Fonts.UI(9.5f);
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = 26;
            _index = Array.IndexOf(_items, current);
            if (_index < 0 && _items.Length > 0) _index = 0;
        }

        public string SelectedItem
        {
            get { return _index >= 0 && _index < _items.Length ? _items[_index] : ""; }
        }

        public string[] Items
        {
            get { return (string[])_items.Clone(); }
        }

        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                if (value < -1 || value >= _items.Length || value == _index) return;
                _index = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler SelectedIndexChanged;

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        public void SetItems(string[] items, string current)
        {
            _items = items ?? new string[0];
            _index = Array.IndexOf(_items, current);
            if (_index < 0 && _items.Length > 0) _index = 0;
            Invalidate();
            if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnClick(EventArgs e)
        {
            if (!Enabled) return;
            Focus();
            ShowOptions();
            base.OnClick(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            // Las flechas son teclas de navegacion para WinForms; sin esto
            // nunca llegan a OnKeyDown y el desplegable no se abre.
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Up || key == Keys.Down)
                return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space ||
                e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                ShowOptions();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void ShowOptions()
        {
            if (_items.Length == 0) return;
            if (_menu == null || _menu.IsDisposed)
            {
                _menu = new ContextMenuStrip();
                _menu.Font = Fonts.UI(9.5f);
                _menu.ShowImageMargin = false;
                _menu.ShowCheckMargin = false;
                _menu.Padding = new Padding(4);
            }
            if (_menu.Visible) _menu.Close();
            // Se actualizan en cada apertura para que el menu siga el tema
            // aunque ya se haya abierto antes de cambiarlo.
            _menu.BackColor = Palette.Card;
            _menu.ForeColor = Palette.Text;
            _menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColours());
            _menu.Items.Clear();
            for (int i = 0; i < _items.Length; i++)
            {
                int idx = i;
                ToolStripMenuItem item = new ToolStripMenuItem(_items[i]);
                item.BackColor = Palette.Card;
                item.ForeColor = Palette.Text;
                item.Padding = new Padding(8, 4, 8, 4);
                if (i == _index) item.Font = Fonts.UI(9.5f, FontStyle.Bold);
                item.Click += delegate { SelectedIndex = idx; };
                _menu.Items.Add(item);
            }
            _menu.Show(this, new Point(0, Height + 2));
            if (_index >= 0 && _index < _menu.Items.Count)
            {
                _menu.Items[_index].Select();
                _menu.Focus();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _menu != null)
            {
                _menu.Dispose();
                _menu = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Palette.Backdrop(this, Palette.Card));
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 2f, Height - 2f);
            Draw.FillRound(g, r, 9f, Palette.Input);
            Draw.StrokeRound(g, r, 9f, Enabled ? ((_hover || Focused) ? Palette.Accent : Palette.Border) : Palette.Border, 1f);
            TextRenderer.DrawText(g, SelectedItem, Font, new Rectangle(10, 0, Math.Max(10, Width - 34), Height),
                Enabled ? Palette.Text : Palette.Dim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            float cy = Height / 2f;
            using (Pen p = new Pen(Enabled ? Palette.Muted : Palette.Dim, 1.6f))
            {
                g.DrawLines(p, new PointF[]
                {
                    new PointF(Width - 21f, cy - 2f),
                    new PointF(Width - 17f, cy + 2.5f),
                    new PointF(Width - 13f, cy - 2f)
                });
            }
        }

        private sealed class DarkMenuColours : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Palette.Card; } }
            public override Color ImageMarginGradientBegin { get { return Palette.Card; } }
            public override Color ImageMarginGradientMiddle { get { return Palette.Card; } }
            public override Color ImageMarginGradientEnd { get { return Palette.Card; } }
            public override Color MenuBorder { get { return Palette.Border; } }
            public override Color MenuItemBorder { get { return Palette.AccentDim; } }
            public override Color MenuItemSelected { get { return Palette.AccentDim; } }
            public override Color MenuItemSelectedGradientBegin { get { return Palette.AccentDim; } }
            public override Color MenuItemSelectedGradientEnd { get { return Palette.AccentDim; } }
            public override Color MenuItemPressedGradientBegin { get { return Palette.Card; } }
            public override Color MenuItemPressedGradientEnd { get { return Palette.Card; } }
            public override Color SeparatorDark { get { return Palette.Border; } }
            public override Color SeparatorLight { get { return Palette.Border; } }
        }
    }

    internal sealed class ConfigEntry : Control
    {
        private bool _hover;
        private string _title = "";
        private string _description = "";
        private string _glyph = "sliders";
        private bool _disabled;

        public ConfigEntry()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            Height = 92;
            Cursor = Cursors.Hand;
            TabStop = true;
            BackColor = Color.Transparent;
        }

        public string Title
        {
            get { return _title; }
            set { _title = value ?? ""; AccessibleName = _title; Invalidate(); }
        }

        public string Description
        {
            get { return _description; }
            set { _description = value ?? ""; Invalidate(); }
        }

        public string Glyph
        {
            get { return _glyph; }
            set { _glyph = value ?? ""; Invalidate(); }
        }

        // Una entrada deshabilitada se dibuja atenuada, muestra "PROXIMAMENTE"
        // y no responde al raton ni al teclado (pero no se oculta del hub).
        public bool Disabled
        {
            get { return _disabled; }
            set
            {
                _disabled = value;
                Cursor = value ? Cursors.Default : Cursors.Hand;
                TabStop = !value;
                _hover = false;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (_disabled) return;
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_disabled) return;
            base.OnMouseDown(e);
        }

        protected override void OnClick(EventArgs e)
        {
            if (_disabled) return;
            base.OnClick(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!_disabled && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
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
            g.Clear(Palette.Backdrop(this, Palette.Bg));

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color fill = _disabled
                ? Palette.Mix(Palette.Card, Palette.Bg, 0.35f)
                : (_hover ? Palette.Mix(Palette.Card, Color.White, 0.045f) : Palette.Card);
            Draw.FillRound(g, r, 12f, fill);
            Color border = _disabled ? Palette.BorderSoft : (_hover ? Palette.Accent : Palette.Border);
            Draw.StrokeRound(g, r, 12f, border, 1f);

            RectangleF icon = new RectangleF(18f, (Height - 42f) / 2f, 42f, 42f);
            Draw.FillRound(g, icon, 10f, _disabled ? Palette.Mix(Palette.CardAlt, Palette.Bg, 0.3f) : Palette.AccentDim);
            Draw.Glyph(g, _glyph, new RectangleF(icon.X + 11f, icon.Y + 11f, 20f, 20f),
                _disabled ? Palette.Dim : Palette.Accent, 1.8f);

            Color titleColor = _disabled ? Palette.Dim : Palette.Text;
            Color descColor = _disabled ? Palette.Dim : Palette.Muted;

            int textLeft = 76;
            int textWidth = Math.Max(40, Width - textLeft - 52);
            TextRenderer.DrawText(g, _title, Fonts.UI(10.5f, FontStyle.Bold),
                new Rectangle(textLeft, 18, textWidth, 22), titleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, _description, Fonts.UI(8.75f),
                new Rectangle(textLeft, 43, textWidth, 34), descColor,
                TextFormatFlags.Left | TextFormatFlags.Top |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis);

            if (_disabled)
            {
                RectangleF pill = new RectangleF(Width - 132f, 13f, 116f, 22f);
                Draw.FillRound(g, pill, 11f, Palette.Mix(Palette.CardAlt, Palette.Bg, 0.25f));
                TextRenderer.DrawText(g, "PROXIMAMENTE", Fonts.UI(7.5f, FontStyle.Bold),
                    Rectangle.Round(pill), Palette.Dim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
            else
            {
                Draw.Glyph(g, "arrow-right",
                    new RectangleF(Width - 38f, (Height - 18f) / 2f, 18f, 18f),
                    _hover ? Palette.Accent : Palette.Dim, 1.6f);
            }

            if (Focused && ShowFocusCues && !_disabled)
            {
                Draw.StrokeRound(g, new RectangleF(3.5f, 3.5f, Width - 8f, Height - 8f),
                    10f, Palette.Alpha(Palette.Accent, 120), 1f);
            }
        }
    }

    internal sealed class InputBox : Panel
    {
        private readonly TextBox _box;
        private bool _focused;
        private int _radius = 9;
        private string _placeholder = "";
        private string _glyph = "";

        public InputBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Palette.Input;
            Height = 36;
            Padding = new Padding(12, 0, 12, 0);
            _box = new TextBox();
            _box.BorderStyle = BorderStyle.None;
            _box.BackColor = Palette.Input;
            _box.ForeColor = Palette.Text;
            _box.Font = Fonts.UI(10f);
            _box.TabStop = true;
            _box.TextChanged += delegate { OnTextChanged(EventArgs.Empty); };
            _box.Enter += delegate { _focused = true; Invalidate(); };
            _box.Leave += delegate { _focused = false; Invalidate(); };
            _box.KeyDown += delegate(object s, KeyEventArgs e) { if (KeyDownInternal != null) KeyDownInternal(s, e); };
            _box.KeyUp += delegate(object s, KeyEventArgs e) { if (KeyUpInternal != null) KeyUpInternal(s, e); };
            _box.KeyPress += delegate(object s, KeyPressEventArgs e) { if (KeyPressInternal != null) KeyPressInternal(s, e); };
            Controls.Add(_box);
            UpdateLayout();
            // Do not use Region clipping to avoid cutting overlapping rounded containers
        }

        public TextBox Inner { get { return _box; } }
        public new string Text { get { return _box.Text; } set { _box.Text = value; } }
        public string Placeholder
        {
            get { return _placeholder; }
            set { _placeholder = value; SetCue(); }
        }
        public string Glyph
        {
            get { return _glyph; }
            set { _glyph = value ?? ""; UpdateLayout(); Invalidate(); }
        }
        public Keys LastKey { get; set; }

        public event KeyEventHandler KeyDownInternal;
        public event KeyEventHandler KeyUpInternal;
        public event KeyPressEventHandler KeyPressInternal;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayout();
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _box.Focus();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _box.Focus();
            base.OnMouseDown(e);
        }

        private void UpdateLayout()
        {
            if (_box == null) return;
            int h = _box.PreferredHeight;
            int glyphSpace = string.IsNullOrEmpty(_glyph) ? 0 : 22;
            _box.SetBounds(Padding.Left + glyphSpace, (Height - h) / 2,
                Math.Max(10, Width - Padding.Left - Padding.Right - glyphSpace), h);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetCue();
        }

        private void SetCue()
        {
            if (_box == null) return;
            // Fuerza la creacion del handle del TextBox: el placeholder suele
            // asignarse antes de que exista la ventana del control.
            IntPtr handle = _box.Handle;
            SendMessage(handle, 0x1501, (IntPtr)1, _placeholder);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color parent = Palette.Backdrop(this, Palette.Bg);
            e.Graphics.Clear(parent);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Draw.FillRound(e.Graphics, r, _radius, Palette.Input);
            Draw.StrokeRound(e.Graphics, r, _radius, _focused ? Palette.Accent : Palette.Border, 1f);
            if (!string.IsNullOrEmpty(_glyph))
            {
                Draw.Glyph(e.Graphics, _glyph,
                    new RectangleF(12f, (Height - 16f) / 2f, 16f, 16f),
                    _focused ? Palette.Text : Palette.Muted, 1.5f);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }

    internal static class MessageDialog
    {
        public static bool Confirm(IWin32Window owner, string title, string message, string confirmText, bool danger)
        {
            return Show(owner, title, message, confirmText, "Cancelar", null, danger) == 0;
        }

        public static void Info(IWin32Window owner, string title, string message)
        {
            Show(owner, title, message, "Entendido", null, null, false);
        }

        public static int Choice(IWin32Window owner, string title, string message, string option1, string option2, string option3, bool danger)
        {
            return Show(owner, title, message, option1, option2, option3, danger);
        }

        private static int Show(IWin32Window owner, string title, string message, string primary, string secondary, string tertiary, bool danger)
        {
            int result = 2;
            using (Form f = new Form())
            {
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ShowIcon = false;
                f.ShowInTaskbar = false;
                f.BackColor = Palette.Bg;
                f.ForeColor = Palette.Text;
                f.ClientSize = new Size(480, 214);
                f.Font = Fonts.UI(9.5f);
                f.Padding = new Padding(22);
                f.AutoScaleMode = AutoScaleMode.None;

                Label titleLabel = new Label();
                titleLabel.Text = title;
                titleLabel.Font = Fonts.UI(13f, FontStyle.Bold);
                titleLabel.ForeColor = Palette.Text;
                titleLabel.AutoSize = false;
                titleLabel.SetBounds(22, 18, 436, 28);
                f.Controls.Add(titleLabel);

                Label msg = new Label();
                msg.Text = message;
                msg.Font = Fonts.UI(9.75f);
                msg.ForeColor = Palette.Muted;
                msg.AutoSize = false;
                msg.SetBounds(22, 54, 436, 100);
                f.Controls.Add(msg);

                int y = 168;
                int x = 458;
                if (tertiary != null)
                {
                    RoundButton b3 = MakeButton(tertiary, Palette.Card, Palette.Text, false);
                    b3.Width = TextRenderer.MeasureText(tertiary, b3.Font).Width + 32;
                    x -= b3.Width;
                    b3.SetBounds(x, y, b3.Width, 34);
                    int idx = result;
                    b3.Click += delegate { result = 2; f.DialogResult = DialogResult.Cancel; f.Close(); };
                    f.Controls.Add(b3);
                    x -= 10;
                }
                if (secondary != null)
                {
                    RoundButton b2 = MakeButton(secondary, Palette.Card, Palette.Text, false);
                    b2.Width = TextRenderer.MeasureText(secondary, b2.Font).Width + 32;
                    x -= b2.Width;
                    b2.SetBounds(x, y, b2.Width, 34);
                    b2.Click += delegate { result = 1; f.DialogResult = DialogResult.No; f.Close(); };
                    f.Controls.Add(b2);
                    f.CancelButton = b2;
                    x -= 10;
                }
                RoundButton b1 = MakeButton(primary, danger ? Palette.Red : Palette.Accent, danger ? Palette.White : ColorTranslator.FromHtml("#06120A"), true);
                b1.Width = TextRenderer.MeasureText(primary, b1.Font).Width + 34;
                x -= b1.Width;
                b1.SetBounds(x, y, b1.Width, 34);
                b1.Click += delegate { result = 0; f.DialogResult = DialogResult.OK; f.Close(); };
                f.Controls.Add(b1);
                f.AcceptButton = b1;

                f.HandleCreated += delegate { WindowFx.DarkTitleBar(f.Handle); };
                f.ShowDialog(owner);
            }
            return result;
        }

        private static RoundButton MakeButton(string text, Color fill, Color fg, bool bold)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
            b.FillColor = fill;
            b.HoverFill = Palette.Mix(fill, Color.White, 0.12f);
            b.PressFill = Palette.Mix(fill, Color.Black, 0.15f);
            b.TextColor = fg;
            b.Font = Fonts.UI(9.5f, bold ? FontStyle.Bold : FontStyle.Regular);
            b.Radius = 8;
            return b;
        }
    }

    internal sealed class Toast : Panel
    {
        private readonly System.Windows.Forms.Timer _timer;
        private string _message = "";
        private Color _accent = Palette.Accent;
        private string _glyph = "check";

        public Toast()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(380, 54);
            Visible = false;
            BackColor = Palette.Card;
            Font = Fonts.UI(9.5f);
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 4800;
            _timer.Tick += delegate { _timer.Stop(); Visible = false; };
            Region = new Region(Draw.Round(new RectangleF(0, 0, Width, Height), 12f));
        }

        public void ShowMessage(string message, Color accent, string glyph)
        {
            _message = message;
            _accent = accent;
            _glyph = glyph;
            _timer.Stop();
            _timer.Start();
            Visible = true;
            BringToFront();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Region old = Region;
            Region = new Region(Draw.Round(new RectangleF(0, 0, Width, Height), 12f));
            if (old != null) old.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Palette.Backdrop(this, Palette.Bg));
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Draw.FillRound(g, r, 12f, Palette.CardAlt);
            Draw.StrokeRound(g, r, 12f, Palette.Border, 1f);
            RectangleF bar = new RectangleF(0f, 0f, 4f, Height);
            Draw.FillRound(g, bar, 2f, _accent);
            RectangleF gr = new RectangleF(18f, (Height - 20f) / 2f, 20f, 20f);
            Draw.Glyph(g, _glyph, gr, _accent, 1.8f);
            RectangleF tr = new RectangleF(50f, 0f, Width - 66f, Height);
            TextRenderer.DrawText(g, _message, Font, Rectangle.Round(tr), Palette.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    internal static class WindowFx
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void DarkTitleBar(IntPtr handle)
        {
            try
            {
                int value = 1;
                if (DwmSetWindowAttribute(handle, 20, ref value, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
                }

                // DWMWA_CAPTION_COLOR (35) y DWMWA_TEXT_COLOR (36) en Windows 11.
                // En versiones anteriores devuelven error y se ignora.
                int caption = ToColorRef(Palette.TitleBar);
                DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
                int text = ToColorRef(Palette.Text);
                DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));

                // DWMWA_WINDOW_CORNER_PREFERENCE (33): esquinas redondeadas
                // tambien en la ventana sin borde del panel.
                int round = 2;
                DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
            }
            catch
            {
            }
        }

        private static int ToColorRef(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }
    }
}
