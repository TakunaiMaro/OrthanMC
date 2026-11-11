#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace McPanel
{
    internal enum AddonKind
    {
        None,
        Plugins,
        Mods
    }

    public partial class MainForm
    {
        private Card _cardAddons;
        private Label _addonTitle;
        private Label _addonDesc;
        private Label _addonPath;
        private Label _addonCount;
        private Label _addonEmpty;
        private Panel _addonList;
        private RoundButton _addonOpen;
        private bool _addonsLoading;
        private bool _markReadyAfterAddonBuild;
        private int _addonBuildWidth;
        private string _addonSignature = "";
        private List<AddonFileInfo> _addonFiles;
        private List<Control> _addonRows;
        private LoadingOverlay _loadingOverlay;

        private sealed class AddonFileInfo
        {
            public string FilePath = "";
            public string FileName = "";
            public string BaseName = "";
            public bool Enabled;
            public long Size;
            public DateTime Modified;
        }

        private Panel BuildAddons()
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.Padding = new Padding(0);
            grid.BackColor = Color.Transparent;
            grid.ColumnCount = 1;
            grid.RowCount = 2;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(132)));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ---------------- Cabecera ----------------
            Card header = new Card();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0, 0, 0, Dp(7));

            TableLayoutPanel head = MakeCardStack(1, 4);
            head.RowStyles[3] = new RowStyle(SizeType.Percent, 100f);

            _addonTitle = MakeLabel("PLUGINS", Fonts.UI(15f, FontStyle.Bold), Palette.Text);
            _addonDesc = MakeWrapLabel("Archivos .jar descargados en la carpeta plugins.",
                Fonts.UI(9f), Palette.Muted, Dp(22));
            _addonPath = MakeWrapLabel("Carpeta: --", Fonts.UI(8.25f), Palette.Dim, Dp(26));

            head.Controls.Add(_addonTitle, 0, 0);
            head.Controls.Add(_addonDesc, 0, 1);
            head.Controls.Add(_addonPath, 0, 2);
            header.Controls.Add(head);

            // ---------------- Lista ----------------
            _cardAddons = new Card();
            _cardAddons.Dock = DockStyle.Fill;
            _cardAddons.Margin = new Padding(0);

            TableLayoutPanel list = MakeCardStack(1, 3);
            list.RowStyles[1] = new RowStyle(SizeType.Percent, 100f);
            list.RowStyles[2] = new RowStyle(SizeType.Absolute, Dp(44));

            TableLayoutPanel listHead = new TableLayoutPanel();
            listHead.Dock = DockStyle.Fill;
            listHead.Margin = new Padding(0);
            listHead.Padding = new Padding(0);
            listHead.BackColor = Palette.Card;
            listHead.ColumnCount = 2;
            listHead.RowCount = 1;
            listHead.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            listHead.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            listHead.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label listTitle = MakeLabel("ARCHIVOS DESCARGADOS", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            listTitle.Anchor = AnchorStyles.Left;
            _addonCount = MakeLabel("0 archivos", Fonts.UI(9f, FontStyle.Bold), Palette.Accent);
            _addonCount.Anchor = AnchorStyles.Right;
            listHead.Controls.Add(listTitle, 0, 0);
            listHead.Controls.Add(_addonCount, 1, 0);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.Margin = new Padding(0, Dp(8), 0, Dp(6));
            listHost.BackColor = Palette.Card;

            _addonList = new Panel();
            _addonList.Dock = DockStyle.Fill;
            _addonList.Margin = new Padding(0);
            _addonList.Padding = new Padding(0);
            _addonList.AutoScroll = true;
            _addonList.BackColor = Palette.Card;
            _addonList.Resize += delegate { LayoutAddonRows(); };
            _addonList.Scroll += delegate { EnsureVisibleAddonRows(); };
            _addonList.MouseWheel += delegate { EnsureVisibleAddonRows(); };

            _addonEmpty = new Label();
            _addonEmpty.Dock = DockStyle.Fill;
            _addonEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _addonEmpty.Font = Fonts.UI(9f);
            _addonEmpty.ForeColor = Palette.Dim;
            _addonEmpty.BackColor = Palette.Card;
            _addonEmpty.Visible = false;

            listHost.Controls.Add(_addonList);
            listHost.Controls.Add(_addonEmpty);

            TableLayoutPanel bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.Margin = new Padding(0);
            bottom.Padding = new Padding(0);
            bottom.BackColor = Palette.Card;
            bottom.ColumnCount = 2;
            bottom.RowCount = 1;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(220)));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label hint = MakeLabel(
                "Activar o desactivar cambia la extension del archivo. Los cambios se aplican al reiniciar.",
                Fonts.UI(8.25f), Palette.Dim);
            hint.Anchor = AnchorStyles.Left;
            hint.AutoEllipsis = true;
            hint.AutoSize = false;
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;

            _addonOpen = new RoundButton();
            _addonOpen.Text = "ABRIR CARPETA";
            _addonOpen.Glyph = "folder";
            _addonOpen.Dock = DockStyle.Fill;
            _addonOpen.Margin = new Padding(Dp(12), 0, 0, 0);
            _addonOpen.FillColor = Palette.Card;
            _addonOpen.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            _addonOpen.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            _addonOpen.TextColor = Palette.Text;
            _addonOpen.BorderColor = Palette.Border;
            _addonOpen.Click += delegate { OpenAddonFolder(); };

            bottom.Controls.Add(hint, 0, 0);
            bottom.Controls.Add(_addonOpen, 1, 0);

            list.Controls.Add(listHead, 0, 0);
            list.Controls.Add(listHost, 0, 1);
            list.Controls.Add(bottom, 0, 2);
            _cardAddons.Controls.Add(list);

            grid.Controls.Add(header, 0, 0);
            grid.Controls.Add(_cardAddons, 0, 1);
            p.Controls.Add(grid);

            RefreshAddons();
            return p;
        }

        // ------------------------------------------------------------------
        // Deteccion del tipo de servidor
        // ------------------------------------------------------------------
        private AddonKind DetectAddonKind()
        {
            string provider = "";
            try { provider = JarInspector.Inspect(_paths.Jar).Provider; } catch { }
            AddonKind kind = AddonKindFor(provider);
            if (kind != AddonKind.None) return kind;

            // Instalaciones Forge/NeoForge: el instalador deja estas carpetas
            // aunque el server.jar sea solo una copia del instalador.
            try
            {
                string libs = Path.Combine(_paths.ServerDir, "libraries", "net");
                if (Directory.Exists(Path.Combine(libs, "neoforged", "neoforge")) ||
                    Directory.Exists(Path.Combine(libs, "minecraftforge", "forge")))
                    return AddonKind.Mods;
            }
            catch { }

            // Metadatos del panel, solo si pertenecen al server.jar actual.
            try
            {
                AppConfig cfg = AppConfig.Instance;
                FileInfo info = new FileInfo(_paths.Jar);
                if (info.Exists && cfg.InstalledJarSize > 0 &&
                    cfg.InstalledJarSize == info.Length &&
                    cfg.InstalledJarWriteTicks == info.LastWriteTimeUtc.Ticks)
                {
                    kind = AddonKindFor(cfg.InstalledJarProvider);
                    if (kind != AddonKind.None) return kind;
                }
            }
            catch { }

            try
            {
                string plugins = Path.Combine(_paths.ServerDir, "plugins");
                string mods = Path.Combine(_paths.ServerDir, "mods");
                bool hasPlugins = HasAddonFiles(plugins, ".jar", ".offplugin");
                bool hasMods = HasAddonFiles(mods, ".jar", ".offmod");
                if (hasPlugins && !hasMods) return AddonKind.Plugins;
                if (hasMods && !hasPlugins) return AddonKind.Mods;
                if (hasPlugins) return AddonKind.Plugins;
                if (hasMods) return AddonKind.Mods;
            }
            catch { }

            return AddonKind.None;
        }

        private static AddonKind AddonKindFor(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return AddonKind.None;
            string text = provider.ToLowerInvariant();
            if (text.Contains("paper") || text.Contains("purpur") ||
                text.Contains("spigot") || text.Contains("bukkit") ||
                text.Contains("folia"))
                return AddonKind.Plugins;
            if (text.Contains("fabric") || text.Contains("forge") ||
                text.Contains("neoforge") || text.Contains("quilt"))
                return AddonKind.Mods;
            return AddonKind.None;
        }

        private static bool HasAddonFiles(string dir, string enabledExt, string disabledExt)
        {
            try
            {
                if (!Directory.Exists(dir)) return false;
                foreach (string file in Directory.GetFiles(dir))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith(enabledExt, StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(disabledExt, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        // Mantiene visible el menu Plugins/Mods despues de instalar un jar
        // nuevo sin reiniciar la aplicacion.
        private void RefreshAddonAvailability(bool refreshContent)
        {
            AddonKind kind = DetectAddonKind();
            bool changed = kind != _addonKind;
            _addonKind = kind;

            if (_navAddons != null)
            {
                _navAddons.Visible = kind != AddonKind.None;
                _navAddons.Text = kind == AddonKind.Mods ? "Mods" : "Plugins";
                _navAddons.Glyph = kind == AddonKind.Mods ? "chip" : "plug";
            }

            if (refreshContent || changed) RefreshAddons();

            if (kind == AddonKind.None && _viewAddons != null && _viewAddons.Visible)
                NavigateTo(3);
        }

        private string AddonFolder()
        {
            string name = _addonKind == AddonKind.Mods ? "mods" : "plugins";
            return Path.Combine(_paths.ServerDir, name);
        }

        private string DisabledExtension()
        {
            return _addonKind == AddonKind.Mods ? ".offmod" : ".offplugin";
        }

        // Al entrar a Plugins/Mods se muestra la pantalla de carga y la lista
        // se reconstruye cuando la capa ya esta pintada. El Update fuerza un
        // primer repintado para que nunca quede la vista sin cubrir.
        private void BeginAddonsLoading()
        {
            if (_addonList == null) return;
            if (_addonsLoading) return;

            if (_loadingOverlay == null || IsDisposed || Disposing)
            {
                RefreshAddons();
                return;
            }

            // Si la capa ya esta animando (por ejemplo, el usuario vuelve a
            // pulsar Mods), no se reinicia: solo se refresca la lista.
            if (_loadingOverlay.Visible)
            {
                _markReadyAfterAddonBuild = true;
                RefreshAddons();
                return;
            }

            _addonsLoading = true;
            try
            {
                PositionLoadingOverlay();
                _loadingOverlay.Begin(_rootLayout);
                _loadingOverlay.Update();

                if (_viewAddons == null || !_viewAddons.Visible)
                {
                    _loadingOverlay.Cancel();
                    return;
                }

                // La capa queda esperando (esmeralda palpitando) hasta que
                // el ultimo lote de filas este creado. Con 200+ mods esto
                // evita que la interfaz se congele y que la animacion
                // termine antes de tiempo.
                _markReadyAfterAddonBuild = true;
                RefreshAddons();
            }
            catch
            {
                _markReadyAfterAddonBuild = false;
                if (_loadingOverlay != null) _loadingOverlay.MarkContentReady();
            }
            finally
            {
                _addonsLoading = false;
            }
        }

        // La lista esta virtualizada: con cientos de mods solo se crean las
        // filas visibles (mas un margen). Asi entrar a Mods no bloquea la
        // interfaz ni congela la animacion de carga.
        // Si la carpeta no cambio desde la ultima vez, se reutiliza la lista.
        private void RefreshAddons()
        {
            if (_addonList == null || _addonList.IsDisposed) return;

            bool mods = _addonKind == AddonKind.Mods;
            string folder = AddonFolder();
            string disabled = DisabledExtension();
            List<AddonFileInfo> files = LoadAddonFiles(folder, disabled);
            string signature = AddonSignature(files);

            _addonTitle.Text = mods ? "MODS" : "PLUGINS";
            _addonDesc.Text = mods
                ? "Archivos .jar descargados en la carpeta mods. Desactivar cambia la extension a .offmod."
                : "Archivos .jar descargados en la carpeta plugins. Desactivar cambia la extension a .offplugin.";
            _addonPath.Text = "Carpeta: " + folder;
            _addonEmpty.Text = mods
                ? "No hay mods descargados.\r\nColoca archivos .jar en la carpeta mods."
                : "No hay plugins descargados.\r\nColoca archivos .jar en la carpeta plugins.";
            _addonCount.Text = files.Count +
                (files.Count == 1 ? " archivo" : " archivos");

            // Con la vista oculta el panel reporta un tamano provisional;
            // las filas se crean cuando la vista ya esta visible.
            if (_viewAddons == null || !_viewAddons.Visible)
                return;

            if (signature == _addonSignature && _addonRows != null &&
                _addonRows.Count == files.Count)
            {
                FinishAddonBuild();
                return;
            }

            _addonSignature = signature;
            _addonFiles = files;
            _addonBuildWidth = AddonRowWidth();
            _addonList.AutoScrollPosition = new Point(0, 0);

            // Solo hay que liberar las filas visibles de la lista anterior.
            Control[] oldRows = new Control[_addonList.Controls.Count];
            _addonList.Controls.CopyTo(oldRows, 0);
            _addonList.Controls.Clear();
            foreach (Control old in oldRows)
            {
                try { old.Dispose(); } catch { }
            }

            _addonRows = new List<Control>(files.Count);
            for (int i = 0; i < files.Count; i++) _addonRows.Add(null);

            FinishAddonBuild();
        }

        private static string AddonSignature(List<AddonFileInfo> files)
        {
            StringBuilder builder = new StringBuilder();
            foreach (AddonFileInfo file in files)
            {
                builder.Append(file.FileName).Append('|')
                    .Append(file.Size).Append('|')
                    .Append(file.Modified.Ticks).Append(';');
            }
            return builder.ToString();
        }

        // Crea la fila si todavia no existe. Se llama para el tramo visible
        // y para un pequeno margen arriba/abajo al desplazarse.
        private void EnsureAddonRow(int index)
        {
            if (_addonRows == null || _addonFiles == null) return;
            if (index < 0 || index >= _addonRows.Count) return;
            if (_addonRows[index] != null) return;
            if (index >= _addonFiles.Count) return;

            Control row = CreateAddonRow(_addonFiles[index]);
            int width = _addonBuildWidth > 0 ? _addonBuildWidth : AddonRowWidth();
            if (width > 0) row.Width = width;
            // Las filas hijas de un panel con AutoScroll viven en coordenadas
            // de cliente: al desplazarse, WinForms mueve las existentes. Una
            // fila nueva hay que colocarla descontando el desplazamiento
            // actual o aparece miles de pixeles mas abajo (huecos en blanco).
            row.Location = new Point(0,
                index * AddonRowStep() + _addonList.AutoScrollPosition.Y);
            _addonList.Controls.Add(row);
            _addonRows[index] = row;
        }

        private void EnsureVisibleAddonRows()
        {
            if (_addonRows == null || _addonRows.Count == 0) return;
            if (_viewAddons == null || !_viewAddons.Visible) return;

            int step = AddonRowStep();
            int scrollTop = -_addonList.AutoScrollPosition.Y;
            int clientHeight = Math.Max(step, _addonList.ClientSize.Height);

            const int buffer = 3;
            int first = Math.Max(0, scrollTop / step - buffer);
            int last = Math.Min(_addonRows.Count - 1,
                (scrollTop + clientHeight) / step + buffer);

            for (int i = first; i <= last; i++) EnsureAddonRow(i);
        }

        private void FinishAddonBuild()
        {
            if (_addonList == null || _addonList.IsDisposed) return;

            int step = AddonRowStep();
            int total = _addonFiles == null ? 0 : _addonFiles.Count * step;
            Size min = new Size(0, total);
            if (_addonList.AutoScrollMinSize != min)
                _addonList.AutoScrollMinSize = min;

            _addonEmpty.Visible = _addonFiles == null || _addonFiles.Count == 0;
            if (_addonEmpty.Visible) _addonEmpty.BringToFront();

            LayoutAddonRows();

            if (_markReadyAfterAddonBuild)
            {
                _markReadyAfterAddonBuild = false;
                if (_loadingOverlay != null) _loadingOverlay.MarkContentReady();
            }
        }

        private List<AddonFileInfo> LoadAddonFiles(string folder, string disabledExt)
        {
            List<AddonFileInfo> list = new List<AddonFileInfo>();
            try
            {
                if (!Directory.Exists(folder)) return list;
                foreach (string path in Directory.GetFiles(folder))
                {
                    string name = Path.GetFileName(path);
                    bool enabled;
                    if (name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) enabled = true;
                    else if (name.EndsWith(disabledExt, StringComparison.OrdinalIgnoreCase)) enabled = false;
                    else continue;

                    string baseName;
                    if (enabled) baseName = name.Substring(0, name.Length - 4);
                    else baseName = name.Substring(0, name.Length - disabledExt.Length);

                    AddonFileInfo entry = new AddonFileInfo();
                    entry.FilePath = path;
                    entry.FileName = name;
                    entry.BaseName = baseName;
                    entry.Enabled = enabled;
                    try
                    {
                        FileInfo info = new FileInfo(path);
                        entry.Size = info.Length;
                        entry.Modified = info.LastWriteTime;
                    }
                    catch { }
                    list.Add(entry);
                }
            }
            catch { }

            list.Sort(delegate(AddonFileInfo a, AddonFileInfo b)
            {
                return string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        private Control CreateAddonRow(AddonFileInfo entry)
        {
            bool plugins = _addonKind == AddonKind.Plugins;

            Card row = new Card();
            row.Radius = 9;
            row.Height = Dp(66);
            int rowWidth = _addonBuildWidth > 0 ? _addonBuildWidth : AddonRowWidth();
            row.Width = rowWidth > 0 ? rowWidth : Dp(420);
            row.MinimumSize = new Size(0, Dp(66));
            row.Margin = new Padding(0, 0, 0, Dp(6));
            row.Padding = new Padding(Dp(14), Dp(8), Dp(10), Dp(8));
            row.BorderColor = entry.Enabled ? Palette.Border : Palette.Alpha(Palette.Border, 150);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Margin = new Padding(0);
            layout.Padding = new Padding(0);
            layout.BackColor = Palette.Card;
            layout.ColumnCount = plugins ? 3 : 2;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(128)));
            if (plugins) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(128)));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel texts = new TableLayoutPanel();
            texts.Dock = DockStyle.Fill;
            texts.Margin = new Padding(0);
            texts.Padding = new Padding(0);
            texts.BackColor = Palette.Card;
            texts.ColumnCount = 1;
            texts.RowCount = 2;
            texts.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(24)));
            texts.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label name = MakeLabel(entry.FileName,
                Fonts.UI(9.75f, FontStyle.Bold),
                entry.Enabled ? Palette.Text : Palette.Dim);
            name.AutoSize = false;
            name.Dock = DockStyle.Fill;
            name.AutoEllipsis = true;

            string infoText = Utils.HumanBytes(entry.Size) +
                "  -  " + entry.Modified.ToString("dd/MM/yyyy HH:mm") +
                "  -  " + (entry.Enabled ? "Activo" : "Desactivado");
            Label info = MakeLabel(infoText, Fonts.UI(8.5f),
                entry.Enabled ? Palette.Muted : Palette.Dim);
            info.AutoSize = false;
            info.Dock = DockStyle.Fill;
            info.AutoEllipsis = true;

            texts.Controls.Add(name, 0, 0);
            texts.Controls.Add(info, 0, 1);

            RoundButton toggle = new RoundButton();
            toggle.Text = entry.Enabled ? "DESACTIVAR" : "ACTIVAR";
            toggle.Glyph = entry.Enabled ? "stop" : "play";
            toggle.Dock = DockStyle.Fill;
            toggle.Margin = new Padding(Dp(8), Dp(7), 0, Dp(7));
            toggle.Font = Fonts.UI(8.5f, FontStyle.Bold);
            toggle.FillColor = Palette.Card;
            toggle.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            toggle.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            toggle.TextColor = entry.Enabled ? Palette.Amber : Palette.Accent;
            toggle.BorderColor = Palette.Border;
            AddonFileInfo captured = entry;
            toggle.Click += delegate { ToggleAddon(captured); };

            layout.Controls.Add(texts, 0, 0);
            layout.Controls.Add(toggle, 1, 0);

            if (plugins)
            {
                RoundButton config = new RoundButton();
                config.Text = "CONFIG.YML";
                config.Glyph = "save";
                config.Dock = DockStyle.Fill;
                config.Margin = new Padding(Dp(8), Dp(7), 0, Dp(7));
                config.Font = Fonts.UI(8.5f, FontStyle.Bold);
                config.FillColor = Palette.Card;
                config.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
                config.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
                config.TextColor = Palette.Text;
                config.BorderColor = Palette.Border;
                config.Click += delegate { OpenPluginConfig(captured); };
                layout.Controls.Add(config, 2, 0);
            }

            row.Controls.Add(layout);
            return row;
        }

        // Ancho definitivo de las filas. Con la vista oculta el FlowLayoutPanel
        // reporta un ancho provisional (~150 px), asi que se ignora.
        private int AddonRowWidth()
        {
            if (_addonList == null) return 0;
            // Deja libre el canal de la barra superpuesta (20 px + margen).
            int width = _addonList.ClientSize.Width - Dp(28);
            return width < Dp(420) ? 0 : width;
        }

        // Altura reservada por fila (alto de la tarjeta + separacion).
        private int AddonRowStep()
        {
            return Dp(66) + Dp(6);
        }

        private void LayoutAddonRows()
        {
            if (_addonList == null) return;

            // Mientras la vista esta oculta no se toca el ancho de las filas:
            // el panel reporta un tamano provisional.
            if (_viewAddons == null || !_viewAddons.Visible) return;

            int width = AddonRowWidth();
            if (width <= 0) return;

            _addonBuildWidth = width;
            if (_addonRows != null)
            {
                foreach (Control control in _addonRows)
                {
                    if (control == null) continue;
                    if (control.Width != width) control.Width = width;
                }
            }

            EnsureVisibleAddonRows();
        }

        private void ToggleAddon(AddonFileInfo entry)
        {
            if (entry == null) return;
            try
            {
                string folder = AddonFolder();
                string target;
                if (entry.Enabled)
                {
                    target = Path.Combine(folder, entry.BaseName + DisabledExtension());
                    if (File.Exists(target)) target = UniquePath(target);
                    File.Move(entry.FilePath, target);
                    string message = _addonKind == AddonKind.Mods
                        ? "Mod desactivado" : "Plugin desactivado";
                    if (_bridge.IsRunning) message += " (al reiniciar)";
                    _toast.ShowMessage(message, Palette.Amber, "stop");
                }
                else
                {
                    target = Path.Combine(folder, entry.BaseName + ".jar");
                    if (File.Exists(target)) target = UniquePath(target);
                    File.Move(entry.FilePath, target);
                    string message = _addonKind == AddonKind.Mods
                        ? "Mod activado" : "Plugin activado";
                    if (_bridge.IsRunning) message += " (al reiniciar)";
                    _toast.ShowMessage(message, Palette.Accent, "check");
                }
                RefreshAddons();
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, _addonKind == AddonKind.Mods ? "Mods" : "Plugins", ex.Message);
            }
        }

        private static string UniquePath(string path)
        {
            string dir = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int index = 2;
            string candidate = path;
            do
            {
                candidate = Path.Combine(dir, name + " (" + index + ")" + ext);
                index++;
            }
            while (File.Exists(candidate));
            return candidate;
        }

        private void OpenAddonFolder()
        {
            try
            {
                string folder = AddonFolder();
                Directory.CreateDirectory(folder);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "explorer.exe";
                psi.Arguments = "\"" + folder + "\"";
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Carpeta", ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Configuracion de plugins
        // ------------------------------------------------------------------
        private void OpenPluginConfig(AddonFileInfo entry)
        {
            if (entry == null || _addonKind != AddonKind.Plugins) return;
            string config = FindPluginConfig(entry.BaseName);
            if (string.IsNullOrWhiteSpace(config))
            {
                MessageDialog.Info(this, "config.yml",
                    "No se encontro config.yml para " + entry.BaseName + ".\r\n\r\n" +
                    "Inicia el servidor una vez para que el plugin genere su carpeta y su archivo de configuracion.");
                return;
            }

            string notepad = FindNotepadPlusPlus();
            try
            {
                if (!string.IsNullOrWhiteSpace(notepad))
                {
                    ProcessStartInfo open = new ProcessStartInfo();
                    open.FileName = notepad;
                    open.Arguments = "\"" + config + "\"";
                    open.UseShellExecute = false;
                    Process.Start(open);
                    return;
                }

                MessageDialog.Info(this, "Editar config.yml",
                    "No se encontro Notepad++. Se abrira el menu 'Abrir con' de Windows.\r\n\r\n" +
                    "Puedes elegir el Bloc de notas para editar el archivo.");

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "rundll32.exe";
                psi.Arguments = "shell32.dll,OpenAs_RunDLL \"" + config + "\"";
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Editar config.yml", ex.Message);
            }
        }

        private string FindPluginConfig(string baseName)
        {
            try
            {
                string root = Path.Combine(_paths.ServerDir, "plugins");
                if (!Directory.Exists(root)) return null;

                string exactDir = Path.Combine(root, baseName);
                string exact = Path.Combine(exactDir, "config.yml");
                if (File.Exists(exact)) return exact;

                string best = null;
                int bestScore = -1;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string config = FindConfigInDirectory(dir);
                    if (string.IsNullOrWhiteSpace(config)) continue;
                    string dirName = Path.GetFileName(dir);
                    int score = MatchScore(baseName, dirName);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = config;
                    }
                }
                if (!string.IsNullOrWhiteSpace(best) && bestScore > 0) return best;

                string[] all = Directory.GetFiles(root, "config.yml", SearchOption.AllDirectories);
                if (all.Length == 1) return all[0];
            }
            catch { }
            return null;
        }

        private static string FindConfigInDirectory(string dir)
        {
            string config = Path.Combine(dir, "config.yml");
            if (File.Exists(config)) return config;
            try
            {
                string[] nested = Directory.GetFiles(dir, "config.yml", SearchOption.AllDirectories);
                if (nested.Length > 0) return nested[0];
            }
            catch { }
            return null;
        }

        private static int MatchScore(string baseName, string folderName)
        {
            if (string.IsNullOrWhiteSpace(baseName) || string.IsNullOrWhiteSpace(folderName)) return 0;
            if (string.Equals(baseName, folderName, StringComparison.OrdinalIgnoreCase)) return 1000;
            if (baseName.StartsWith(folderName, StringComparison.OrdinalIgnoreCase) ||
                folderName.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                return 500 + Math.Min(baseName.Length, folderName.Length);
            if (baseName.IndexOf(folderName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                folderName.IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0)
                return 250 + Math.Min(baseName.Length, folderName.Length);
            return 0;
        }

        private static string FindNotepadPlusPlus()
        {
            List<string> candidates = new List<string>();
            candidates.Add(Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles), "Notepad++", "notepad++.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86), "Notepad++", "notepad++.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "Programs", "Notepad++", "notepad++.exe"));
            foreach (string candidate in candidates)
            {
                try { if (File.Exists(candidate)) return candidate; } catch { }
            }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\notepad++.exe"))
                {
                    string value = key != null ? key.GetValue(null) as string : null;
                    if (!string.IsNullOrWhiteSpace(value) && File.Exists(value)) return value;
                }
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\notepad++.exe"))
                {
                    string value = key != null ? key.GetValue(null) as string : null;
                    if (!string.IsNullOrWhiteSpace(value) && File.Exists(value)) return value;
                }
            }
            catch { }
            return null;
        }
    }
}
