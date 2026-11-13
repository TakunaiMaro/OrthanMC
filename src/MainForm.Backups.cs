#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace McPanel
{
    public partial class MainForm
    {
        private static readonly int[] BackupIntervals = { 15, 30, 60, 120, 180, 360, 720, 1440 };
        private static readonly string[] BackupIntervalNames =
        {
            "15 minutos", "30 minutos", "1 hora", "2 horas",
            "3 horas", "6 horas", "12 horas", "24 horas"
        };

        private readonly BackupService _backupService = new BackupService();
        private Card _cardBackupSettings;
        private Card _cardBackupHistory;
        private ToggleSwitch _bkEnabled;
        private ToggleSwitch _bkRecent;
        private SelectBox _bkInterval;
        private SelectBox _bkMode;
        private Label _bkLast;
        private Label _bkNext;
        private Label _bkHint;
        private Label _bkDir;
        private Label _bkProgressText;
        private UsageBar _bkProgress;
        private RoundButton _bkNow;
        private RoundButton _bkOpen;
        private RoundButton _bkOpen2;
        private ListBox _bkList;
        private Label _bkEmpty;
        private bool _backupRunning;
        private bool _backupLoading;
        private DateTime _nextBackupUtc = DateTime.MinValue;
        private DateTime _lastBackupUiUpdate = DateTime.MinValue;

        private Panel BuildBackups()
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
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ---------------- Ajustes ----------------
            _cardBackupSettings = new Card();
            _cardBackupSettings.Dock = DockStyle.Fill;
            _cardBackupSettings.Margin = new Padding(0, 0, Dp(7), 0);

            TableLayoutPanel left = MakeCardStack(2, 16);
            left.ColumnStyles.Clear();
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(150)));
            left.RowStyles[12] = new RowStyle(SizeType.Absolute, Dp(16));
            left.RowStyles[14] = new RowStyle(SizeType.Absolute, Dp(82));

            Label title = MakeLabel("COPIA DE SEGURIDAD", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label desc = MakeLabel("Respaldo del mundo o de los datos de jugadores", Fonts.UI(9f), Palette.Muted);

            Label enableLabel = MakeLabel("Activar backups", Fonts.UI(9.25f), Palette.Text);
            _bkEnabled = new ToggleSwitch();
            _bkEnabled.Anchor = AnchorStyles.Right;
            _bkEnabled.Margin = new Padding(0, 4, 0, 4);

            Label intervalLabel = MakeLabel("Cada", Fonts.UI(9.25f), Palette.Muted);
            string intervalCurrent = BackupIntervalNames[IntervalIndex(AppConfig.Instance.BackupIntervalMinutes)];
            _bkInterval = new SelectBox(BackupIntervalNames, intervalCurrent);
            _bkInterval.Dock = DockStyle.Fill;
            _bkInterval.Margin = new Padding(0, 3, 0, 3);

            Label modeLabel = MakeLabel("Tipo de copia", Fonts.UI(9.25f), Palette.Muted);
            _bkMode = new SelectBox(new string[] { "Mundo + jugadores", "Solo jugadores" },
                AppConfig.Instance.BackupMode == "players" ? "Solo jugadores" : "Mundo + jugadores");
            _bkMode.Dock = DockStyle.Fill;
            _bkMode.Margin = new Padding(0, 3, 0, 3);

            Label recentLabel = MakeLabel("Regiones de 24 h", Fonts.UI(9.25f), Palette.Text);
            _bkRecent = new ToggleSwitch();
            _bkRecent.Anchor = AnchorStyles.Right;
            _bkRecent.Margin = new Padding(0, 4, 0, 4);

            Label recentNote = MakeWrapLabel(
                "Si esta activo, solo se incluyen las regiones modificadas o creadas en las " +
                "ultimas 24 h. Desactivalo para copiar el mundo completo.",
                Fonts.UI(8.25f), Palette.Dim, Dp(38));

            Label retention = MakeLabel("Se conservan las ultimas 5 copias.",
                Fonts.UI(8.5f), Palette.Dim);

            Label sep = new Label();
            sep.Height = 1;
            sep.Dock = DockStyle.Fill;
            sep.BackColor = Palette.Border;
            sep.Margin = new Padding(0, 6, 0, 6);

            _bkLast = MakeLabel("Ultima copia: --", Fonts.UI(9.5f), Palette.Text);
            _bkNext = MakeLabel("Proxima: --", Fonts.UI(9.5f), Palette.Text);
            _bkHint = MakeWrapLabel("Los backups automaticos solo se ejecutan con el servidor en linea.",
                Fonts.UI(8.5f), Palette.Dim, Dp(34));
            _bkProgressText = MakeLabel("", Fonts.UI(8.5f), Palette.Muted);
            _bkProgress = new UsageBar();
            _bkProgress.Dock = DockStyle.Fill;
            _bkProgress.Height = Dp(8);
            _bkProgress.Margin = new Padding(0, 3, 0, 6);

            Panel buttons = new Panel();
            buttons.Dock = DockStyle.Fill;
            buttons.BackColor = Palette.Card;
            buttons.Margin = new Padding(0, 4, 0, 0);

            _bkNow = new RoundButton();
            _bkNow.Text = "COPIAR AHORA";
            _bkNow.Glyph = "copy";
            _bkNow.Dock = DockStyle.Top;
            _bkNow.Height = Dp(38);
            _bkNow.Margin = new Padding(0);

            _bkOpen = new RoundButton();
            _bkOpen.Text = "ABRIR CARPETA";
            _bkOpen.Glyph = "folder";
            _bkOpen.Dock = DockStyle.Top;
            _bkOpen.Height = Dp(34);
            _bkOpen.Margin = new Padding(0, Dp(7), 0, 0);
            _bkOpen.FillColor = Palette.Card;
            _bkOpen.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            _bkOpen.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            _bkOpen.TextColor = Palette.Text;
            _bkOpen.BorderColor = Palette.Border;

            // El ultimo agregado queda arriba en un panel con Dock=Top.
            buttons.Controls.Add(_bkOpen);
            buttons.Controls.Add(_bkNow);

            _bkDir = MakeWrapLabel("Carpeta: --", Fonts.UI(8.25f), Palette.Dim, Dp(24));

            left.Controls.Add(title, 0, 0);
            left.SetColumnSpan(title, 2);
            left.Controls.Add(desc, 0, 1);
            left.SetColumnSpan(desc, 2);
            left.Controls.Add(enableLabel, 0, 2);
            left.Controls.Add(_bkEnabled, 1, 2);
            left.Controls.Add(intervalLabel, 0, 3);
            left.Controls.Add(_bkInterval, 1, 3);
            left.Controls.Add(modeLabel, 0, 4);
            left.Controls.Add(_bkMode, 1, 4);
            left.Controls.Add(recentLabel, 0, 5);
            left.Controls.Add(_bkRecent, 1, 5);
            left.Controls.Add(recentNote, 0, 6);
            left.SetColumnSpan(recentNote, 2);
            left.Controls.Add(retention, 0, 7);
            left.SetColumnSpan(retention, 2);
            left.Controls.Add(sep, 0, 8);
            left.SetColumnSpan(sep, 2);
            left.Controls.Add(_bkLast, 0, 9);
            left.SetColumnSpan(_bkLast, 2);
            left.Controls.Add(_bkNext, 0, 10);
            left.SetColumnSpan(_bkNext, 2);
            left.Controls.Add(_bkHint, 0, 11);
            left.SetColumnSpan(_bkHint, 2);
            left.Controls.Add(_bkProgress, 0, 12);
            left.SetColumnSpan(_bkProgress, 2);
            left.Controls.Add(_bkProgressText, 0, 13);
            left.SetColumnSpan(_bkProgressText, 2);
            left.Controls.Add(buttons, 0, 14);
            left.SetColumnSpan(buttons, 2);
            left.Controls.Add(_bkDir, 0, 15);
            left.SetColumnSpan(_bkDir, 2);
            _cardBackupSettings.Controls.Add(left);

            // ---------------- Historial ----------------
            _cardBackupHistory = new Card();
            _cardBackupHistory.Dock = DockStyle.Fill;
            _cardBackupHistory.Margin = new Padding(Dp(7), 0, 0, 0);

            TableLayoutPanel right = MakeCardStack(1, 4);
            right.RowStyles[1] = new RowStyle(SizeType.Percent, 100f);
            right.RowStyles[2] = new RowStyle(SizeType.Absolute, Dp(44));

            Label htitle = MakeLabel("HISTORIAL", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label hdesc = MakeLabel("Copias disponibles en la carpeta de backups", Fonts.UI(9f), Palette.Muted);

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.BackColor = Palette.Card;
            listHost.Margin = new Padding(0, Dp(8), 0, Dp(6));

            _bkList = new ListBox();
            _bkList.BorderStyle = BorderStyle.None;
            _bkList.BackColor = Palette.Card;
            _bkList.ForeColor = Palette.Text;
            _bkList.Font = Fonts.UI(9f);
            _bkList.IntegralHeight = false;
            _bkList.DrawMode = DrawMode.OwnerDrawFixed;
            _bkList.ItemHeight = Dp(46);
            _bkList.Dock = DockStyle.Fill;
            _bkList.DrawItem += BackupList_DrawItem;
            _bkList.DoubleClick += delegate { OpenBackupFolder(); };

            _bkEmpty = new Label();
            _bkEmpty.Text = "Todavia no hay copias.\r\nUsa COPIAR AHORA o activa los backups automaticos.";
            _bkEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _bkEmpty.Font = Fonts.UI(9f);
            _bkEmpty.ForeColor = Palette.Dim;
            _bkEmpty.BackColor = Palette.Card;
            _bkEmpty.Dock = DockStyle.Fill;
            _bkEmpty.Visible = false;

            listHost.Controls.Add(_bkList);
            listHost.Controls.Add(_bkEmpty);

            _bkOpen2 = new RoundButton();
            _bkOpen2.Text = "ABRIR CARPETA";
            _bkOpen2.Glyph = "folder";
            _bkOpen2.Dock = DockStyle.Fill;
            _bkOpen2.Margin = new Padding(0, Dp(4), 0, 0);
            _bkOpen2.FillColor = Palette.Card;
            _bkOpen2.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            _bkOpen2.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            _bkOpen2.TextColor = Palette.Text;
            _bkOpen2.BorderColor = Palette.Border;

            right.Controls.Add(htitle, 0, 0);
            right.Controls.Add(listHost, 0, 1);
            right.Controls.Add(_bkOpen2, 0, 2);
            _cardBackupHistory.Controls.Add(right);

            grid.Controls.Add(_cardBackupSettings, 0, 0);
            grid.Controls.Add(_cardBackupHistory, 1, 0);
            p.Controls.Add(grid);

            // Valores iniciales y eventos.
            _backupLoading = true;
            AppConfig cfg = AppConfig.Instance;
            _bkEnabled.Checked = cfg.BackupEnabled;
            _bkRecent.Checked = cfg.BackupRecentOnly;
            _bkRecent.Enabled = cfg.BackupMode != "players";
            _bkInterval.Enabled = cfg.BackupEnabled;
            _bkMode.Enabled = true;
            _backupLoading = false;

            _bkEnabled.CheckedChanged += BackupSettingsChanged;
            _bkInterval.SelectedIndexChanged += BackupSettingsChanged;
            _bkMode.SelectedIndexChanged += BackupSettingsChanged;
            _bkRecent.CheckedChanged += BackupSettingsChanged;
            _bkNow.Click += delegate { StartBackup(false); };
            _bkOpen.Click += delegate { OpenBackupFolder(); };
            _bkOpen2.Click += delegate { OpenBackupFolder(); };

            if (cfg.BackupEnabled)
            {
                DateTime last = cfg.LastBackupUtc.HasValue ? cfg.LastBackupUtc.Value : DateTime.UtcNow;
                _nextBackupUtc = last.AddMinutes(IntervalMinutes(cfg.BackupIntervalMinutes));
            }

            RefreshBackupHistory();
            UpdateBackupStatus(true);
            return p;
        }

        private int IntervalIndex(int minutes)
        {
            for (int i = 0; i < BackupIntervals.Length; i++)
                if (BackupIntervals[i] == minutes) return i;
            return 2;
        }

        private int IntervalMinutes(int index)
        {
            if (index < 0 || index >= BackupIntervals.Length) return 60;
            return BackupIntervals[index];
        }

        private void BackupSettingsChanged(object sender, EventArgs e)
        {
            if (_backupLoading) return;
            AppConfig cfg = AppConfig.Instance;
            cfg.BackupEnabled = _bkEnabled.Checked;
            cfg.BackupIntervalMinutes = IntervalMinutes(_bkInterval.SelectedIndex);
            cfg.BackupMode = _bkMode.SelectedIndex == 1 ? "players" : "world";
            cfg.BackupRecentOnly = _bkRecent.Checked;
            cfg.Save();

            _bkInterval.Enabled = cfg.BackupEnabled;
            // El tipo de copia y las regiones de 24 h tambien se usan en las
            // copias manuales, asi que no dependen de los backups automaticos.
            _bkMode.Enabled = true;
            _bkRecent.Enabled = cfg.BackupMode != "players";
            if (cfg.BackupEnabled)
                _nextBackupUtc = DateTime.UtcNow.AddMinutes(cfg.BackupIntervalMinutes);
            else
                _nextBackupUtc = DateTime.MinValue;

            UpdateBackupStatus(true);
        }

        private void BackupTick(bool serverOnline)
        {
            if (_backupLoading || _backupRunning || _versionBusy || _resetRunning) return;
            AppConfig cfg = AppConfig.Instance;
            if (!cfg.BackupEnabled) return;

            bool online = serverOnline || _bridge.IsRunning;
            if (!online) return;

            if (_nextBackupUtc == DateTime.MinValue)
                _nextBackupUtc = DateTime.UtcNow.AddMinutes(cfg.BackupIntervalMinutes);

            if (DateTime.UtcNow >= _nextBackupUtc)
                StartBackup(true);

            if ((DateTime.UtcNow - _lastBackupUiUpdate).TotalSeconds >= 5)
                UpdateBackupStatus(false);
        }

        private async void StartBackup(bool automatic)
        {
            await RunBackupAsync(automatic, false);
        }

        // Ejecuta una copia y devuelve si termino bien. StartBackup lo usa
        // para el boton y el ciclo automatico; el reset programado lo espera
        // antes de detener el servidor con fromReset = true.
        private async System.Threading.Tasks.Task<bool> RunBackupAsync(bool automatic, bool fromReset)
        {
            // Durante un reset solo se permite el backup que dispara el propio
            // reset; el temporizador de backups debe esperar.
            if (_backupRunning || _versionBusy || (_resetRunning && !fromReset)) return false;

            AppConfig cfg = AppConfig.Instance;
            string mode = cfg.BackupMode == "players" ? "players" : "world";
            bool recent = mode == "world" && cfg.BackupRecentOnly;

            _backupRunning = true;
            SetBackupBusy(true);
            _bkProgress.Value = 0;
            _bkProgressText.Text = "Iniciando copia...";
            AppendConsole("[PANEL] Backup " + (automatic ? "automatico" : "manual") +
                " iniciado (" + (mode == "players" ? "solo jugadores" : "mundo") + ").");

            BackupResult result = null;
            try
            {
                result = await System.Threading.Tasks.Task.Run(delegate
                {
                    return _backupService.CreateBackup(_paths, mode, recent,
                        _bridge.IsRunning, _bridge,
                        delegate(double frac, string text) { SafeBackupProgress(frac, text); }, false);
                });
            }
            catch (Exception ex)
            {
                result = new BackupResult();
                result.Error = ex.Message;
            }

            _backupRunning = false;
            SetBackupBusy(false);

            if (result != null && result.Ok)
            {
                cfg.LastBackupUtc = DateTime.UtcNow;
                string next = "";
                if (cfg.BackupEnabled)
                {
                    _nextBackupUtc = DateTime.UtcNow.AddMinutes(cfg.BackupIntervalMinutes);
                    next = _nextBackupUtc.ToLocalTime().ToString("HH:mm");
                }
                else
                {
                    _nextBackupUtc = DateTime.MinValue;
                }
                cfg.Save();

                _bkProgress.Value = 1.0;
                _bkProgressText.Text = "Copia completada: " + Utils.HumanBytes(result.Size);
                if (automatic)
                    AppendConsole("[PANEL] Backup completado: " + Path.GetFileName(result.FilePath) +
                        " (" + Utils.HumanBytes(result.Size) + ")");
                else
                {
                    AppendConsole("[PANEL] Backup completado: " + Path.GetFileName(result.FilePath) +
                        " (" + Utils.HumanBytes(result.Size) + ")");
                    _toast.ShowMessage("Copia completada", Palette.Accent, "check");
                }
                AnnounceBackup(next);
                RefreshBackupHistory();
                UpdateBackupStatus(true);
                return true;
            }
            else
            {
                string error = result == null ? "Error desconocido" : result.Error;
                _bkProgress.Value = 0;
                _bkProgressText.Text = "Backup fallido";
                AppendConsole("[PANEL] Backup fallido: " + error);
                _toast.ShowMessage("Backup fallido", Palette.Red, "warning");
                if (!automatic) MessageDialog.Info(this, "Backup", error);
                if (cfg.BackupEnabled)
                    _nextBackupUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, cfg.BackupIntervalMinutes));
            }

            RefreshBackupHistory();
            UpdateBackupStatus(true);
            return false;
        }

        private void SafeBackupProgress(double fraction, string text)
        {
            try
            {
                if (!IsHandleCreated || IsDisposed) return;
                BeginInvoke(new Action(delegate
                {
                    _bkProgress.Value = fraction;
                    _bkProgressText.Text = text;
                }));
            }
            catch { }
        }

        private void SetBackupBusy(bool busy)
        {
            _bkNow.Enabled = !busy;
            _bkEnabled.Enabled = !busy;
            _bkInterval.Enabled = !busy && AppConfig.Instance.BackupEnabled;
            _bkMode.Enabled = !busy;
            _bkRecent.Enabled = !busy && AppConfig.Instance.BackupMode != "players";
            _bkNow.Text = busy ? "COPIANDO..." : "COPIAR AHORA";
        }

        private void AnnounceBackup(string nextTime)
        {
            if (!_bridge.IsRunning) return;
            string message = string.IsNullOrEmpty(nextTime)
                ? "say [Orthanc Mc] Acabamos de realizar un backup!"
                : "say [Orthanc Mc] Acabamos de realizar un backup! El proximo sera a las " + nextTime + ".";
            _bridge.Send(message);
        }

        private void UpdateBackupStatus(bool force)
        {
            if (_bkLast == null) return;
            if (!force && (DateTime.UtcNow - _lastBackupUiUpdate).TotalSeconds < 2) return;
            _lastBackupUiUpdate = DateTime.UtcNow;

            AppConfig cfg = AppConfig.Instance;
            _bkLast.Text = cfg.LastBackupUtc.HasValue
                ? "Ultima copia: " + cfg.LastBackupUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : "Ultima copia: --";

            bool online = _bridge.IsRunning || _scanner.GetServerStatus().Running;
            if (!cfg.BackupEnabled)
            {
                _bkNext.Text = "Proxima: backups desactivados";
                _bkHint.Text = "Los automaticos requieren el servidor en linea. COPIAR AHORA funciona siempre.";
                _bkProgressText.Text = "";
            }
            else if (!online)
            {
                _bkNext.Text = "Proxima: en espera";
                _bkHint.Text = "Los backups automaticos solo se ejecutan con el servidor en linea.";
            }
            else
            {
                if (_nextBackupUtc == DateTime.MinValue)
                    _nextBackupUtc = DateTime.UtcNow.AddMinutes(cfg.BackupIntervalMinutes);
                _bkNext.Text = "Proxima: " + _nextBackupUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                _bkHint.Text = "El servidor esta en linea; la copia se hara automaticamente.";
            }
            _bkDir.Text = "Carpeta: " + _backupService.GetBackupDir(_paths);
        }

        private void RefreshBackupHistory()
        {
            if (_bkList == null) return;
            List<BackupEntry> entries = _backupService.ListBackups(_paths);
            _bkList.BeginUpdate();
            _bkList.Items.Clear();
            foreach (BackupEntry entry in entries) _bkList.Items.Add(entry);
            _bkList.EndUpdate();

            bool empty = entries.Count == 0;
            _bkList.Visible = !empty;
            _bkEmpty.Visible = empty;
            if (empty) _bkEmpty.BringToFront();
        }

        private void BackupList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            BackupEntry entry = _bkList.Items[e.Index] as BackupEntry;
            if (entry == null) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush bg = new SolidBrush(selected ? Palette.AccentDim : Palette.Card))
                e.Graphics.FillRectangle(bg, e.Bounds);

            Rectangle row = e.Bounds;
            Rectangle dateRect = new Rectangle(row.X + 8, row.Y + 5, row.Width - 110, Dp(18));
            Rectangle kindRect = new Rectangle(row.X + 8, row.Y + Dp(23), row.Width - 110, Dp(16));
            Rectangle sizeRect = new Rectangle(row.Right - 105, row.Y + 5, 97, Dp(20));

            TextRenderer.DrawText(e.Graphics, entry.When.ToString("dd/MM/yyyy HH:mm"),
                Fonts.UI(9.5f, FontStyle.Bold), dateRect, Palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, entry.Label, Fonts.UI(8.5f),
                kindRect, Palette.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics, Utils.HumanBytes(entry.Size),
                Fonts.UI(8.5f), sizeRect, Palette.Muted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private void OpenBackupFolder()
        {
            try
            {
                string dir = _backupService.GetBackupDir(_paths);
                Directory.CreateDirectory(dir);
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "explorer.exe";
                psi.Arguments = "\"" + dir + "\"";
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Backups", ex.Message);
            }
        }

        private static Label MakeWrapLabel(string text, Font font, Color color, int height)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.Height = height;
            label.Dock = DockStyle.Fill;
            label.Margin = new Padding(0, 2, 0, 2);
            return label;
        }
    }
}
