using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace McPanel
{
    public partial class MainForm
    {
        private NavButton _navApp;
        private Panel _viewAppConfigHub;
        private Panel _viewAppTheme;
        private Panel _viewCloudBackup;
        private Panel _viewLanguage;
        private Panel _viewUpdates;

        private Panel _appThemeList;
        private AppTheme _appThemeSelected = AppTheme.Default;
        private PanoramaRenderer _appPanoPreview;
        private RoundButton _appThemePanoramaButton;

        private Label _updateCurrentLabel;
        private Label _updateStatusLabel;
        private Label _updateNotesLabel;
        private ToggleSwitch _updateAutoToggle;
        private RoundButton _updateCheckButton;
        private RoundButton _updateOpenButton;
        private bool _updateBusy;

        private InputBox _cloudGoogleToken;
        private InputBox _cloudGoogleFolder;
        private InputBox _cloudMediaFireAppId;
        private InputBox _cloudMediaFireSession;
        private InputBox _cloudMegaEmail;
        private InputBox _cloudMegaSession;
        private readonly Dictionary<string, Label> _cloudStatus = new Dictionary<string, Label>();
        private readonly Dictionary<string, Card> _cloudCards = new Dictionary<string, Card>();
        private Label _cloudActiveLabel;
        private TextBox _cloudLog;
        private bool _cloudBusy;

        private Panel _languageList;
        private string _languageSelectedCode = "es";
        private Label _languageActiveLabel;

        private Panel _cloudScrollHost;
        private ScrollBarBinder _cloudScroll;

        private void BuildAppConfigViews()
        {
            _viewAppConfigHub = BuildAppConfigHub();
            _viewAppTheme = MakeConfigSection(BuildAppThemeSection(), "Temas", 7);
            _viewCloudBackup = MakeConfigSection(BuildCloudBackupSection(), "Backup en la nube", 7);
            _viewLanguage = MakeConfigSection(BuildLanguageSection(), "Idioma", 7);
            _viewUpdates = MakeConfigSection(BuildUpdatesSection(), "Actualizaciones", 7);

            _viewAppConfigHub.Dock = DockStyle.Fill;
            _viewAppTheme.Dock = DockStyle.Fill;
            _viewCloudBackup.Dock = DockStyle.Fill;
            _viewLanguage.Dock = DockStyle.Fill;
            _viewUpdates.Dock = DockStyle.Fill;
            _viewAppConfigHub.Visible = false;
            _viewAppTheme.Visible = false;
            _viewCloudBackup.Visible = false;
            _viewLanguage.Visible = false;
            _viewUpdates.Visible = false;

            _content.Controls.Add(_viewAppConfigHub);
            _content.Controls.Add(_viewAppTheme);
            _content.Controls.Add(_viewCloudBackup);
            _content.Controls.Add(_viewLanguage);
            _content.Controls.Add(_viewUpdates);

            _appThemeSelected = ThemeManager.Current;
            SelectAppTheme(_appThemeSelected, false);
            LoadCloudSettings();
            RefreshLanguageList();
        }

        private Panel BuildAppConfigHub()
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;

            TableLayoutPanel page = new TableLayoutPanel();
            page.Dock = DockStyle.Fill;
            page.Margin = new Padding(0);
            page.Padding = new Padding(0);
            page.BackColor = Color.Transparent;
            page.ColumnCount = 2;
            page.RowCount = 2;
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(164)));
            page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel stack = new TableLayoutPanel();
            stack.Dock = DockStyle.Fill;
            stack.Margin = new Padding(0, Dp(10), 0, 0);
            stack.BackColor = Color.Transparent;
            stack.ColumnCount = 1;
            stack.RowCount = 5;
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(100)));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(100)));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(100)));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(100)));
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            ConfigEntry themes = new ConfigEntry();
            themes.Title = "Temas";
            themes.Description = "Cambia la paleta de colores de toda la aplicacion.";
            themes.Glyph = "brush";
            themes.Dock = DockStyle.Fill;
            themes.Margin = new Padding(0, 0, 0, Dp(8));
            themes.Click += delegate { NavigateTo(8); };

            ConfigEntry updates = new ConfigEntry();
            updates.Title = "Actualizaciones";
            updates.Description = "Comprueba si hay una version nueva publicada en GitHub.";
            updates.Glyph = "restart";
            updates.Dock = DockStyle.Fill;
            updates.Margin = new Padding(0, 0, 0, Dp(8));
            updates.Click += delegate { NavigateTo(11); };

            ConfigEntry language = new ConfigEntry();
            language.Title = "Idioma";
            language.Description = "Cambia el idioma de la interfaz o agrega un archivo nuevo.";
            language.Glyph = "globe";
            language.Dock = DockStyle.Fill;
            language.Margin = new Padding(0, 0, 0, Dp(8));
            language.Click += delegate { NavigateTo(10); };

            // Los respaldos en la nube quedan visibles pero deshabilitados
            // hasta que la funcion este terminada.
            ConfigEntry cloud = new ConfigEntry();
            cloud.Title = "Backup en la nube";
            cloud.Description = "Deshabilitado temporalmente. Estara disponible en una proxima version.";
            cloud.Glyph = "cloud";
            cloud.Dock = DockStyle.Fill;
            cloud.Margin = new Padding(0);
            cloud.Disabled = true;

            Label hint = MakeWrapLabel(
                "Añade idiomas, modificando el archivo 'Plantilla.json.example' " +
                "esta alojado en la carpeta /languages, se pueden editar para añadir nuevos idiomas.",
                Fonts.UI(8.5f), Palette.Dim, Dp(40));
            hint.Margin = new Padding(0, Dp(16), 0, 0);

            stack.Controls.Add(themes, 0, 0);
            stack.Controls.Add(updates, 0, 1);
            stack.Controls.Add(language, 0, 2);
            stack.Controls.Add(cloud, 0, 3);
            stack.Controls.Add(hint, 0, 4);
            page.Controls.Add(stack, 0, 1);
            page.SetColumnSpan(stack, 2);
            p.Controls.Add(page);
            return p;
        }

        private Panel BuildAppThemeSection()
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.BackColor = Color.Transparent;
            grid.ColumnCount = 2;
            grid.RowCount = 1;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Card left = new Card();
            left.Dock = DockStyle.Fill;
            left.Margin = new Padding(0, 0, Dp(14), 0);
            left.Padding = new Padding(Dp(16));
            TableLayoutPanel leftStack = new TableLayoutPanel();
            leftStack.Dock = DockStyle.Fill;
            leftStack.Margin = new Padding(0);
            leftStack.BackColor = Color.Transparent;
            leftStack.ColumnCount = 1;
            leftStack.RowCount = 2;
            leftStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            leftStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label leftTitle = MakeLabel("TEMAS", Fonts.UI(9f, FontStyle.Bold), Palette.Dim);
            _appThemeList = new Panel();
            _appThemeList.Dock = DockStyle.Fill;
            _appThemeList.Margin = new Padding(0, Dp(8), 0, 0);
            _appThemeList.AutoScroll = true;
            _appThemeList.BackColor = Color.Transparent;
            leftStack.Controls.Add(leftTitle, 0, 0);
            leftStack.Controls.Add(_appThemeList, 0, 1);
            left.Controls.Add(leftStack);

            Card right = new Card();
            right.Dock = DockStyle.Fill;
            right.Margin = new Padding(0);
            right.Padding = new Padding(Dp(22));
            TableLayoutPanel rightStack = new TableLayoutPanel();
            rightStack.Dock = DockStyle.Fill;
            rightStack.Margin = new Padding(0);
            rightStack.BackColor = Color.Transparent;
            rightStack.ColumnCount = 1;
            rightStack.RowCount = 6;
            rightStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label rightTitle = MakeLabel("TEMAS", Fonts.UI(11f, FontStyle.Bold), Palette.Text);
            Label rightDesc = MakeWrapLabel(
                "Selecciona un tema. Overworld/Nether/End solo cambian colores. " +
                "Las panoramicas custom llegaran en una proxima version.",
                Fonts.UI(9f), Palette.Muted, Dp(52));
            rightDesc.Margin = new Padding(0, Dp(4), 0, Dp(10));

            _appPanoPreview = new PanoramaRenderer();
            _appPanoPreview.Dock = DockStyle.Fill;
            _appPanoPreview.Margin = new Padding(0, 0, 0, Dp(10));
            _appPanoPreview.Visible = false;

            _appThemePanoramaButton = new RoundButton();
            _appThemePanoramaButton.Text = "CARGAR PANORAMA CUSTOM (.ZIP)";
            _appThemePanoramaButton.Glyph = "folder";
            _appThemePanoramaButton.Width = Dp(300);
            _appThemePanoramaButton.Height = Dp(38);
            _appThemePanoramaButton.Margin = new Padding(0, 0, 0, Dp(10));
            _appThemePanoramaButton.Visible = false;
            _appThemePanoramaButton.Click += delegate { LoadCustomPanorama(); };

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            actions.AutoSize = true;
            actions.Margin = new Padding(0);
            actions.BackColor = Color.Transparent;
            RoundButton apply = new RoundButton();
            apply.Text = "GUARDAR Y APLICAR";
            apply.Glyph = "check";
            apply.Width = Dp(220);
            apply.Height = Dp(40);
            apply.Click += delegate { SaveAppTheme(); };
            actions.Controls.Add(apply);
            actions.Controls.Add(_appThemePanoramaButton);

            rightStack.Controls.Add(rightTitle, 0, 0);
            rightStack.Controls.Add(rightDesc, 0, 1);
            rightStack.Controls.Add(_appPanoPreview, 0, 2);
            rightStack.Controls.Add(actions, 0, 3);
            right.Controls.Add(rightStack);

            grid.Controls.Add(left, 0, 0);
            grid.Controls.Add(right, 1, 0);
            p.Controls.Add(grid);

            AddThemeItem("DEFAULT", AppTheme.Default);
            AddThemeItem("OVERWORLD", AppTheme.Overworld);
            AddThemeItem("NETHER", AppTheme.Nether);
            AddThemeItem("END", AppTheme.End);
            // Las panoramicas custom quedan visibles pero deshabilitadas hasta
            // que su rendimiento este resuelto.
            AddThemeItem("PANORAMICAS", AppTheme.CustomPanorama, true);
            SelectAppTheme(ThemeManager.Current, false);
            return p;
        }

        private void AddThemeItem(string text, AppTheme theme, bool disabled = false)
        {
            NavButton button = new NavButton();
            button.Text = text;
            button.Dock = DockStyle.Top;
            button.Height = Dp(44);
            button.Margin = new Padding(0, Dp(3), 0, Dp(3));
            button.Tag = theme;
            button.Disabled = disabled;
            if (!disabled) button.Click += delegate { SelectAppTheme(theme, true); };
            _appThemeList.Controls.Add(button);
        }

        private void SelectAppTheme(AppTheme theme, bool applyPreview)
        {
            // La vista previa de panoramicas esta deshabilitada por ahora.
            if (theme == AppTheme.CustomPanorama) return;
            _appThemeSelected = theme;
            if (_appThemeList != null)
            {
                foreach (Control control in _appThemeList.Controls)
                {
                    NavButton button = control as NavButton;
                    if (button != null && button.Tag is AppTheme)
                        button.Active = (AppTheme)button.Tag == theme;
                }
            }

            if (applyPreview)
            {
                ThemeManager.Apply(theme, ThemeManager.CurrentPanorama);
                LanguageService.ApplyTo(this);
            }
        }

        private void SaveAppTheme()
        {
            if (_appThemeSelected == AppTheme.CustomPanorama) return;
            try
            {
                ThemeManager.Apply(_appThemeSelected, ThemeManager.CurrentPanorama);
                AppConfig.Instance.Theme = _appThemeSelected.ToString().ToLowerInvariant();
                AppConfig.Instance.Save();
                LanguageService.ApplyTo(this);
                if (_toast != null)
                    _toast.ShowMessage("Tema aplicado", Palette.Accent, "check");
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Temas", ex.Message);
            }
        }

        private async void LoadCustomPanorama()
        {
            string file = null;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Seleccionar panorama custom (.zip)";
                dialog.Filter = "ZIP (*.zip)|*.zip";
                string themes = Path.Combine(AppContext.BaseDirectory, "themes");
                try { Directory.CreateDirectory(themes); } catch { }
                if (Directory.Exists(themes)) dialog.InitialDirectory = themes;
                if (dialog.ShowDialog(this) == DialogResult.OK) file = dialog.FileName;
            }
            if (string.IsNullOrWhiteSpace(file)) return;

            try
            {
                // Decodificar las seis caras y armar el equirectangular tarda
                // lo suficiente para notarse: se hace fuera del hilo de UI
                // para que minimizar o mover la ventana siga respondiendo.
                UseWaitCursor = true;
                string storedPath = null;
                PanoramaLayer layer = await Task.Run(delegate
                {
                    // La copia a AppData tambien es E/S: se hace fuera del
                    // hilo de UI junto con la decodificacion.
                    storedPath = CustomPanoramaStore.Import(file);
                    return PanoramaLoader.LoadFromZip(file);
                });
                UseWaitCursor = false;
                if (IsDisposed || Disposing) return;
                if (layer == null || !layer.HasImages)
                {
                    MessageDialog.Info(this, "Temas",
                        "El ZIP no contiene las caras panorama_0 a panorama_5.");
                    return;
                }

                ThemeManager.Apply(AppTheme.CustomPanorama, layer);
                SelectAppTheme(AppTheme.CustomPanorama, false);
                if (_appPanoPreview != null)
                {
                    _appPanoPreview.SetLayer(layer);
                    _appPanoPreview.Visible = layer.HasImages;
                }

                // Se guarda una copia del ZIP, su ruta y el tema para
                // reconstruir el panorama al reiniciar.
                try
                {
                    AppConfig.Instance.PanoramaZip = string.IsNullOrWhiteSpace(storedPath)
                        ? file : storedPath;
                    // El tema ya se aplico arriba, asi que tambien se guarda:
                    // si el usuario cierra la app sin pulsar GUARDAR Y APLICAR,
                    // el panorama sigue estando al reiniciar.
                    AppConfig.Instance.Theme = "custompanorama";
                    AppConfig.Instance.Save();
                }
                catch { }

                LanguageService.ApplyTo(this);
            }
            catch (Exception ex)
            {
                UseWaitCursor = false;
                if (!IsDisposed && !Disposing)
                    MessageDialog.Info(this, "Temas", ex.Message);
            }
        }

        private Panel BuildCloudBackupSection()
        {
            Panel scroll = new Panel();
            scroll.Dock = DockStyle.Fill;
            scroll.BackColor = Color.Transparent;
            scroll.AutoScroll = true;
            _cloudScrollHost = scroll;

            TableLayoutPanel stack = new TableLayoutPanel();
            stack.Dock = DockStyle.Top;
            stack.AutoSize = true;
            stack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            stack.Margin = new Padding(0);
            stack.BackColor = Color.Transparent;
            stack.ColumnCount = 1;
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            Label title = MakeLabel("BACKUP EN LA NUBE", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            title.Margin = new Padding(0, 0, 0, Dp(4));
            Label desc = MakeWrapLabel(
                "La validacion es experimental: comprueba que la API responda y que las credenciales " +
                "tengan el formato esperado. No sube archivos todavia.",
                Fonts.UI(8.75f), Palette.Muted, Dp(34));
            desc.Margin = new Padding(0, 0, 0, Dp(10));

            _cloudActiveLabel = MakeLabel("Proveedor activo: Google Drive",
                Fonts.UI(8.75f, FontStyle.Bold), Palette.Accent);
            _cloudActiveLabel.Margin = new Padding(0, 0, 0, Dp(10));

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Top;
            actions.AutoSize = true;
            actions.WrapContents = false;
            actions.Margin = new Padding(0, 0, 0, Dp(12));
            actions.BackColor = Color.Transparent;

            RoundButton validateAll = new RoundButton();
            validateAll.Text = "VALIDAR TODAS LAS APIS";
            validateAll.Glyph = "cloud";
            validateAll.Width = Dp(240);
            validateAll.Height = Dp(40);
            validateAll.Margin = new Padding(0, 0, Dp(10), 0);
            validateAll.Click += delegate { ValidateAllCloudApis(); };

            RoundButton save = new RoundButton();
            save.Text = "GUARDAR PROVEEDOR";
            save.Glyph = "save";
            save.Width = Dp(220);
            save.Height = Dp(40);
            save.FillColor = Palette.Card;
            save.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            save.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            save.TextColor = Palette.Text;
            save.BorderColor = Palette.Border;
            save.Click += delegate
            {
                SaveCloudSettings();
                LoadCloudSettings();
                if (_toast != null)
                    _toast.ShowMessage("Credenciales guardadas", Palette.Accent, "check");
            };
            actions.Controls.Add(validateAll);
            actions.Controls.Add(save);

            stack.Controls.Add(title);
            stack.Controls.Add(desc);
            stack.Controls.Add(_cloudActiveLabel);
            stack.Controls.Add(actions);
            stack.Controls.Add(BuildGoogleCard());
            stack.Controls.Add(BuildMediaFireCard());
            stack.Controls.Add(BuildMegaCard());
            stack.Controls.Add(BuildCloudLog());

            scroll.Controls.Add(stack);
            UpdateCloudActiveLabel();
            return scroll;
        }

        private Card NewCloudCard(string provider, out Label status)
        {
            Card card = new Card();
            card.Dock = DockStyle.Top;
            card.AutoSize = true;
            card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            card.Margin = new Padding(0, 0, 0, Dp(10));
            card.Padding = new Padding(Dp(18));
            _cloudCards[provider] = card;

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Top;
            content.AutoSize = true;
            content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            content.Margin = new Padding(0);
            content.BackColor = Color.Transparent;
            content.ColumnCount = 1;
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = Dp(28);
            header.Margin = new Padding(0);
            header.BackColor = Color.Transparent;

            Label name = new Label();
            name.Text = CloudBackupService.DisplayName(provider);
            name.Font = Fonts.UI(10.5f, FontStyle.Bold);
            name.ForeColor = Palette.Text;
            name.AutoSize = true;
            name.BackColor = Color.Transparent;
            name.Location = new Point(0, Dp(4));

            status = new Label();
            status.Text = "Sin probar";
            status.Font = Fonts.UI(8.5f, FontStyle.Bold);
            status.ForeColor = Palette.Dim;
            status.AutoSize = true;
            status.BackColor = Color.Transparent;
            status.TextAlign = ContentAlignment.MiddleRight;
            status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            status.Location = new Point(Math.Max(0, header.Width - Dp(180)), Dp(6));
            Label statusLabel = status;
            header.Resize += delegate
            {
                statusLabel.Location = new Point(
                    Math.Max(0, header.ClientSize.Width - statusLabel.Width - Dp(26)), Dp(6));
            };
            header.Controls.Add(name);
            header.Controls.Add(status);
            _cloudStatus[provider] = status;

            content.Controls.Add(header);
            card.Controls.Add(content);
            return card;
        }

        private void AddCloudField(TableLayoutPanel content, string label, string placeholder,
            out InputBox box)
        {
            Label caption = MakeLabel(label, Fonts.UI(8.5f), Palette.Muted);
            caption.Margin = new Padding(0, Dp(6), 0, Dp(2));
            box = new InputBox();
            box.Dock = DockStyle.Top;
            box.Height = Dp(36);
            box.Margin = new Padding(0);
            box.Placeholder = placeholder;
            content.Controls.Add(caption);
            content.Controls.Add(box);
        }

        private FlowLayoutPanel CloudActions(string provider)
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Top;
            row.AutoSize = true;
            row.WrapContents = false;
            row.Margin = new Padding(0, Dp(10), 0, 0);
            row.BackColor = Color.Transparent;

            RoundButton validate = new RoundButton();
            validate.Text = "VALIDAR API";
            validate.Glyph = "cloud";
            validate.Width = Dp(160);
            validate.Height = Dp(36);
            validate.Margin = new Padding(0, 0, Dp(8), 0);
            validate.Click += delegate { ValidateCloudProvider(provider); };

            RoundButton use = new RoundButton();
            use.Text = "Usar este proveedor";
            use.Glyph = "check";
            use.Width = Dp(200);
            use.Height = Dp(36);
            use.Margin = new Padding(0, 0, Dp(8), 0);
            use.FillColor = Palette.Card;
            use.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            use.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            use.TextColor = Palette.Text;
            use.BorderColor = Palette.Border;
            use.Click += delegate { UseCloudProvider(provider); };

            RoundButton docs = new RoundButton();
            docs.Text = "ABRIR DOCUMENTACION";
            docs.Glyph = "book";
            docs.Width = Dp(210);
            docs.Height = Dp(36);
            docs.FillColor = Palette.Card;
            docs.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            docs.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            docs.TextColor = Palette.Text;
            docs.BorderColor = Palette.Border;
            docs.Click += delegate { OpenCloudDocs(provider); };

            row.Controls.Add(validate);
            row.Controls.Add(use);
            row.Controls.Add(docs);
            return row;
        }

        private Card BuildGoogleCard()
        {
            Label status;
            Card card = NewCloudCard(CloudBackupService.GoogleDrive, out status);
            TableLayoutPanel content = (TableLayoutPanel)card.Controls[0];
            AddCloudField(content, "Token de acceso", "ya29....", out _cloudGoogleToken);
            AddCloudField(content, "Carpeta (ID)", "ID de la carpeta destino", out _cloudGoogleFolder);
            content.Controls.Add(CloudActions(CloudBackupService.GoogleDrive));
            return card;
        }

        private Card BuildMediaFireCard()
        {
            Label status;
            Card card = NewCloudCard(CloudBackupService.MediaFire, out status);
            TableLayoutPanel content = (TableLayoutPanel)card.Controls[0];
            AddCloudField(content, "App ID", "Application ID", out _cloudMediaFireAppId);
            AddCloudField(content, "Session token", "session_token", out _cloudMediaFireSession);
            content.Controls.Add(CloudActions(CloudBackupService.MediaFire));
            return card;
        }

        private Card BuildMegaCard()
        {
            Label status;
            Card card = NewCloudCard(CloudBackupService.Mega, out status);
            TableLayoutPanel content = (TableLayoutPanel)card.Controls[0];
            AddCloudField(content, "Correo", "cuenta@ejemplo.com", out _cloudMegaEmail);
            AddCloudField(content, "Sesion MEGA", "session id", out _cloudMegaSession);
            content.Controls.Add(CloudActions(CloudBackupService.Mega));
            return card;
        }

        private Card BuildCloudLog()
        {
            Card card = new Card();
            card.Dock = DockStyle.Top;
            card.Height = Dp(190);
            card.Margin = new Padding(0, 0, 0, Dp(10));
            card.Padding = new Padding(Dp(18));

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0);
            content.BackColor = Color.Transparent;
            content.ColumnCount = 1;
            content.RowCount = 2;
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label title = MakeLabel("Resultado de las pruebas", Fonts.UI(9f, FontStyle.Bold), Palette.Dim);
            _cloudLog = new TextBox();
            _cloudLog.Multiline = true;
            _cloudLog.ReadOnly = true;
            _cloudLog.ScrollBars = ScrollBars.Vertical;
            _cloudLog.BorderStyle = BorderStyle.None;
            _cloudLog.BackColor = Palette.Input;
            _cloudLog.ForeColor = Palette.Muted;
            _cloudLog.Font = Fonts.Mono(8.5f);
            _cloudLog.Dock = DockStyle.Fill;
            _cloudLog.Margin = new Padding(0, Dp(6), 0, 0);
            content.Controls.Add(title, 0, 0);
            content.Controls.Add(_cloudLog, 0, 1);
            card.Controls.Add(content);
            return card;
        }

        private void LoadCloudSettings()
        {
            IDictionary<string, string> settings = CloudBackupService.SettingsFromConfig();
            if (_cloudGoogleToken != null) _cloudGoogleToken.Text = GetCloudSetting(settings, "google.token");
            if (_cloudGoogleFolder != null) _cloudGoogleFolder.Text = GetCloudSetting(settings, "google.folder");
            if (_cloudMediaFireAppId != null) _cloudMediaFireAppId.Text = GetCloudSetting(settings, "mediafire.appid");
            if (_cloudMediaFireSession != null) _cloudMediaFireSession.Text = GetCloudSetting(settings, "mediafire.session");
            if (_cloudMegaEmail != null) _cloudMegaEmail.Text = GetCloudSetting(settings, "mega.email");
            if (_cloudMegaSession != null) _cloudMegaSession.Text = GetCloudSetting(settings, "mega.session");
            UpdateCloudActiveLabel();
        }

        private static string GetCloudSetting(IDictionary<string, string> settings, string key)
        {
            string value;
            return settings.TryGetValue(key, out value) && value != null ? value : "";
        }

        private Dictionary<string, string> ReadCloudSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();
            settings["google.token"] = _cloudGoogleToken != null ? _cloudGoogleToken.Text.Trim() : "";
            settings["google.folder"] = _cloudGoogleFolder != null ? _cloudGoogleFolder.Text.Trim() : "";
            settings["mediafire.appid"] = _cloudMediaFireAppId != null ? _cloudMediaFireAppId.Text.Trim() : "";
            settings["mediafire.session"] = _cloudMediaFireSession != null ? _cloudMediaFireSession.Text.Trim() : "";
            settings["mega.email"] = _cloudMegaEmail != null ? _cloudMegaEmail.Text.Trim() : "";
            settings["mega.session"] = _cloudMegaSession != null ? _cloudMegaSession.Text.Trim() : "";
            return settings;
        }

        private void SaveCloudSettings()
        {
            try
            {
                AppConfig config = AppConfig.Instance;
                config.CloudSettings = ReadCloudSettings();
                if (string.IsNullOrWhiteSpace(config.CloudProvider))
                    config.CloudProvider = CloudBackupService.GoogleDrive;
                config.Save();
            }
            catch { }
        }

        private void UpdateCloudActiveLabel()
        {
            if (_cloudActiveLabel == null) return;
            string provider = AppConfig.Instance.CloudProvider;
            if (string.IsNullOrWhiteSpace(provider)) provider = CloudBackupService.GoogleDrive;
            _cloudActiveLabel.Text = "Proveedor activo: " + CloudBackupService.DisplayName(provider);
            foreach (KeyValuePair<string, Card> pair in _cloudCards)
                pair.Value.BorderColor = string.Equals(pair.Key, provider,
                    StringComparison.OrdinalIgnoreCase) ? Palette.Accent : Palette.Border;
        }

        private void UseCloudProvider(string provider)
        {
            AppConfig.Instance.CloudProvider = provider;
            SaveCloudSettings();
            UpdateCloudActiveLabel();
            if (_toast != null)
                _toast.ShowMessage("Proveedor activo: " + CloudBackupService.DisplayName(provider),
                    Palette.Accent, "cloud");
        }

        private void OpenCloudDocs(string provider)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                CloudBackupService.DocsUrl(provider)) { UseShellExecute = true }); }
            catch { }
        }

        private void CloudAppendLog(string text)
        {
            if (_cloudLog == null) return;
            try
            {
                _cloudLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text +
                    Environment.NewLine);
                _cloudLog.SelectionStart = _cloudLog.TextLength;
                _cloudLog.ScrollToCaret();
            }
            catch { }
        }

        private void SetCloudStatus(string provider, string text, Color color)
        {
            Label label;
            if (_cloudStatus.TryGetValue(provider, out label))
            {
                label.Text = text;
                label.ForeColor = color;
                if (label.Parent != null)
                    label.Location = new Point(
                        Math.Max(0, label.Parent.ClientSize.Width - label.Width - Dp(26)),
                        label.Location.Y);
            }
        }

        private void ApplyCloudResult(CloudCheckResult result)
        {
            if (result == null) return;
            Color color;
            if (result.Ok) color = Palette.Accent;
            else if (result.Reachable) color = Palette.Amber;
            else color = Palette.Red;
            SetCloudStatus(result.Provider, result.Message, color);
            CloudAppendLog(CloudBackupService.DisplayName(result.Provider) + ": " +
                result.Message + (string.IsNullOrEmpty(result.Detail) ? "" : " | " + result.Detail));
        }

        private async void ValidateAllCloudApis()
        {
            if (_cloudBusy) return;
            _cloudBusy = true;
            SaveCloudSettings();
            Dictionary<string, string> settings = ReadCloudSettings();
            foreach (string provider in CloudBackupService.Providers)
                SetCloudStatus(provider, "Comprobando...", Palette.Amber);
            CloudAppendLog("Validando Google Drive, MediaFire y MEGA...");
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(16);
                    Task<CloudCheckResult> google = CloudBackupService.ValidateAsync(
                        CloudBackupService.GoogleDrive, settings, client, CancellationToken.None);
                    Task<CloudCheckResult> mediafire = CloudBackupService.ValidateAsync(
                        CloudBackupService.MediaFire, settings, client, CancellationToken.None);
                    Task<CloudCheckResult> mega = CloudBackupService.ValidateAsync(
                        CloudBackupService.Mega, settings, client, CancellationToken.None);
                    await Task.WhenAll(google, mediafire, mega);
                    ApplyCloudResult(google.Result);
                    ApplyCloudResult(mediafire.Result);
                    ApplyCloudResult(mega.Result);
                }
            }
            catch (Exception ex)
            {
                CloudAppendLog("Error general: " + ex.Message);
            }
            finally
            {
                _cloudBusy = false;
            }
        }

        private async void ValidateCloudProvider(string provider)
        {
            if (_cloudBusy) return;
            _cloudBusy = true;
            SaveCloudSettings();
            Dictionary<string, string> settings = ReadCloudSettings();
            SetCloudStatus(provider, "Comprobando...", Palette.Amber);
            CloudAppendLog("Validando " + CloudBackupService.DisplayName(provider) + "...");
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(16);
                    CloudCheckResult result = await CloudBackupService.ValidateAsync(
                        provider, settings, client, CancellationToken.None);
                    ApplyCloudResult(result);
                }
            }
            catch (Exception ex)
            {
                SetCloudStatus(provider, "API no disponible", Palette.Red);
                CloudAppendLog(CloudBackupService.DisplayName(provider) + ": " + ex.Message);
            }
            finally
            {
                _cloudBusy = false;
            }
        }

        private Panel BuildLanguageSection()
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.BackColor = Color.Transparent;
            grid.ColumnCount = 2;
            grid.RowCount = 1;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Card left = new Card();
            left.Dock = DockStyle.Fill;
            left.Margin = new Padding(0, 0, Dp(14), 0);
            left.Padding = new Padding(Dp(16));
            TableLayoutPanel leftStack = new TableLayoutPanel();
            leftStack.Dock = DockStyle.Fill;
            leftStack.Margin = new Padding(0);
            leftStack.BackColor = Color.Transparent;
            leftStack.ColumnCount = 1;
            leftStack.RowCount = 2;
            leftStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            leftStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label leftTitle = MakeLabel("IDIOMAS DISPONIBLES", Fonts.UI(9f, FontStyle.Bold), Palette.Dim);
            _languageList = new Panel();
            _languageList.Dock = DockStyle.Fill;
            _languageList.Margin = new Padding(0, Dp(8), 0, 0);
            _languageList.AutoScroll = true;
            _languageList.BackColor = Color.Transparent;
            leftStack.Controls.Add(leftTitle, 0, 0);
            leftStack.Controls.Add(_languageList, 0, 1);
            left.Controls.Add(leftStack);

            Card right = new Card();
            right.Dock = DockStyle.Fill;
            right.Margin = new Padding(0);
            right.Padding = new Padding(Dp(22));
            TableLayoutPanel rightStack = new TableLayoutPanel();
            rightStack.Dock = DockStyle.Fill;
            rightStack.Margin = new Padding(0);
            rightStack.BackColor = Color.Transparent;
            rightStack.ColumnCount = 1;
            rightStack.RowCount = 5;
            rightStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label title = MakeLabel("IDIOMA", Fonts.UI(11f, FontStyle.Bold), Palette.Text);
            _languageActiveLabel = MakeLabel("Idioma activo: " + LanguageService.CurrentCode,
                Fonts.UI(9f, FontStyle.Bold), Palette.Accent);
            _languageActiveLabel.Margin = new Padding(0, Dp(6), 0, Dp(10));
            Label note = MakeWrapLabel(
                "Los archivos de idioma son JSON editables. Cada clave es el texto en espanol " +
                "y el valor es su traduccion. El nombre de la app no cambia con el idioma.",
                Fonts.UI(8.75f), Palette.Muted, Dp(52));
            note.Margin = new Padding(0, 0, 0, Dp(12));

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            actions.AutoSize = true;
            actions.Margin = new Padding(0);
            actions.BackColor = Color.Transparent;

            RoundButton apply = new RoundButton();
            apply.Text = "APLICAR IDIOMA";
            apply.Glyph = "globe";
            apply.Width = Dp(190);
            apply.Height = Dp(40);
            apply.Margin = new Padding(0, 0, Dp(8), 0);
            apply.Click += delegate { ApplySelectedLanguage(); };

            RoundButton add = new RoundButton();
            add.Text = "AGREGAR IDIOMA";
            add.Glyph = "plus";
            add.Width = Dp(190);
            add.Height = Dp(40);
            add.Margin = new Padding(0, 0, Dp(8), 0);
            add.FillColor = Palette.Card;
            add.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            add.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            add.TextColor = Palette.Text;
            add.BorderColor = Palette.Border;
            add.Click += delegate { AddLanguageFile(); };

            RoundButton folder = new RoundButton();
            folder.Text = "ABRIR CARPETA";
            folder.Glyph = "folder";
            folder.Width = Dp(170);
            folder.Height = Dp(40);
            folder.FillColor = Palette.Card;
            folder.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            folder.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            folder.TextColor = Palette.Text;
            folder.BorderColor = Palette.Border;
            folder.Click += delegate { OpenLanguagesFolder(); };

            actions.Controls.Add(apply);
            actions.Controls.Add(add);
            actions.Controls.Add(folder);

            Label format = MakeWrapLabel(
                "Formato: { \"code\": \"en\", \"name\": \"English\", \"strings\": { \"Texto\": \"Translation\" } }",
                Fonts.Mono(8.25f), Palette.Dim, Dp(34));
            format.Margin = new Padding(0, Dp(12), 0, 0);

            rightStack.Controls.Add(title, 0, 0);
            rightStack.Controls.Add(_languageActiveLabel, 0, 1);
            rightStack.Controls.Add(note, 0, 2);
            rightStack.Controls.Add(actions, 0, 3);
            rightStack.Controls.Add(format, 0, 4);
            right.Controls.Add(rightStack);

            grid.Controls.Add(left, 0, 0);
            grid.Controls.Add(right, 1, 0);
            p.Controls.Add(grid);
            return p;
        }

        private void RefreshLanguageList()
        {
            if (_languageList == null) return;
            _languageList.SuspendLayout();
            _languageList.Controls.Clear();
            List<LanguagePack> packs = new List<LanguagePack>(LanguageService.Available);
            for (int i = packs.Count - 1; i >= 0; i--)
            {
                LanguagePack pack = packs[i];
                NavButton button = new NavButton();
                button.Text = pack.Name + "  (" + pack.Code.ToUpperInvariant() + ")";
                button.Dock = DockStyle.Top;
                button.Height = Dp(44);
                button.Margin = new Padding(0, Dp(3), 0, Dp(3));
                button.Tag = pack.Code;
                button.Active = string.Equals(pack.Code, _languageSelectedCode,
                    StringComparison.OrdinalIgnoreCase);
                button.Click += delegate
                {
                    _languageSelectedCode = pack.Code;
                    RefreshLanguageList();
                };
                _languageList.Controls.Add(button);
            }
            _languageList.ResumeLayout();
            if (_languageActiveLabel != null)
            {
                LanguagePack current = LanguageService.Current;
                string name = current != null ? current.Name : _languageSelectedCode;
                _languageActiveLabel.Text = "Idioma activo: " + name;
            }
        }

        private void ApplySelectedLanguage()
        {
            LanguagePack pack = LanguageService.Find(_languageSelectedCode);
            if (pack == null) return;
            LanguageService.SetLanguage(pack.Code, true);
            _languageActiveLabel.Text = "Idioma activo: " + pack.Name;
            RefreshLanguageList();
            if (_toast != null)
                _toast.ShowMessage("Idioma aplicado: " + pack.Name, Palette.Accent, "globe");
        }

        private void AddLanguageFile()
        {
            try
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Selecciona un archivo JSON de idioma";
                    dialog.Filter = "Idioma JSON (*.json)|*.json|Todos los archivos (*.*)|*.*";
                    string folder = LanguageService.LanguagesDirectory;
                    try { Directory.CreateDirectory(folder); } catch { }
                    if (Directory.Exists(folder)) dialog.InitialDirectory = folder;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;

                    string code;
                    string message;
                    if (LanguageService.ImportLanguage(dialog.FileName, out code, out message))
                    {
                        _languageSelectedCode = code;
                        LanguageService.SetLanguage(code, true);
                        RefreshLanguageList();
                        if (_toast != null)
                            _toast.ShowMessage(message, Palette.Accent, "check");
                    }
                    else
                    {
                        MessageDialog.Info(this, "No se pudo agregar el idioma", message);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "No se pudo agregar el idioma", ex.Message);
            }
        }

        private void OpenLanguagesFolder()
        {
            try
            {
                string folder = LanguageService.LanguagesDirectory;
                Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Carpeta de idiomas", ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Actualizaciones
        // ------------------------------------------------------------------
        private Panel BuildUpdatesSection()
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.BackColor = Color.Transparent;
            grid.ColumnCount = 2;
            grid.RowCount = 1;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ---------------- Columna izquierda: canal / repositorio ----------------
            Card left = new Card();
            left.Dock = DockStyle.Fill;
            left.Margin = new Padding(0, 0, Dp(14), 0);
            left.Padding = new Padding(Dp(16));
            TableLayoutPanel leftStack = new TableLayoutPanel();
            leftStack.Dock = DockStyle.Fill;
            leftStack.Margin = new Padding(0);
            leftStack.BackColor = Color.Transparent;
            leftStack.ColumnCount = 1;
            leftStack.RowCount = 5;
            leftStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 4; i++)
                leftStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label leftTitle = MakeLabel("REPOSITORIO", Fonts.UI(9f, FontStyle.Bold), Palette.Dim);
            Label repo = MakeWrapLabel(ReleaseConfig.RepoDisplay, Fonts.Mono(8.75f), Palette.Accent, Dp(20));
            repo.Margin = new Padding(0, Dp(8), 0, Dp(6));

            Label autoLabel = MakeLabel("Buscar actualizaciones al iniciar", Fonts.UI(9f), Palette.Text);
            _updateAutoToggle = new ToggleSwitch();
            _updateAutoToggle.Checked = AppConfig.Instance.CheckUpdatesOnStartup;
            _updateAutoToggle.CheckedChanged += delegate
            {
                try
                {
                    AppConfig.Instance.CheckUpdatesOnStartup = _updateAutoToggle.Checked;
                    AppConfig.Instance.Save();
                }
                catch { }
            };

            Panel autoHost = new Panel();
            autoHost.Dock = DockStyle.Fill;
            autoHost.Height = Dp(34);
            autoHost.Margin = new Padding(0, Dp(4), 0, 0);
            autoHost.BackColor = Color.Transparent;
            _updateAutoToggle.Location = new Point(Dp(2), Dp(5));
            autoHost.Controls.Add(_updateAutoToggle);

            RoundButton repoButton = new RoundButton();
            repoButton.Text = "ABRIR GITHUB";
            repoButton.Glyph = "external";
            repoButton.Width = Dp(190);
            repoButton.Height = Dp(38);
            repoButton.Margin = new Padding(0, Dp(10), 0, 0);
            repoButton.FillColor = Palette.Card;
            repoButton.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            repoButton.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            repoButton.TextColor = Palette.Text;
            repoButton.BorderColor = Palette.Border;
            repoButton.Click += delegate { UpdateService.OpenUrl(ReleaseConfig.RepoUrl); };

            leftStack.Controls.Add(leftTitle, 0, 0);
            leftStack.Controls.Add(repo, 0, 1);
            leftStack.Controls.Add(autoLabel, 0, 2);
            leftStack.Controls.Add(autoHost, 0, 3);
            leftStack.Controls.Add(repoButton, 0, 4);
            left.Controls.Add(leftStack);

            // ---------------- Columna derecha: estado y acciones ----------------
            Card right = new Card();
            right.Dock = DockStyle.Fill;
            right.Margin = new Padding(0);
            right.Padding = new Padding(Dp(22));
            TableLayoutPanel rightStack = new TableLayoutPanel();
            rightStack.Dock = DockStyle.Fill;
            rightStack.Margin = new Padding(0);
            rightStack.BackColor = Color.Transparent;
            rightStack.ColumnCount = 1;
            rightStack.RowCount = 6;
            rightStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label title = MakeLabel("ACTUALIZACIONES", Fonts.UI(11f, FontStyle.Bold), Palette.Text);
            _updateCurrentLabel = MakeLabel("Version actual: " + UpdateService.CurrentVersion,
                Fonts.UI(9f, FontStyle.Bold), Palette.Accent);
            _updateCurrentLabel.Margin = new Padding(0, Dp(6), 0, Dp(10));

            Label desc = MakeWrapLabel(
                "Se consulta la ultima release publicada en GitHub. Si hay una version " +
                "nueva se muestra el enlace para descargarla; la app no se actualiza sola.",
                Fonts.UI(9f), Palette.Muted, Dp(46));
            desc.Margin = new Padding(0, 0, 0, Dp(10));

            _updateStatusLabel = MakeWrapLabel(
                "Pulsa BUSCAR ACTUALIZACIONES para comprobar la version disponible.",
                Fonts.UI(9f), Palette.Muted, Dp(44));
            _updateStatusLabel.Margin = new Padding(0, 0, 0, Dp(8));

            _updateNotesLabel = MakeWrapLabel("", Fonts.Mono(8.25f), Palette.Dim, Dp(120));
            _updateNotesLabel.Visible = false;
            _updateNotesLabel.Margin = new Padding(0, 0, 0, Dp(8));

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            actions.AutoSize = true;
            actions.Margin = new Padding(0);
            actions.BackColor = Color.Transparent;

            _updateCheckButton = new RoundButton();
            _updateCheckButton.Text = "BUSCAR ACTUALIZACIONES";
            _updateCheckButton.Glyph = "restart";
            _updateCheckButton.Width = Dp(250);
            _updateCheckButton.Height = Dp(40);
            _updateCheckButton.Margin = new Padding(0, 0, Dp(8), 0);
            _updateCheckButton.Click += delegate { CheckForUpdates(true); };

            _updateOpenButton = new RoundButton();
            _updateOpenButton.Text = "ABRIR RELEASE";
            _updateOpenButton.Glyph = "external";
            _updateOpenButton.Width = Dp(190);
            _updateOpenButton.Height = Dp(40);
            _updateOpenButton.Visible = false;
            _updateOpenButton.Click += delegate
            {
                string url = _updateOpenButton.Tag as string;
                UpdateService.OpenUrl(string.IsNullOrWhiteSpace(url) ? ReleaseConfig.ReleasesUrl : url);
            };

            actions.Controls.Add(_updateCheckButton);
            actions.Controls.Add(_updateOpenButton);

            rightStack.Controls.Add(title, 0, 0);
            rightStack.Controls.Add(_updateCurrentLabel, 0, 1);
            rightStack.Controls.Add(desc, 0, 2);
            rightStack.Controls.Add(_updateStatusLabel, 0, 3);
            rightStack.Controls.Add(_updateNotesLabel, 0, 4);
            rightStack.Controls.Add(actions, 0, 5);
            right.Controls.Add(rightStack);

            grid.Controls.Add(left, 0, 0);
            grid.Controls.Add(right, 1, 0);
            p.Controls.Add(grid);
            return p;
        }

        // Comprobacion manual: muestra siempre el resultado, incluidos errores.
        private async void CheckForUpdates(bool manual)
        {
            if (_updateBusy) return;
            _updateBusy = true;
            if (_updateCheckButton != null) _updateCheckButton.Enabled = false;
            if (manual && _updateStatusLabel != null)
            {
                _updateStatusLabel.ForeColor = Palette.Muted;
                _updateStatusLabel.Text = "Comprobando actualizaciones...";
            }

            UpdateInfo info = await UpdateService.CheckAsync();
            _updateBusy = false;
            if (IsDisposed || Disposing) return;
            if (_updateCheckButton != null) _updateCheckButton.Enabled = true;
            ApplyUpdateInfo(info, manual);
        }

        // Comprobacion silenciosa al iniciar: solo avisa si hay una version
        // nueva. Los errores de red se ignoran para no molestar al arrancar.
        private void CheckForUpdatesOnStartup()
        {
            if (!AppConfig.Instance.CheckUpdatesOnStartup) return;
            CheckForUpdates(false);
        }

        private void ApplyUpdateInfo(UpdateInfo info, bool manual)
        {
            if (info == null) return;

            if (_updateCurrentLabel != null)
                _updateCurrentLabel.Text = "Version actual: " +
                    (string.IsNullOrWhiteSpace(info.CurrentVersion) ? UpdateService.CurrentVersion : info.CurrentVersion);

            if (info.HasUpdate)
            {
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.ForeColor = Palette.Accent;
                    _updateStatusLabel.Text = "Hay una version nueva disponible: " +
                        (string.IsNullOrWhiteSpace(info.ReleaseName) ? info.LatestVersion : info.ReleaseName) +
                        "  (" + info.LatestVersion + ")";
                }
                if (_updateNotesLabel != null)
                {
                    string notes = (info.Notes ?? "").Trim();
                    if (notes.Length > 1400) notes = notes.Substring(0, 1400) + "...";
                    _updateNotesLabel.Text = notes;
                    _updateNotesLabel.Visible = notes.Length > 0;
                }
                if (_updateOpenButton != null)
                {
                    _updateOpenButton.Tag = info.ReleaseUrl;
                    _updateOpenButton.Visible = true;
                }
                if (_toast != null)
                    _toast.ShowMessage("Nueva version " + info.LatestVersion + " disponible",
                        Palette.Accent, "restart");
                return;
            }

            if (_updateOpenButton != null) _updateOpenButton.Visible = false;

            bool failed = !string.IsNullOrWhiteSpace(info.Error);
            if (_updateStatusLabel != null && (manual || failed))
            {
                _updateStatusLabel.ForeColor = failed ? Palette.Amber : Palette.Muted;
                _updateStatusLabel.Text = failed
                    ? info.Error
                    : "Estas en la ultima version (" + info.CurrentVersion + ").";
            }
            if (!manual && _updateNotesLabel != null)
                _updateNotesLabel.Visible = false;
        }
    }
}
