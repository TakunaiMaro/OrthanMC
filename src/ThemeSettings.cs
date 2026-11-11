using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace McPanel
{
    public enum AppTheme
    {
        Default,
        Overworld,
        Nether,
        End,
        CustomPanorama
    }

    public static class ThemeManager
    {
        public static AppTheme Current = AppTheme.Default;
        public static PanoramaLayer CurrentPanorama;
        public static event EventHandler ThemeChanged;

        public static void Apply(AppTheme theme, PanoramaLayer layer)
        {
            Current = theme;
            CurrentPanorama = layer;
            ThemeApplier.ApplyToOpenForms();
            if (ThemeChanged != null) ThemeChanged(null, EventArgs.Empty);
        }

        public static void LoadDefault()
        {
            Apply(AppTheme.Default, null);
        }
    }

    public class ThemeConfigForm : Form
    {
        private Panel _listPanel;
        private Panel _detailPanel;
        private PanoramaRenderer _panorama;
        private AppTheme _selected = AppTheme.Default;

        public ThemeConfigForm()
        {
            Text = "Configuración App";
            Size = new Size(920, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Palette.Bg;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;

            Panel left = new Panel();
            left.Dock = DockStyle.Left;
            left.Width = 240;
            left.BackColor = Palette.Sidebar;
            Label t = new Label();
            t.Text = "TEMAS";
            t.Font = Fonts.UI(11f, FontStyle.Bold);
            t.ForeColor = Palette.Text;
            t.AutoSize = true;
            t.Location = new Point(18, 18);
            t.BackColor = Color.Transparent;
            _listPanel = new Panel();
            _listPanel.Dock = DockStyle.Fill;
            _listPanel.Padding = new Padding(10, 60, 10, 10);
            _listPanel.BackColor = Palette.Sidebar;
            left.Controls.Add(t);
            left.Controls.Add(_listPanel);

            _detailPanel = new Panel();
            _detailPanel.Dock = DockStyle.Fill;
            _detailPanel.BackColor = Palette.Bg;
            Label dt = new Label();
            dt.Text = "TEMAS";
            dt.Font = Fonts.UI(11f, FontStyle.Bold);
            dt.ForeColor = Palette.Text;
            dt.AutoSize = true;
            dt.Location = new Point(28, 22);
            dt.BackColor = Color.Transparent;
            _panorama = new PanoramaRenderer();
            _panorama.Location = new Point(28, 68);
            _panorama.Size = new Size(620, 320);
            _panorama.Visible = false;
            Label pdesc = new Label();
            pdesc.Text = "Selecciona un tema. Overworld/Nether/End solo cambian colores, nada más.";
            pdesc.Font = Fonts.UI(9f);
            pdesc.ForeColor = Palette.Muted;
            pdesc.AutoSize = true;
            pdesc.Location = new Point(28, 410);
            pdesc.BackColor = Color.Transparent;
            RoundButton btnLoad = new RoundButton();
            btnLoad.Text = "CARGAR PANORAMA CUSTOM (.ZIP)";
            btnLoad.Glyph = "folder";
            btnLoad.Width = 290;
            btnLoad.Location = new Point(28, 450);
            btnLoad.Visible = false;
            btnLoad.Click += BtnLoad_Click;
            RoundButton btnApply = new RoundButton();
            btnApply.Text = "GUARDAR Y APLICAR";
            btnApply.Glyph = "check";
            btnApply.Width = 210;
            btnApply.Location = new Point(28, 500);
            btnApply.Click += delegate
            {
                ThemeManager.Apply(_selected, ThemeManager.CurrentPanorama);
                try
                {
                    AppConfig.Instance.Theme = _selected.ToString().ToLowerInvariant();
                    AppConfig.Instance.Save();
                }
                catch { }
                MessageDialog.Info(this, "Tema aplicado", "Tema guardado y aplicado a toda la app.");
                Close();
            };

            _detailPanel.Controls.Add(dt);
            _detailPanel.Controls.Add(_panorama);
            _detailPanel.Controls.Add(pdesc);
            _detailPanel.Controls.Add(btnLoad);
            _detailPanel.Controls.Add(btnApply);

            Controls.Add(_detailPanel);
            Controls.Add(left);
            BuildList();
            LoadTheme(ThemeManager.Current);
        }

        private void BuildList()
        {
            AddItem("DEFAULT", AppTheme.Default, 0);
            AddItem("OVERWORLD", AppTheme.Overworld, 1);
            AddItem("NETHER", AppTheme.Nether, 2);
            AddItem("END", AppTheme.End, 3);
            AddItem("PANORAMICAS CUSTOM", AppTheme.CustomPanorama, 4);
        }

        private void AddItem(string text, AppTheme theme, int idx)
        {
            NavButton b = new NavButton();
            b.Text = text;
            b.Dock = DockStyle.Top;
            b.Margin = new Padding(0, 4, 0, 0);
            b.Click += delegate { LoadTheme(theme); };
            _listPanel.Controls.Add(b);
            if (idx == 0) b.Active = true;
        }

        private void LoadTheme(AppTheme theme)
        {
            _selected = theme;
            foreach (Control c in _listPanel.Controls)
            {
                if (c is NavButton)
                {
                    NavButton nb = (NavButton)c;
                    nb.Active = (theme == AppTheme.Default && nb.Text == "DEFAULT") ||
                                (theme == AppTheme.Overworld && nb.Text == "OVERWORLD") ||
                                (theme == AppTheme.Nether && nb.Text == "NETHER") ||
                                (theme == AppTheme.End && nb.Text == "END") ||
                                (theme == AppTheme.CustomPanorama && nb.Text.Contains("PANORAMICAS"));
                }
            }
            RoundButton btnLoad = null;
            foreach (Control c in _detailPanel.Controls) if (c is RoundButton && c.Text.Contains("PANORAMA")) btnLoad = (RoundButton)c;
            if (btnLoad != null) btnLoad.Visible = theme == AppTheme.CustomPanorama;
            _panorama.Visible = theme == AppTheme.CustomPanorama &&
                ThemeManager.CurrentPanorama != null && ThemeManager.CurrentPanorama.HasImages;
            if (_panorama.Visible && ThemeManager.CurrentPanorama != null) _panorama.SetLayer(ThemeManager.CurrentPanorama);
            ThemeManager.Apply(theme, ThemeManager.CurrentPanorama);
        }

        private async void BtnLoad_Click(object sender, EventArgs e)
        {
            string file = null;
            try
            {
                using (OpenFileDialog ofd = new OpenFileDialog())
                {
                    ofd.Title = "Seleccionar panorama custom (.zip)";
                    ofd.Filter = "ZIP (*.zip)|*.zip";
                    string themes = Path.Combine(AppContext.BaseDirectory, "themes");
                    try { Directory.CreateDirectory(themes); } catch { }
                    if (Directory.Exists(themes)) ofd.InitialDirectory = themes;
                    if (ofd.ShowDialog(this) == DialogResult.OK) file = ofd.FileName;
                }
            }
            catch { }
            if (string.IsNullOrWhiteSpace(file)) return;

            try
            {
                UseWaitCursor = true;
                PanoramaLayer layer = await System.Threading.Tasks.Task.Run(delegate
                {
                    return PanoramaLoader.LoadFromZip(file);
                });
                UseWaitCursor = false;
                if (IsDisposed || Disposing) return;
                if (layer == null || !layer.HasImages) return;

                ThemeManager.Apply(AppTheme.CustomPanorama, layer);
                _panorama.SetLayer(layer);
                _panorama.Visible = layer.HasImages;

                try
                {
                    AppConfig.Instance.PanoramaZip = CustomPanoramaStore.Import(file);
                    AppConfig.Instance.Theme = "custompanorama";
                    AppConfig.Instance.Save();
                }
                catch { }
            }
            catch
            {
                UseWaitCursor = false;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowFx.DarkTitleBar(Handle);
        }
    }
}
