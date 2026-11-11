#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace McPanel
{
    // Pantalla inicial "Perfiles": muestra cada carpeta de servidor como una
    // tarjeta con screenshot, version, tipo, horas de uso y peso.
    internal sealed class ProfilesView : Panel
    {
        private const string AllVersions = "Version MC";
        private const string AllTypes = "Tipo de Servidor";

        private readonly Label _title;
        private readonly ProfileCountBadge _count;
        private readonly InputBox _search;
        private readonly RoundButton _sortButton;
        private readonly SelectBox _versionFilter;
        private readonly SelectBox _typeFilter;
        private readonly Panel _gridHost;
        private readonly Panel _canvas;
        private readonly SlimScrollBar _bar;
        private readonly ToolTip _tips;

        private readonly List<ServerEntry> _entries = new List<ServerEntry>();
        private readonly List<ProfileCardView> _visualOrder = new List<ProfileCardView>();
        private readonly Dictionary<string, ProfileScan> _scans =
            new Dictionary<string, ProfileScan>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ProfileCardView> _cards =
            new Dictionary<string, ProfileCardView>(StringComparer.OrdinalIgnoreCase);

        private float _scale = 1f;
        private int _columns = 3;
        private int _scroll;
        private int _contentHeight;
        private int _sortMode;
        private int _generation;
        private int _pendingScans;
        private bool _updatingFilters;

        public event Action<ServerEntry> OpenRequested;
        public event Action ContentReady;

        public ProfilesView()
        {
            try
            {
                using (Graphics probe = CreateGraphics()) _scale = probe.DpiX / 96f;
            }
            catch { _scale = 1f; }

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Palette.Bg;
            Padding = new Padding(0);

            _title = new Label();
            _title.Text = "Explora los perfiles";
            _title.Font = Fonts.UI(15.5f, FontStyle.Bold);
            _title.ForeColor = Palette.Text;
            _title.BackColor = Color.Transparent;
            _title.AutoSize = true;

            _count = new ProfileCountBadge();
            _count.Text = "0";

            _search = new InputBox();
            _search.Glyph = "search";
            _search.Placeholder = "Buscar perfil...";
            _search.TextChanged += delegate { ApplyFilter(); };

            _sortButton = new RoundButton();
            _sortButton.Text = "";
            _sortButton.Glyph = "sort";
            _sortButton.FillColor = Palette.Card;
            _sortButton.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.07f);
            _sortButton.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.10f);
            _sortButton.TextColor = Palette.Text;
            _sortButton.BorderColor = Palette.Border;
            _sortButton.Radius = 9;
            _sortButton.Click += delegate { CycleSort(); };

            _versionFilter = new SelectBox(new string[] { AllVersions }, AllVersions);
            _versionFilter.SelectedIndexChanged += delegate
            {
                if (!_updatingFilters) ApplyFilter();
            };

            _typeFilter = new SelectBox(
                new string[] { AllTypes, "Vanilla", "Plugins", "Modded" }, AllTypes);
            _typeFilter.SelectedIndexChanged += delegate
            {
                if (!_updatingFilters) ApplyFilter();
            };

            _gridHost = new Panel();
            _gridHost.BackColor = Palette.Bg;
            _gridHost.MouseWheel += GridWheel;

            _canvas = new Panel();
            _canvas.BackColor = Palette.Bg;
            _canvas.MouseWheel += GridWheel;
            _gridHost.Controls.Add(_canvas);

            _bar = new SlimScrollBar(delegate { return Palette.Bg; });
            _bar.ValueChanged += delegate
            {
                _scroll = _bar.Value;
                _canvas.Top = -_scroll;
            };

            _tips = new ToolTip();
            UpdateSortTip();

            Controls.Add(_title);
            Controls.Add(_count);
            Controls.Add(_search);
            Controls.Add(_sortButton);
            Controls.Add(_versionFilter);
            Controls.Add(_typeFilter);
            Controls.Add(_gridHost);
            Controls.Add(_bar);
            _bar.BringToFront();
        }

        private int Dp(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        public void Reload()
        {
            _generation++;
            _entries.Clear();
            _scans.Clear();
            _pendingScans = 0;

            List<ServerEntry> servers = ServerProjects.List();
            _entries.AddRange(servers);

            BuildCards();
            RefreshVersionOptions();
            UpdateCount();
            ResetScroll();
            StartScans();

            if (ContentReady != null) ContentReady();
        }

        // ----------------------------------------------------------------
        // Datos / filtros
        // ----------------------------------------------------------------
        private ProfileScan ScanFor(ServerEntry entry)
        {
            string key = ProfileStore.Key(entry.Path);
            ProfileScan scan;
            if (_scans.TryGetValue(key, out scan) && scan != null) return scan;
            scan = ProfileStore.CachedScan(entry.Path);
            _scans[key] = scan;
            return scan;
        }

        private List<ServerEntry> FilteredEntries()
        {
            IEnumerable<ServerEntry> query = _entries;

            string text = (_search.Text ?? "").Trim();
            if (text.Length > 0)
                query = query.Where(e => e.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);

            string version = _versionFilter.SelectedItem;
            if (!string.IsNullOrWhiteSpace(version) && version != AllVersions)
                query = query.Where(e => string.Equals(ScanFor(e).Version, version,
                    StringComparison.OrdinalIgnoreCase));

            string type = _typeFilter.SelectedItem;
            if (!string.IsNullOrWhiteSpace(type) && type != AllTypes)
                query = query.Where(e => string.Equals(ProfileStore.TypeLabel(ScanFor(e).Kind), type,
                    StringComparison.OrdinalIgnoreCase));

            List<ServerEntry> list = query.ToList();
            if (_sortMode == 0)
            {
                list = list.OrderByDescending(delegate(ServerEntry e)
                {
                    DateTime? seen = ProfileStore.Get(e.Path).LastOpenedUtc;
                    return seen.HasValue ? seen.Value : DateTime.MinValue;
                }).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            else if (_sortMode == 1)
            {
                list = list.OrderByDescending(delegate(ServerEntry e)
                {
                    return ProfileStore.Get(e.Path).TotalSeconds;
                }).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            else
            {
                list = list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            return list;
        }

        private void RefreshVersionOptions()
        {
            List<string> versions = new List<string>();
            versions.Add(AllVersions);
            foreach (ServerEntry entry in _entries)
            {
                string version = ScanFor(entry).Version;
                if (string.IsNullOrWhiteSpace(version) || version == "desconocida") continue;
                if (!versions.Any(v => string.Equals(v, version, StringComparison.OrdinalIgnoreCase)))
                    versions.Add(version);
            }
            versions.Sort(1, versions.Count - 1, StringComparer.OrdinalIgnoreCase);

            string current = _versionFilter.SelectedItem;
            if (string.IsNullOrWhiteSpace(current) ||
                !versions.Any(v => string.Equals(v, current, StringComparison.OrdinalIgnoreCase)))
                current = AllVersions;

            _updatingFilters = true;
            try
            {
                _versionFilter.SetItems(versions.ToArray(), current);
            }
            finally { _updatingFilters = false; }
        }

        private void UpdateCount()
        {
            int shown = FilteredEntries().Count;
            _count.Text = shown == _entries.Count
                ? _entries.Count.ToString()
                : shown + "/" + _entries.Count;
            int width = TextRenderer.MeasureText(_count.Text, _count.Font).Width + Dp(18);
            _count.Width = Math.Max(Dp(40), width);
            LayoutHeader();
        }

        private void CycleSort()
        {
            _sortMode = (_sortMode + 1) % 3;
            UpdateSortTip();
            ApplyFilter();
        }

        private void UpdateSortTip()
        {
            string text = _sortMode == 0 ? "Ordenar: últimos abiertos"
                : (_sortMode == 1 ? "Ordenar: más horas" : "Ordenar: nombre (A-Z)");
            try { _tips.SetToolTip(_sortButton, text); } catch { }
        }

        private void ApplyFilter()
        {
            BuildCards();
            UpdateCount();
            ResetScroll();
        }

        // ----------------------------------------------------------------
        // Tarjetas
        // ----------------------------------------------------------------
        private void BuildCards()
        {
            foreach (ProfileCardView old in _visualOrder)
            {
                try
                {
                    _canvas.Controls.Remove(old);
                    old.Dispose();
                }
                catch { }
            }
            _visualOrder.Clear();
            _cards.Clear();

            double maxSeconds = 0;
            foreach (ServerEntry entry in _entries)
            {
                double seconds = ProfileStore.Get(entry.Path).TotalSeconds;
                if (seconds > maxSeconds) maxSeconds = seconds;
            }

            foreach (ServerEntry entry in FilteredEntries())
            {
                ProfileMeta meta = ProfileStore.Get(entry.Path);
                ProfileScan scan = ScanFor(entry);
                ProfileCardView card = new ProfileCardView(entry, meta, scan);
                card.IsFavorite = maxSeconds > 0 && Math.Abs(meta.TotalSeconds - maxSeconds) < 0.5;
                card.OpenRequested += delegate(ServerEntry selected)
                {
                    if (OpenRequested != null) OpenRequested(selected);
                };
                card.MouseWheel += GridWheel;
                _canvas.Controls.Add(card);
                _visualOrder.Add(card);
                _cards[ProfileStore.Key(entry.Path)] = card;
            }

            LayoutCards();
        }

        private void LayoutCards()
        {
            if (_gridHost == null) return;
            int usable = _gridHost.ClientSize.Width - Dp(28);
            if (usable < Dp(260)) usable = Dp(260);
            int gap = Dp(18);
            // Tres columnas solo cuando el contenido tiene ancho de sobra;
            // con la barra lateral visible se prefieren dos tarjetas legibles.
            _columns = usable >= Dp(1180) ? 3 : (usable >= Dp(640) ? 2 : 1);
            int cardWidth = (usable - gap * (_columns - 1)) / _columns;
            int cardHeight = Dp(356);

            int rows = (_visualOrder.Count + _columns - 1) / _columns;
            for (int i = 0; i < _visualOrder.Count; i++)
            {
                int col = i % _columns;
                int row = i / _columns;
                _visualOrder[i].SetBounds(
                    col * (cardWidth + gap),
                    row * (cardHeight + gap),
                    cardWidth, cardHeight);
            }

            _contentHeight = rows <= 0 ? 0 : rows * cardHeight + (rows - 1) * gap;
            int canvasHeight = Math.Max(_contentHeight, _gridHost.ClientSize.Height);
            _canvas.SetBounds(0, -_scroll, Math.Max(1, _gridHost.ClientSize.Width), Math.Max(1, canvasHeight));
            UpdateBar();
        }

        private void UpdateBar()
        {
            int viewport = Math.Max(1, _gridHost.ClientSize.Height);
            int maxScroll = Math.Max(0, _contentHeight - viewport);
            if (_scroll > maxScroll) _scroll = maxScroll;
            if (_scroll < 0) _scroll = 0;
            _bar.SetRange(maxScroll, viewport);
            _bar.SetValue(_scroll, true);
            _canvas.Top = -_scroll;

            bool visible = maxScroll > 0;
            _bar.Visible = visible;
            if (visible)
            {
                _bar.SetBounds(
                    _gridHost.Right - Dp(20) - Dp(2),
                    _gridHost.Top,
                    Dp(20),
                    _gridHost.Height);
                _bar.BringToFront();
            }
        }

        private void ResetScroll()
        {
            _scroll = 0;
            _canvas.Top = 0;
            UpdateBar();
        }

        private void GridWheel(object sender, MouseEventArgs e)
        {
            int maxScroll = Math.Max(0, _contentHeight - _gridHost.ClientSize.Height);
            if (maxScroll <= 0) return;
            int step = (e.Delta / 120) * Dp(96);
            _scroll -= step;
            if (_scroll < 0) _scroll = 0;
            if (_scroll > maxScroll) _scroll = maxScroll;
            _bar.SetValue(_scroll, true);
            _canvas.Top = -_scroll;
        }

        // ----------------------------------------------------------------
        // Escaneo en segundo plano
        // ----------------------------------------------------------------
        private void StartScans()
        {
            int generation = _generation;
            _pendingScans = _entries.Count;
            foreach (ServerEntry entry in _entries)
            {
                string path = entry.Path;
                string key = ProfileStore.Key(path);
                Task.Run(delegate { return ProfileStore.Scan(path); })
                    .ContinueWith(delegate(Task<ProfileScan> task)
                    {
                        if (task.Status != TaskStatus.RanToCompletion) return;
                        ProfileScan scan = task.Result;
                        try
                        {
                            if (IsDisposed || !IsHandleCreated) return;
                            BeginInvoke(new Action(delegate
                            {
                                ApplyScanResult(key, path, scan, generation);
                            }));
                        }
                        catch { }
                    });
            }
        }

        private void ApplyScanResult(string key, string path, ProfileScan scan, int generation)
        {
            if (IsDisposed || generation != _generation) return;
            scan.FromCache = true;
            _scans[key] = scan;
            ProfileStore.ApplyScan(path, scan);

            ProfileCardView card;
            if (_cards.TryGetValue(key, out card) && card != null) card.SetScan(scan);

            RefreshVersionOptions();
            RefreshFavorites();
            UpdateCount();

            _pendingScans--;
            if (_pendingScans <= 0)
            {
                _pendingScans = 0;
                ProfileStore.Save();
            }
        }

        private void RefreshFavorites()
        {
            double maxSeconds = 0;
            foreach (ServerEntry entry in _entries)
            {
                double seconds = ProfileStore.Get(entry.Path).TotalSeconds;
                if (seconds > maxSeconds) maxSeconds = seconds;
            }
            foreach (ProfileCardView card in _visualOrder)
            {
                bool favorite = maxSeconds > 0 &&
                    Math.Abs(card.Meta.TotalSeconds - maxSeconds) < 0.5;
                if (card.IsFavorite != favorite) card.IsFavorite = favorite;
            }
        }

        // ----------------------------------------------------------------
        // Layout
        // ----------------------------------------------------------------
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutHeader();
            LayoutCards();
        }

        private void LayoutHeader()
        {
            if (_title == null) return;
            int left = Dp(38);
            int right = ClientSize.Width - Dp(38);
            if (right <= left + Dp(300)) right = left + Dp(300);
            int top = Dp(18);

            _title.Location = new Point(left, top);

            int titleWidth = TextRenderer.MeasureText(_title.Text, _title.Font).Width;
            _count.Location = new Point(left + titleWidth + Dp(12), top + Dp(9));
            _count.Height = Dp(22);

            int filtersTop = top + Dp(46);
            int gap = Dp(10);
            int sortWidth = Dp(44);
            int versionWidth = Dp(190);
            int typeWidth = Dp(190);
            int searchWidth = right - left - sortWidth - versionWidth - typeWidth - gap * 3;
            if (searchWidth < Dp(180)) searchWidth = Dp(180);

            _search.SetBounds(left, filtersTop, searchWidth, Dp(38));
            _sortButton.SetBounds(left + searchWidth + gap, filtersTop, sortWidth, Dp(38));
            _versionFilter.SetBounds(left + searchWidth + gap + sortWidth + gap,
                filtersTop, versionWidth, Dp(38));
            _typeFilter.SetBounds(left + searchWidth + gap + sortWidth + gap + versionWidth + gap,
                filtersTop, typeWidth, Dp(38));

            int gridTop = filtersTop + Dp(38) + Dp(18);
            int gridHeight = ClientSize.Height - gridTop - Dp(26);
            _gridHost.SetBounds(left, gridTop, right - left, Math.Max(Dp(120), gridHeight));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Palette.Bg);
            base.OnPaint(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _generation++;
                if (_tips != null) _tips.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ----------------------------------------------------------------
    // Contador junto al titulo ("1109")
    // ----------------------------------------------------------------
    internal sealed class ProfileCountBadge : Control
    {
        private string _text = "0";

        public ProfileCountBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            Font = Fonts.UI(8.5f, FontStyle.Bold);
            Height = 22;
            Width = 42;
            BackColor = Color.Transparent;
        }

        public new string Text
        {
            get { return _text; }
            set { _text = value ?? ""; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Palette.Backdrop(this, Palette.Bg));
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Draw.FillRound(g, r, Height / 2f, Palette.Alpha(Palette.Accent, 26));
            Draw.StrokeRound(g, r, Height / 2f, Palette.Alpha(Palette.Accent, 80), 1f);
            TextRenderer.DrawText(g, _text, Font,
                new Rectangle(0, 0, Width, Height), Palette.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }

    // ----------------------------------------------------------------
    // Tarjeta individual de perfil
    // ----------------------------------------------------------------
    internal sealed class ProfileCardView : Control
    {
        private readonly ServerEntry _entry;
        private ProfileMeta _meta;
        private ProfileScan _scan;
        private Image _image;
        private bool _hover;
        private bool _favorite;
        private readonly System.Windows.Forms.Timer _wiggle;
        private float _wigglePhase;
        private float _wiggleAngle;

        private readonly Font _nameFont = Fonts.UI(19.5f, FontStyle.Bold);
        private readonly Font _smallFont = Fonts.UI(9.5f);
        private readonly Font _tinyFont = Fonts.UI(8.4f);
        private readonly Font _tinyBoldFont = Fonts.UI(8.4f, FontStyle.Bold);
        private readonly Font _hoursFont = Fonts.UI(12f);
        private readonly Font _placeholderFont = Fonts.Mono(18f, FontStyle.Bold);

        public event Action<ServerEntry> OpenRequested;

        public ProfileCardView(ServerEntry entry, ProfileMeta meta, ProfileScan scan)
        {
            _entry = entry;
            _meta = meta ?? new ProfileMeta();
            _scan = scan ?? new ProfileScan();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Palette.Bg;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleName = entry.Name;
            AccessibleRole = AccessibleRole.PushButton;

            _wiggle = new System.Windows.Forms.Timer();
            _wiggle.Interval = 30;
            _wiggle.Tick += delegate
            {
                _wigglePhase += 0.32f;
                _wiggleAngle = (float)Math.Sin(_wigglePhase) * 10f;
                Invalidate(new Rectangle(Width - 70, 8, 62, 62));
            };

            LoadImage();
        }

        public ServerEntry Entry { get { return _entry; } }
        public ProfileMeta Meta { get { return _meta; } }
        public ProfileScan Scan { get { return _scan; } }

        public bool IsFavorite
        {
            get { return _favorite; }
            set
            {
                if (_favorite == value) return;
                _favorite = value;
                if (!_favorite)
                {
                    _wiggle.Stop();
                    _wiggleAngle = 0f;
                }
                else if (_hover)
                {
                    _wigglePhase = 0f;
                    _wiggle.Start();
                }
                Invalidate();
            }
        }

        public void SetScan(ProfileScan scan)
        {
            _scan = scan ?? new ProfileScan();
            Invalidate();
        }

        public void RefreshMeta()
        {
            _meta = ProfileStore.Get(_entry.Path);
            LoadImage();
            Invalidate();
        }

        private void LoadImage()
        {
            if (_image != null)
            {
                try { _image.Dispose(); } catch { }
                _image = null;
            }
            try
            {
                string path = _meta != null ? _meta.ScreenshotPath : "";
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    using (Image source = Image.FromFile(path))
                        _image = new Bitmap(source);
                }
            }
            catch { _image = null; }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            if (_favorite)
            {
                _wigglePhase = 0f;
                _wiggle.Start();
            }
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _wiggle.Stop();
            _wiggleAngle = 0f;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            if (OpenRequested != null) OpenRequested(_entry);
            base.OnDoubleClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                if (OpenRequested != null) OpenRequested(_entry);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Palette.Backdrop(this, Palette.Bg));

            RectangleF card = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Draw.FillRound(g, card, 11f, Palette.Card);

            int imageHeight = ImageHeight();
            Rectangle imageRect = new Rectangle(1, 1, Width - 2, imageHeight);
            GraphicsState state = g.Save();
            using (GraphicsPath clip = Draw.Round(card, 11f))
            {
                g.SetClip(clip);
                // La captura vive solo en su area: nunca puede invadir la
                // franja de texto. Asi el texto siempre queda por encima.
                g.IntersectClip(imageRect);
                DrawImageArea(g, imageRect);

                Color line = Palette.Alpha(Palette.Accent, _hover ? 210 : 85);
                using (Pen pen = new Pen(line, _hover ? 2f : 1.4f))
                    g.DrawLine(pen, 16f, 1f, Width - 16f, 1f);
            }
            g.Restore(state);

            // Capa de texto opaca sobre el fondo de la tarjeta. Refuerza el
            // orden visual aunque la imagen tenga un aspecto muy vertical.
            using (SolidBrush infoBack = new SolidBrush(Palette.Card))
                g.FillRectangle(infoBack, 0, imageHeight, Width, Height - imageHeight);

            DrawInfo(g, imageHeight);

            if (_hover)
                Draw.StrokeRound(g, card, 11f, Palette.Alpha(Palette.Accent, 190), 1.5f);
            else
                Draw.StrokeRound(g, card, 11f, Palette.BorderSoft, 1f);

            if (_favorite) DrawFavorite(g, imageRect);
        }

        private int ImageHeight()
        {
            int height = Height - Dp(150);
            if (height < Dp(150)) height = Dp(150);
            if (height > Height - Dp(96)) height = Height - Dp(96);
            return height;
        }

        private int Dp(int value)
        {
            try { return (int)Math.Round(value * (DeviceDpi / 96f)); }
            catch { return value; }
        }

        private void DrawImageArea(Graphics g, Rectangle imageRect)
        {
            Color background = Palette.Mix(Palette.Card, Palette.Bg, 0.55f);
            using (SolidBrush brush = new SolidBrush(background)) g.FillRectangle(brush, imageRect);

            if (_image != null)
            {
                float scale = Math.Max((float)imageRect.Width / _image.Width,
                    (float)imageRect.Height / _image.Height);
                int width = (int)Math.Ceiling(_image.Width * scale);
                int height = (int)Math.Ceiling(_image.Height * scale);
                Rectangle dest = new Rectangle(
                    imageRect.X + (imageRect.Width - width) / 2,
                    imageRect.Y + (imageRect.Height - height) / 2,
                    width, height);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(_image, dest);
                return;
            }

            // Placeholder con el texto "IMAGEN", como en la referencia.
            string text = "IMAGEN";
            Size textSize = TextRenderer.MeasureText(text, _placeholderFont,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int cx = imageRect.X + imageRect.Width / 2;
            int cy = imageRect.Y + imageRect.Height / 2 - Dp(8);
            TextRenderer.DrawText(g, text, _placeholderFont,
                new Rectangle(cx - textSize.Width / 2, cy - textSize.Height / 2,
                    textSize.Width, textSize.Height),
                Palette.Dim,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Pequeños corchetes decorativos alrededor del texto.
            using (Pen pen = new Pen(Palette.Alpha(Palette.Dim, 150), 1.5f))
            {
                int left = cx - textSize.Width / 2 - Dp(16);
                int right = cx + textSize.Width / 2 + Dp(10);
                int top = cy - Dp(8);
                int bottom = cy + Dp(8);
                g.DrawLine(pen, left, top, left, bottom);
                g.DrawLine(pen, left, top, left + Dp(5), top);
                g.DrawLine(pen, left, bottom, left + Dp(5), bottom);
                g.DrawLine(pen, right, top, right, bottom);
                g.DrawLine(pen, right - Dp(5), top, right, top);
                g.DrawLine(pen, right - Dp(5), bottom, right, bottom);
            }

            TextRenderer.DrawText(g, LanguageService.T("Doble clic para abrir"), _tinyFont,
                new Rectangle(imageRect.X, cy + textSize.Height / 2 + Dp(12),
                    imageRect.Width, Dp(20)),
                Palette.Dim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                TextFormatFlags.NoPrefix);
        }

        private void DrawInfo(Graphics g, int imageHeight)
        {
            int left = Dp(22);
            int right = Width - Dp(22);
            int textWidth = Math.Max(10, right - left);
            int topRowY = imageHeight + Dp(14);
            string provider = string.IsNullOrWhiteSpace(_scan.Provider)
                ? ProfileStore.TypeLabel(_scan.Kind)
                : _scan.Provider;
            string version = string.IsNullOrWhiteSpace(_scan.Version)
                ? LanguageService.T("desconocida")
                : _scan.Version;
            string versionText = provider + " - " + version;

            Rectangle topRow = new Rectangle(left, topRowY, textWidth - Dp(90), Dp(20));
            TextRenderer.DrawText(g, versionText, _smallFont, topRow, Palette.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (_scan.Kind != AddonKind.None)
            {
                string kind = _scan.Kind == AddonKind.Mods
                    ? (_scan.AddonCount == 1 ? " Mod" : " Mods")
                    : (_scan.AddonCount == 1 ? " Plugin" : " Plugins");
                string addons = _scan.AddonCount + kind;
                TextRenderer.DrawText(g, addons, _tinyFont,
                    new Rectangle(right - Dp(120), topRowY + Dp(2), Dp(120), Dp(18)),
                    Palette.Dim,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix);
            }
            int NameTop = imageHeight + Dp(36);
            int NameHeight = Math.Max(Dp(30), _nameFont.Height+Dp(4));
            Rectangle nameRect = new Rectangle(left, NameTop, textWidth, NameHeight);

            TextRenderer.DrawText(g, _entry.Name, _nameFont, nameRect,
                _entry.HasJar ? Palette.Text : Palette.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            
            int hoursTop = nameRect.Bottom + Dp(2);

            Rectangle hoursRect = new Rectangle(left, hoursTop, textWidth, Dp(22));
            TextRenderer.DrawText(g, HoursText(), _hoursFont, hoursRect, Palette.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            int dividerY = Height - Dp(50);
            using (Pen pen = new Pen(Palette.BorderSoft, 1f))
                g.DrawLine(pen, left, dividerY, right, dividerY);

            int bottomY = Height - Dp(42);
            int half = textWidth / 2;
            TextRenderer.DrawText(g, LastLaunchText(), _tinyFont,
                new Rectangle(left, bottomY, half - Dp(4), Dp(20)), Palette.Dim,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            string sizeLabel = LanguageService.T("Peso:") + " " + SizeText();
            TextRenderer.DrawText(g, sizeLabel, _tinyFont,
                new Rectangle(left + half, bottomY, half, Dp(20)), Palette.Dim,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix);
        }

        private string HoursText()
        {
            double seconds = _meta != null ? _meta.TotalSeconds : 0;
            if (seconds < 60) return LanguageService.T("Sin horas registradas");
            double hours = seconds / 3600.0;
            if (hours < 1)
            {
                int minutes = Math.Max(1, (int)Math.Round(seconds / 60.0));
                return minutes + LanguageService.T(minutes == 1 ? " Minuto en línea" : " Minutos en línea");
            }
            if (hours < 10) return hours.ToString("0.0") + LanguageService.T(" Horas en línea");
            int rounded = (int)Math.Round(hours);
            return rounded + LanguageService.T(rounded == 1 ? " Hora en línea" : " Horas en línea");
        }

        private string LastLaunchText()
        {
            if (_meta == null || !_meta.LastOpenedUtc.HasValue)
                return LanguageService.T("Último inicio: nunca");
            TimeSpan elapsed = DateTime.UtcNow - _meta.LastOpenedUtc.Value;
            if (elapsed.TotalSeconds < 0) elapsed = TimeSpan.Zero;
            if (elapsed.TotalMinutes < 1) return LanguageService.T("Último inicio: hace un momento");
            if (elapsed.TotalHours < 1)
            {
                int minutes = Math.Max(1, (int)Math.Floor(elapsed.TotalMinutes));
                return LanguageService.T("Último inicio: hace") + " " + minutes +
                    LanguageService.T(minutes == 1 ? " minuto" : " minutos");
            }
            if (elapsed.TotalDays < 1)
            {
                int hours = Math.Max(1, (int)Math.Floor(elapsed.TotalHours));
                return LanguageService.T("Último inicio: hace") + " " + hours +
                    LanguageService.T(hours == 1 ? " hora" : " horas");
            }
            int days = Math.Max(1, (int)Math.Floor(elapsed.TotalDays));
            return LanguageService.T("Último inicio: hace") + " " + days +
                LanguageService.T(days == 1 ? " día" : " días");
        }

        private string SizeText()
        {
            if (_scan == null || !_scan.FromCache) return LanguageService.T("calculando...");
            return Utils.HumanBytes(_scan.SizeBytes);
        }

        private void DrawFavorite(Graphics g, Rectangle imageRect)
        {
            float cx = Width - Dp(34);
            float cy = Dp(36);
            float outer = Dp(13);
            float inner = outer * 0.44f;

            // Sombra suave para separar la estrella de la screenshot.
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            {
                PointF[] shadowPoints = StarPoints(cx + 1f, cy + 1.5f, outer, inner, _wiggleAngle);
                g.FillPolygon(shadow, shadowPoints);
            }

            using (SolidBrush brush = new SolidBrush(ColorTranslator.FromHtml("#FFD633")))
            {
                PointF[] points = StarPoints(cx, cy, outer, inner, _wiggleAngle);
                g.FillPolygon(brush, points);
            }

            if (!_hover) return;
            string tip = "Favorito";
            Size tipSize = TextRenderer.MeasureText(tip, _tinyBoldFont,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
            int width = tipSize.Width + Dp(16);
            int height = Dp(22);
            int x = (int)(cx - outer - width - Dp(8));
            if (x < Dp(8)) x = (int)(cx + outer + Dp(8));
            int y = (int)(cy - height / 2f);
            Rectangle tipRect = new Rectangle(x, y, width, height);
            Draw.FillRound(g, tipRect, 6f, Palette.Alpha(Palette.CardAlt, 248));
            Draw.StrokeRound(g, tipRect, 6f, Palette.Border, 1f);
            TextRenderer.DrawText(g, tip, _tinyBoldFont, tipRect, Palette.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix);
        }

        private static PointF[] StarPoints(float cx, float cy, float outer, float inner, float angle)
        {
            PointF[] points = new PointF[10];
            float rotation = (angle - 90f) * (float)Math.PI / 180f;
            for (int i = 0; i < 10; i++)
            {
                float radius = (i % 2 == 0) ? outer : inner;
                float a = rotation + i * (float)Math.PI / 5f;
                points[i] = new PointF(
                    cx + radius * (float)Math.Cos(a),
                    cy + radius * (float)Math.Sin(a));
            }
            return points;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _wiggle.Stop(); _wiggle.Dispose(); } catch { }
                if (_image != null) { try { _image.Dispose(); } catch { } _image = null; }
                _nameFont.Dispose();
                _smallFont.Dispose();
                _tinyFont.Dispose();
                _tinyBoldFont.Dispose();
                _hoursFont.Dispose();
                _placeholderFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
