using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace McPanel
{
    // Modo oscuro nativo de Windows: hace que scrollbars, combos y menus
    // contextuales usen la paleta oscura en lugar del gris claro clasico.
    internal static class NativeDarkMode
    {
        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        private static extern int SetPreferredAppMode(int mode);

        [DllImport("uxtheme.dll", EntryPoint = "#136")]
        private static extern void FlushMenuThemes();

        // 0 = default, 1 = allow dark, 2 = force dark, 3 = force light
        public static void Enable()
        {
            try
            {
                SetPreferredAppMode(2);
                FlushMenuThemes();
            }
            catch
            {
                // En Windows sin uxtheme nuevo simplemente se mantiene el tema claro nativo.
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                NativeDarkMode.Enable();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // El idioma guardado se carga antes de construir las ventanas.
                try { LanguageService.Initialize(AppConfig.Instance.Language); }
                catch { }

                // El tema guardado se aplica antes de construir cualquier
                // ventana; asi el setup y el panel nacen con la paleta correcta.
                try
                {
                    string savedTheme = AppConfig.Instance.Theme;
                    if (!string.IsNullOrWhiteSpace(savedTheme))
                    {
                        AppTheme theme = (AppTheme)Enum.Parse(typeof(AppTheme), savedTheme, true);
                        // Si el tema es CustomPanorama se reconstruye el
                        // panorama guardado para que no arranque vacio.
                        PanoramaLayer layer = theme == AppTheme.CustomPanorama
                            ? CustomPanoramaStore.LoadSaved()
                            : null;
                        if (theme != AppTheme.CustomPanorama || layer != null)
                            ThemeManager.Apply(theme, layer);
                    }
                }
                catch { }

                if (!AppConfig.Instance.IsConfigured())
                {
                    using (SetupForm setup = new SetupForm())
                    {
                        if (setup.ShowDialog() != DialogResult.OK)
                        {
                            return;
                        }
                    }
                }

                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
