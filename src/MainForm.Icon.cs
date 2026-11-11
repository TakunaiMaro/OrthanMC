using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace McPanel
{
    public partial class MainForm
    {
        private void DrawIconPreview(Graphics g, Panel p)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Palette.Input);
            string iconPath = _paths.BaseDir + "\\server-icon.png";
            try
            {
                if (File.Exists(iconPath))
                {
                    using (Image img = Image.FromFile(iconPath))
                    {
                        g.DrawImage(img, 0, 0, p.Width, p.Height);
                        return;
                    }
                }
            }
            catch { }
            g.DrawString("64x64", Fonts.UI(7.5f), new SolidBrush(Palette.Dim), new RectangleF(0,0,p.Width,p.Height), new StringFormat{ Alignment=StringAlignment.Center, LineAlignment=StringAlignment.Center });
        }

        private void PickServerIcon(Panel preview)
        {
            try
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Seleccionar imagen para server-icon.png";
                    ofd.Filter = "Imagenes (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Todos los archivos (*.*)|*.*";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        string dest = _paths.BaseDir + "\\server-icon.png";
                        string src = ofd.FileName;
                        using (Image img = Image.FromFile(src))
                        {
                            using (Bitmap bmp = new Bitmap(64, 64))
                            using (Graphics g2 = Graphics.FromImage(bmp))
                            {
                                g2.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g2.SmoothingMode = SmoothingMode.AntiAlias;
                                g2.DrawImage(img, 0, 0, 64, 64);
                                bmp.Save(dest, ImageFormat.Png);
                            }
                        }
                        preview.Invalidate();
                        _toast.ShowMessage("server-icon.png actualizado (64x64)", Palette.Accent, "check");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Error", ex.Message);
            }
        }
    }
}
