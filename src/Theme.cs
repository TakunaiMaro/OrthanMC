using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace McPanel
{
    internal static class Palette
    {
        // Los colores de superficie cambian con el tema. Se exponen como
        // propiedades para que todo lo que se pinta en tiempo real use el
        // valor vigente, no una copia del tema anterior.
        private static Color _bg = ColorTranslator.FromHtml("#0B0E14");
        private static Color _sidebar = ColorTranslator.FromHtml("#0D1117");
        private static Color _card = ColorTranslator.FromHtml("#11161F");
        private static Color _cardAlt = ColorTranslator.FromHtml("#0F141C");
        private static Color _input = ColorTranslator.FromHtml("#0B1017");
        private static Color _border = ColorTranslator.FromHtml("#222B36");
        private static Color _borderSoft = ColorTranslator.FromHtml("#1A222C");

        public static Color Bg { get { return _bg; } }
        // Barra de titulo: un tono mas oscuro del fondo del tema.
        public static Color TitleBar { get { return Shade(_bg, 65); } }
        public static Color Sidebar { get { return _sidebar; } }
        public static Color Card { get { return _card; } }
        public static Color CardAlt { get { return _cardAlt; } }
        public static Color Input { get { return _input; } }
        public static Color Border { get { return _border; } }
        public static Color BorderSoft { get { return _borderSoft; } }

        public static readonly Color Text = ColorTranslator.FromHtml("#E6EDF3");
        public static readonly Color Muted = ColorTranslator.FromHtml("#8B949E");
        public static readonly Color Dim = ColorTranslator.FromHtml("#6E7681");

        // El acento cambia con el tema. Se calcula como complemento del
        // fondo principal; Default/End conservan el verde de marca.
        private static readonly Color BaseAccent = ColorTranslator.FromHtml("#57B84A");
        private static Color _accent = BaseAccent;
        private static Color _accentHover = Mix(BaseAccent, Color.White, 0.16f);
        private static Color _accentDim = Mix(BaseAccent, _bg, 0.82f);
        public static Color Accent { get { return _accent; } }
        public static Color AccentHover { get { return _accentHover; } }
        public static Color AccentDim { get { return _accentDim; } }
        public static readonly Color Red = ColorTranslator.FromHtml("#F85149");
        public static readonly Color RedHover = ColorTranslator.FromHtml("#FF6B63");
        public static readonly Color RedDim = ColorTranslator.FromHtml("#3A1B1B");
        public static readonly Color Amber = ColorTranslator.FromHtml("#D29922");
        public static readonly Color AmberDim = ColorTranslator.FromHtml("#3A2D12");
        public static readonly Color Blue = ColorTranslator.FromHtml("#58A6FF");
        public static readonly Color BlueDim = ColorTranslator.FromHtml("#132A46");
        public static readonly Color Teal = ColorTranslator.FromHtml("#39C5BB");
        public static readonly Color Purple = ColorTranslator.FromHtml("#BC8CFF");
        public static readonly Color White = ColorTranslator.FromHtml("#FFFFFF");

        public static void ApplyTheme(AppTheme theme)
        {
            if (theme == AppTheme.Overworld)
            {
                _bg = ColorTranslator.FromHtml("#0B1410");
                _sidebar = ColorTranslator.FromHtml("#0D1612");
                _card = ColorTranslator.FromHtml("#121D17");
                _cardAlt = ColorTranslator.FromHtml("#101A14");
                _input = ColorTranslator.FromHtml("#08110C");
                _border = ColorTranslator.FromHtml("#243229");
                _borderSoft = ColorTranslator.FromHtml("#19241C");
            }
            else if (theme == AppTheme.Nether)
            {
                _bg = ColorTranslator.FromHtml("#140B0B");
                _sidebar = ColorTranslator.FromHtml("#1A0F0F");
                _card = ColorTranslator.FromHtml("#201413");
                _cardAlt = ColorTranslator.FromHtml("#1B1110");
                _input = ColorTranslator.FromHtml("#150B0B");
                _border = ColorTranslator.FromHtml("#392321");
                _borderSoft = ColorTranslator.FromHtml("#2A1918");
            }
            else if (theme == AppTheme.End)
            {
                _bg = ColorTranslator.FromHtml("#0F0B14");
                _sidebar = ColorTranslator.FromHtml("#14101A");
                _card = ColorTranslator.FromHtml("#19141F");
                _cardAlt = ColorTranslator.FromHtml("#16111B");
                _input = ColorTranslator.FromHtml("#100B16");
                _border = ColorTranslator.FromHtml("#2B2137");
                _borderSoft = ColorTranslator.FromHtml("#21182A");
            }
            else
            {
                _bg = ColorTranslator.FromHtml("#0B0E14");
                _sidebar = ColorTranslator.FromHtml("#0D1117");
                _card = ColorTranslator.FromHtml("#11161F");
                _cardAlt = ColorTranslator.FromHtml("#0F141C");
                _input = ColorTranslator.FromHtml("#0B1017");
                _border = ColorTranslator.FromHtml("#222B36");
                _borderSoft = ColorTranslator.FromHtml("#1A222C");
            }
            SetAccent(AccentFor(theme));
        }

        // Acento complementario por tema. Los tonos de Overworld y Nether
        // ya estan ajustados para tener contraste suficiente sobre sus
        // respectivos fondos. Default, End y panoramicas conservan el verde.
        private static Color AccentFor(AppTheme theme)
        {
            if (theme == AppTheme.Overworld) return ColorTranslator.FromHtml("#973FBA");
            if (theme == AppTheme.Nether) return ColorTranslator.FromHtml("#ED9339");
            if (theme == AppTheme.Default || theme == AppTheme.End ||
                theme == AppTheme.CustomPanorama) return BaseAccent;
            return Complementary(_bg);
        }

        private static void SetAccent(Color accent)
        {
            _accent = accent;
            _accentHover = Mix(accent, Color.White, 0.16f);
            _accentDim = Mix(accent, _bg, 0.82f);
        }

        // Complemento HSL de cualquier color. Se usa como utilidad para
        // fondos nuevos o personalizados.
        public static Color Complementary(Color color)
        {
            double h, s, l;
            ToHsl(color, out h, out s, out l);
            h = (h + 180.0) % 360.0;
            if (s < 0.45) s = 0.45;
            if (l < 0.45) l = 0.45;
            else if (l > 0.58) l = 0.58;
            return FromHsl(h, s, l);
        }

        private static void ToHsl(Color color, out double h, out double s, out double l)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2.0;
            if (max == min)
            {
                h = 0.0;
                s = 0.0;
                return;
            }
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = 60.0 * (((g - b) / d) % 6.0);
            else if (max == g) h = 60.0 * (((b - r) / d) + 2.0);
            else h = 60.0 * (((r - g) / d) + 4.0);
            if (h < 0.0) h += 360.0;
        }

        private static Color FromHsl(double h, double s, double l)
        {
            h = ((h % 360.0) + 360.0) % 360.0;
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            double m = l - c / 2.0;
            double r = 0.0, g = 0.0, b = 0.0;
            if (h < 60.0) { r = c; g = x; }
            else if (h < 120.0) { r = x; g = c; }
            else if (h < 180.0) { g = c; b = x; }
            else if (h < 240.0) { g = x; b = c; }
            else if (h < 300.0) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromArgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
        }

        private static int ToByte(double value)
        {
            int v = (int)Math.Round(value * 255.0);
            if (v < 0) return 0;
            if (v > 255) return 255;
            return v;
        }

        public static Color Mix(Color a, Color b, float amount)
        {
            if (amount < 0f) amount = 0f;
            if (amount > 1f) amount = 1f;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }

        public static Color Alpha(Color c, int alpha)
        {
            return Color.FromArgb(alpha, c);
        }

        // Color.Transparent es (0,255,255,255): mezclarlo produce blancos
        // inesperados. Este helper devuelve un color solido para pintar.
        private static Color Shade(Color color, int percent)
        {
            int r = color.R * percent / 100;
            int g = color.G * percent / 100;
            int b = color.B * percent / 100;
            return Color.FromArgb(color.A, r, g, b);
        }

        public static Color Solid(Color c, Color fallback)
        {
            return c.A == 255 ? c : fallback;
        }

        // Devuelve el primer fondo solido del arbol. Sirve para pintar las
        // esquinas de controles redondeados con el color real de atras y no
        // dejar triangulos con restos del tema anterior.
        public static Color Backdrop(Control control, Color fallback)
        {
            Control parent = control != null ? control.Parent : null;
            while (parent != null)
            {
                if (parent.BackColor.A == 255) return parent.BackColor;
                parent = parent.Parent;
            }
            return fallback;
        }

        // Limpia el fondo de un control: usa el panorama cuando el tema
        // CustomPanorama esta activo y el control vive dentro del panel
        // anfitrion; si no, pinta el color solido de siempre.
        public static void PaintBackdrop(Graphics g, Control control, Color fallback)
        {
            if (g == null) return;
            if (!PanoramaBackdrop.TryPaint(g, control)) g.Clear(fallback);
        }
    }

    internal static class Fonts
    {
        private const string UiFamily = "Segoe UI";
        private const string MonoFamily = "Consolas";

        public static Font UI(float size)
        {
            return new Font(UiFamily, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font UI(float size, FontStyle style)
        {
            return new Font(UiFamily, size, style, GraphicsUnit.Point);
        }

        public static Font Mono(float size)
        {
            return new Font(MonoFamily, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font Mono(float size, FontStyle style)
        {
            return new Font(MonoFamily, size, style, GraphicsUnit.Point);
        }
    }

    internal static class Draw
    {
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0f || r.Width <= 0f || r.Height <= 0f)
            {
                p.AddRectangle(r);
                p.CloseFigure();
                return p;
            }
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color color)
        {
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillPath(b, p);
            }
        }

        public static void StrokeRound(Graphics g, RectangleF r, float radius, Color color, float width)
        {
            RectangleF rr = r;
            rr.Inflate(-width / 2f, -width / 2f);
            using (GraphicsPath p = Round(rr, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, p);
            }
        }

        public static void Glyph(Graphics g, string kind, RectangleF r, Color color, float stroke)
        {
            if (string.IsNullOrEmpty(kind)) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(color, stroke))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float x = r.X;
                float y = r.Y;
                float w = r.Width;
                float h = r.Height;
                float cx = x + w / 2f;
                float cy = y + h / 2f;

                switch (kind)
                {
                    case "play":
                        {
                            PointF[] pts = new PointF[] {
                                new PointF(x + w * 0.30f, y + h * 0.18f),
                                new PointF(x + w * 0.82f, cy),
                                new PointF(x + w * 0.30f, y + h * 0.82f)
                            };
                            g.FillPolygon(brush, pts);
                            break;
                        }
                    case "stop":
                        {
                            RectangleF rr = new RectangleF(x + w * 0.25f, y + h * 0.25f, w * 0.5f, h * 0.5f);
                            using (GraphicsPath p = Round(rr, 2f)) g.FillPath(brush, p);
                            break;
                        }
                    case "restart":
                        {
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                PointF P(float px, float py)
                                {
                                    return new PointF(
                                        x + w * (px + 2.578125f) / 100f,
                                        y + h * (py * 1.119318182f + 29.014559659f) / 100f
                                    );
                                }

                                path.StartFigure();

                                path.AddBezier(
                                    P(16.44792f, 24.61460f),
                                    P(20.20484f, 31.22793f),
                                    P(24.60938f, 43.45654f),
                                    P(47.42188f, 43.45654f)
                                );
                                path.AddBezier(
                                    P(47.42188f, 43.45654f),
                                    P(77.55342f, 43.45654f),
                                    P(81.17188f, 27.14199f),
                                    P(81.17188f, 12.1875f)
                                );
                                path.AddBezier(
                                    P(81.17188f, 12.1875f),
                                    P(81.17188f, -11.875f),
                                    P(60.79897f, -19.08154f),
                                    P(47.42188f, -19.08154f)
                                );
                                path.AddBezier(
                                    P(47.42188f, -19.08154f),
                                    P(23.98438f, -19.08154f),
                                    P(16.15295f, 0.40980f),
                                    P(16.15295f, 0.40980f)
                                );
                                path.AddLine(
                                    P(16.15295f, 0.40980f),
                                    P(26.48438f, -2.74905f)
                                );
                                path.AddBezier(
                                    P(26.48438f, -2.74905f),
                                    P(26.48438f, -2.74905f),
                                    P(15.625f, 15f),
                                    P(15.625f, 15f)
                                );

                                path.AddLine(
                                    P(15.625f, 15f),
                                    P(6.484375f, -3.307424f)
                                );

                                path.AddLine(
                                    P(6.484375f, -3.307424f),
                                    P(16.15295f, 0.40980f)
                                );

                                g.DrawPath(pen, path);
                            }
                        break;
                        }
                    case "cloud":
                        {
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                path.StartFigure();

                                path.AddBezier(
                                    x + w * 0.18f, y + h * 0.72f,
                                    x + w * 0.04f, y + h * 0.68f,
                                    x + w * 0.04f, y + h * 0.43f,
                                    x + w * 0.20f, y + h * 0.36f
                                );

                                path.AddBezier(
                                    x + w * 0.20f, y + h * 0.36f,
                                    x + w * 0.22f, y + h * 0.12f,
                                    x + w * 0.48f, y + h * 0.08f,
                                    x + w * 0.55f, y + h * 0.32f
                                );

                                path.AddBezier(
                                    x + w * 0.55f, y + h * 0.32f,
                                    x + w * 0.70f, y + h * 0.16f,
                                    x + w * 0.92f, y + h * 0.28f,
                                    x + w * 0.88f, y + h * 0.48f
                                );

                                path.AddBezier(
                                    x + w * 0.88f, y + h * 0.48f,
                                    x + w * 1.02f, y + h * 0.58f,
                                    x + w * 0.94f, y + h * 0.78f,
                                    x + w * 0.76f, y + h * 0.78f
                                );

                                path.AddBezier(
                                    x + w * 0.76f, y + h * 0.78f,
                                    x + w * 0.55f, y + h * 0.88f,
                                    x + w * 0.35f, y + h * 0.85f,
                                    x + w * 0.25f, y + h * 0.76f
                                );

                                path.AddBezier(
                                    x + w * 0.25f, y + h * 0.76f,
                                    x + w * 0.21f, y + h * 0.76f,
                                    x + w * 0.18f, y + h * 0.75f,
                                    x + w * 0.18f, y + h * 0.72f
                                );

                                path.CloseFigure();

                                g.DrawPath(pen, path);
                                break;
                            }
                        }
                    case "brush":
                        {
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                path.StartFigure();
                                path.AddBezier(
                                    x + w * 0.18f, y + h * 0.16f,
                                    x + w * 0.12f, y + h * 0.08f,
                                    x + w * 0.24f, y + h * 0.02f,
                                    x + w * 0.34f, y + h * 0.12f
                                );
                                path.AddLine(
                                    x + w * 0.34f, y + h * 0.12f,
                                    x + w * 0.72f, y + h * 0.50f
                                );
                                path.AddBezier(
                                    x + w * 0.72f, y + h * 0.50f,
                                    x + w * 0.78f, y + h * 0.56f,
                                    x + w * 0.80f, y + h * 0.64f,
                                    x + w * 0.74f, y + h * 0.70f
                                );
                                path.AddLine(
                                    x + w * 0.74f, y + h * 0.70f,
                                    x + w * 0.40f, y + h * 0.96f
                                );
                                path.AddBezier(
                                    x + w * 0.40f, y + h * 0.96f,
                                    x + w * 0.30f, y + h * 1.00f,
                                    x + w * 0.24f, y + h * 0.88f,
                                    x + w * 0.30f, y + h * 0.80f
                                );
                                path.AddLine(
                                    x + w * 0.30f, y + h * 0.80f,
                                    x + w * 0.58f, y + h * 0.52f
                                );
                                path.AddLine(
                                    x + w * 0.58f, y + h * 0.52f,
                                    x + w * 0.18f, y + h * 0.16f
                                );
                                path.CloseFigure();
                                g.DrawPath(pen, path);
                                g.DrawLine(
                                    pen,
                                    x + w * 0.27f, y + h * 0.23f,
                                    x + w * 0.38f, y + h * 0.12f
                                );
                                break;
                            }
                        }
                    case "pulse":
                        {
                            PointF[] pts = new PointF[] {
                                new PointF(x + w * 0.08f, y + h * 0.55f),
                                new PointF(x + w * 0.28f, y + h * 0.55f),
                                new PointF(x + w * 0.40f, y + h * 0.24f),
                                new PointF(x + w * 0.56f, y + h * 0.78f),
                                new PointF(x + w * 0.68f, y + h * 0.45f),
                                new PointF(x + w * 0.92f, y + h * 0.45f)
                            };
                            g.DrawLines(pen, pts);
                            break;
                        }
                    case "terminal":
                        {
                            RectangleF rr = new RectangleF(x + w * 0.08f, y + h * 0.14f, w * 0.84f, h * 0.72f);
                            using (GraphicsPath p = Round(rr, 3f)) g.DrawPath(pen, p);
                            g.DrawLines(pen, new PointF[] {
                                new PointF(x + w * 0.25f, y + h * 0.38f),
                                new PointF(x + w * 0.40f, y + h * 0.50f),
                                new PointF(x + w * 0.25f, y + h * 0.62f)
                            });
                            g.DrawLine(pen, new PointF(x + w * 0.48f, y + h * 0.65f), new PointF(x + w * 0.72f, y + h * 0.65f));
                            break;
                        }
                    case "sliders":
                        {
                            float[] ys = new float[] { y + h * 0.25f, y + h * 0.50f, y + h * 0.75f };
                            float[] kx = new float[] { x + w * 0.66f, x + w * 0.36f, x + w * 0.56f };
                            for (int i = 0; i < 3; i++)
                            {
                                g.DrawLine(pen, x + w * 0.12f, ys[i], x + w * 0.88f, ys[i]);
                                g.FillEllipse(brush, kx[i] - w * 0.08f, ys[i] - w * 0.08f, w * 0.16f, w * 0.16f);
                            }
                            break;
                        }
                    case "folder":
                        {
                            GraphicsPath p = new GraphicsPath();
                            p.AddLines(new PointF[] {
                                new PointF(x + w * 0.10f, y + h * 0.28f),
                                new PointF(x + w * 0.42f, y + h * 0.28f),
                                new PointF(x + w * 0.50f, y + h * 0.38f),
                                new PointF(x + w * 0.90f, y + h * 0.38f),
                                new PointF(x + w * 0.90f, y + h * 0.78f),
                                new PointF(x + w * 0.10f, y + h * 0.78f)
                            });
                            p.CloseFigure();
                            g.DrawPath(pen, p);
                            p.Dispose();
                            break;
                        }
                    case "copy":
                        {
                            RectangleF back = new RectangleF(x + w * 0.30f, y + h * 0.16f, w * 0.54f, h * 0.54f);
                            RectangleF front = new RectangleF(x + w * 0.16f, y + h * 0.30f, w * 0.54f, h * 0.54f);
                            using (GraphicsPath p = Round(back, 2f)) g.DrawPath(pen, p);
                            using (GraphicsPath p = Round(front, 2f)) g.DrawPath(pen, p);
                            break;
                        }
                    case "send":
                        {
                            PointF[] pts = new PointF[] {
                                new PointF(x + w * 0.10f, y + h * 0.50f),
                                new PointF(x + w * 0.88f, y + h * 0.15f),
                                new PointF(x + w * 0.58f, y + h * 0.85f),
                                new PointF(x + w * 0.47f, y + h * 0.58f)
                            };
                            g.DrawPolygon(pen, pts);
                            g.DrawLine(pen, new PointF(x + w * 0.47f, y + h * 0.58f), new PointF(x + w * 0.88f, y + h * 0.15f));
                            break;
                        }
                    case "trash":
                        {
                            g.DrawLine(pen, x + w * 0.20f, y + h * 0.26f, x + w * 0.80f, y + h * 0.26f);
                            g.DrawLine(pen, x + w * 0.40f, y + h * 0.26f, x + w * 0.43f, y + h * 0.15f);
                            g.DrawLine(pen, x + w * 0.60f, y + h * 0.26f, x + w * 0.57f, y + h * 0.15f);
                            g.DrawLine(pen, x + w * 0.43f, y + h * 0.15f, x + w * 0.57f, y + h * 0.15f);
                            g.DrawLines(pen, new PointF[] {
                                new PointF(x + w * 0.28f, y + h * 0.30f),
                                new PointF(x + w * 0.33f, y + h * 0.86f),
                                new PointF(x + w * 0.67f, y + h * 0.86f),
                                new PointF(x + w * 0.72f, y + h * 0.30f)
                            });
                            g.DrawLine(pen, x + w * 0.44f, y + h * 0.42f, x + w * 0.46f, y + h * 0.74f);
                            g.DrawLine(pen, x + w * 0.56f, y + h * 0.42f, x + w * 0.54f, y + h * 0.74f);
                            break;
                        }
                    case "save":
                        {
                            RectangleF rr = new RectangleF(x + w * 0.14f, y + h * 0.14f, w * 0.72f, h * 0.72f);
                            g.DrawRectangle(pen, rr.X, rr.Y, rr.Width, rr.Height);
                            g.DrawRectangle(pen, x + w * 0.32f, y + h * 0.14f, w * 0.36f, h * 0.26f);
                            g.DrawRectangle(pen, x + w * 0.28f, y + h * 0.56f, w * 0.44f, h * 0.30f);
                            break;
                        }
                    case "search":
                        {
                            g.DrawEllipse(pen, x + w * 0.14f, y + h * 0.14f, w * 0.52f, h * 0.52f);
                            g.DrawLine(pen, x + w * 0.60f, y + h * 0.60f, x + w * 0.86f, y + h * 0.86f);
                            break;
                        }
                    case "users":
                        {
                            g.DrawEllipse(pen, x + w * 0.34f, y + h * 0.16f, w * 0.30f, h * 0.30f);
                            g.DrawArc(pen, x + w * 0.20f, y + h * 0.52f, w * 0.58f, h * 0.42f, 180f, 180f);
                            g.DrawEllipse(pen, x + w * 0.08f, y + h * 0.26f, w * 0.20f, h * 0.20f);
                            g.DrawArc(pen, x + w * 0.02f, y + h * 0.55f, w * 0.34f, h * 0.30f, 180f, 180f);
                            break;
                        }
                    case "chip":
                    case "ram":
                        {
                            float ix = x + w * 0.24f;
                            float iy = y + h * 0.24f;
                            float iw = w * 0.52f;
                            float ih = h * 0.52f;
                            g.DrawRectangle(pen, ix, iy, iw, ih);
                            for (int i = 0; i < 3; i++)
                            {
                                float px = ix + iw * (0.22f + i * 0.28f);
                                g.DrawLine(pen, px, y + h * 0.08f, px, iy);
                                g.DrawLine(pen, px, iy + ih, px, y + h * 0.92f);
                                float py = iy + ih * (0.22f + i * 0.28f);
                                g.DrawLine(pen, x + w * 0.08f, py, ix, py);
                                g.DrawLine(pen, ix + iw, py, x + w * 0.92f, py);
                            }
                            break;
                        }
                    case "globe":
                        {
                            g.DrawEllipse(pen, x + w * 0.12f, y + h * 0.12f, w * 0.76f, h * 0.76f);
                            g.DrawEllipse(pen, x + w * 0.32f, y + h * 0.12f, w * 0.36f, h * 0.76f);
                            g.DrawLine(pen, x + w * 0.12f, cy, x + w * 0.88f, cy);
                            break;
                        }
                    case "clock":
                        {
                            g.DrawEllipse(pen, x + w * 0.12f, y + h * 0.12f, w * 0.76f, h * 0.76f);
                            g.DrawLine(pen, cx, cy, cx, y + h * 0.30f);
                            g.DrawLine(pen, cx, cy, x + w * 0.66f, y + h * 0.60f);
                            break;
                        }
                    case "link":
                        {
                            g.DrawArc(pen, x + w * 0.08f, y + h * 0.34f, w * 0.44f, h * 0.34f, 40f, 220f);
                            g.DrawArc(pen, x + w * 0.48f, y + h * 0.34f, w * 0.44f, h * 0.34f, 220f, 220f);
                            g.DrawLine(pen, x + w * 0.36f, y + h * 0.50f, x + w * 0.64f, y + h * 0.50f);
                            break;
                        }
                    case "warning":
                        {
                            g.DrawPolygon(pen, new PointF[] {
                                new PointF(cx, y + h * 0.10f),
                                new PointF(x + w * 0.92f, y + h * 0.86f),
                                new PointF(x + w * 0.08f, y + h * 0.86f)
                            });
                            g.DrawLine(pen, cx, y + h * 0.38f, cx, y + h * 0.60f);
                            g.FillEllipse(brush, cx - stroke * 0.7f, y + h * 0.69f, stroke * 1.4f, stroke * 1.4f);
                            break;
                        }
                    case "check":
                        {
                            g.DrawLines(pen, new PointF[] {
                                new PointF(x + w * 0.15f, y + h * 0.53f),
                                new PointF(x + w * 0.42f, y + h * 0.78f),
                                new PointF(x + w * 0.86f, y + h * 0.24f)
                            });
                            break;
                        }
                    case "close":
                        {
                            g.DrawLine(pen, x + w * 0.24f, y + h * 0.24f, x + w * 0.76f, y + h * 0.76f);
                            g.DrawLine(pen, x + w * 0.76f, y + h * 0.24f, x + w * 0.24f, y + h * 0.76f);
                            break;
                        }
                    case "external":
                        {
                            g.DrawLines(pen, new PointF[] {
                                new PointF(x + w * 0.52f, y + h * 0.16f),
                                new PointF(x + w * 0.16f, y + h * 0.16f),
                                new PointF(x + w * 0.16f, y + h * 0.84f),
                                new PointF(x + w * 0.84f, y + h * 0.84f),
                                new PointF(x + w * 0.84f, y + h * 0.50f)
                            });
                            g.DrawLine(pen, x + w * 0.50f, y + h * 0.50f, x + w * 0.92f, y + h * 0.14f);
                            g.DrawLine(pen, x + w * 0.68f, y + h * 0.14f, x + w * 0.92f, y + h * 0.14f);
                            g.DrawLine(pen, x + w * 0.92f, y + h * 0.14f, x + w * 0.92f, y + h * 0.38f);
                            break;
                        }
                    case "power":
                        {
                            g.DrawArc(pen, x + w * 0.16f, y + h * 0.18f, w * 0.68f, h * 0.68f, -60f, 300f);
                            g.DrawLine(pen, cx, y + h * 0.10f, cx, y + h * 0.48f);
                            break;
                        }
                    case "chevron":
                        {
                            g.DrawLines(pen, new PointF[] {
                                new PointF(x + w * 0.25f, y + h * 0.40f),
                                new PointF(cx, y + h * 0.65f),
                                new PointF(x + w * 0.75f, y + h * 0.40f)
                            });
                            break;
                        }
                    case "arrow-right":
                        {
                            g.DrawLine(pen, x + w * 0.15f, cy, x + w * 0.85f, cy);
                            g.DrawLines(pen, new PointF[] {
                                new PointF(x + w * 0.62f, y + h * 0.28f),
                                new PointF(x + w * 0.85f, cy),
                                new PointF(x + w * 0.62f, y + h * 0.72f)
                            });
                            break;
                        }
                    case "back":
                        {
                            g.DrawLine(pen, x + w * 0.85f, cy, x + w * 0.15f, cy);
                            g.DrawLines(pen, new PointF[] {
                                new PointF(x + w * 0.38f, y + h * 0.28f),
                                new PointF(x + w * 0.15f, cy),
                                new PointF(x + w * 0.38f, y + h * 0.72f)
                            });
                            break;
                        }
                    case "plug":
                        {
                            g.DrawArc(pen, x + w * 0.22f, y + h * 0.42f, w * 0.56f, h * 0.44f, 0f, 180f);
                            g.DrawLine(pen, x + w * 0.22f, y + h * 0.64f, x + w * 0.78f, y + h * 0.64f);
                            g.DrawLine(pen, x + w * 0.34f, y + h * 0.42f, x + w * 0.34f, y + h * 0.18f);
                            g.DrawLine(pen, x + w * 0.66f, y + h * 0.42f, x + w * 0.66f, y + h * 0.18f);
                            g.DrawLine(pen, cx, y + h * 0.42f, cx, y + h * 0.10f);
                            break;
                        }
                    case "grid":
                        {
                            float gap = w * 0.08f;
                            float size = (w - gap * 3f) / 2f;
                            using (GraphicsPath p = Round(new RectangleF(x + gap, y + h * 0.12f, size, size), 2.5f)) g.DrawPath(pen, p);
                            using (GraphicsPath p = Round(new RectangleF(x + gap * 2f + size, y + h * 0.12f, size, size), 2.5f)) g.DrawPath(pen, p);
                            using (GraphicsPath p = Round(new RectangleF(x + gap, y + h * 0.12f + size + gap, size, size), 2.5f)) g.DrawPath(pen, p);
                            using (GraphicsPath p = Round(new RectangleF(x + gap * 2f + size, y + h * 0.12f + size + gap, size, size), 2.5f)) g.DrawPath(pen, p);
                            break;
                        }
                    case "sort":
                        {
                            float[] ys = new float[] { y + h * 0.28f, y + h * 0.50f, y + h * 0.72f };
                            float[] lengths = new float[] { 0.62f, 0.44f, 0.26f };
                            for (int i = 0; i < 3; i++)
                            {
                                g.DrawLine(pen, x + w * 0.18f, ys[i], x + w * 0.18f + w * lengths[i], ys[i]);
                                g.FillEllipse(brush, x + w * 0.18f + w * lengths[i] - w * 0.055f,
                                    ys[i] - w * 0.055f, w * 0.11f, w * 0.11f);
                            }
                            break;
                        }
                    case "dot":
                        {
                            g.FillEllipse(brush, x + w * 0.30f, y + h * 0.30f, w * 0.40f, h * 0.40f);
                            break;
                        }
                }
            }
        }

        public static void MinecraftBlock(Graphics g, RectangleF r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = r.Width;
            float h = r.Height;
            float top = r.Y + h * 0.12f;
            float mid = r.Y + h * 0.36f;
            float bottom = r.Y + h * 0.88f;
            float cx = r.X + w * 0.50f;
            float left = r.X + w * 0.10f;
            float right = r.X + w * 0.90f;

            PointF[] topFace = new PointF[] {
                new PointF(cx, top),
                new PointF(right, mid),
                new PointF(cx, mid + (mid - top)),
                new PointF(left, mid)
            };
            PointF[] leftFace = new PointF[] {
                new PointF(left, mid),
                new PointF(cx, mid + (mid - top)),
                new PointF(cx, bottom),
                new PointF(left, bottom - (mid - top))
            };
            PointF[] rightFace = new PointF[] {
                new PointF(cx, mid + (mid - top)),
                new PointF(right, mid),
                new PointF(right, bottom - (mid - top)),
                new PointF(cx, bottom)
            };

            using (SolidBrush b = new SolidBrush(ColorTranslator.FromHtml("#57B84A"))) g.FillPolygon(b, topFace);
            using (SolidBrush b = new SolidBrush(ColorTranslator.FromHtml("#8B5A2B"))) g.FillPolygon(b, leftFace);
            using (SolidBrush b = new SolidBrush(ColorTranslator.FromHtml("#6B4423"))) g.FillPolygon(b, rightFace);

            // A few pixel-like highlights.
            using (SolidBrush b = new SolidBrush(ColorTranslator.FromHtml("#6ED45C")))
            {
                float s = Math.Max(1f, w * 0.055f);
                g.FillRectangle(b, cx - w * 0.18f, top + h * 0.12f, s, s);
                g.FillRectangle(b, cx + w * 0.06f, top + h * 0.06f, s, s);
                g.FillRectangle(b, cx - w * 0.02f, top + h * 0.20f, s, s);
            }
            using (SolidBrush b = new SolidBrush(ColorTranslator.FromHtml("#3E8E36")))
            {
                float s = Math.Max(1f, w * 0.05f);
                g.FillRectangle(b, left + w * 0.08f, mid + h * 0.04f, s, s);
                g.FillRectangle(b, right - w * 0.16f, mid + h * 0.10f, s, s);
            }
        }
    }
}
