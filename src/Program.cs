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
                // En el setup se parte siempre del idioma de Windows.
                try
                {
                    string language = AppConfig.Instance.IsConfigured()
                        ? AppConfig.Instance.Language
                        : LanguageService.DetectSystemLanguage();
                    LanguageService.Initialize(language);
                }
                catch { }

                // El tema guardado se aplica antes de construir cualquier
                // ventana; asi el setup y el panel nacen con la paleta correcta.
                try
                {
                    string savedTheme = AppConfig.Instance.Theme;
                    if (!string.IsNullOrWhiteSpace(savedTheme))
                    {
                        AppTheme theme = (AppTheme)Enum.Parse(typeof(AppTheme), savedTheme, true);
                        // Las panoramicas custom quedan deshabilitadas por
                        // ahora: se vuelve a Default y se guarda la migracion.
                        if (theme == AppTheme.CustomPanorama)
                        {
                            theme = AppTheme.Default;
                            AppConfig.Instance.Theme = "default";
                            AppConfig.Instance.Save();
                        }
                        ThemeManager.Apply(theme, null);
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
