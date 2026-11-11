#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

namespace McPanel
{
    // Textos de la pantalla de carga. Estan centralizados para poder
    // traducirlos en el futuro sin tocar la animacion.
    internal static class LoadingStrings
    {
        public static string Loading = "Cargando";
        public static string Ready = "Listo";
    }

    // Pantalla de carga de la lista de Plugins/Mods.
    // Secuencia: el pico se balancea y se licua; sus pixeles se reacomodan
    // hasta formar una esmeralda. La esmeralda se licua de nuevo y sus
    // pixeles se reacomodan hasta formar la palabra LISTO. Si la lista de
    // mods todavia se esta construyendo, la esmeralda sigue palpitando
    // (la animacion nunca se queda congelada al final).
    internal sealed class LoadingOverlay : Control
    {
        private const float PhaseSwingEnd = 0.12f;
        private const float PhasePickaxeMorphEnd = 0.40f;
        private const float PhaseEmeraldSettleEnd = 0.52f;
        private const float PhaseFinalMorphSpan = 0.28f;
        private const int PhaseWordHoldMs = 800;
        private const int PhaseFadeMs = 600;

        // Color base pedido para todos los pixeles. La esmeralda usa
        // variaciones ligeras del mismo tono para conservar su volumen.
        private static readonly Color PixelBase = ColorTranslator.FromHtml("#E6E4DF");
        private static readonly Color PixelSoft = ColorTranslator.FromHtml("#D6D3CC");
        private static readonly Color PixelDeep = ColorTranslator.FromHtml("#C2BFB8");
        private static readonly Color PixelBright = ColorTranslator.FromHtml("#F4F2EC");

        private static readonly string[] PickaxePixels =
        {
            "....KKKKK....",
            "...K.....KKM.",
            "....KKKK..MK.",
            "........K..K.",
            ".......K.K..K",
            "......K.K.K.K",
            ".....K.K..K.K",
            "....K.K...K.K",
            "...K.K....K.K",
            "..K.K......K.",
            ".K.K.........",
            "K.K..........",
            "KK..........."
        };

        private static readonly string[] EmeraldPixels =
        {
            "...7777...",
            "..7....7..",
            ".7.A....7.",
            "7.AA.....7",
            "7...AA...7",
            "7...AC.S.7",
            "7...AC.S.7",
            "7...CC.S.7",
            "7...C.S..7",
            ".7..SS..7.",
            "..7..S.7..",
            "...7777..."
        };

        // La palabra final tambien se dibuja pixel a pixel. Se toma del
        // texto localizable para no duplicar "Listo" en dos lugares.
        private static readonly string[] WordPixels = BuildWordPixels(
            (LoadingStrings.Ready ?? "Listo").ToUpperInvariant());

        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly Random _random = new Random();
        private Bitmap _snapshot;
        private Control _snapshotSource;
        private Font _textFont;
        private float _durationMs = 5500f;
        private double _contentReadyAtMs = double.MaxValue;
        private bool _contentReady;
        private bool _finishing;

        public LoadingOverlay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Palette.Bg;
            Dock = DockStyle.None;
            Margin = new Padding(0);
            TabStop = false;
            Visible = false;
            _timer.Interval = 16;
            _timer.Tick += delegate { Tick(); };
        }

        public event EventHandler Finished;

        public bool IsAnimating
        {
            get { return _timer.Enabled && Visible; }
        }

        public double DurationMs
        {
            get { return _durationMs; }
        }

        public double ElapsedMs
        {
            get { return _clock.Elapsed.TotalMilliseconds; }
        }

        public string CurrentText
        {
            get { return IsReadyPhase() ? LoadingStrings.Ready : LoadingStrings.Loading; }
        }

        public int HighlightedIndex
        {
            get
            {
                string text = LoadingStrings.Loading ?? "";
                if (text.Length == 0 || IsReadyPhase()) return -1;
                return (int)(_clock.ElapsedMilliseconds / 500) % text.Length;
            }
        }

        // La esmeralda se queda palpitando hasta que la lista esta lista.
        // Asi el ultimo tramo solo empieza cuando de verdad se puede decir
        // "Listo", incluso si hay cientos de mods.
        private double FinalMorphStartMs
        {
            get
            {
                if (!_contentReady) return double.MaxValue;
                return Math.Max(_durationMs * PhaseEmeraldSettleEnd,
                    _contentReadyAtMs);
            }
        }

        private double FinalMorphEndMs
        {
            get
            {
                if (!_contentReady) return double.MaxValue;
                return FinalMorphStartMs + _durationMs * PhaseFinalMorphSpan;
            }
        }

        public void Begin(Control snapshotSource)
        {
            _timer.Stop();
            _finishing = false;
            _contentReady = false;
            _contentReadyAtMs = double.MaxValue;
            _durationMs = 5000f + (float)_random.NextDouble() * 1000f;

            _snapshotSource = snapshotSource;
            CaptureSnapshot(snapshotSource);

            _clock.Restart();
            Visible = true;
            BringToFront();
            _timer.Start();
            Invalidate();
        }

        public void MarkContentReady()
        {
            if (_contentReady) return;
            _contentReady = true;
            _contentReadyAtMs = _clock.Elapsed.TotalMilliseconds;

            // La lista ya esta armada: se actualiza la foto que se vera en el
            // desvanecido final. Al estar virtualizada solo hay unas pocas
            // filas, asi que DrawToBitmap es rapido y no incluye la capa.
            RefreshSnapshotFromSource();
            Invalidate();
        }

        public void Cancel()
        {
            _timer.Stop();
            Visible = false;
            DisposeSnapshot();
            _snapshotSource = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                if (_textFont != null) { _textFont.Dispose(); _textFont = null; }
                DisposeSnapshot();
                _snapshotSource = null;
            }
            base.Dispose(disposing);
        }

        private void Tick()
        {
            Invalidate();
            if (!Visible) return;

            // La capa es hermana de la interfaz, no hija del contenido.
            // Si otra vista intenta pasar al frente, la devolvemos arriba
            // para que la animacion siempre quede por encima de la lista.
            if (Parent != null && Parent.Controls.GetChildIndex(this) != 0)
                BringToFront();

            double elapsed = _clock.Elapsed.TotalMilliseconds;
            double end = FinalMorphEndMs + PhaseWordHoldMs;
            if (!_contentReady || elapsed < end + PhaseFadeMs) return;

            _timer.Stop();
            Visible = false;
            DisposeSnapshot();
            if (!_finishing)
            {
                _finishing = true;
                if (Finished != null) Finished(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            double elapsed = _clock.Elapsed.TotalMilliseconds;
            bool readyPhase = IsReadyPhase();
            float fade = ComputeFade(elapsed);

            // Fondo: primero la foto de la ventana y encima la capa opaca.
            // Al final la capa baja su alfa y deja ver la interfaz real.
            if (fade < 0.999f && _snapshot != null)
            {
                g.DrawImage(_snapshot, new Rectangle(0, 0, Width, Height));
            }
            using (SolidBrush background = new SolidBrush(WithAlpha(Palette.Bg, fade)))
            {
                g.FillRectangle(background, ClientRectangle);
            }

            float barHeight = Math.Max(64f, Height * 0.115f);
            RectangleF bar = new RectangleF(0f, Height - barHeight, Width, barHeight);
            RectangleF stage = new RectangleF(0f, 0f, Width, bar.Top);

            DrawStageGlow(g, stage, fade);
            DrawBottomBar(g, bar, fade);

            float centerX = stage.Width / 2f;
            float centerY = stage.Height * 0.5f;
            float iconSize = Math.Max(96f, Math.Min(stage.Width, stage.Height) * 0.26f);
            iconSize = Math.Min(iconSize, stage.Height * 0.62f);
            float wordWidth = Math.Min(stage.Width * 0.62f, iconSize * 3.4f);
            float wordHeight = Math.Min(stage.Height * 0.34f, iconSize * 1.1f);

            double swingEnd = _durationMs * PhaseSwingEnd;
            double pickaxeMorphEnd = _durationMs * PhasePickaxeMorphEnd;
            double finalStart = FinalMorphStartMs;
            double finalEnd = FinalMorphEndMs;

            if (elapsed < swingEnd)
            {
                float u = Clamp01(elapsed / Math.Max(1.0, swingEnd));
                DrawSwingingPickaxe(g, centerX, centerY, iconSize, u, fade);
            }
            else if (elapsed < pickaxeMorphEnd)
            {
                float u = Clamp01((elapsed - swingEnd) /
                    Math.Max(1.0, pickaxeMorphEnd - swingEnd));
                DrawShapeMorph(g, PickaxePixels, EmeraldPixels, centerX, centerY,
                    iconSize, iconSize, iconSize, u, fade, 3);
            }
            else if (elapsed < finalStart)
            {
                // Esmeralda ya formada: pulso suave mientras se construye
                // la lista o mientras llega el turno del ultimo tramo.
                float pulse = 0.5f + 0.5f * (float)Math.Sin(elapsed / 260.0);
                DrawEmeraldHold(g, centerX, centerY, iconSize, pulse, fade);
            }
            else if (elapsed < finalEnd)
            {
                float u = Clamp01((elapsed - finalStart) /
                    Math.Max(1.0, finalEnd - finalStart));
                DrawShapeMorph(g, EmeraldPixels, WordPixels, centerX, centerY,
                    iconSize, wordWidth, wordHeight, u, fade, 5);
            }
            else
            {
                float pulse = 0.5f + 0.5f * (float)Math.Sin(
                    (elapsed - finalEnd) / 220.0);
                DrawWord(g, centerX, centerY, wordWidth, wordHeight, pulse, fade);
            }

            DrawLoadingText(g, bar, readyPhase, elapsed, fade);
        }

        private bool IsReadyPhase()
        {
            if (!_contentReady) return false;
            return _clock.Elapsed.TotalMilliseconds >= FinalMorphEndMs;
        }

        private float ComputeFade(double elapsed)
        {
            if (!_contentReady) return 1f;
            double fadeStart = FinalMorphEndMs + PhaseWordHoldMs;
            if (elapsed <= fadeStart) return 1f;
            return 1f - Clamp01((elapsed - fadeStart) / PhaseFadeMs);
        }

        private void CaptureSnapshot(Control source)
        {
            DisposeSnapshot();
            if (source == null || source.Width <= 0 || source.Height <= 0) return;

            try
            {
                // Capturar el area visible directamente de la pantalla es
                // instantaneo incluso con cientos de filas. DrawToBitmap
                // recorria todo el arbol y bloqueaba la interfaz ~2 s.
                Point origin = source.PointToScreen(Point.Empty);
                _snapshot = new Bitmap(source.Width, source.Height,
                    PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(_snapshot))
                {
                    graphics.CopyFromScreen(origin, Point.Empty, source.Size,
                        CopyPixelOperation.SourceCopy);
                }
            }
            catch
            {
                // Respaldo lento por si la ventana no esta visible.
                try
                {
                    _snapshot = new Bitmap(source.Width, source.Height,
                        PixelFormat.Format32bppPArgb);
                    source.DrawToBitmap(_snapshot,
                        new Rectangle(0, 0, _snapshot.Width, _snapshot.Height));
                }
                catch
                {
                    DisposeSnapshot();
                }
            }
        }

        // Recaptura el contenido ya armado para el desvanecido final. A
        // diferencia de CopyFromScreen, DrawToBitmap no incluye la capa de
        // carga (es hermana de la vista, no hija).
        private void RefreshSnapshotFromSource()
        {
            Control source = _snapshotSource;
            if (source == null || source.Width <= 0 || source.Height <= 0) return;

            Bitmap next = null;
            try
            {
                next = new Bitmap(source.Width, source.Height,
                    PixelFormat.Format32bppPArgb);
                source.DrawToBitmap(next,
                    new Rectangle(0, 0, next.Width, next.Height));
            }
            catch
            {
                if (next != null) { try { next.Dispose(); } catch { } }
                next = null;
            }

            if (next == null) return;
            DisposeSnapshot();
            _snapshot = next;
        }

        private void DisposeSnapshot()
        {
            if (_snapshot == null) return;
            try { _snapshot.Dispose(); } catch { }
            _snapshot = null;
        }

        private void DrawStageGlow(Graphics g, RectangleF stage, float fade)
        {
            float radius = Math.Min(stage.Width, stage.Height) * 0.42f;
            RectangleF glow = new RectangleF(
                stage.Width / 2f - radius,
                stage.Height / 2f - radius,
                radius * 2f, radius * 2f);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(glow);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = WithAlpha(Palette.Alpha(Palette.Accent, 46), fade);
                    brush.SurroundColors = new Color[]
                    {
                        WithAlpha(Palette.Alpha(Palette.Accent, 0), fade)
                    };
                    g.FillPath(brush, path);
                }
            }
        }

        private void DrawBottomBar(Graphics g, RectangleF bar, float fade)
        {
            using (SolidBrush fill = new SolidBrush(WithAlpha(Palette.Card, fade)))
            {
                g.FillRectangle(fill, bar);
            }
            using (SolidBrush line = new SolidBrush(WithAlpha(
                Palette.Alpha(Palette.Accent, 120), fade)))
            {
                g.FillRectangle(line, bar.X, bar.Y, bar.Width, 1f);
            }
        }

        private void DrawSwingingPickaxe(Graphics g, float centerX, float centerY,
            float iconSize, float u, float fade)
        {
            float angle = (float)Math.Sin(u * Math.PI * 2.0) * 21f;
            float scale = 0.82f + 0.18f * EaseOut(u);
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(centerX, centerY);
                g.RotateTransform(angle);
                g.ScaleTransform(scale, scale);
                DrawPixelIcon(g, -iconSize / 2f, -iconSize / 2f,
                    iconSize, iconSize, PickaxePixels, fade);
            }
            finally
            {
                g.Restore(state);
            }
        }

        // Derrite la figura de origen y reacomoda sus pixeles en la figura
        // de destino. Cada pixel primero cae (licuado) y despues vuela en
        // arco hasta su hueco en la nueva silueta.
        private void DrawShapeMorph(Graphics g, string[] source, string[] target,
            float centerX, float centerY, float sourceSize, float targetWidth,
            float targetHeight, float u, float fade, int salt)
        {
            List<PixelCell> sourceCells = new List<PixelCell>();
            CollectCells(source, centerX, centerY, sourceSize, sourceSize,
                sourceCells);
            if (sourceCells.Count == 0) return;

            int targetRows = target.Length;
            int targetCols = target[0].Length;
            float cell = Math.Min(targetWidth / targetCols, targetHeight / targetRows);
            float targetLeft = centerX - targetCols * cell / 2f;
            float targetTop = centerY - targetRows * cell / 2f;

            // La silueta original se desvanece mientras empieza a licuarse.
            float sourceAlpha = 1f - Clamp01(u / 0.10f);
            if (sourceAlpha > 0.01f)
            {
                DrawPixelIcon(g, centerX - sourceSize / 2f,
                    centerY - sourceSize / 2f, sourceSize, sourceSize,
                    source, sourceAlpha * fade);
            }

            SmoothingMode oldSmoothing = g.SmoothingMode;
            PixelOffsetMode oldOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                for (int row = 0; row < targetRows; row++)
                {
                    for (int col = 0; col < targetCols; col++)
                    {
                        Color targetColor = PixelColor(target[row][col]);
                        if (targetColor.IsEmpty) continue;

                        // Recorrido secuencial: garantiza que todos los
                        // pixeles del origen participen aunque el destino
                        // tenga mas celdas.
                        int sourceIndex = (row * targetCols + col + salt) %
                            sourceCells.Count;
                        PixelCell origin = sourceCells[sourceIndex];

                        float delay = Hash(row, col, salt + 3) * 0.16f;
                        float p = Clamp01((u - delay) /
                            Math.Max(0.01f, 1f - delay));

                        float melt = EaseOut(Clamp01(p / 0.32f));
                        float fly = EaseInOut(Clamp01((p - 0.32f) / 0.68f));

                        float meltX = origin.X + (Hash(row, col, salt + 5) - 0.5f) *
                            cell * 1.1f * melt;
                        float meltY = origin.Y + cell *
                            (0.55f + Hash(row, col, salt + 7) * 0.65f) * melt;

                        float targetX = targetLeft + col * cell + cell / 2f;
                        float targetY = targetTop + row * cell + cell / 2f;

                        float c1x = meltX + (Hash(row, col, salt + 11) - 0.5f) *
                            cell * 2.0f;
                        float c1y = meltY + cell * 1.4f;
                        float c2x = targetX + (Hash(row, col, salt + 13) - 0.5f) *
                            cell * 2.4f;
                        float c2y = targetY - cell * 1.8f;

                        float x = Cubic(meltX, c1x, c2x, targetX, fly);
                        float y = Cubic(meltY, c1y, c2y, targetY, fly);

                        float midShrink = (float)Math.Sin(fly * Math.PI);
                        float size = (origin.Size + (cell - origin.Size) * fly) *
                            (1f - 0.30f * midShrink);
                        float alpha = 1f - 0.22f * midShrink;

                        Color color = Palette.Mix(origin.Color, targetColor, fly);
                        brush.Color = WithAlpha(color, Clamp01(alpha) * fade);
                        g.FillRectangle(brush, x - size / 2f, y - size / 2f,
                            Math.Max(1.5f, size), Math.Max(1.5f, size));
                    }
                }
            }

            g.SmoothingMode = oldSmoothing;
            g.PixelOffsetMode = oldOffset;
        }

        private void DrawEmeraldHold(Graphics g, float centerX, float centerY,
            float iconSize, float pulse, float fade)
        {
            float scale = 1f + 0.022f * (pulse * 2f - 1f);
            float glowRadius = iconSize * (0.62f + 0.05f * pulse);

            // Halo radial suave: no debe leerse como una tarjeta detras
            // del sprite, solo como luz del escenario.
            RectangleF glow = new RectangleF(centerX - glowRadius,
                centerY - glowRadius, glowRadius * 2f, glowRadius * 2f);
            using (GraphicsPath glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(glow);
                using (PathGradientBrush glowBrush = new PathGradientBrush(glowPath))
                {
                    glowBrush.CenterColor = WithAlpha(
                        Palette.Alpha(Palette.Accent, (int)(30 + 24 * pulse)), fade);
                    glowBrush.SurroundColors = new Color[]
                    {
                        WithAlpha(Palette.Alpha(Palette.Accent, 0), fade)
                    };
                    glowBrush.FocusScales = new PointF(0.42f, 0.42f);
                    g.FillPath(glowBrush, glowPath);
                }
            }

            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(centerX, centerY);
                g.ScaleTransform(scale, scale);
                DrawPixelIcon(g, -iconSize / 2f, -iconSize / 2f,
                    iconSize, iconSize, EmeraldPixels, fade);
            }
            finally
            {
                g.Restore(state);
            }
        }

        private void DrawWord(Graphics g, float centerX, float centerY,
            float width, float height, float pulse, float fade)
        {
            int rows = WordPixels.Length;
            int cols = WordPixels[0].Length;
            float cell = Math.Min(width / cols, height / rows);
            float startX = centerX - cols * cell / 2f;
            float startY = centerY - rows * cell / 2f;

            // Un brillo muy leve para que la palabra respire sin cambiar
            // el color base pedido.
            Color color = Palette.Mix(PixelBase, PixelBright, 0.25f + 0.35f * pulse);

            SmoothingMode oldSmoothing = g.SmoothingMode;
            PixelOffsetMode oldOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            using (SolidBrush brush = new SolidBrush(WithAlpha(color, fade)))
            {
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        if (PixelColor(WordPixels[row][col]).IsEmpty) continue;
                        g.FillRectangle(brush, startX + col * cell,
                            startY + row * cell, cell, cell);
                    }
                }
            }

            g.SmoothingMode = oldSmoothing;
            g.PixelOffsetMode = oldOffset;
        }

        private void DrawLoadingText(Graphics g, RectangleF bar, bool ready,
            double elapsed, float fade)
        {
            if (_textFont == null) _textFont = Fonts.UI(15f, FontStyle.Bold);
            string text = ready ? LoadingStrings.Ready : LoadingStrings.Loading;
            if (string.IsNullOrEmpty(text)) return;

            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
                format.Alignment = StringAlignment.Near;

                float[] widths = new float[text.Length];
                float total = 0f;
                for (int i = 0; i < text.Length; i++)
                {
                    widths[i] = g.MeasureString(text[i].ToString(), _textFont,
                        new PointF(0f, 0f), format).Width;
                    total += widths[i];
                    if (i > 0) total += 0.6f;
                }

                float x = (Width - total) / 2f;
                float lineHeight = _textFont.GetHeight(g);
                float y = bar.Top + (bar.Height - lineHeight) / 2f - lineHeight * 0.08f;

                int highlighted = -1;
                if (!ready && text.Length > 0)
                    highlighted = (int)(elapsed / 500.0) % text.Length;

                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        bool active = ready || i == highlighted;
                        Color color = active ? Palette.Accent : Palette.Muted;
                        brush.Color = WithAlpha(color, fade);
                        g.DrawString(text[i].ToString(), _textFont, brush, x, y, format);

                        if (!ready && i == highlighted)
                        {
                            using (SolidBrush underline = new SolidBrush(
                                WithAlpha(Palette.Accent, 0.8f * fade)))
                            {
                                g.FillRectangle(underline, x + 1f,
                                    y + lineHeight * 0.9f,
                                    Math.Max(2f, widths[i] - 2f), 2f);
                            }
                        }
                        x += widths[i] + 0.6f;
                    }
                }
            }
        }

        private static void CollectCells(string[] pixels, float centerX,
            float centerY, float width, float height, List<PixelCell> cells)
        {
            int rows = pixels.Length;
            int cols = pixels[0].Length;
            float cell = Math.Min(width / cols, height / rows);
            float startX = centerX - cols * cell / 2f;
            float startY = centerY - rows * cell / 2f;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    Color color = PixelColor(pixels[row][col]);
                    if (color.IsEmpty) continue;

                    PixelCell cellInfo = new PixelCell();
                    cellInfo.Row = row;
                    cellInfo.Col = col;
                    cellInfo.X = startX + col * cell + cell / 2f;
                    cellInfo.Y = startY + row * cell + cell / 2f;
                    cellInfo.Size = cell;
                    cellInfo.Color = color;
                    cells.Add(cellInfo);
                }
            }
        }

        private void DrawPixelIcon(Graphics g, float x, float y, float w, float h,
            string[] pixels, float fade)
        {
            int rows = pixels.Length;
            int cols = pixels[0].Length;

            float size = Math.Min(w / cols, h / rows);
            float startX = x + (w - cols * size) / 2f;
            float startY = y + (h - rows * size) / 2f;

            SmoothingMode oldMode = g.SmoothingMode;
            PixelOffsetMode oldPixelMode = g.PixelOffsetMode;

            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        Color color = PixelColor(pixels[row][col]);
                        if (color.IsEmpty) continue;

                        brush.Color = WithAlpha(color, fade);
                        g.FillRectangle(brush,
                            startX + col * size,
                            startY + row * size,
                            size,
                            size);
                    }
                }
            }

            g.SmoothingMode = oldMode;
            g.PixelOffsetMode = oldPixelMode;
        }

        private static Color PixelColor(char pixel)
        {
            switch (pixel)
            {
                case 'K': return PixelBase;
                case 'M': return PixelBright;
                case '7': return PixelDeep;
                case 'S': return PixelSoft;
                case 'A': return PixelBase;
                case 'C': return PixelBright;
                case '#': return PixelBase;
                default: return Color.Empty;
            }
        }

        private static string[] BuildWordPixels(string word)
        {
            if (string.IsNullOrEmpty(word)) word = "LISTO";
            const int rows = 7;
            List<string> lines = new List<string>();
            for (int row = 0; row < rows; row++)
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < word.Length; i++)
                {
                    if (i > 0) builder.Append('.');
                    builder.Append(GlyphRows(word[i])[row]);
                }
                lines.Add(builder.ToString());
            }
            return lines.ToArray();
        }

        // Fuente pixelada 5x7 para las letras de "LISTO".
        private static string[] GlyphRows(char letter)
        {
            switch (letter)
            {
                case 'L':
                    return new[]
                    {
                        "#....", "#....", "#....", "#....",
                        "#....", "#....", "#####"
                    };
                case 'I':
                    return new[]
                    {
                        "#####", "..#..", "..#..", "..#..",
                        "..#..", "..#..", "#####"
                    };
                case 'S':
                    return new[]
                    {
                        ".####", "#....", "#....", ".###.",
                        "....#", "....#", "####."
                    };
                case 'T':
                    return new[]
                    {
                        "#####", "..#..", "..#..", "..#..",
                        "..#..", "..#..", "..#.."
                    };
                case 'O':
                    return new[]
                    {
                        ".###.", "#...#", "#...#", "#...#",
                        "#...#", "#...#", ".###."
                    };
                case ' ':
                    return new[]
                    {
                        ".....", ".....", ".....", ".....",
                        ".....", ".....", "....."
                    };
                default:
                    // Respaldo visible por si se traduce Ready con otra letra.
                    return new[]
                    {
                        "#####", "#...#", "#...#", "#...#",
                        "#...#", "#...#", "#####"
                    };
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            int a = (int)Math.Round(color.A * Clamp01(alpha));
            return Color.FromArgb(a, color.R, color.G, color.B);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static float Clamp01(double value)
        {
            if (value < 0.0) return 0f;
            if (value > 1.0) return 1f;
            return (float)value;
        }

        private static float EaseOut(float value)
        {
            float inverse = 1f - Clamp01(value);
            return 1f - inverse * inverse;
        }

        private static float EaseInOut(float value)
        {
            value = Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float Cubic(float p0, float p1, float p2, float p3, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * p0 +
                3f * inverse * inverse * t * p1 +
                3f * inverse * t * t * p2 +
                t * t * t * p3;
        }

        private static float Hash(int row, int col, int salt)
        {
            unchecked
            {
                uint h = (uint)(row * 73856093) ^ (uint)(col * 19349663) ^
                    (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private struct PixelCell
        {
            public int Row;
            public int Col;
            public float X;
            public float Y;
            public float Size;
            public Color Color;
        }
    }
}
