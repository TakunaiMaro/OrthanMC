#nullable disable
using System;
using System.Drawing;
using System.IO;

namespace McPanel
{
    // Nombre visible de la aplicacion y recursos de marca. El nombre del
    // producto no se traduce: los archivos de idioma nunca lo reemplazan.
    internal static class Brand
    {
        public const string Name = "Orthanc Mc";
        public const string NameUpper = "ORTHANC MC";

        private static Image _logoLarge;
        private static Image _logoSmall;
        private static Icon _icon;

        private static readonly string[] SearchFolders = new string[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        public static string FindAsset(string fileName)
        {
            foreach (string folder in SearchFolders)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(folder)) continue;
                    string path = Path.Combine(folder, fileName);
                    if (File.Exists(path)) return path;
                }
                catch { }
            }
            return null;
        }

        public static Image LogoLarge
        {
            get
            {
                if (_logoLarge == null) _logoLarge = Load("logo-Orthanc-800.png", 800);
                return _logoLarge;
            }
        }

        public static Image LogoSmall
        {
            get
            {
                if (_logoSmall == null) _logoSmall = Load("logo-Orthanc-136.png", 136);
                return _logoSmall;
            }
        }

        private static Image Load(string png, int fallbackSize)
        {
            try
            {
                string path = FindAsset(png);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    using (Image source = Image.FromFile(path))
                        return new Bitmap(source);
                }
            }
            catch { }

            try
            {
                string path = FindAsset("logo-Orthanc-136.ico");
                if (string.IsNullOrWhiteSpace(path)) path = FindAsset("logo-Orthanc.ico");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    using (Image source = Image.FromFile(path))
                        return new Bitmap(source, new Size(fallbackSize, fallbackSize));
                }
            }
            catch { }
            return null;
        }

        public static Icon AppIcon
        {
            get
            {
                if (_icon != null) return _icon;
                try
                {
                    string path = FindAsset("logo-Orthanc.ico");
                    if (string.IsNullOrWhiteSpace(path)) path = FindAsset("logo-Orthanc-136.ico");
                    if (!string.IsNullOrWhiteSpace(path)) _icon = new Icon(path);
                }
                catch { _icon = null; }
                return _icon;
            }
        }

        // El .ico multi-tamano permite pedir el frame exacto que usa la
        // barra de tareas. Si no existe, se usa el icono normal.
        public static Icon IconFor(int width, int height)
        {
            try
            {
                string path = FindAsset("logo-Orthanc.ico");
                if (!string.IsNullOrWhiteSpace(path))
                    return new Icon(path, width, height);
            }
            catch { }
            return AppIcon;
        }
    }
}
