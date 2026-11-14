#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace McPanel
{
    // Resets automaticos: reinicio ciclico del servidor con avisos en la
    // action bar, backup previo y arranque posterior. La cuenta atras solo
    // corre mientras el servidor esta iniciado desde el panel.
    public partial class MainForm
    {
        private static readonly int[] ResetIntervals =
            { 60, 120, 180, 240, 360, 480, 720, 1440, 2880, 4320 };
        private static readonly string[] ResetIntervalNames =
        {
            "1 hora", "2 horas", "3 horas", "4 horas", "6 horas",
            "8 horas", "12 horas", "24 horas", "48 horas", "72 horas"
        };

        // Etiquetas de la lista de avisos; se traducen al construirlas y al
        // cambiar de idioma.
        private static readonly string[] ResetWarningTitles =
        {
            "10 minutos", "5 minutos", "1 minuto", "30 segundos",
            "5, 4, 3, 2 y 1 segundo"
        };

        // Segundos antes del reset en los que se avisa por action bar.
        // De mayor a menor; el primero que se cruza al arrancar la app es el
        // que se anuncia.
        private static readonly int[] ResetWarnSeconds = { 600, 300, 60, 30, 5, 4, 3, 2, 1 };

        private Panel _viewResets;
        private Panel _viewResetsHost;
        private ConfigEntry _cfgEntryResets;
        private ToggleSwitch _resetEnabled;
        private SelectBox _resetInterval;
        private Label[] _resetWarnRows;
        private Label _resetNext;
        private Label _resetRemaining;
        private Label _resetState;
        private Label _resetLast;
        private Label _resetWarning;
        private Label _resetHint;
        private bool _resetLoading;
        private bool _resetRunning;
        private DateTime _nextResetUtc = DateTime.MinValue;
        private int _resetLastWarning = int.MaxValue;

        private Panel BuildResets()
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
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ---------------- Ajustes ----------------
            Card settings = new Card();
            settings.Dock = DockStyle.Fill;
            settings.Margin = new Padding(0, 0, Dp(7), 0);

            TableLayoutPanel left = MakeCardStack(2, 12);
            left.ColumnStyles.Clear();
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(170)));
            left.RowStyles[10] = new RowStyle(SizeType.Percent, 100f);

            Label title = MakeLabel("RESETS AUTOMATICOS", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label desc = MakeLabel("Reinicio ciclico con backup previo y avisos a los jugadores",
                Fonts.UI(9f), Palette.Muted);

            Label enableLabel = MakeLabel("Activar resets automaticos", Fonts.UI(9.25f), Palette.Text);
            _resetEnabled = new ToggleSwitch();
            _resetEnabled.Anchor = AnchorStyles.Right;
            _resetEnabled.Margin = new Padding(0, 4, 0, 4);

            Label intervalLabel = MakeLabel("Cada", Fonts.UI(9.25f), Palette.Muted);
            _resetInterval = new SelectBox(TranslatedResetIntervals(),
                TranslatedResetIntervalName(ResetIntervalIndex(AppConfig.Instance.ResetIntervalMinutes)));
            _resetInterval.Dock = DockStyle.Fill;
            _resetInterval.Margin = new Padding(0, 3, 0, 3);

            Label sequence = MakeWrapLabel(
                "Al llegar la hora: se crea un backup, se detiene el servidor y se vuelve a iniciar. " +
                "Los jugadores reciben avisos en la action bar a los 10 min, 5 min, 1 min, 30 s " +
                "y en la cuenta atras de 5 a 1 segundo. Los avisos de 10 y 5 minutos tambien se " +
                "envian por el chat.",
                Fonts.UI(8.5f), Palette.Dim, Dp(56));

            Label sep = new Label();
            sep.Height = 1;
            sep.Dock = DockStyle.Fill;
            sep.BackColor = Palette.Border;
            sep.Margin = new Padding(0, 6, 0, 6);

            _resetNext = MakeLabel("Proximo reset: --", Fonts.UI(9.5f), Palette.Text);
            _resetLast = MakeLabel("Ultimo reset: --", Fonts.UI(9f), Palette.Muted);
            _resetState = MakeLabel("Desactivado", Fonts.UI(9f, FontStyle.Bold), Palette.Dim);
            _resetHint = MakeWrapLabel(
                "El ciclo solo corre con el servidor iniciado desde el panel.",
                Fonts.UI(8.5f), Palette.Dim, Dp(36));

            left.Controls.Add(title, 0, 0);
            left.SetColumnSpan(title, 2);
            left.Controls.Add(desc, 0, 1);
            left.SetColumnSpan(desc, 2);
            left.Controls.Add(enableLabel, 0, 2);
            left.Controls.Add(_resetEnabled, 1, 2);
            left.Controls.Add(intervalLabel, 0, 3);
            left.Controls.Add(_resetInterval, 1, 3);
            left.Controls.Add(sequence, 0, 4);
            left.SetColumnSpan(sequence, 2);
            left.Controls.Add(sep, 0, 5);
            left.SetColumnSpan(sep, 2);
            left.Controls.Add(_resetNext, 0, 6);
            left.SetColumnSpan(_resetNext, 2);
            left.Controls.Add(_resetLast, 0, 7);
            left.SetColumnSpan(_resetLast, 2);
            left.Controls.Add(_resetState, 0, 8);
            left.SetColumnSpan(_resetState, 2);
            left.Controls.Add(_resetHint, 0, 9);
            left.SetColumnSpan(_resetHint, 2);
            settings.Controls.Add(left);

            // ---------------- Estado ----------------
            Card status = new Card();
            status.Dock = DockStyle.Fill;
            status.Margin = new Padding(Dp(7), 0, 0, 0);

            TableLayoutPanel right = MakeCardStack(1, 11);
            right.RowStyles[10] = new RowStyle(SizeType.Percent, 100f);

            Label stitle = MakeLabel("TIEMPO PARA EL RESET", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label sdesc = MakeLabel("Cuenta atras del proximo reinicio programado", Fonts.UI(9f), Palette.Muted);

            _resetRemaining = MakeLabel("--:--:--", Fonts.UI(28f, FontStyle.Bold), Palette.Dim);
            _resetRemaining.Margin = new Padding(0, Dp(16), 0, Dp(2));

            _resetWarning = MakeLabel("Proximo aviso: --", Fonts.UI(9f), Palette.Muted);

            Label wtitle = MakeLabel("AVISOS A LOS JUGADORES", Fonts.UI(8.5f, FontStyle.Bold), Palette.Dim);
            wtitle.Margin = new Padding(0, Dp(18), 0, Dp(6));

            right.Controls.Add(stitle, 0, 0);
            right.Controls.Add(sdesc, 0, 1);
            right.Controls.Add(_resetRemaining, 0, 2);
            right.Controls.Add(_resetWarning, 0, 3);
            right.Controls.Add(wtitle, 0, 4);

            Color[] warnColors =
            {
                Palette.Accent, Palette.Amber, Palette.Amber, Palette.Red, Palette.Red
            };
            _resetWarnRows = new Label[ResetWarningTitles.Length];
            for (int i = 0; i < ResetWarningTitles.Length; i++)
            {
                Label row = MakeLabel("\u2022  " + LanguageService.T(ResetWarningTitles[i]),
                    Fonts.UI(9f), warnColors[i]);
                row.Tag = ResetWarningTitles[i];
                row.Margin = new Padding(0, 2, 0, 2);
                right.Controls.Add(row, 0, 5 + i);
                _resetWarnRows[i] = row;
            }
            status.Controls.Add(right);

            grid.Controls.Add(settings, 0, 0);
            grid.Controls.Add(status, 1, 0);
            p.Controls.Add(grid);

            // Valores iniciales y eventos.
            _resetLoading = true;
            AppConfig cfg = AppConfig.Instance;
            _resetEnabled.Checked = cfg.ResetEnabled;
            _resetInterval.Enabled = cfg.ResetEnabled;
            if (cfg.ResetEnabled)
            {
                if (cfg.NextResetUtc.HasValue)
                    _nextResetUtc = cfg.NextResetUtc.Value;
                else
                {
                    _nextResetUtc = DateTime.UtcNow.AddMinutes(ResetIntervalMinutes(cfg.ResetIntervalMinutes));
                    cfg.NextResetUtc = _nextResetUtc;
                    cfg.Save();
                }
            }
            _resetLoading = false;

            _resetEnabled.CheckedChanged += ResetSettingsChanged;
            _resetInterval.SelectedIndexChanged += ResetSettingsChanged;
            // Los nombres de intervalo y la lista de avisos no pasan por
            // ApplyTo (el desplegable y el prefijo del bullet no coinciden),
            // asi que se traducen aqui al cambiar de idioma.
            LanguageService.Changed += delegate { RefreshResetLanguage(); };

            UpdateResetStatus(true);
            return p;
        }

        private int ResetIntervalIndex(int minutes)
        {
            for (int i = 0; i < ResetIntervals.Length; i++)
                if (ResetIntervals[i] == minutes) return i;
            return 4;
        }

        private int ResetIntervalMinutes(int index)
        {
            if (index < 0 || index >= ResetIntervals.Length) return 360;
            return ResetIntervals[index];
        }

        private static string TranslatedResetIntervalName(int index)
        {
            if (index < 0 || index >= ResetIntervalNames.Length) index = 4;
            return LanguageService.T(ResetIntervalNames[index]);
        }

        private static string[] TranslatedResetIntervals()
        {
            string[] items = new string[ResetIntervalNames.Length];
            for (int i = 0; i < items.Length; i++)
                items[i] = LanguageService.T(ResetIntervalNames[i]);
            return items;
        }

        // Cambio de idioma: renombra el desplegable y la lista de avisos sin
        // reprogramar el reset.
        private void RefreshResetLanguage()
        {
            if (_resetWarnRows != null)
            {
                foreach (Label row in _resetWarnRows)
                    if (row != null)
                        row.Text = "\u2022  " + LanguageService.T(Convert.ToString(row.Tag));
            }

            if (_resetInterval != null)
            {
                bool loading = _resetLoading;
                _resetLoading = true;
                int index = _resetInterval.SelectedIndex;
                _resetInterval.SetItems(TranslatedResetIntervals(),
                    TranslatedResetIntervalName(index < 0 ? 4 : index));
                _resetLoading = loading;
            }

            UpdateResetStatus(true);
        }

        private void ResetSettingsChanged(object sender, EventArgs e)
        {
            if (_resetLoading) return;
            AppConfig cfg = AppConfig.Instance;
            cfg.ResetEnabled = _resetEnabled.Checked;
            cfg.ResetIntervalMinutes = ResetIntervalMinutes(_resetInterval.SelectedIndex);
            _resetInterval.Enabled = cfg.ResetEnabled;

            if (cfg.ResetEnabled)
            {
                ScheduleResetFromNow(cfg, true);
                AppendConsole("[PANEL] Resets automaticos activados cada " +
                    ResetIntervalNames[ResetIntervalIndex(cfg.ResetIntervalMinutes)] + ".");
            }
            else
            {
                _nextResetUtc = DateTime.MinValue;
                cfg.NextResetUtc = null;
                cfg.Save();
                AppendConsole("[PANEL] Resets automaticos desactivados.");
            }
            UpdateResetStatus(true);
        }

        private void ScheduleResetFromNow(AppConfig cfg, bool save)
        {
            _nextResetUtc = DateTime.UtcNow.AddMinutes(cfg.ResetIntervalMinutes);
            cfg.NextResetUtc = _nextResetUtc;
            _resetLastWarning = int.MaxValue;
            if (save) cfg.Save();
        }

        // Se llama cada segundo desde Tick_Tick, en el hilo de interfaz.
        private void ResetTick()
        {
            AppConfig cfg = AppConfig.Instance;
            if (_resetNext == null) return;

            if (!cfg.ResetEnabled)
            {
                if (_viewResets.Visible) UpdateResetStatus(false);
                return;
            }

            if (!_resetRunning)
            {
                if (!_bridge.IsRunning)
                {
                    // Sin servidor gestionado no hay a quien avisar ni reiniciar.
                    if (_viewResets.Visible) UpdateResetStatus(false);
                    return;
                }

                if (_nextResetUtc == DateTime.MinValue)
                    ScheduleResetFromNow(cfg, true);

                double remaining = (_nextResetUtc - DateTime.UtcNow).TotalSeconds;
                if (remaining <= 0)
                {
                    if (!_backupRunning && !_versionBusy && !_startBusy)
                        PerformReset();
                    else if (_viewResets.Visible)
                        UpdateResetStatus(false);
                    return;
                }

                AnnounceResetWarnings(remaining);
            }

            if (_viewResets.Visible) UpdateResetStatus(false);
        }

        private void AnnounceResetWarnings(double remaining)
        {
            // Si el ciclo arranca con la cuenta ya empezada (por ejemplo al
            // abrir el panel con el reset cerca), no se anuncia un umbral ya
            // pasado: se marca el limite superior actual y se espera al
            // siguiente cruce real. Si aun queda mas de 10 min no se toca.
            if (_resetLastWarning == int.MaxValue)
            {
                int boundary = 0;
                foreach (int seconds in ResetWarnSeconds)
                    if (remaining <= seconds) boundary = seconds;
                if (boundary > 0)
                {
                    _resetLastWarning = boundary;
                    return;
                }
            }

            int chosen = 0;
            foreach (int seconds in ResetWarnSeconds)
            {
                if (remaining <= seconds && _resetLastWarning > seconds)
                    chosen = seconds;
            }
            if (chosen <= 0) return;

            string color = ResetWarningColor(chosen);
            string text = "Reinicio en " + ResetWarningLabel(chosen);
            string json = "{\"text\":\"" + text + "\",\"color\":\"" + color + "\",\"bold\":true}";
            _bridge.Send("title @a actionbar " + json);

            // Los avisos de 10 y 5 minutos se refuerzan ademas en el chat
            // para que no se pierdan si el jugador no mira la action bar.
            bool chat = chosen == 600 || chosen == 300;
            if (chat)
            {
                string chatJson = "{\"text\":\"[Orthanc Mc] " + text +
                    "\",\"color\":\"" + color + "\"}";
                _bridge.Send("tellraw @a " + chatJson);
            }

            AppendConsole("[PANEL] Aviso de reset enviado: " + text +
                (chat ? " (action bar y chat)." : "."));
            _resetLastWarning = chosen;
        }

        private static string ResetWarningColor(int seconds)
        {
            if (seconds >= 600) return "green";
            if (seconds >= 300) return "yellow";
            if (seconds >= 60) return "gold";
            if (seconds >= 30) return "red";
            return "dark_red";
        }

        private static string ResetWarningLabel(int seconds)
        {
            if (seconds >= 60)
            {
                int minutes = seconds / 60;
                return minutes + (minutes == 1 ? " minuto" : " minutos");
            }
            return seconds + (seconds == 1 ? " segundo" : " segundos");
        }

        // Secuencia del reset: backup -> stop -> start. Se ejecuta en el hilo
        // de interfaz pero las operaciones lentas van a segundo plano.
        private async void PerformReset()
        {
            if (_resetRunning) return;
            _resetRunning = true;

            AppConfig cfg = AppConfig.Instance;
            int interval = cfg.ResetIntervalMinutes > 0 ? cfg.ResetIntervalMinutes : 360;

            SetStartBusy(true);
            SetResetBusy(true);
            UpdateResetStatus(true);
            AppendConsole("[PANEL] Reset automatico iniciado: backup previo.");

            bool backupOk = await RunBackupAsync(true, true);
            if (!backupOk)
                AppendConsole("[PANEL] El backup previo fallo; se continua con el reinicio.");

            _bridge.Send("say [Orthanc Mc] Reinicio programado: el servidor se detiene y vuelve en unos segundos.");
            UpdateResetStatus(true);

            bool stopped = await System.Threading.Tasks.Task.Run(delegate
            {
                bool ok = _bridge.StopGracefully(60000);
                try { _playit.Stop(); } catch { }
                return ok;
            });

            AppendConsole(stopped
                ? "[PANEL] Servidor detenido para el reset."
                : "[PANEL] El servidor no se detuvo a tiempo; se fuerza el arranque.");

            await System.Threading.Tasks.Task.Delay(1500);

            int ram = _propsFile.GetInt("server-ram-mb", _selectedRamMb);
            if (ram < 1024) ram = 1024;
            if (ram > 12288) ram = 12288;

            bool started = await System.Threading.Tasks.Task.Run(delegate
            {
                return _bridge.Start(_paths, ram);
            });
            if (started)
            {
                try { _playit.Start(); } catch { }
            }

            cfg.LastResetUtc = DateTime.UtcNow;
            _nextResetUtc = DateTime.UtcNow.AddMinutes(interval);
            cfg.NextResetUtc = _nextResetUtc;
            cfg.Save();
            _resetLastWarning = int.MaxValue;
            _resetRunning = false;

            SetResetBusy(false);
            SetStartBusy(false);
            UpdateResetStatus(true);

            if (started)
            {
                AppendConsole("[PANEL] Reset completado; proximo reset: " +
                    _nextResetUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + ".");
                _toast.ShowMessage("Servidor reiniciado", Palette.Accent, "restart");
            }
            else
            {
                AppendConsole("[PANEL] El servidor no pudo reiniciarse tras el reset.");
                _toast.ShowMessage("No se pudo reiniciar el servidor", Palette.Red, "warning");
            }
        }

        // Un arranque manual tras una hora de reset vencida empieza un ciclo
        // nuevo para no reiniciar el servidor justo despues de abrirlo.
        private void ResetOnServerStarted()
        {
            AppConfig cfg = AppConfig.Instance;
            if (!cfg.ResetEnabled) return;
            if (_nextResetUtc != DateTime.MinValue && _nextResetUtc > DateTime.UtcNow) return;

            ScheduleResetFromNow(cfg, true);
            AppendConsole("[PANEL] Ciclo de resets reprogramado: proximo " +
                _nextResetUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + ".");
        }

        private void SetResetBusy(bool busy)
        {
            if (_resetEnabled != null) _resetEnabled.Enabled = !busy;
            if (_resetInterval != null)
                _resetInterval.Enabled = !busy && AppConfig.Instance.ResetEnabled;
        }

        private void UpdateResetStatus(bool force)
        {
            if (_resetNext == null) return;

            AppConfig cfg = AppConfig.Instance;
            _resetLast.Text = cfg.LastResetUtc.HasValue
                ? LanguageService.T("Ultimo reset: ") + cfg.LastResetUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                : LanguageService.T("Ultimo reset: --");

            if (!cfg.ResetEnabled)
            {
                _resetState.Text = LanguageService.T("Desactivado");
                _resetState.ForeColor = Palette.Dim;
                _resetRemaining.Text = "--:--:--";
                _resetRemaining.ForeColor = Palette.Dim;
                _resetNext.Text = LanguageService.T("Proximo reset: --");
                _resetWarning.Text = LanguageService.T("Proximo aviso: --");
                _resetHint.Text = LanguageService.T(
                    "El ciclo solo corre con el servidor iniciado desde el panel.");
                return;
            }

            if (_resetRunning)
            {
                _resetState.Text = LanguageService.T("Reiniciando...");
                _resetState.ForeColor = Palette.Amber;
                _resetRemaining.Text = "--:--:--";
                _resetRemaining.ForeColor = Palette.Amber;
                _resetNext.Text = LanguageService.T("Proximo reset: --");
                _resetWarning.Text = LanguageService.T("Reiniciando el servidor y su backup");
                _resetHint.Text = LanguageService.T(
                    "El servidor se detiene y vuelve a iniciar automaticamente.");
                return;
            }

            if (!_bridge.IsRunning)
            {
                _resetState.Text = LanguageService.T("En espera");
                _resetState.ForeColor = Palette.Amber;
                _resetRemaining.Text = "--:--:--";
                _resetRemaining.ForeColor = Palette.Dim;
                _resetNext.Text = _nextResetUtc == DateTime.MinValue
                    ? LanguageService.T("Proximo reset: --")
                    : LanguageService.T("Proximo reset: ") + _nextResetUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                _resetWarning.Text = LanguageService.T("Proximo aviso: --");
                _resetHint.Text = LanguageService.T(
                    "Inicia el servidor desde el panel para reanudar la cuenta atras.");
                return;
            }

            if (_nextResetUtc == DateTime.MinValue)
                ScheduleResetFromNow(cfg, true);

            double remaining = (_nextResetUtc - DateTime.UtcNow).TotalSeconds;
            if (remaining < 0) remaining = 0;
            int total = (int)Math.Ceiling(remaining);

            _resetState.Text = LanguageService.T("Activo");
            _resetState.ForeColor = Palette.Accent;
            _resetRemaining.Text = string.Format("{0:00}:{1:00}:{2:00}",
                total / 3600, (total % 3600) / 60, total % 60);
            _resetRemaining.ForeColor = total <= 60 ? Palette.Red
                : (total <= 300 ? Palette.Amber : Palette.Accent);
            _resetNext.Text = LanguageService.T("Proximo reset: ") +
                _nextResetUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            int nextWarn = 0;
            foreach (int seconds in ResetWarnSeconds)
            {
                if (total > seconds) { nextWarn = seconds; break; }
            }
            _resetWarning.Text = nextWarn > 0
                ? LanguageService.T("Proximo aviso: ") + LanguageService.T(ResetWarningLabel(nextWarn))
                : LanguageService.T("Reinicio inminente");
            _resetHint.Text = LanguageService.T(
                "Avisos por action bar y chat: 10 y 5 minutos. Backup automatico antes del reinicio.");
        }
    }
}

