#nullable disable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace McPanel
{
    public class PanoramaLayer
    {
        // 0 frente, 1 derecha, 2 atras, 3 izquierda, 4 arriba, 5 abajo.
        public Bitmap[] Faces = new Bitmap[6];

        // Proyeccion equirectangular 2:1 construida a partir del cubo.
        public Bitmap Equirect;

        public bool HasImages
        {
            get { return Equirect != null || Faces[0] != null || Faces[1] != null; }
        }
    }

    internal class PanoramaRenderer : Panel
    {
        private float _angle = 0f;
        private System.Windows.Forms.Timer _timer;
        private PanoramaLayer _layer;
        private float _speed = 0.12f;

        // Copia reducida al tamano de dibujo. Sin esto cada cuadro escalaba
        // un bitmap de varios megapixeles y bloqueaba el hilo de la interfaz.
        private Bitmap _strip;
        private int _stripHeight = -1;
        private int _stripWidth = -1;

        public PanoramaRenderer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Dock = DockStyle.Fill;
            BackColor = Color.Black;
            _timer = new System.Windows.Forms.Timer();
            // 30 fps es suficiente para un fondo que gira despacio y deja
            // aire al hilo de UI para repintar el resto de la ventana.
            _timer.Interval = 33;
            _timer.Tick += Tick;
            // El tick es barato y se sale de inmediato si el preview no se
            // ve; asi no hay que reiniciar el temporizador al volver a la
            // seccion de Temas.
            _timer.Start();
            VisibleChanged += delegate
            {
                if (Visible) Invalidate();
            };
        }

        public void SetLayer(PanoramaLayer layer)
        {
            _layer = layer;
            DisposeStrip();
            if (Visible) Invalidate();
        }

        public void SetSpeed(float speed)
        {
            _speed = speed;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                }
                DisposeStrip();
            }
            base.Dispose(disposing);
        }

        private void Tick(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (!Visible || _layer == null || !_layer.HasImages) return;

            // Minimizada no hay nada que pintar: se evita generar repintados
            // que luego dejan la ventana a medio dibujar al restaurar.
            Form form = FindForm();
            if (form != null && form.WindowState == FormWindowState.Minimized) return;

            _angle += _speed;
            if (_angle >= 360f) _angle -= 360f;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (_layer == null)
            {
                DrawFallback(g);
                return;
            }

            if (_layer.Equirect != null)
            {
                EnsureStrip();
                if (_strip != null) DrawEquirect(g);
                else DrawFallback(g);
            }
            else
            {
                // Compatibilidad con panoramas incompletos (sin las 6 caras).
                Bitmap front = _layer.Faces[1] != null ? _layer.Faces[1] : _layer.Faces[0];
                if (front != null) DrawSingle(g, front);
                else DrawFallback(g);
            }

            using (SolidBrush ov = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                g.FillRectangle(ov, 0, 0, Width, Height);
        }

        // Rectangulo donde se dibuja el panorama 2:1 dentro del panel.
        private RectangleF ComputeDrawRect()
        {
            float aspect = _layer != null && _layer.Equirect != null
                ? _layer.Equirect.Width / (float)_layer.Equirect.Height
                : 2f;
            if (aspect <= 0.01f) aspect = 2f;

            float drawW = Height * aspect;
            float drawH = Height;
            if (drawW < Width)
            {
                drawW = Width;
                drawH = Width / aspect;
            }
            return new RectangleF(0f, (Height - drawH) / 2f, drawW, drawH);
        }

        // Escala el panorama una sola vez al tamano en que se va a mostrar.
        // Cada cuadro solo copia ese bitmap pequeno.
        private void EnsureStrip()
        {
            if (_layer == null || _layer.Equirect == null)
            {
                DisposeStrip();
                return;
            }

            RectangleF target = ComputeDrawRect();
            int h = (int)Math.Ceiling(target.Height);
            if (h < 32) h = 32;
            if (h > 1080) h = 1080;
            int w = h * 2;

            // Si el tamano cambia muy poco (por ejemplo durante la animacion
            // de restaurar la ventana) se reutiliza la tira: recrearla en cada
            // cuadro intermedio bloquea el hilo de UI y deja la ventana a
            // medio pintar.
            if (_strip != null && Math.Abs(_stripHeight - h) <= 8 &&
                _stripWidth == _stripHeight * 2) return;

            try
            {
                Bitmap next = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(next))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(_layer.Equirect, new Rectangle(0, 0, w, h));
                }
                DisposeStrip();
                _strip = next;
                _stripHeight = h;
                _stripWidth = w;
            }
            catch
            {
                DisposeStrip();
            }
        }

        private void DisposeStrip()
        {
            if (_strip != null)
            {
                try { _strip.Dispose(); } catch { }
                _strip = null;
            }
            _stripHeight = -1;
            _stripWidth = -1;
        }

        // La tira se dibuja dos veces para que el giro sea continuo al pasar
        // del borde derecho al izquierdo.
        private void DrawEquirect(Graphics g)
        {
            RectangleF r = ComputeDrawRect();
            float offset = (float)(_angle / 360.0) * r.Width;
            offset %= r.Width;
            if (offset < 0f) offset += r.Width;

            g.DrawImage(_strip, new RectangleF(r.X - offset, r.Y, r.Width, r.Height));
            g.DrawImage(_strip, new RectangleF(r.X + r.Width - offset, r.Y, r.Width, r.Height));
        }

        private void DrawSingle(Graphics g, Bitmap front)
        {
            int w = front.Width;
            float offset = (float)(Width * ((_angle % 360f) / 360f));
            g.DrawImage(front, new Rectangle((int)-offset, 0, Width + w, Height));
            if (offset > 0) g.DrawImage(front, new Rectangle((int)(Width - offset), 0, Width + w, Height));
        }

        private void DrawFallback(Graphics g)
        {
            using (LinearGradientBrush br = new LinearGradientBrush(
                new Rectangle(0, 0, Width, Height),
                ColorTranslator.FromHtml("#0B1020"),
                ColorTranslator.FromHtml("#101826"), 90f))
                g.FillRectangle(br, 0, 0, Width, Height);
        }
    }

    internal static class PanoramaLoader
    {
        // 2048x1024 es mas que suficiente para el preview: se ve a ~90
        // grados en pantalla y tarda una fraccion de lo que costaba 4096.
        private const int EquirectWidth = 2048;
        private const int EquirectHeight = 1024;

        public static PanoramaLayer LoadFromZip(string path)
        {
            PanoramaLayer layer = new PanoramaLayer();
            try
            {
                if (!File.Exists(path)) return layer;
                using (ZipArchive zip = ZipFile.OpenRead(path))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        int index;
                        if (!TryFaceIndex(entry.FullName, out index)) continue;
                        if (layer.Faces[index] != null) continue;
                        layer.Faces[index] = ReadBitmap(entry);
                    }
                }

                // Minecraft guarda la cara superior rotada 90 grados
                // respecto a la convencion del cubo que usamos aqui.
                if (layer.Faces[4] != null)
                    layer.Faces[4].RotateFlip(RotateFlipType.Rotate90FlipNone);

                BuildEquirect(layer);
            }
            catch { }
            return layer;
        }

        private static bool TryFaceIndex(string entryName, out int index)
        {
            index = -1;
            try
            {
                string name = Path.GetFileNameWithoutExtension(entryName);
                if (name == null) return false;
                name = name.Trim();
                const string prefix = "panorama_";
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
                string number = name.Substring(prefix.Length);
                int value;
                if (!int.TryParse(number, out value)) return false;
                if (value < 0 || value > 5) return false;
                index = value;
                return true;
            }
            catch { return false; }
        }

        private static Bitmap ReadBitmap(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            using (Bitmap source = new Bitmap(stream))
            {
                // Copia 32bpp propia: el stream del ZIP se cierra al salir y
                // asi LockBits siempre trabaja con el mismo formato.
                Bitmap copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(copy))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(source, 0, 0, source.Width, source.Height);
                }
                return copy;
            }
        }

        private static void BuildEquirect(PanoramaLayer layer)
        {
            if (layer.Faces[0] == null && layer.Faces[1] == null &&
                layer.Faces[2] == null && layer.Faces[3] == null)
                return;

            FaceData[] faces = new FaceData[6];
            try
            {
                for (int i = 0; i < 6; i++)
                    if (layer.Faces[i] != null) faces[i] = new FaceData(layer.Faces[i]);

                Bitmap output = new Bitmap(EquirectWidth, EquirectHeight, PixelFormat.Format32bppArgb);
                Rectangle outRect = new Rectangle(0, 0, EquirectWidth, EquirectHeight);
                BitmapData outData = output.LockBits(outRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                byte[] outPixels = new byte[outData.Stride * EquirectHeight];

                double[] sinPhi = new double[EquirectWidth];
                double[] cosPhi = new double[EquirectWidth];
                double[] sinTheta = new double[EquirectHeight];
                double[] cosTheta = new double[EquirectHeight];
                for (int x = 0; x < EquirectWidth; x++)
                {
                    double phi = (x + 0.5) / EquirectWidth * 2.0 * Math.PI;
                    sinPhi[x] = Math.Sin(phi);
                    cosPhi[x] = Math.Cos(phi);
                }
                for (int y = 0; y < EquirectHeight; y++)
                {
                    double theta = (0.5 - (y + 0.5) / EquirectHeight) * Math.PI;
                    sinTheta[y] = Math.Sin(theta);
                    cosTheta[y] = Math.Cos(theta);
                }

                // Cada fila es independiente; en paralelo el armado deja de
                // congelar la ventana durante la carga del ZIP.
                Parallel.For(0, EquirectHeight, y =>
                {
                    double vDir = sinTheta[y];
                    double ct = cosTheta[y];
                    double ay = Math.Abs(vDir);
                    int row = y * outData.Stride;
                    for (int x = 0; x < EquirectWidth; x++)
                    {
                        double vx = sinPhi[x] * ct;
                        double vz = cosPhi[x] * ct;
                        double ax = Math.Abs(vx);
                        double az = Math.Abs(vz);

                        int face;
                        double u, v;
                        if (ay >= ax && ay >= az)
                        {
                            // Arriba y abajo.
                            if (vDir >= 0.0)
                            {
                                face = 4;
                                u = 0.5 + 0.5 * vx / ay;
                                v = 0.5 + 0.5 * vz / ay;
                            }
                            else
                            {
                                face = 5;
                                u = 0.5 - 0.5 * vx / vDir;
                                v = 0.5 + 0.5 * vz / vDir;
                            }
                        }
                        else if (ax >= az)
                        {
                            face = vx >= 0.0 ? 1 : 3;
                            u = 0.5 - 0.5 * vz / vx;
                            v = 0.5 - 0.5 * vDir / ax;
                        }
                        else
                        {
                            face = vz >= 0.0 ? 0 : 2;
                            u = 0.5 + 0.5 * vx / vz;
                            v = 0.5 - 0.5 * vDir / az;
                        }

                        FaceData fd = faces[face];
                        if (fd == null) fd = FirstFace(faces);
                        byte b = 0, g = 0, r = 0;
                        if (fd != null) fd.Sample(u, v, out b, out g, out r);

                        int offset = row + x * 4;
                        outPixels[offset] = b;
                        outPixels[offset + 1] = g;
                        outPixels[offset + 2] = r;
                        outPixels[offset + 3] = 255;
                    }
                });

                Marshal.Copy(outPixels, 0, outData.Scan0, outPixels.Length);
                output.UnlockBits(outData);
                layer.Equirect = output;
            }
            finally
            {
                for (int i = 0; i < faces.Length; i++)
                {
                    if (faces[i] != null) faces[i].Dispose();
                }
            }
        }

        private static FaceData FirstFace(FaceData[] faces)
        {
            for (int i = 0; i < 4; i++)
                if (faces[i] != null) return faces[i];
            return null;
        }

        private sealed class FaceData : IDisposable
        {
            private Bitmap _bitmap;
            private BitmapData _data;
            private int _stride;
            private int _width;
            private int _height;
            private byte[] _pixels;

            public FaceData(Bitmap bitmap)
            {
                _bitmap = bitmap;
                _width = bitmap.Width;
                _height = bitmap.Height;
                Rectangle rect = new Rectangle(0, 0, _width, _height);
                _data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                _stride = _data.Stride;
                _pixels = new byte[_stride * _height];
                Marshal.Copy(_data.Scan0, _pixels, 0, _pixels.Length);
            }

            public void Sample(double u, double v, out byte b, out byte g, out byte r)
            {
                if (u < 0.0) u = 0.0; else if (u > 1.0) u = 1.0;
                if (v < 0.0) v = 0.0; else if (v > 1.0) v = 1.0;

                double fx = u * (_width - 1);
                double fy = v * (_height - 1);
                int x0 = (int)fx;
                int y0 = (int)fy;
                int x1 = x0 + 1; if (x1 >= _width) x1 = _width - 1;
                int y1 = y0 + 1; if (y1 >= _height) y1 = _height - 1;
                double tx = fx - x0;
                double ty = fy - y0;

                int i00 = y0 * _stride + x0 * 4;
                int i10 = y0 * _stride + x1 * 4;
                int i01 = y1 * _stride + x0 * 4;
                int i11 = y1 * _stride + x1 * 4;

                b = Blend(_pixels[i00], _pixels[i10], _pixels[i01], _pixels[i11], tx, ty);
                g = Blend(_pixels[i00 + 1], _pixels[i10 + 1], _pixels[i01 + 1], _pixels[i11 + 1], tx, ty);
                r = Blend(_pixels[i00 + 2], _pixels[i10 + 2], _pixels[i01 + 2], _pixels[i11 + 2], tx, ty);
            }

            private static byte Blend(byte c00, byte c10, byte c01, byte c11, double tx, double ty)
            {
                double top = c00 + (c10 - c00) * tx;
                double bottom = c01 + (c11 - c01) * tx;
                double value = top + (bottom - top) * ty;
                if (value < 0.0) value = 0.0; else if (value > 255.0) value = 255.0;
                return (byte)value;
            }

            public void Dispose()
            {
                if (_bitmap == null) return;
                try { _bitmap.UnlockBits(_data); } catch { }
                _bitmap = null;
                _pixels = null;
                _data = default(BitmapData);
            }
        }
    }

    // Guarda el ZIP elegido por el usuario en una carpeta estable de la app
    // (AppData) para que el tema CustomPanorama sobreviva a reinicios aunque
    // el archivo original se mueva o se borre.
    internal static class CustomPanoramaStore
    {
        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "McPanel", "panoramas");
            }
        }

        public static string Import(string source)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return source;
                string folder = Path.GetFullPath(Folder);
                Directory.CreateDirectory(folder);
                string full = Path.GetFullPath(source);
                if (string.Equals(Path.GetDirectoryName(full), folder, StringComparison.OrdinalIgnoreCase))
                    return full;
                string destination = Path.Combine(folder, Path.GetFileName(full));
                File.Copy(full, destination, true);
                return destination;
            }
            catch
            {
                return source;
            }
        }

        public static PanoramaLayer LoadSaved()
        {
            try
            {
                string path = AppConfig.Instance.PanoramaZip;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
                PanoramaLayer layer = PanoramaLoader.LoadFromZip(path);
                return layer != null && layer.HasImages ? layer : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
