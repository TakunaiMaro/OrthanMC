using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace McPanel
{
    // Boton circular de la barra de titulo. Al pasar el mouse se expande
    // hacia la izquierda mostrando "Cambiar de server".
    internal sealed class ServerSwitchButton : Control
    {
        private readonly System.Windows.Forms.Timer _timer;
        private bool _hover;
        private float _progress;
        private const int CollapsedWidth = 30;
        private const int ExpandedWidth = 172;

        public ServerSwitchButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            Height = 30;
            Width = CollapsedWidth;
            Cursor = Cursors.Hand;
            TabStop = true;
            BackColor = Color.Transparent;
            AccessibleName = "Cambiar de server";
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 15;
            _timer.Tick += delegate { Animate(); };
        }

        public bool Expanded { get { return _progress > 0.5f; } }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            _timer.Start();
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _timer.Start();
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            Focus();
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void Animate()
        {
            float target = _hover ? 1f : 0f;
            float delta = target - _progress;
            if (Math.Abs(delta) < 0.04f)
            {
                _progress = target;
                _timer.Stop();
            }
            else
            {
                _progress += delta * 0.22f;
            }
            int width = CollapsedWidth + (int)Math.Round((ExpandedWidth - CollapsedWidth) * Ease(_progress));
            if (Width != width) Width = width;
            Invalidate();
        }

        private static float Ease(float value)
        {
            float t = Math.Max(0f, Math.Min(1f, value));
            return 1f - (1f - t) * (1f - t);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Palette.Backdrop(this, Palette.TitleBar));

            bool active = _hover || _progress > 0.01f;
            Color fill = Palette.Mix(Palette.TitleBar, Palette.Card, active ? 0.95f : 0.55f);
            Color border = Palette.Mix(Palette.TitleBar, Palette.White, active ? 0.16f : 0.08f);
            RectangleF pill = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Draw.FillRound(g, pill, Height / 2f, fill);
            Draw.StrokeRound(g, pill, Height / 2f, border, 1f);

            RectangleF icon = new RectangleF((Height - 18f) / 2f, (Height - 18f) / 2f, 18f, 18f);
            Draw.Glyph(g, "restart", icon, Palette.Accent, 1.8f);

            if (Width > 58)
            {
                RectangleF text = new RectangleF(30f, 0f, Width - 36f, Height);
                TextRenderer.DrawText(g, "Cambiar de server", Fonts.UI(8.75f, FontStyle.Bold), Rectangle.Round(text),
                    Palette.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal enum CaptionButtonKind
    {
        Minimize,
        Maximize,
        Restore,
        Close
    }

    internal sealed class CaptionButton : Control
    {
        private bool _hover;
        private bool _pressed;

        public CaptionButtonKind Kind { get; set; }

        public CaptionButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            Width = 46;
            Height = 38;
            Cursor = Cursors.Hand;
            TabStop = false;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Palette.Backdrop(this, Palette.TitleBar));

            bool close = Kind == CaptionButtonKind.Close;
            if (_hover || _pressed)
            {
                Color fill = close
                    ? (_pressed ? Palette.Mix(Palette.Red, Color.Black, 0.18f) : Palette.Red)
                    : Palette.Mix(Palette.TitleBar, Palette.White, _pressed ? 0.16f : 0.10f);
                g.FillRectangle(new SolidBrush(fill), ClientRectangle);
            }

            Color ink = close && _hover ? Palette.White : Palette.Text;
            using (Pen pen = new Pen(ink, 1.3f))
            {
                float cx = Width / 2f;
                float cy = Height / 2f;
                if (Kind == CaptionButtonKind.Minimize)
                {
                    g.DrawLine(pen, cx - 5f, cy, cx + 5f, cy);
                }
                else if (Kind == CaptionButtonKind.Maximize)
                {
                    g.DrawRectangle(pen, cx - 5f, cy - 5f, 10f, 10f);
                }
                else if (Kind == CaptionButtonKind.Restore)
                {
                    g.DrawRectangle(pen, cx - 6f, cy - 4f, 8f, 8f);
                    g.DrawLine(pen, cx - 3f, cy - 7f, cx + 5f, cy - 7f);
                    g.DrawLine(pen, cx + 5f, cy - 7f, cx + 5f, cy + 1f);
                }
                else
                {
                    g.DrawLine(pen, cx - 5f, cy - 5f, cx + 5f, cy + 5f);
                    g.DrawLine(pen, cx + 5f, cy - 5f, cx - 5f, cy + 5f);
                }
            }
        }
    }

    // Desplegable del selector de servidores. Se abre pegado al boton de la
    // barra de titulo y no permite confirmar hasta elegir una fila.
    internal sealed class ServerSwitchPopup : Form
    {
        private readonly ListBox _list;
        private readonly RoundButton _confirm;
        private readonly List<ServerEntry> _entries;
        private readonly Action<ServerEntry> _onConfirm;
        private readonly string _activePath;
        private readonly bool _serverRunning;
        private readonly bool _externalRunning;
        private Label _subtitle;
        private string _subtitleDefault = "";
        private bool _closingForConfirm;

        public ServerSwitchPopup(IWin32Window owner, Point screenLocation, List<ServerEntry> entries,
            string activePath, bool serverRunning, bool externalRunning, Action<ServerEntry> onConfirm)
        {
            _entries = entries ?? new List<ServerEntry>();
            _onConfirm = onConfirm;
            _activePath = activePath;
            _serverRunning = serverRunning;
            _externalRunning = externalRunning;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            KeyPreview = true;
            BackColor = Palette.Card;
            ForeColor = Palette.Text;
            Font = Fonts.UI(9.5f);
            ClientSize = new Size(360, 318);
            Location = ClampToScreen(screenLocation, Size, owner);
            Padding = new Padding(16);

            Label title = new Label();
            title.Text = "CAMBIAR DE SERVER";
            title.Font = Fonts.UI(11f, FontStyle.Bold);
            title.ForeColor = Palette.Text;
            title.BackColor = Color.Transparent;
            title.AutoSize = true;
            title.Location = new Point(16, 14);

            _subtitle = new Label();
            _subtitle.Font = Fonts.UI(8.75f);
            _subtitle.ForeColor = Palette.Muted;
            _subtitle.BackColor = Color.Transparent;
            _subtitle.AutoSize = false;
            _subtitle.SetBounds(16, 38, 328, 32);
            _subtitle.Text = _externalRunning
                ? "El servidor actual se inicio fuera del panel. Detenlo manualmente antes de cambiar."
                : (_serverRunning
                    ? "El servidor actual se detendra con stop y tambien se detendra Playit."
                    : "Selecciona el servidor que quieres administrar.");
            if (_entries.Count == 0)
                _subtitle.Text = "No hay servidores disponibles en este momento.";
            _subtitleDefault = _subtitle.Text;

            _list = new ListBox();
            _list.BorderStyle = BorderStyle.None;
            _list.BackColor = Palette.Card;
            _list.ForeColor = Palette.Text;
            _list.Font = Fonts.UI(9f);
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = 52;
            _list.IntegralHeight = false;
            _list.SetBounds(16, 78, 328, 156);
            _list.DrawItem += DrawServerItem;
            _list.DoubleClick += delegate { ConfirmSelection(); };
            _list.SelectedIndexChanged += delegate
            {
                if (_subtitle == null) return;
                _subtitle.ForeColor = Palette.Muted;
                _subtitle.Text = _subtitleDefault;
            };

            foreach (ServerEntry entry in _entries) _list.Items.Add(entry);
            for (int i = 0; i < _entries.Count; i++)
            {
                if (ServerProjects.SamePath(_entries[i].Path, activePath))
                {
                    _list.SelectedIndex = i;
                    break;
                }
            }
            if (_list.SelectedIndex < 0 && _list.Items.Count > 0) _list.SelectedIndex = 0;

            Label empty = null;
            if (_entries.Count == 0)
            {
                _list.Visible = false;
                empty = new Label();
                empty.Text = "Crea una carpeta con server.jar dentro de:\r\n\r\n" + ServerProjects.Root +
                    "\r\n\r\nO vuelve a ejecutar la configuracion inicial.";
                empty.Font = Fonts.UI(9f);
                empty.ForeColor = Palette.Muted;
                empty.BackColor = Color.Transparent;
                empty.AutoSize = false;
                empty.SetBounds(20, 82, 320, 140);
            }

            _confirm = new RoundButton();
            _confirm.Text = "CONFIRMAR";
            _confirm.Glyph = "check";
            _confirm.SetBounds(196, 268, 164, 36);
            _confirm.Margin = new Padding(0);
            _confirm.Enabled = _entries.Count > 0;
            _confirm.Click += delegate { ConfirmSelection(); };

            RoundButton cancel = new RoundButton();
            cancel.Text = "CANCELAR";
            cancel.SetBounds(72, 268, 116, 36);
            cancel.FillColor = Palette.Card;
            cancel.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.08f);
            cancel.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.12f);
            cancel.TextColor = Palette.Text;
            cancel.BorderColor = Palette.Border;
            cancel.Margin = new Padding(0);
            cancel.Click += delegate { Close(); };

            Controls.Add(title);
            Controls.Add(_subtitle);
            Controls.Add(_list);
            if (empty != null) Controls.Add(empty);
            Controls.Add(cancel);
            Controls.Add(_confirm);

            Deactivate += delegate
            {
                if (_closingForConfirm) return;
                // Un dialogo abierto desde el popup (por ejemplo, aviso de
                // server.jar) desactiva la ventana pero no debe cerrarla.
                Form active = Form.ActiveForm;
                if (active != null && !ReferenceEquals(active, this) && active.Modal) return;
                // Al pulsar un boton interno el cursor sigue dentro del
                // popup. Cerrar aqui cancelaria el Click antes de que se
                // ejecute, que era el fallo del boton CONFIRMAR.
                if (Bounds.Contains(Cursor.Position)) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!_closingForConfirm && !IsDisposed) Close();
                });
            };
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { ConfirmSelection(); e.Handled = true; }
            };
            Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Draw.StrokeRound(e.Graphics, new RectangleF(0.5f, 0.5f, ClientSize.Width - 1f, ClientSize.Height - 1f),
                    10f, Palette.Border, 1f);
            };
            Resize += delegate { ApplyRegion(); };
            ApplyRegion();
            Shown += delegate
            {
                WindowFx.DarkTitleBar(Handle);
                if (_list.Visible) _list.Focus();
            };
        }

        // Esquinas redondeadas y sombra DWM para el desplegable sin borde.
        private void ApplyRegion()
        {
            try
            {
                Region = new Region(Draw.Round(new RectangleF(0, 0, Width, Height), 10f));
            }
            catch { }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        private void ConfirmSelection()
        {
            if (_closingForConfirm) return;
            if (_list.SelectedItem == null)
            {
                if (_subtitle != null)
                {
                    _subtitle.ForeColor = Palette.Amber;
                    _subtitle.Text = "Selecciona un servidor antes de confirmar.";
                }
                return;
            }

            ServerEntry entry = (ServerEntry)_list.SelectedItem;
            if (ServerProjects.SamePath(entry.Path, _activePath))
            {
                if (_subtitle != null)
                {
                    _subtitle.ForeColor = Palette.Amber;
                    _subtitle.Text = "Ese servidor ya esta activo. Elige otro para cambiar.";
                }
                return;
            }
            if (!File.Exists(Path.Combine(entry.Path, "server.jar")))
            {
                MessageDialog.Info(this, "Cambiar de server",
                    "La carpeta seleccionada no contiene server.jar:\r\n\r\n" + entry.Path);
                return;
            }
            _closingForConfirm = true;
            Close();
            if (_onConfirm != null) _onConfirm(entry);
        }

        private void DrawServerItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;
            ServerEntry entry = (ServerEntry)_list.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (SolidBrush back = new SolidBrush(selected ? Palette.AccentDim : Palette.Card))
                e.Graphics.FillRectangle(back, e.Bounds);

            string suffix = entry.IsActive ? "  ·  ACTUAL" : "";
            string label = entry.Name + suffix;
            Rectangle nameRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 4, e.Bounds.Width - 8, 22);
            TextRenderer.DrawText(e.Graphics, label, Fonts.UI(9.25f, FontStyle.Bold), nameRect,
                selected ? Palette.Text : Palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis);

            string detail = entry.Path;
            if (!entry.HasJar) detail += "  (sin server.jar)";
            Rectangle pathRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 26, e.Bounds.Width - 8, 18);
            TextRenderer.DrawText(e.Graphics, detail, Fonts.UI(8f), pathRect,
                entry.HasJar ? Palette.Muted : Palette.Amber,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                TextFormatFlags.EndEllipsis);
        }

        private static Point ClampToScreen(Point desired, Size size, IWin32Window owner)
        {
            Screen screen = Screen.FromHandle((owner as Control) != null
                ? ((Control)owner).Handle
                : IntPtr.Zero);
            Rectangle wa = screen.WorkingArea;
            int x = Math.Max(wa.Left + 4, Math.Min(desired.X, wa.Right - size.Width - 4));
            int y = Math.Max(wa.Top + 4, Math.Min(desired.Y, wa.Bottom - size.Height - 4));
            return new Point(x, y);
        }
    }
}
