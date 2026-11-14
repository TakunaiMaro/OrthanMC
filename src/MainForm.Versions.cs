#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace McPanel
{
    public partial class MainForm
    {
        private static readonly string[] VersionChannels = { "Vanilla", "Snapshots", "Plugins", "Modded" };
        private static readonly string[] VersionPluginProviders = { "Paper", "Folia", "Purpur" };
        // El selector usa los nombres pedidos; el nombre visible del proveedor
        // sigue siendo NeoForge/Forge/Fabric en las etiquetas.
        private static readonly string[] VersionModProviders = { "neoforged", "forge", "fabric" };

        private Card _cardVersionCurrent;
        private Label _verCurrentName;
        private Label _verCurrentFile;
        private Label _verCurrentJava;
        private Label _verJavaTitle;
        private Label _verJavaPath;
        private RoundButton _verJavaButton;
        private Label _verOldJars;
        private RoundButton _verOpenJars;
        private Label _verLastInstall;
        private SelectBox _verChannel;
        private Label _verProviderLabel;
        private SelectBox _verProvider;
        private InputBox _verSearch;
        private ListBox _verList;
        private TableLayoutPanel _verPickerStack;
        private Label _verSelectedInfo;
        private UsageBar _verProgress;
        private Label _verProgressText;
        private RoundButton _verInstall;
        private Label _verHint;

        private readonly List<JarOption> _verAllOptions = new List<JarOption>();
        private readonly Dictionary<string, List<JarOption>> _verCatalogCache =
            new Dictionary<string, List<JarOption>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<JarProvider> _verPluginProviders = JarCatalog.GetPluginProviders();
        private readonly List<JarProvider> _verModProviders = JarCatalog.GetModProviders();
        private JarProvider _verVanillaProvider;
        private JarProvider _verSnapshotProvider;
        private bool _verBuilding;
        private bool _verFilling;
        private bool _verCatalogLoading;
        private int _verLoadToken;
        private int _verCurrentJavaMajor;
        private string _verCurrentLabel = "server";

        private Panel BuildVersions()
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.Padding = new Padding(0);
            grid.BackColor = Color.Transparent;
            grid.ColumnCount = 2;
            grid.RowCount = 1;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ---------------- Version actual / Java ----------------
            _cardVersionCurrent = new Card();
            _cardVersionCurrent.Dock = DockStyle.Fill;
            _cardVersionCurrent.Margin = new Padding(0, 0, Dp(7), 0);

            TableLayoutPanel left = MakeCardStack(1, 16);
            left.RowStyles[5] = new RowStyle(SizeType.Absolute, Dp(13));
            left.RowStyles[10] = new RowStyle(SizeType.Absolute, Dp(13));
            left.RowStyles[15] = new RowStyle(SizeType.Percent, 100f);

            Label title = MakeLabel("VERSION ACTUAL", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label desc = MakeLabel("server.jar instalado en la carpeta del servidor", Fonts.UI(9f), Palette.Muted);

            _verCurrentName = MakeLabel("Detectando...", Fonts.UI(15f, FontStyle.Bold), Palette.Text);
            _verCurrentName.AutoSize = false;
            _verCurrentName.Dock = DockStyle.Fill;
            _verCurrentName.AutoEllipsis = true;
            _verCurrentName.Height = Dp(30);
            _verCurrentName.Margin = new Padding(0, Dp(8), 0, Dp(2));

            _verCurrentFile = MakeWrapLabel("--", Fonts.UI(8.75f), Palette.Muted, Dp(36));
            _verCurrentJava = MakeLabel("Java requerido: --", Fonts.UI(9.25f), Palette.Text);

            Label sep1 = MakeSectionLine();
            Label sep2 = MakeSectionLine();

            Label javaTitle = MakeLabel("ENTORNO JAVA", Fonts.UI(9f, FontStyle.Bold), Palette.Dim);
            _verJavaTitle = MakeLabel("Java: --", Fonts.UI(9.5f), Palette.Muted);
            _verJavaPath = MakeWrapLabel("--", Fonts.UI(8.25f), Palette.Dim, Dp(32));

            _verJavaButton = MakeSecondaryButton("DESCARGAR JAVA", "download");
            _verJavaButton.Height = Dp(34);
            _verJavaButton.Margin = new Padding(0, Dp(4), 0, Dp(2));
            _verJavaButton.Visible = false;

            Label oldTitle = MakeLabel("JARS ANTERIORES", Fonts.UI(9f, FontStyle.Bold), Palette.Dim);
            _verOldJars = MakeWrapLabel("--", Fonts.UI(8.5f), Palette.Muted, Dp(32));

            _verOpenJars = MakeSecondaryButton("ABRIR CARPETA DE JARS", "folder");
            _verOpenJars.Height = Dp(34);
            _verOpenJars.Margin = new Padding(0, Dp(4), 0, Dp(2));

            _verLastInstall = MakeWrapLabel("Ultimo cambio desde el panel: --", Fonts.UI(8.5f), Palette.Dim, Dp(34));

            Label note = MakeWrapLabel(
                "Compatible con Vanilla, Snapshots, Paper, Purpur, Folia, Fabric, Forge y NeoForge. " +
                "Forge y NeoForge se instalan con su instalador oficial.",
                Fonts.UI(8.25f), Palette.Dim, Dp(52));

            left.Controls.Add(title, 0, 0);
            left.Controls.Add(desc, 0, 1);
            left.Controls.Add(_verCurrentName, 0, 2);
            left.Controls.Add(_verCurrentFile, 0, 3);
            left.Controls.Add(_verCurrentJava, 0, 4);
            left.Controls.Add(sep1, 0, 5);
            left.Controls.Add(javaTitle, 0, 6);
            left.Controls.Add(_verJavaTitle, 0, 7);
            left.Controls.Add(_verJavaPath, 0, 8);
            left.Controls.Add(_verJavaButton, 0, 9);
            left.Controls.Add(sep2, 0, 10);
            left.Controls.Add(oldTitle, 0, 11);
            left.Controls.Add(_verOldJars, 0, 12);
            left.Controls.Add(_verOpenJars, 0, 13);
            left.Controls.Add(_verLastInstall, 0, 14);
            left.Controls.Add(note, 0, 15);
            _cardVersionCurrent.Controls.Add(left);

            // ---------------- Elegir version ----------------
            Card cardPicker = new Card();
            cardPicker.Dock = DockStyle.Fill;
            cardPicker.Margin = new Padding(Dp(7), 0, 0, 0);

            TableLayoutPanel right = MakeCardStack(2, 11);
            _verPickerStack = right;
            right.ColumnStyles.Clear();
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(96)));
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            right.RowStyles[2] = new RowStyle(SizeType.Absolute, Dp(42));
            right.RowStyles[3] = new RowStyle(SizeType.Absolute, 0f);
            right.RowStyles[4] = new RowStyle(SizeType.Absolute, Dp(44));
            right.RowStyles[5] = new RowStyle(SizeType.Percent, 100f);
            right.RowStyles[6] = new RowStyle(SizeType.Absolute, Dp(40));
            right.RowStyles[7] = new RowStyle(SizeType.Absolute, Dp(14));
            right.RowStyles[9] = new RowStyle(SizeType.Absolute, Dp(46));

            Label pickerTitle = MakeLabel("CAMBIAR SERVER.JAR", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label pickerDesc = MakeLabel(
                "Se descarga, se verifica el hash y se instala con copia previa del mundo",
                Fonts.UI(9f), Palette.Muted);

            Label channelLabel = MakeLabel("Canal", Fonts.UI(9.5f), Palette.Muted);
            channelLabel.Anchor = AnchorStyles.Left;
            _verChannel = new SelectBox(VersionChannels, VersionChannels[0]);
            _verChannel.Dock = DockStyle.Fill;
            _verChannel.Margin = new Padding(0, Dp(5), 0, Dp(5));

            _verProviderLabel = MakeLabel("Proveedor", Fonts.UI(9.5f), Palette.Muted);
            _verProviderLabel.Anchor = AnchorStyles.Left;
            _verProvider = new SelectBox(VersionPluginProviders, VersionPluginProviders[0]);
            _verProvider.Dock = DockStyle.Fill;
            _verProvider.Margin = new Padding(0, Dp(5), 0, Dp(5));
            _verProviderLabel.Visible = false;
            _verProvider.Visible = false;

            _verSearch = new InputBox();
            _verSearch.Placeholder = "Buscar version...";
            _verSearch.Dock = DockStyle.Fill;
            _verSearch.Margin = new Padding(0, Dp(3), 0, Dp(5));

            _verList = new ListBox();
            _verList.BorderStyle = BorderStyle.None;
            _verList.BackColor = Palette.Card;
            _verList.ForeColor = Palette.Text;
            _verList.Font = Fonts.UI(9f);
            _verList.IntegralHeight = false;
            _verList.DrawMode = DrawMode.OwnerDrawFixed;
            _verList.ItemHeight = Dp(42);
            _verList.Dock = DockStyle.Fill;
            _verList.Margin = new Padding(0, Dp(4), 0, Dp(2));
            _verList.DrawItem += VersionList_DrawItem;

            _verSelectedInfo = MakeWrapLabel("Selecciona una version para continuar.",
                Fonts.UI(8.75f), Palette.Muted, Dp(38));

            _verProgress = new UsageBar();
            _verProgress.Dock = DockStyle.Fill;
            _verProgress.Height = Dp(8);
            _verProgress.Margin = new Padding(0, Dp(3), 0, Dp(4));

            _verProgressText = MakeLabel("", Fonts.UI(8.5f), Palette.Muted);

            _verInstall = new RoundButton();
            _verInstall.Text = "DESCARGAR E INSTALAR";
            _verInstall.Glyph = "download";
            _verInstall.Dock = DockStyle.Fill;
            _verInstall.Margin = new Padding(0, Dp(4), 0, Dp(2));

            _verHint = MakeWrapLabel(
                "El servidor se detiene solo durante el cambio. Antes se guarda una copia " +
                "completa del mundo y el jar anterior en backups\\jars.",
                Fonts.UI(8.25f), Palette.Dim, Dp(34));

            right.Controls.Add(pickerTitle, 0, 0);
            right.SetColumnSpan(pickerTitle, 2);
            right.Controls.Add(pickerDesc, 0, 1);
            right.SetColumnSpan(pickerDesc, 2);
            right.Controls.Add(channelLabel, 0, 2);
            right.Controls.Add(_verChannel, 1, 2);
            right.Controls.Add(_verProviderLabel, 0, 3);
            right.Controls.Add(_verProvider, 1, 3);
            right.Controls.Add(_verSearch, 0, 4);
            right.SetColumnSpan(_verSearch, 2);
            right.Controls.Add(_verList, 0, 5);
            right.SetColumnSpan(_verList, 2);
            right.Controls.Add(_verSelectedInfo, 0, 6);
            right.SetColumnSpan(_verSelectedInfo, 2);
            right.Controls.Add(_verProgress, 0, 7);
            right.SetColumnSpan(_verProgress, 2);
            right.Controls.Add(_verProgressText, 0, 8);
            right.SetColumnSpan(_verProgressText, 2);
            right.Controls.Add(_verInstall, 0, 9);
            right.SetColumnSpan(_verInstall, 2);
            right.Controls.Add(_verHint, 0, 10);
            right.SetColumnSpan(_verHint, 2);
            cardPicker.Controls.Add(right);

            grid.Controls.Add(_cardVersionCurrent, 0, 0);
            grid.Controls.Add(cardPicker, 1, 0);
            p.Controls.Add(grid);

            _verBuilding = true;
            _verChannel.SelectedIndexChanged += VersionChannelChanged;
            _verProvider.SelectedIndexChanged += VersionProviderChanged;
            _verSearch.TextChanged += delegate { FillVersionList(); };
            _verList.SelectedIndexChanged += delegate { UpdateSelectedVersion(); };
            _verInstall.Click += delegate { InstallSelectedVersion(); };
            _verJavaButton.Click += delegate { DownloadJavaForCurrent(); };
            _verOpenJars.Click += delegate { OpenOldJarsFolder(); };
            _verBuilding = false;

            _verInstall.Enabled = false;
            RefreshJarCurrent();
            return p;
        }

        private RoundButton MakeSecondaryButton(string text, string glyph)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Glyph = glyph;
            button.Dock = DockStyle.Top;
            button.FillColor = Palette.Card;
            button.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            button.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            button.TextColor = Palette.Text;
            button.BorderColor = Palette.Border;
            return button;
        }

        private Label MakeSectionLine()
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.BackColor = Palette.Border;
            label.Margin = new Padding(0, Dp(6), 0, Dp(6));
            return label;
        }

        private JarProvider CurrentVersionProvider()
        {
            if (_verChannel.SelectedIndex == 1)
            {
                if (_verSnapshotProvider == null) _verSnapshotProvider = JarCatalog.GetVanillaProvider(true);
                return _verSnapshotProvider;
            }
            if (_verChannel.SelectedIndex == 2)
            {
                JarProvider provider = SelectedProvider(_verPluginProviders, _verProvider.SelectedItem);
                return provider != null ? provider : _verPluginProviders[0];
            }
            if (_verChannel.SelectedIndex == 3)
            {
                JarProvider provider = SelectedProvider(_verModProviders, _verProvider.SelectedItem);
                return provider != null ? provider : _verModProviders[0];
            }
            if (_verVanillaProvider == null) _verVanillaProvider = JarCatalog.GetVanillaProvider(false);
            return _verVanillaProvider;
        }

        // El orden visible del selector no tiene que coincidir con el orden
        // interno del catalogo: se busca por Id/Nombre y nunca por indice.
        private static JarProvider SelectedProvider(List<JarProvider> providers, string selected)
        {
            if (providers == null || providers.Count == 0) return null;
            if (!string.IsNullOrWhiteSpace(selected))
            {
                foreach (JarProvider provider in providers)
                {
                    if (string.Equals(provider.Id, selected, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(provider.Name, selected, StringComparison.OrdinalIgnoreCase))
                        return provider;
                }
            }
            return providers[0];
        }

        private JarProvider VersionProviderFor(JarOption option)
        {
            if (option == null) return null;
            if (string.Equals(option.ProviderId, "vanilla", StringComparison.OrdinalIgnoreCase))
                return _verVanillaProvider != null ? _verVanillaProvider : JarCatalog.GetVanillaProvider(false);
            if (string.Equals(option.ProviderId, "snapshot", StringComparison.OrdinalIgnoreCase))
                return _verSnapshotProvider != null ? _verSnapshotProvider : JarCatalog.GetVanillaProvider(true);
            foreach (JarProvider provider in _verPluginProviders)
            {
                if (string.Equals(provider.Id, option.ProviderId, StringComparison.OrdinalIgnoreCase))
                    return provider;
            }
            foreach (JarProvider provider in _verModProviders)
            {
                if (string.Equals(provider.Id, option.ProviderId, StringComparison.OrdinalIgnoreCase))
                    return provider;
            }
            return CurrentVersionProvider();
        }

        private void VersionChannelChanged(object sender, EventArgs e)
        {
            if (_verBuilding) return;
            int index = _verChannel.SelectedIndex;
            bool providers = index == 2 || index == 3;
            _verProviderLabel.Visible = providers;
            _verProvider.Visible = providers;
            if (_verPickerStack != null)
                _verPickerStack.RowStyles[3].Height = providers ? Dp(42) : 0f;

            // Cambiar de canal tambien cambia la lista de proveedores. Se evita
            // que SetItems dispare dos cargas seguidas del catalogo.
            if (providers)
            {
                _verBuilding = true;
                try
                {
                    if (index == 2) _verProvider.SetItems(VersionPluginProviders, "Paper");
                    else _verProvider.SetItems(VersionModProviders, "neoforged");
                }
                finally
                {
                    _verBuilding = false;
                }
            }
            LoadVersionList();
        }

        private void VersionProviderChanged(object sender, EventArgs e)
        {
            if (_verBuilding) return;
            int index = _verChannel.SelectedIndex;
            if (index != 2 && index != 3) return;
            LoadVersionList();
        }

        private void LoadVersionList()
        {
            if (_verBuilding || !IsHandleCreated) return;
            JarProvider provider = CurrentVersionProvider();
            if (provider == null) return;

            int token = ++_verLoadToken;
            List<JarOption> cached;
            if (_verCatalogCache.TryGetValue(provider.Id, out cached))
            {
                _verCatalogLoading = false;
                FillVersionListFrom(cached);
                return;
            }

            _verCatalogLoading = true;
            _verAllOptions.Clear();
            _verList.BeginUpdate();
            _verList.Items.Clear();
            _verList.EndUpdate();
            _verSelectedInfo.Text = "Cargando versiones de " + provider.Name + "...";
            _verProgress.Value = 0;
            _verProgressText.Text = "";
            _verList.Enabled = false;
            _verInstall.Enabled = false;

            provider.List().ContinueWith(delegate(Task<List<JarOption>> task)
            {
                try
                {
                    if (!IsHandleCreated || IsDisposed) return;
                    BeginInvoke(new Action(delegate
                    {
                        if (token != _verLoadToken) return;
                        _verCatalogLoading = false;
                        if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
                        {
                            _verCatalogCache[provider.Id] = task.Result;
                            FillVersionListFrom(task.Result);
                        }
                        else
                        {
                            Exception error = task.Exception != null && task.Exception.InnerException != null
                                ? task.Exception.InnerException : null;
                            _verSelectedInfo.Text = "No se pudieron cargar las versiones: " +
                                (error != null ? error.Message : "error de red");
                            _verProgressText.Text = "";
                            _verList.Enabled = !_versionBusy;
                        }
                    }));
                }
                catch { }
            });
        }

        private void FillVersionListFrom(List<JarOption> options)
        {
            _verAllOptions.Clear();
            if (options != null) _verAllOptions.AddRange(options);
            FillVersionList();
        }

        private void FillVersionList()
        {
            if (_verList == null || _verFilling) return;
            _verFilling = true;
            try
            {
                string query = _verSearch != null ? (_verSearch.Text ?? "").Trim() : "";
                _verList.BeginUpdate();
                _verList.Items.Clear();
                int added = 0;
                foreach (JarOption option in _verAllOptions)
                {
                    if (query.Length > 0 &&
                        option.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    _verList.Items.Add(option);
                    added++;
                    if (added >= 400) break;
                }
                _verList.EndUpdate();
                _verList.Enabled = !_versionBusy && !_verCatalogLoading && added > 0;
                if (added > 0) _verList.SelectedIndex = 0;
                else if (!_verCatalogLoading)
                    _verSelectedInfo.Text = _verAllOptions.Count == 0
                        ? "No hay versiones disponibles para este canal."
                        : "Ninguna version coincide con la busqueda.";
            }
            finally
            {
                _verFilling = false;
            }
            UpdateSelectedVersion();
        }

        private JarOption SelectedVersionOption()
        {
            if (_verList == null)
                return null;
            return _verList.SelectedItem as JarOption;
        }

        private void UpdateSelectedVersion()
        {
            if (_verSelectedInfo == null) return;
            JarOption option = SelectedVersionOption();
            if (option == null)
            {
                if (!_verCatalogLoading && _verAllOptions.Count == 0)
                    _verSelectedInfo.Text = "Selecciona un canal para cargar las versiones.";
                _verInstall.Enabled = false;
                return;
            }

            int java = JavaForMinecraft(option.MinecraftVersion);
            string text = option.Label;
            if (java > 0) text += "   |   Java requerido: " + java;
            else text += "   |   Java se detectara al descargar";
            if (string.Equals(option.ProviderId, "paper", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(option.ProviderId, "folia", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(option.ProviderId, "purpur", StringComparison.OrdinalIgnoreCase))
                text += "   |   ultimo build estable";
            else if (string.Equals(option.ProviderId, "forge", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(option.ProviderId, "neoforged", StringComparison.OrdinalIgnoreCase))
                text += "   |   instalador oficial";
            else if (string.Equals(option.ProviderId, "fabric", StringComparison.OrdinalIgnoreCase))
                text += "   |   fabric server launcher";
            _verSelectedInfo.Text = text;
            _verInstall.Enabled = !_versionBusy && !_verCatalogLoading;
        }

        private void VersionList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            JarOption option = _verList.Items[e.Index] as JarOption;
            if (option == null) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush bg = new SolidBrush(selected ? Palette.AccentDim : Palette.Card))
                e.Graphics.FillRectangle(bg, e.Bounds);

            Rectangle row = e.Bounds;
            Rectangle titleRect = new Rectangle(row.X + 8, row.Y + 4, row.Width - 16, Dp(20));
            Rectangle subRect = new Rectangle(row.X + 8, row.Y + Dp(23), row.Width - 16, Dp(16));

            TextRenderer.DrawText(e.Graphics, option.Label, Fonts.UI(9.5f, FontStyle.Bold),
                titleRect, Palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            string sub = option.Channel.ToUpperInvariant() + "  -  " + option.ProviderName;
            int java = JavaForMinecraft(option.MinecraftVersion);
            if (java > 0) sub += "  -  Java " + java;
            TextRenderer.DrawText(e.Graphics, sub, Fonts.UI(8.25f),
                subRect, Palette.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        private void RefreshJarCurrent()
        {
            // La instalacion corre en segundo plano y puede terminar justo
            // cuando el usuario cierra el panel. No hay que recrear controles
            // sobre una ventana ya destruida.
            if (_verCurrentName == null || IsDisposed || Disposing) return;

            JarMetadata meta = new JarMetadata();
            FileInfo file = new FileInfo(_paths.Jar);
            bool exists = file.Exists;
            if (exists)
            {
                try { meta = JarInspector.Inspect(_paths.Jar); } catch { }
            }

            AppConfig cfg = AppConfig.Instance;
            bool fromPanel = exists && cfg.InstalledJarSize > 0 &&
                cfg.InstalledJarSize == file.Length &&
                cfg.InstalledJarWriteTicks == file.LastWriteTimeUtc.Ticks &&
                !string.IsNullOrWhiteSpace(cfg.InstalledJarVersion);

            string providerName;
            string version;
            string build = "";
            if (fromPanel)
            {
                providerName = ProviderDisplayName(cfg.InstalledJarProvider);
                version = cfg.InstalledJarVersion;
                build = cfg.InstalledJarBuild;
            }
            else
            {
                providerName = string.IsNullOrWhiteSpace(meta.Provider) ? "Minecraft" : meta.Provider;
                version = !string.IsNullOrWhiteSpace(meta.Id) ? meta.Id : "desconocida";
            }

            _verCurrentLabel = (providerName + "-" + version).Replace(' ', '-');
            _verCurrentName.Text = exists ? providerName + " " + version : "server.jar no encontrado";
            _verCurrentName.ForeColor = exists ? Palette.Text : Palette.Red;
            _verCurrentFile.Text = exists
                ? file.Name + "  -  " + Utils.HumanBytes(file.Length) +
                  "\r\nModificado: " + file.LastWriteTime.ToString("dd/MM/yyyy HH:mm")
                : _paths.Jar;

            int java = meta.JavaMajor;
            if (java <= 0 && !string.IsNullOrWhiteSpace(meta.Id)) java = JavaForMinecraft(meta.Id);
            if (java <= 0 && !string.IsNullOrWhiteSpace(version)) java = JavaForMinecraft(version);
            _verCurrentJavaMajor = java;

            string versionId = !string.IsNullOrWhiteSpace(meta.Id) ? meta.Id : version;
            if (_versionLabel != null)
                _versionLabel.Text = "MC " + versionId + " - Java " + (java > 0 ? java.ToString() : "?");

            ApplyJavaState(java);
            _verCurrentJava.Text = "Java requerido: " + (java > 0 ? java.ToString() : "--");
            if (!string.IsNullOrWhiteSpace(build))
                _verCurrentJava.Text += "   |   build " + build;

            _verLastInstall.Text = string.IsNullOrWhiteSpace(cfg.InstalledJarVersion)
                ? "Ultimo cambio desde el panel: --"
                : "Ultimo cambio desde el panel: " + ProviderDisplayName(cfg.InstalledJarProvider) +
                  " " + cfg.InstalledJarVersion +
                  (string.IsNullOrWhiteSpace(cfg.InstalledJarBuild) ? "" : " (build " + cfg.InstalledJarBuild + ")");

            UpdateOldJarsInfo();
        }

        private void ApplyJavaState(int major)
        {
            if (_verJavaTitle == null) return;
            if (major <= 0)
            {
                _verJavaTitle.Text = "Java: version desconocida";
                _verJavaTitle.ForeColor = Palette.Muted;
                _verJavaPath.Text = "El jar no declara su version de Java. Se usara la instalacion disponible.";
                _verJavaButton.Visible = false;
                return;
            }

            string javaExe = JavaRuntimeManager.Find(major);
            if (!string.IsNullOrWhiteSpace(javaExe))
            {
                _verJavaTitle.Text = "Java " + major + " detectado";
                _verJavaTitle.ForeColor = Palette.Accent;
                _verJavaPath.Text = javaExe;
                _verJavaButton.Visible = false;
            }
            else
            {
                _verJavaTitle.Text = "Java " + major + " no instalado";
                _verJavaTitle.ForeColor = Palette.Amber;
                _verJavaPath.Text = "Se pedira descargar Temurin JDK " + major + " al instalar.";
                _verJavaButton.Text = "DESCARGAR JAVA " + major;
                _verJavaButton.Visible = true;
                _verJavaButton.Enabled = !_versionBusy;
            }
        }

        private void UpdateOldJarsInfo()
        {
            if (_verOldJars == null) return;
            int count = 0;
            try
            {
                string dir = ServerProjects.JarsBackupDirFor(_paths.ServerDir);
                if (Directory.Exists(dir))
                    count = Directory.GetFiles(dir, "server-*.jar").Length;
            }
            catch { }
            _verOldJars.Text = count == 0
                ? "Todavia no hay jars anteriores guardados. Se conservaran los ultimos 3."
                : count + " jar(s) guardados. Se conservan los ultimos 3.";
        }

        private void OpenOldJarsFolder()
        {
            try
            {
                ServerProjects.MigrateLegacyBackups(_paths.ServerDir);
                string dir = ServerProjects.JarsBackupDirFor(_paths.ServerDir);
                Directory.CreateDirectory(dir);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "explorer.exe";
                psi.Arguments = "\"" + dir + "\"";
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Jars anteriores", ex.Message);
            }
        }

        private void SetVersionBusy(bool busy)
        {
            if (IsDisposed || Disposing) return;
            if (_verChannel != null) _verChannel.Enabled = !busy;
            if (_verProvider != null) _verProvider.Enabled = !busy;
            if (_verSearch != null) _verSearch.Enabled = !busy;
            if (_verList != null) _verList.Enabled = !busy && !_verCatalogLoading;
            if (_verInstall != null)
            {
                _verInstall.Text = busy ? "TRABAJANDO..." : "DESCARGAR E INSTALAR";
                _verInstall.Enabled = !busy && !_verCatalogLoading && SelectedVersionOption() != null;
            }
            if (_verOpenJars != null) _verOpenJars.Enabled = !busy;
            if (_verJavaButton != null && _verJavaButton.Visible) _verJavaButton.Enabled = !busy;
            if (_bkNow != null) _bkNow.Enabled = !busy && !_backupRunning;
        }

        private void SetVersionProgress(double value, string text)
        {
            if (IsDisposed || Disposing) return;
            if (_verProgress != null) _verProgress.Value = value;
            if (_verProgressText != null) _verProgressText.Text = text ?? "";
        }

        private void SafeVersionProgress(double fraction, string text)
        {
            try
            {
                if (!IsHandleCreated || IsDisposed) return;
                BeginInvoke(new Action(delegate { SetVersionProgress(fraction, text); }));
            }
            catch { }
        }

        private async void InstallSelectedVersion()
        {
            if (_versionBusy) return;
            if (_backupRunning)
            {
                MessageDialog.Info(this, "Cambiar version",
                    "Hay una copia de seguridad en curso. Espera a que termine para cambiar el server.jar.");
                return;
            }

            JarOption option = SelectedVersionOption();
            if (option == null) return;

            JarProvider provider = VersionProviderFor(option);
            if (provider == null)
            {
                MessageDialog.Info(this, "Cambiar version", "No se encontro el proveedor de esta version.");
                return;
            }

            _versionBusy = true;
            SetVersionBusy(true);
            SetVersionProgress(0, "Consultando " + option.Label + "...");
            string downloaded = "";
            bool stoppedForInstall = false;
            bool restartPrompted = false;

            try
            {
                AppendConsole("[PANEL] Preparando cambio a " + option.Label + "...");
                JarDownload download = await provider.Resolve(option);
                if (download == null || string.IsNullOrWhiteSpace(download.Url))
                    throw new Exception("El proveedor no devolvio una descarga valida.");

                string fileName = SafeFileName(download.FileName);
                if (!fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) fileName += ".jar";
                string downloadDir = Path.Combine(Path.GetTempPath(), "McPanel", "downloads");
                Directory.CreateDirectory(downloadDir);
                downloaded = Path.Combine(downloadDir, fileName);

                SetVersionProgress(0, "Descargando " + download.FileName + "...");
                AppendConsole("[PANEL] Descargando " + download.FileName + "...");
                await HttpDownloader.DownloadAsync(download.Url, downloaded, download.Size,
                    download.Hash, download.HashAlgorithm,
                    delegate(double fraction, string text) { SafeVersionProgress(fraction, text); },
                    CancellationToken.None);
                SetVersionProgress(1, "Descarga verificada.");

                JarMetadata meta = JarInspector.Inspect(downloaded);
                int javaMajor = meta.JavaMajor;
                if (javaMajor <= 0) javaMajor = JavaForMinecraft(option.MinecraftVersion);
                if (javaMajor <= 0)
                {
                    MessageDialog.Info(this, "Cambiar version",
                        "No se pudo determinar que version de Java necesita este jar. No se instalara.");
                    return;
                }

                string javaExe = JavaRuntimeManager.Find(javaMajor);
                if (string.IsNullOrWhiteSpace(javaExe))
                {
                    SetVersionProgress(0.5, "Se necesita Java " + javaMajor + ".");
                    javaExe = await DownloadJavaAsync(javaMajor);
                    if (string.IsNullOrWhiteSpace(javaExe))
                    {
                        SetVersionProgress(0, "Instalacion cancelada: falta Java " + javaMajor + ".");
                        return;
                    }
                }

                bool serverRunning = _bridge.IsRunning || _scanner.GetServerStatus().Running;
                string message = "Se instalara " + provider.Name + " " + option.MinecraftVersion +
                    (string.IsNullOrWhiteSpace(option.Build) ? "" : " (build " + option.Build + ")") +
                    " usando Java " + javaMajor + ".";
                if (string.Equals(option.Kind, "installer", StringComparison.OrdinalIgnoreCase))
                    message += "\r\n\r\nSe ejecutara el instalador oficial y se descargaran las librerias. " +
                        "Este paso puede tardar varios minutos.";
                if (serverRunning)
                    message += "\r\n\r\nEl servidor se detendra durante el cambio.";
                message += "\r\n\r\nAntes de reemplazar el jar se guardara una copia completa del mundo " +
                    "y el server.jar actual en backups\\jars.\r\n\r\n¿Continuar?";
                if (!MessageDialog.Confirm(this, "Cambiar version", message, "Instalar", false))
                {
                    SetVersionProgress(0, "Instalacion cancelada.");
                    return;
                }

                if (serverRunning)
                {
                    if (!_bridge.IsRunning)
                    {
                        MessageDialog.Info(this, "Cambiar version",
                            "El servidor fue iniciado fuera del panel. Detenlo manualmente y vuelve a intentarlo.");
                        return;
                    }
                    SetVersionProgress(0.02, "Deteniendo el servidor...");
                    AppendConsole("[PANEL] Deteniendo el servidor para cambiar server.jar...");
                    stoppedForInstall = true;
                    await Task.Run(delegate { _bridge.StopGracefully(60000); });
                    await WaitForJarUnlocked(15000);
                }

                SetVersionProgress(0.05, "Guardando copia completa del mundo...");
                AppendConsole("[PANEL] Creando copia de seguridad previa al cambio de version...");
                BackupResult backup = await Task.Run(delegate
                {
                    return _backupService.CreateBackup(_paths, "world", false, false, _bridge,
                        delegate(double fraction, string text)
                        {
                            SafeVersionProgress(0.05 + fraction * 0.3, text);
                        }, true);
                });
                if (backup == null || !backup.Ok)
                {
                    string error = backup == null ? "Error desconocido" : backup.Error;
                    AppendConsole("[PANEL] No se pudo crear la copia previa: " + error);
                    MessageDialog.Info(this, "Cambiar version",
                        "No se creo la copia de seguridad previa, asi que no se reemplazo el jar.\r\n\r\n" + error);
                    return;
                }
                AppendConsole("[PANEL] Copia previa: " + Path.GetFileName(backup.FilePath) +
                    " (" + Utils.HumanBytes(backup.Size) + ")");

                JarInstallResult installed;
                if (string.Equals(option.Kind, "installer", StringComparison.OrdinalIgnoreCase))
                {
                    SetVersionProgress(0.72, "Ejecutando instalador de " + provider.Name + "...");
                    AppendConsole("[PANEL] Ejecutando instalador de " + provider.Name +
                        ". Puede tardar varios minutos...");
                    ModdedInstallResult modded = await ModdedInstaller.InstallServerAsync(
                        downloaded, _paths, javaExe, AppendConsole);
                    if (modded == null || !modded.Ok)
                        throw new Exception(modded == null
                            ? "El instalador de " + provider.Name + " no devolvio resultado."
                            : "No se pudo instalar " + provider.Name + ": " + modded.Error);

                    SetVersionProgress(0.92, "Guardando server.jar...");
                    installed = JarInstaller.Install(_paths, downloaded, _verCurrentLabel);
                }
                else
                {
                    SetVersionProgress(0.72, "Reemplazando server.jar...");
                    installed = JarInstaller.Install(_paths, downloaded, _verCurrentLabel);
                }
                if (installed == null || !installed.Ok)
                    throw new Exception(installed == null ? "No se pudo instalar el jar." : installed.Error);

                AppConfig cfg = AppConfig.Instance;
                cfg.InstalledJarProvider = option.ProviderId;
                cfg.InstalledJarVersion = option.MinecraftVersion;
                cfg.InstalledJarBuild = option.Build ?? "";
                try
                {
                    FileInfo info = new FileInfo(_paths.Jar);
                    cfg.InstalledJarSize = info.Length;
                    cfg.InstalledJarWriteTicks = info.LastWriteTimeUtc.Ticks;
                }
                catch { }
                cfg.Save();

                SetVersionProgress(1, "server.jar actualizado.");
                RefreshJarCurrent();
                AppendConsole("[PANEL] server.jar actualizado a " + option.Label + ".");
                if (!string.IsNullOrWhiteSpace(installed.OldJarPath))
                    AppendConsole("[PANEL] Jar anterior guardado en " + installed.OldJarPath);
                _toast.ShowMessage("server.jar actualizado", Palette.Accent, "check");
                RefreshAddonAvailability(true);

                restartPrompted = true;
                if (MessageDialog.Confirm(this, "Version instalada",
                    "Se instalo " + provider.Name + " " + option.MinecraftVersion + ".\r\n\r\n" +
                    "¿Iniciar el servidor ahora?", "Iniciar", false))
                {
                    StartServerFromPanel();
                }
            }
            catch (Exception ex)
            {
                AppendConsole("[PANEL] Error al cambiar version: " + ex.Message);
                MessageDialog.Info(this, "Cambiar version", ex.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(downloaded))
                {
                    try { if (File.Exists(downloaded)) File.Delete(downloaded); } catch { }
                }
                _versionBusy = false;
                SetVersionBusy(false);
                RefreshJarCurrent();
                if (stoppedForInstall && !restartPrompted)
                {
                    if (MessageDialog.Confirm(this, "Servidor detenido",
                        "El servidor quedo detenido. ¿Iniciarlo de nuevo?", "Iniciar", false))
                        StartServerFromPanel();
                }
            }
        }

        private async void DownloadJavaForCurrent()
        {
            if (_versionBusy || _verCurrentJavaMajor <= 0) return;
            _versionBusy = true;
            SetVersionBusy(true);
            try
            {
                if (string.IsNullOrWhiteSpace(await DownloadJavaAsync(_verCurrentJavaMajor)))
                    SetVersionProgress(0, "Descarga cancelada.");
            }
            catch (Exception ex)
            {
                AppendConsole("[PANEL] Error al descargar Java: " + ex.Message);
                MessageDialog.Info(this, "Java", ex.Message);
            }
            finally
            {
                _versionBusy = false;
                SetVersionBusy(false);
                RefreshJarCurrent();
            }
        }

        private async Task<string> DownloadJavaAsync(int major)
        {
            SetVersionProgress(0, "Consultando Java " + major + " para Windows x64...");
            JavaAsset asset = await JavaRuntimeManager.GetAssetAsync(major);
            if (asset == null)
            {
                MessageDialog.Info(this, "Java " + major,
                    "No se encontro un JDK " + major + " para Windows x64 en Adoptium.");
                return null;
            }

            string target = Path.Combine(JavaRuntimeManager.RootDir, major.ToString());
            string message = "Esta version del servidor necesita Java " + major + ".\r\n\r\n" +
                "¿Descargar Temurin JDK " + major + " (" + Utils.HumanBytes(asset.Size) + ")?\r\n\r\n" +
                "Se instalara en:\r\n" + target;
            if (!MessageDialog.Confirm(this, "Descargar Java " + major, message, "Descargar", false))
                return null;

            SetVersionProgress(0, "Descargando Java " + major + "...");
            AppendConsole("[PANEL] Descargando Java " + major + " (" + Utils.HumanBytes(asset.Size) + ")...");
            string javaExe = await JavaRuntimeManager.DownloadAsync(asset,
                delegate(double fraction, string text) { SafeVersionProgress(fraction, text); },
                CancellationToken.None);
            AppendConsole("[PANEL] Java " + major + " instalado en " + javaExe);
            _toast.ShowMessage("Java " + major + " instalado", Palette.Accent, "check");
            return javaExe;
        }

        private async Task WaitForJarUnlocked(int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, timeoutMs));
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using (FileStream stream = new FileStream(_paths.Jar, FileMode.Open,
                        FileAccess.ReadWrite, FileShare.None))
                    {
                        return;
                    }
                }
                catch
                {
                    await Task.Delay(300);
                }
            }
        }

        private static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "server.jar";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim();
            if (string.IsNullOrEmpty(name)) return "server.jar";
            return name;
        }

        private static string ProviderDisplayName(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return "Minecraft";
            if (string.Equals(provider, "vanilla", StringComparison.OrdinalIgnoreCase)) return "Vanilla";
            if (string.Equals(provider, "snapshot", StringComparison.OrdinalIgnoreCase)) return "Snapshot";
            if (string.Equals(provider, "paper", StringComparison.OrdinalIgnoreCase)) return "Paper";
            if (string.Equals(provider, "folia", StringComparison.OrdinalIgnoreCase)) return "Folia";
            if (string.Equals(provider, "purpur", StringComparison.OrdinalIgnoreCase)) return "Purpur";
            if (string.Equals(provider, "fabric", StringComparison.OrdinalIgnoreCase)) return "Fabric";
            if (string.Equals(provider, "forge", StringComparison.OrdinalIgnoreCase)) return "Forge";
            if (string.Equals(provider, "neoforge", StringComparison.OrdinalIgnoreCase)) return "NeoForge";
            return provider;
        }

        // Java que necesita cada version de Minecraft. El jar descargado manda:
        // esto solo se usa como pista antes de descargar y como respaldo.
        private static int JavaForMinecraft(string version)
        {
            return JarCatalog.JavaForMinecraft(version);
        }
    }
}
