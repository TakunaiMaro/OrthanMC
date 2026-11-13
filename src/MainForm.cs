using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace McPanel
{
    public partial class MainForm : Form
    {
        private readonly ServerPaths _paths = new ServerPaths();
        private readonly RamMonitor _ram = new RamMonitor();
        private readonly ProcessScanner _scanner = new ProcessScanner();
        private readonly LogTail _logTail = new LogTail();
        private readonly ServiceCtrl _playit = new ServiceCtrl("playitd");
        private readonly ConsoleBridge _bridge = new ConsoleBridge();
        private readonly System.Windows.Forms.Timer _tick = new System.Windows.Forms.Timer();

        private Panel _caption;
        private Panel _captionRight;
        private Label _captionTitle;
        private ServerSwitchButton _serverSwitch;
        private CaptionButton _captionMin;
        private CaptionButton _captionMax;
        private CaptionButton _captionClose;
        private const int WM_NCHITTEST = 0x84;
        private const int WM_SETICON = 0x80;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MAXIMIZE = 0xF030;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private Panel _sidebar;
        private Panel _header;
        private Panel _logoMark;
        private Label _logoTitle;
        private Label _logoSub;
        private Label _versionLabel;
        private NavButton _navStatus;
        private NavButton _navConsole;
        private NavButton _navAddons;
        private NavButton _navConfig;
        private Label _pageTitle;
        private Label _pageSub;
        private StatusBadge _serverBadge;
        private StatusBadge _playitBadge;
        private Panel _content;
        private TableLayoutPanel _rootLayout;
        private SidebarToggleButton _sidebarToggle;
        private ToolTip _sidebarTip;
        private System.Windows.Forms.Timer _sidebarAnim;
        private int _sidebarWidth;
        private int _sidebarFrom;
        private int _sidebarTo;
        private DateTime _sidebarAnimStart;
        private bool _sidebarHidden;
        private const int SidebarAnimMs = 280;
        private readonly List<int> _navHistory = new List<int>();
        private int _currentSection = -1;
        private Panel _viewStatus;
        private Panel _viewConsole;
        private Panel _viewAddons;
        private Panel _viewConfigHub;
        private Panel _viewConfig;
        private Panel _viewConfigRam;
        private Panel _viewBackups;
        private Panel _viewBackupsHost;
        private Panel _viewVersions;
        private Panel _viewVersionsHost;
        private ProfilesView _profilesView;
        private Panel _body;
        private NavButton _navProfiles;
        private bool _inProfiles = true;
        private FormWindowState _lastWindowState = FormWindowState.Normal;
        private string _hoursProfilePath = "";
        private DateTime _lastHoursSave = DateTime.MinValue;
        private ScrollBarBinder _configScroll;
        private ScrollBarBinder _propsScroll;
        private ScrollBarBinder _addonScroll;
        private Panel _configScrollHost;
        private AddonKind _addonKind = AddonKind.None;
        private Toast _toast;
        private RoundButton _btnStart;
        private RoundButton _btnRestart;
        private Icon _taskbarIconBig;
        private Icon _taskbarIconSmall;

        // Status
        private Card _cardServer;
        private Label _srvTitle;
        private Label _srvState;
        private Label _srvIpDomain;
        private RoundButton _copyDomain;
        private Label _srvIpLocal;
        private Label _srvPid;
        private Label _srvRamVal;
        private UsageBar _srvRamBar;
        private Label _sysRamTitle;
        private Label _sysRamVal;
        private UsageBar _sysRamBar;
        private Label _sysFree;
        private Card _cardPlayers;
        private Label _plyTitle;
        private Label _plyCount;
        private ListBox _plyList;
        private Card _cardPlayit;
        private Label _pitTitle;
        private Label _pitStatus;
        private Label _pitTunnel;
        private Label _pitClients;
        private TextBox _pitLog;

        // Dominio publico de Playit (IPC) e IP local del servidor.
        private string _lastPlayitDomain = "";
        private string _lastPlayitDestination = "";
        private DateTime _lastPlayitPoll = DateTime.MinValue;
        private bool _playitPolling;

        // Console
        private TextBox _consoleOut;
        private InputBox _consoleIn;
        private RoundButton _consoleSend;
        private Label _consoleHint;
        private List<string> _consoleHistory = new List<string>();
        private int _historyIndex = -1;
        // Un modpack puede escribir miles de lineas por segundo al arrancar.
        // La salida se acumula y se pinta en lotes para no saturar la interfaz.
        private readonly Queue<string> _consoleQueue = new Queue<string>();
        private readonly object _consoleQueueLock = new object();
        private readonly System.Windows.Forms.Timer _consoleTimer = new System.Windows.Forms.Timer();
        private bool _startBusy;

        // Config
        private Card _cardRam;
        private Label _ramTitle;
        private Label _ramVal;
        private SliderBar _ramSlider;
        private RoundButton _ramApply;
        private Card _cardProps;
        private Label _propsTitle;
        private TextBox _propsSearch;
        private FlowLayoutPanel _propsFlow;
        private RoundButton _propsSave;
        private ConfigEntry _cfgEntryRam;
        private ConfigEntry _cfgEntryBackups;
        private ConfigEntry _cfgEntryVersions;
        private PropertiesFile _propsFile;
        private int _selectedRamMb = 4096;
        private bool _serverWasRunning;
        private bool _versionBusy;

        public MainForm()
        {
            if (_toast == null) _toast = new Toast();
            _propsFile = new PropertiesFile(_paths.Properties);
            InitializeUI();
            Wire();
            _tick.Interval = 1000;
            _tick.Start();
            _consoleTimer.Interval = 100;
            _consoleTimer.Tick += delegate { FlushConsoleQueue(); };
            _consoleTimer.Start();
            _bridge.Output += AppendConsole;
            RefreshAll();
        }

        // ------------------------------------------------------------------
        // BASE DEL LAYOUT
        // Todo el HUD se arma con TableLayoutPanel en lugar de depender del
        // orden de docking: WinForms acomoda los controles en orden inverso al
        // de la coleccion, por eso el contenido Fill terminaba ocupando toda
        // la ventana y el header/sidebar quedaban encima tapando las tarjetas.
        // ------------------------------------------------------------------
        private float _uiScale = 1f;

        private int Dp(int value)
        {
            return (int)Math.Round(value * _uiScale);
        }

        private void InitializeUI()
        {
            try
            {
                using (Graphics probe = CreateGraphics()) _uiScale = probe.DpiX / 96f;
            }
            catch
            {
                _uiScale = 1f;
            }

            Text = Brand.Name + " - " + ServerProjects.ActiveName;
            Icon icon = Brand.AppIcon;
            if (icon != null) Icon = icon;
            BackColor = Palette.TitleBar;
            DoubleBuffered = true;
            // Barra de titulo propia: permite colocar el selector de servers
            // justo al lado de minimizar y conservar el color del tema.
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = true;
            MinimizeBox = true;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            Padding = new Padding(Dp(4));

            // La ventana nunca debe exceder el area util de la pantalla.
            Rectangle workArea = Screen.PrimaryScreen != null
                ? Screen.PrimaryScreen.WorkingArea
                : new Rectangle(0, 0, 1280, 800);
            int winW = Math.Min(Dp(1400), Math.Max(Dp(1024), workArea.Width - Dp(32)));
            int winH = Math.Min(Dp(920), Math.Max(Dp(720), workArea.Height - Dp(24)));
            Size = new Size(winW, winH);
            MinimumSize = new Size(Math.Min(Dp(1080), workArea.Width), Math.Min(Dp(720), workArea.Height));

            TableLayoutPanel root = new TableLayoutPanel();
            root.Name = "rootLayout";
            root.Dock = DockStyle.Fill;
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);
            // Transparente para que el fondo de panorama del _body se vea
            // detras de la barra lateral y de la columna principal.
            root.BackColor = Color.Transparent;
            root.ColumnCount = 2;
            root.RowCount = 1;
            _sidebarWidth = Dp(290);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _sidebarWidth));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _rootLayout = root;

            // Animacion de la barra lateral: se mueve el ancho de la primera
            // columna y el contenido se reacomoda en cada cuadro.
            _sidebarAnim = new System.Windows.Forms.Timer();
            _sidebarAnim.Interval = 10;
            _sidebarAnim.Tick += SidebarAnim_Tick;

            _addonKind = DetectAddonKind();
            BuildSidebar();

            TableLayoutPanel main = new TableLayoutPanel();
            main.Name = "mainColumn";
            main.Dock = DockStyle.Fill;
            main.Margin = new Padding(0);
            main.Padding = new Padding(0);
            main.BackColor = Color.Transparent;
            main.ColumnCount = 1;
            main.RowCount = 2;
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(112)));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _header = new Panel();
            _header.Name = "_header";
            _header.Dock = DockStyle.Fill;
            _header.Margin = new Padding(0);
            _header.BackColor = Color.Transparent;
            _header.Padding = new Padding(Dp(38), Dp(12), Dp(38), Dp(10));
            BuildHeader();

            _content = new Panel();
            _content.Name = "_content";
            _content.Dock = DockStyle.Fill;
            _content.Margin = new Padding(0);
            _content.BackColor = Color.Transparent;
            _content.Padding = new Padding(Dp(38), Dp(16), Dp(38), Dp(28));

            _viewStatus = BuildStatus();
            _viewConsole = BuildConsole();
            _viewBackups = BuildBackups();
            _viewVersions = BuildVersions();
            _viewConfig = BuildConfig();
            _viewAddons = BuildAddons();
            _viewConfigHub = BuildConfigHub();
            _viewConfigRam = MakeConfigSection(_viewConfig, "RAM, Server.properties y icono");
            _viewBackupsHost = MakeConfigSection(_viewBackups, "Copias de seguridad");
            _viewVersionsHost = MakeConfigSection(_viewVersions, "Versiones del servidor");
            _viewResets = BuildResets();
            _viewResetsHost = MakeConfigSection(_viewResets, "Resets automaticos");
            BuildAppConfigViews();
            _viewStatus.Dock = DockStyle.Fill;
            _viewConsole.Dock = DockStyle.Fill;
            _viewAddons.Dock = DockStyle.Fill;
            _viewConfigHub.Dock = DockStyle.Fill;
            _viewConfigRam.Dock = DockStyle.Fill;
            _viewBackupsHost.Dock = DockStyle.Fill;
            _viewVersionsHost.Dock = DockStyle.Fill;
            _viewResetsHost.Dock = DockStyle.Fill;
            _viewConsole.Visible = false;
            _viewAddons.Visible = false;
            _viewConfigHub.Visible = false;
            _viewConfigRam.Visible = false;
            _viewBackupsHost.Visible = false;
            _viewVersionsHost.Visible = false;
            _viewResetsHost.Visible = false;
            _content.Controls.Add(_viewStatus);
            _content.Controls.Add(_viewConsole);
            _content.Controls.Add(_viewAddons);
            _content.Controls.Add(_viewConfigHub);
            _content.Controls.Add(_viewConfigRam);
            _content.Controls.Add(_viewBackupsHost);
            _content.Controls.Add(_viewVersionsHost);
            _content.Controls.Add(_viewResetsHost);
            _viewStatus.BringToFront();

            main.Controls.Add(_header, 0, 0);
            main.Controls.Add(_content, 0, 1);
            root.Controls.Add(_sidebar, 0, 0);
            root.Controls.Add(main, 1, 0);

            // El cuerpo aloja la barra lateral y la columna principal. La
            // pantalla de Perfiles vive dentro de esa columna para que el
            // boton de la barra lateral funcione igual en todas las vistas.
            // El panel tambien dibuja el panorama custom como fondo.
            _body = new PanoramaBackdrop.PanoramaHostPanel();
            _body.Name = "_body";
            _body.Dock = DockStyle.Fill;
            _body.Margin = new Padding(0);
            _body.BackColor = Palette.Bg;
            _body.Controls.Add(root);

            _profilesView = new ProfilesView();
            _profilesView.Name = "_profilesView";
            _profilesView.Dock = DockStyle.Fill;
            _profilesView.Margin = new Padding(0);
            _profilesView.Visible = false;
            _profilesView.OpenRequested += delegate(ServerEntry entry) { OpenProfileRequest(entry); };
            _profilesView.ContentReady += delegate { MarkProfilesReady(); };
            main.Controls.Add(_profilesView, 0, 0);
            main.SetRowSpan(_profilesView, 2);

            // Al arrancar se ve Perfiles, pero la barra lateral permanece
            // disponible para navegar al HUD.
            _header.Visible = false;
            _content.Visible = false;
            _profilesView.Visible = true;
            _profilesView.BringToFront();
            if (_navProfiles != null) _navProfiles.Active = true;

            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Name = "windowShell";
            shell.Dock = DockStyle.Fill;
            shell.Margin = new Padding(0);
            shell.Padding = new Padding(0);
            shell.BackColor = Palette.TitleBar;
            shell.ColumnCount = 1;
            shell.RowCount = 2;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(38)));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            BuildCaption();
            shell.Controls.Add(_caption, 0, 0);
            shell.Controls.Add(_body, 0, 1);
            Controls.Add(shell);

            // Capa de carga a pantalla completa (debajo de la barra de titulo).
            // Cubre el panel mientras se leen los plugins o mods.
            _loadingOverlay = new LoadingOverlay();
            _loadingOverlay.Visible = false;
            _loadingOverlay.Finished += delegate
            {
                if (_loadingOverlay != null) _loadingOverlay.Visible = false;
            };
            Controls.Add(_loadingOverlay);
            _loadingOverlay.BringToFront();
            PositionLoadingOverlay();

            if (_toast == null) _toast = new Toast();
            Controls.Add(_toast);
            PositionToast();
            _toast.Visible = false;
            _toast.BringToFront();

            Resize += delegate
            {
                bool restored = _lastWindowState == FormWindowState.Minimized &&
                    WindowState != FormWindowState.Minimized;
                _lastWindowState = WindowState;
                UpdateWindowFrame();
                PositionToast();
                PositionLoadingOverlay();
                // Al volver de minimizado algunos controles pueden quedar con
                // zonas sin pintar (se ve la ventana vacia). Un repintado
                // completo, ya con el tamano final, lo evita.
                if (restored)
                {
                    BeginInvoke(new Action(delegate
                    {
                        if (IsDisposed || Disposing) return;
                        Invalidate(true);
                        Update();
                    }));
                }
            };
            Shown += delegate
            {
                _lastWindowState = WindowState;
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                UpdateWindowFrame();
                PositionLoadingOverlay();
                BeginInvoke(new Action(BeginProfilesStartup));
                // La comprobacion de actualizaciones arranca unos segundos
                // despues para no competir con la animacion de carga inicial.
                System.Threading.Tasks.Task.Delay(4000).ContinueWith(delegate
                {
                    try
                    {
                        if (IsHandleCreated && !IsDisposed && !Disposing)
                            BeginInvoke(new Action(CheckForUpdatesOnStartup));
                    }
                    catch { }
                });
            };
            // ThemeManager.Apply ya repinta todas las ventanas abiertas.
            ThemeApplier.ApplyTo(this);

            _serverSwitch.Visible = false;
            if (_captionTitle != null) _captionTitle.Text = Brand.NameUpper + "  ·  PERFILES";
            LanguageService.Changed += delegate
            {
                if (_sidebarTip != null && _sidebarToggle != null)
                    _sidebarTip.SetToolTip(_sidebarToggle,
                        LanguageService.T(_sidebarHidden ? "Atraer Barra lateral" : "Esconder Barra lateral"));
            };
            LanguageService.ApplyTo(this);
        }

        // ------------------------------------------------------------------
        // Barra de titulo propia y selector de servidores
        // ------------------------------------------------------------------
        private void BuildCaption()
        {
            _caption = new BufferedPanel();
            _caption.Name = "_caption";
            _caption.Dock = DockStyle.Fill;
            _caption.Margin = new Padding(0);
            _caption.BackColor = Palette.TitleBar;

            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.Margin = new Padding(0);
            bar.Padding = new Padding(0);
            bar.BackColor = Palette.TitleBar;
            bar.ColumnCount = 2;
            bar.RowCount = 1;
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(330)));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            FlowLayoutPanel left = new FlowLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.FlowDirection = FlowDirection.LeftToRight;
            left.WrapContents = false;
            left.BackColor = Palette.TitleBar;
            left.Margin = new Padding(0);
            left.Padding = new Padding(Dp(12), 0, 0, 0);

            Panel mark = new Panel();
            mark.Width = Dp(24);
            mark.Height = Dp(24);
            mark.Margin = new Padding(0, Dp(7), Dp(10), 0);
            mark.BackColor = Palette.TitleBar;
            mark.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Image logo = Brand.LogoSmall;
                if (logo != null)
                {
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(logo, new Rectangle(Dp(2), Dp(2), Dp(20), Dp(20)));
                }
                else
                {
                    Draw.MinecraftBlock(e.Graphics, new RectangleF(2f, 2f, Dp(20), Dp(20)));
                }
            };

            _sidebarToggle = new SidebarToggleButton();
            _sidebarToggle.Name = "_sidebarToggle";
            _sidebarToggle.Width = Dp(30);
            _sidebarToggle.Height = Dp(30);
            _sidebarToggle.Margin = new Padding(0, Dp(4), Dp(10), 0);
            _sidebarToggle.Click += delegate { ToggleSidebar(); };

            _sidebarTip = new ToolTip();
            _sidebarTip.InitialDelay = 350;
            _sidebarTip.ReshowDelay = 100;
            _sidebarTip.SetToolTip(_sidebarToggle, LanguageService.T("Esconder Barra lateral"));

            _captionTitle = new Label();
            _captionTitle.Text = "ORTHANC MC  ·  " + ServerProjects.ActiveName.ToUpperInvariant();
            _captionTitle.Font = Fonts.UI(9f, FontStyle.Bold);
            _captionTitle.ForeColor = Palette.Text;
            _captionTitle.BackColor = Palette.TitleBar;
            _captionTitle.AutoSize = true;
            _captionTitle.Margin = new Padding(0, Dp(11), 0, 0);
            _captionTitle.Cursor = Cursors.Default;

            left.Controls.Add(_sidebarToggle);
            left.Controls.Add(mark);
            left.Controls.Add(_captionTitle);

            Panel right = new BufferedPanel();
            right.Dock = DockStyle.Fill;
            right.Margin = new Padding(0);
            right.Padding = new Padding(0);
            right.BackColor = Palette.TitleBar;
            _captionRight = right;

            _serverSwitch = new ServerSwitchButton();
            _serverSwitch.Margin = new Padding(0, 0, Dp(6), 0);
            _serverSwitch.Click += delegate { ShowServerPopup(); };

            _captionMin = new CaptionButton();
            _captionMin.Kind = CaptionButtonKind.Minimize;
            _captionMin.Width = Dp(46);
            _captionMin.Height = Dp(30);
            _captionMin.Margin = new Padding(0);
            _captionMin.Click += delegate { WindowState = FormWindowState.Minimized; };

            _captionMax = new CaptionButton();
            _captionMax.Kind = CaptionButtonKind.Maximize;
            _captionMax.Width = Dp(46);
            _captionMax.Height = Dp(30);
            _captionMax.Margin = new Padding(0);
            _captionMax.Click += delegate { ToggleMaximize(); };

            _captionClose = new CaptionButton();
            _captionClose.Kind = CaptionButtonKind.Close;
            _captionClose.Width = Dp(46);
            _captionClose.Height = Dp(30);
            _captionClose.Margin = new Padding(0);
            _captionClose.Click += delegate { Close(); };

            // Fondos opacos: la barra no usa transparencia, asi el repintado
            // de la animacion no arrastra a los demas controles.
            _serverSwitch.BackColor = Palette.TitleBar;
            _captionMin.BackColor = Palette.TitleBar;
            _captionMax.BackColor = Palette.TitleBar;
            _captionClose.BackColor = Palette.TitleBar;

            right.Controls.Add(_serverSwitch);
            right.Controls.Add(_captionMin);
            right.Controls.Add(_captionMax);
            right.Controls.Add(_captionClose);

            // La barra derecha queda fija: la animacion solo mueve y ensancha
            // el boton de servidores. Antes el FlowLayoutPanel se reacomodaba
            // en cada frame y hacia vibrar todos los botones del caption.
            right.Resize += delegate { LayoutCaptionRight(); };
            _serverSwitch.SizeChanged += delegate { LayoutCaptionRight(); };
            LayoutCaptionRight();

            bar.Controls.Add(left, 0, 0);
            bar.Controls.Add(right, 1, 0);
            _caption.Controls.Add(bar);

            // Arrastrar la ventana desde cualquier zona libre de la barra.
            // El doble clic se maneja aqui (Clicks == 2) y no con el evento
            // DoubleClick para que un solo gesto no alterne dos veces.
            MouseEventHandler drag = delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                if (e.Clicks >= 2) { ToggleMaximize(); return; }
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)HTCAPTION, IntPtr.Zero);
            };
            _caption.MouseDown += drag;
            bar.MouseDown += drag;
            left.MouseDown += drag;
            mark.MouseDown += drag;
            _captionTitle.MouseDown += drag;
        }

        private void LayoutCaptionRight()
        {
            if (_captionRight == null || _serverSwitch == null || _captionMin == null ||
                _captionMax == null || _captionClose == null) return;

            int height = _captionRight.ClientSize.Height;
            int buttonTop = Math.Max(0, (height - _captionMin.Height) / 2);
            int closeX = _captionRight.ClientSize.Width - Dp(4) - _captionClose.Width;
            int maxX = closeX - _captionMax.Width;
            int minX = maxX - _captionMin.Width;

            Point closePoint = new Point(closeX, buttonTop);
            Point maxPoint = new Point(maxX, buttonTop);
            Point minPoint = new Point(minX, buttonTop);
            if (_captionClose.Location != closePoint) _captionClose.Location = closePoint;
            if (_captionMax.Location != maxPoint) _captionMax.Location = maxPoint;
            if (_captionMin.Location != minPoint) _captionMin.Location = minPoint;

            int switchTop = Math.Max(0, (height - _serverSwitch.Height) / 2);
            Point switchPoint = new Point(
                minX - Dp(6) - _serverSwitch.Width, switchTop);
            if (_serverSwitch.Location != switchPoint) _serverSwitch.Location = switchPoint;
        }

        private void UpdateWindowFrame()
        {
            Padding frame = WindowState == FormWindowState.Maximized ? new Padding(0) : new Padding(Dp(4));
            if (Padding != frame) Padding = frame;
            if (_captionMax != null)
            {
                CaptionButtonKind kind = WindowState == FormWindowState.Maximized
                    ? CaptionButtonKind.Restore : CaptionButtonKind.Maximize;
                if (_captionMax.Kind != kind) { _captionMax.Kind = kind; _captionMax.Invalidate(); }
            }
        }

        private void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
            }
            else
            {
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
            UpdateWindowFrame();
        }

        private void ShowServerPopup()
        {
            List<ServerEntry> entries = ServerProjects.List();
            bool managed = _bridge.IsRunning;
            bool running = managed || _scanner.GetServerStatus().Running;
            bool external = running && !managed;
            Point screen = _serverSwitch.PointToScreen(new Point(
                _serverSwitch.Width - Dp(360), _caption.Height + Dp(2)));
            ServerSwitchPopup popup = new ServerSwitchPopup(this, screen, entries,
                _paths.ServerDir, running, external, SwitchServer);
            popup.Show(this);
        }

        private async void SwitchServer(ServerEntry entry)
        {
            if (entry == null || ServerProjects.SamePath(entry.Path, _paths.ServerDir)) return;

            // Nunca detener el servidor actual si el destino no tiene jar.
            if (!Directory.Exists(entry.Path) ||
                !File.Exists(Path.Combine(entry.Path, "server.jar")))
            {
                MessageDialog.Info(this, "Cambiar de server",
                    "La carpeta seleccionada no contiene server.jar:\r\n\r\n" + entry.Path);
                return;
            }

            if (!await PrepareProfileSwitch(entry)) return;

            if (!ServerProjects.SetActive(entry))
            {
                MessageDialog.Info(this, "Cambiar de server", "No se pudo guardar el servidor seleccionado.");
                return;
            }

            ProfileStore.TouchOpened(entry.Path);
            ProfileStore.Save();

            // Se reapunta el HUD en el lugar en vez de reiniciar la app.
            ApplyActiveServer(entry);
            EnterHud(entry);
            _toast.ShowMessage("Servidor cambiado a " + entry.Name, Palette.Accent, "check");
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTaskbarIcon();
        }

        // La barra de tareas usa WM_SETICON. Se envian los frames exactos del
        // .ico multi-tamano para que Windows no tenga que escalar uno solo.
        private void ApplyTaskbarIcon()
        {
            if (!IsHandleCreated) return;
            try
            {
                Size big = SystemInformation.IconSize;
                Size small = SystemInformation.SmallIconSize;
                _taskbarIconBig = Brand.IconFor(big.Width, big.Height);
                _taskbarIconSmall = Brand.IconFor(small.Width, small.Height);
                if (_taskbarIconBig != null)
                    SendMessage(Handle, WM_SETICON, (IntPtr)ICON_BIG, _taskbarIconBig.Handle);
                if (_taskbarIconSmall != null)
                    SendMessage(Handle, WM_SETICON, (IntPtr)ICON_SMALL, _taskbarIconSmall.Handle);
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            // Al maximizar con Win+Arriba o el menu del sistema, el limite
            // debe ser el area util de la pantalla donde esta la ventana.
            if (m.Msg == WM_SYSCOMMAND &&
                ((int)m.WParam.ToInt64() & 0xFFF0) == SC_MAXIMIZE)
            {
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
            }

            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    int x = unchecked((short)(long)m.LParam);
                    int y = unchecked((short)((long)m.LParam >> 16));
                    Point p = PointToClient(new Point(x, y));
                    int edge = Math.Max(4, Dp(5));
                    bool left = p.X < edge;
                    bool right = p.X >= ClientSize.Width - edge;
                    bool top = p.Y < edge;
                    bool bottom = p.Y >= ClientSize.Height - edge;
                    int hit = 0;
                    if (top && left) hit = HTTOPLEFT;
                    else if (top && right) hit = HTTOPRIGHT;
                    else if (bottom && left) hit = HTBOTTOMLEFT;
                    else if (bottom && right) hit = HTBOTTOMRIGHT;
                    else if (left) hit = HTLEFT;
                    else if (right) hit = HTRIGHT;
                    else if (top) hit = HTTOP;
                    else if (bottom) hit = HTBOTTOM;
                    if (hit != 0) m.Result = (IntPtr)hit;
                }
                return;
            }
            base.WndProc(ref m);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // El toast es una capa flotante: se ancla abajo a la derecha y se
        // recalcula en cada resize para que nunca quede fuera de la ventana.
        private void PositionToast()
        {
            if (_toast == null) return;
            int x = ClientSize.Width - _toast.Width - Dp(24);
            int y = ClientSize.Height - _toast.Height - Dp(24);
            if (x < Dp(10)) x = Dp(10);
            if (y < Dp(10)) y = Dp(10);
            _toast.Location = new Point(x, y);
        }

        // La capa de carga es hija del formulario para poder cubrir tambien
        // la barra lateral y el encabezado, pero empieza debajo del caption.
        private void PositionLoadingOverlay()
        {
            if (_loadingOverlay == null || _body == null) return;
            try
            {
                Point origin = _body.PointToScreen(Point.Empty);
                origin = PointToClient(origin);
                _loadingOverlay.Bounds = new Rectangle(origin, _body.Size);
            }
            catch { }
        }

        // ------------------------------------------------------------------
        // Pantalla de Perfiles
        // ------------------------------------------------------------------
        private void BeginProfilesStartup()
        {
            if (IsDisposed || Disposing || _profilesView == null) return;
            if (_loadingOverlay != null && !_loadingOverlay.Visible)
            {
                PositionLoadingOverlay();
                _loadingOverlay.Begin(_body);
                _loadingOverlay.Update();
            }
            _profilesView.Reload();
        }

        private void MarkProfilesReady()
        {
            if (_loadingOverlay != null && _loadingOverlay.Visible)
                _loadingOverlay.MarkContentReady();
        }

        // Vuelve a la pantalla inicial sin detener el servidor. El tiempo de
        // uso del perfil se corta al salir del HUD.
        private void ShowProfiles()
        {
            ShowProfilesInternal(true);
        }

        private void ShowProfilesInternal(bool recordHistory)
        {
            if (_profilesView == null || _inProfiles) return;
            if (recordHistory && _currentSection != -1) PushNavHistory(_currentSection);
            CommitProfileHours();
            _inProfiles = true;
            _currentSection = -1;
            _header.Visible = false;
            _content.Visible = false;
            _profilesView.Visible = true;
            _profilesView.BringToFront();
            _serverSwitch.Visible = false;
            if (_configScroll != null) _configScroll.SetActive(false);
            if (_propsScroll != null) _propsScroll.SetActive(false);
            if (_addonScroll != null) _addonScroll.SetActive(false);
            if (_captionTitle != null) _captionTitle.Text = "ORTHANC MC  ·  PERFILES";
            if (_navProfiles != null) _navProfiles.Active = true;
            LayoutCaptionRight();
            _profilesView.Reload();
        }

        private void EnterHud(ServerEntry entry)
        {
            if (_currentSection != 0) PushNavHistory(_currentSection);
            LeaveProfiles(entry);
            _currentSection = 0;
            SwitchView(0);
            if (_navProfiles != null) _navProfiles.Active = false;
        }

        // Restaura la columna del HUD conservando la barra lateral y el
        // estado del boton de esconder/atraer.
        private void LeaveProfiles(ServerEntry entry)
        {
            _inProfiles = false;
            _hoursProfilePath = entry != null ? entry.Path : ServerProjects.ActivePath;
            _lastHoursSave = DateTime.UtcNow;
            CommitProfileHours(false);

            if (_profilesView != null) _profilesView.Visible = false;
            _header.Visible = true;
            _content.Visible = true;
            _serverSwitch.Visible = true;
            if (_captionTitle != null)
                _captionTitle.Text = "ORTHANC MC  ·  " + ServerProjects.ActiveName.ToUpperInvariant();
            if (_navProfiles != null) _navProfiles.Active = false;
            LayoutCaptionRight();
        }

        // Navegacion desde la barra lateral: si estamos en Perfiles primero
        // se restaura el HUD y luego se abre la seccion pedida.
        private void NavigateTo(int idx)
        {
            if (idx == 2 && _addonKind == AddonKind.None) idx = 0;
            // Los respaldos en la nube estan deshabilitados por ahora.
            if (idx == 9) idx = 7;
            if (_inProfiles) LeaveProfiles(null);
            if (_currentSection != idx)
            {
                PushNavHistory(_currentSection);
                _currentSection = idx;
            }
            SwitchView(idx);
        }

        private void PushNavHistory(int section)
        {
            if (section == int.MinValue) return;
            if (_navHistory.Count == 0 || _navHistory[_navHistory.Count - 1] != section)
                _navHistory.Add(section);
            if (_navHistory.Count > 60) _navHistory.RemoveAt(0);
        }

        private List<int> AvailableSections()
        {
            List<int> order = new List<int>();
            order.Add(-1); // Perfiles
            order.Add(0);
            order.Add(1);
            if (_addonKind != AddonKind.None) order.Add(2);
            order.Add(3);
            order.Add(4);
            order.Add(5);
            order.Add(6);
            order.Add(12);
            order.Add(7);
            order.Add(8);
            order.Add(10);
            order.Add(11);
            return order;
        }

        private void SectionNext()
        {
            List<int> order = AvailableSections();
            int current = _inProfiles ? -1 : _currentSection;
            int index = order.IndexOf(current);
            if (index < 0) index = -1;
            if (index + 1 < order.Count) NavigateTo(order[index + 1]);
        }

        private void SectionBack()
        {
            if (_navHistory.Count > 0)
            {
                int target = _navHistory[_navHistory.Count - 1];
                _navHistory.RemoveAt(_navHistory.Count - 1);
                if (target == -1)
                {
                    ShowProfilesInternal(false);
                }
                else
                {
                    if (_inProfiles) LeaveProfiles(null);
                    _currentSection = target;
                    SwitchView(target);
                }
                return;
            }

            List<int> order = AvailableSections();
            int current = _inProfiles ? -1 : _currentSection;
            int index = order.IndexOf(current);
            if (index < 0) index = order.Count;
            if (index > 0) NavigateTo(order[index - 1]);
        }

        // Paginas arriba/abajo navegan entre secciones. Los cuadros de texto y
        // listas conservan la tecla para desplazar su propio contenido.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.PageDown || keyData == Keys.PageUp)
            {
                if (FocusedControlAcceptsPaging()) return base.ProcessCmdKey(ref msg, keyData);
                if (keyData == Keys.PageDown) SectionNext();
                else SectionBack();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool FocusedControlAcceptsPaging()
        {
            Control focused = FindFocused(ActiveForm ?? this);
            return focused is TextBoxBase || focused is ComboBox || focused is ListBox ||
                focused is ListView || focused is TreeView || focused is UpDownBase ||
                focused is ScrollBar || focused is TrackBar || focused is SliderBar;
        }

        private static Control? FindFocused(Control? root)
        {
            if (root == null) return null;
            foreach (Control child in root.Controls)
            {
                if (child.Focused) return child;
                Control deep = FindFocused(child);
                if (deep != null) return deep;
            }
            return root.Focused ? root : null;
        }

        // ------------------------------------------------------------------
        // Barra lateral animada
        // ------------------------------------------------------------------
        private void ToggleSidebar()
        {
            if (_rootLayout == null || _sidebarAnim == null) return;
            _sidebarHidden = !_sidebarHidden;
            _sidebarFrom = (int)Math.Round(_rootLayout.ColumnStyles[0].Width);
            _sidebarTo = _sidebarHidden ? 0 : _sidebarWidth;
            _sidebarAnimStart = DateTime.UtcNow;
            _sidebarAnim.Start();

            if (_sidebarToggle != null) _sidebarToggle.Collapsed = _sidebarHidden;
            if (_sidebarTip != null)
                _sidebarTip.SetToolTip(_sidebarToggle,
                    LanguageService.T(_sidebarHidden ? "Atraer Barra lateral" : "Esconder Barra lateral"));
            // Al esconder la barra, el titulo del panel deja libre el
            // espacio de la barra de titulo y el contenido gana ancho.
            if (_captionTitle != null) _captionTitle.Visible = !_sidebarHidden;
        }

        private void SidebarAnim_Tick(object sender, EventArgs e)
        {
            if (_rootLayout == null || _sidebarAnim == null) return;

            double t = (DateTime.UtcNow - _sidebarAnimStart).TotalMilliseconds / SidebarAnimMs;
            bool done = t >= 1.0;
            if (done) t = 1.0;
            // Curva suave de entrada y salida.
            double eased = t < 0.5
                ? 4.0 * t * t * t
                : 1.0 - Math.Pow(-2.0 * t + 2.0, 3.0) / 2.0;
            int width = (int)Math.Round(_sidebarFrom + (_sidebarTo - _sidebarFrom) * eased);
            if (width < 0) width = 0;

            if (_sidebar != null)
            {
                bool visible = width > 0;
                if (_sidebar.Visible != visible) _sidebar.Visible = visible;
            }
            _rootLayout.ColumnStyles[0].Width = width;

            if (done)
            {
                _sidebarAnim.Stop();
                _rootLayout.ColumnStyles[0].Width = _sidebarTo;
                if (_sidebar != null) _sidebar.Visible = _sidebarTo > 0;
            }
        }

        private async void OpenProfileRequest(ServerEntry entry)
        {
            if (entry == null || IsDisposed || Disposing) return;
            try
            {
                if (!Directory.Exists(entry.Path) ||
                    !File.Exists(Path.Combine(entry.Path, "server.jar")))
                {
                    MessageDialog.Info(this, "Perfil",
                        "La carpeta seleccionada no contiene server.jar:\r\n\r\n" + entry.Path);
                    return;
                }

                EnsureScreenshot(entry);

                if (!ServerProjects.SamePath(entry.Path, _paths.ServerDir))
                {
                    if (!await PrepareProfileSwitch(entry)) return;
                    if (!ServerProjects.SetActive(entry))
                    {
                        MessageDialog.Info(this, "Cambiar de server",
                            "No se pudo guardar el servidor seleccionado.");
                        return;
                    }
                    ApplyActiveServer(entry);
                }

                ProfileStore.TouchOpened(entry.Path);
                ProfileStore.Save();
                EnterHud(entry);
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Perfil", "No se pudo abrir el perfil:\r\n\r\n" + ex.Message);
            }
        }

        // Detiene el servidor administrado y Playit antes de cambiar de
        // carpeta. Devuelve false si el usuario cancela o si el server actual
        // se inicio fuera del panel.
        private async Task<bool> PrepareProfileSwitch(ServerEntry entry)
        {
            bool managed = _bridge.IsRunning;
            bool running = managed || _scanner.GetServerStatus().Running;
            if (!running) return true;

            if (!managed)
            {
                MessageDialog.Info(this, "Cambiar de server",
                    "El servidor actual fue iniciado fuera del panel. Detenlo manualmente antes de cambiar de server.");
                return false;
            }

            if (!MessageDialog.Confirm(this, "Cambiar de server",
                "El servidor actual se detendra con stop y tambien se detendra Playit.\r\n\r\n" +
                "¿Cambiar a " + entry.Name + "?", "Cambiar", true))
                return false;

            _toast.ShowMessage("Deteniendo servidor...", Palette.Amber, "stop");
            bool stopped = await Task.Run(delegate { return _bridge.StopGracefully(60000); });
            if (!stopped)
            {
                MessageDialog.Info(this, "Cambiar de server",
                    "El servidor no respondio al comando stop y se cerro forzosamente. " +
                    "Revisa el mundo antes de seguir jugando.");
            }
            await Task.Run(delegate { try { _playit.Stop(); } catch { } });
            return true;
        }

        // Reapunta todas las rutas y los textos del HUD al servidor elegido,
        // sin reiniciar la aplicacion.
        private void ApplyActiveServer(ServerEntry entry)
        {
            _paths.SetBaseDir(entry.Path);
            _propsFile = new PropertiesFile(_paths.Properties);
            Text = Brand.Name + " - " + (string.IsNullOrWhiteSpace(entry.Name) ? "Servidor" : entry.Name);
            if (_logoSub != null) _logoSub.Text = ActiveServerFolderName() + " Server Control";
            if (_logoMark != null) _logoMark.Invalidate();
            if (_captionTitle != null)
                _captionTitle.Text = "ORTHANC MC  ·  " + ServerProjects.ActiveName.ToUpperInvariant();
            if (_versionLabel != null) _versionLabel.Text = "MC -- - Java --";

            _addonKind = DetectAddonKind();
            if (_navAddons != null)
            {
                _navAddons.Visible = _addonKind != AddonKind.None;
                _navAddons.Text = _addonKind == AddonKind.Mods ? "Mods" : "Plugins";
                _navAddons.Glyph = _addonKind == AddonKind.Mods ? "chip" : "plug";
            }

            try { if (_consoleOut != null) _consoleOut.Clear(); } catch { }
            _consoleHistory.Clear();
            try { _playerCache.Clear(); _notified.Clear(); } catch { }
            if (_plyList != null) _plyList.Items.Clear();
            _lastPlayitDomain = AppConfig.Instance.LastPlayitDomain ?? "";

            BuildProps();
            RefreshAddons();
            RefreshJarCurrent();
            RefreshBackupHistory();
            UpdateBackupStatus(true);
        }

        // Flujo de screenshot pedido en la referencia. Se guarda la decision
        // para no volver a preguntar si el usuario dijo "No" a futuras veces.
        private void EnsureScreenshot(ServerEntry entry)
        {
            ProfileMeta meta = ProfileStore.Get(entry.Path);
            try
            {
                if (!string.IsNullOrWhiteSpace(meta.ScreenshotPath) && File.Exists(meta.ScreenshotPath))
                    return;
            }
            catch { return; }

            if (!meta.AskScreenshot) return;

            int first = MessageDialog.Choice(this, "Screenshot del server",
                "¿Quieres cargar una screenshot de tu server?", "Sí", "No", null, false);

            if (first == 0)
            {
                int second = MessageDialog.Choice(this, "Screenshot del server",
                    "¿Ya tienes el screenshot?", "Sí", "No", null, false);
                if (second == 0)
                {
                    PickScreenshot(entry);
                }
                else
                {
                    MessageDialog.Info(this, "Screenshot del server",
                        "Entra al servidor y toma la captura con F2.\r\n\r\n" +
                        "La podrás cargar la próxima vez que abras el panel.");
                }
                meta.AskScreenshot = true;
                ProfileStore.Save();
            }
            else
            {
                int again = MessageDialog.Choice(this, "Screenshot del server",
                    "¿Quieres que vuelva a preguntar?", "Sí", "No", null, false);
                meta.AskScreenshot = again != 1;
                ProfileStore.Save();
            }
        }

        private void PickScreenshot(ServerEntry entry)
        {
            string screenshots = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft", "screenshots");
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Elegir screenshot del servidor";
                dialog.Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp|Todos los archivos|*.*";
                if (Directory.Exists(screenshots)) dialog.InitialDirectory = screenshots;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    string saved = ProfileStore.ImportScreenshot(entry.Path, dialog.FileName);
                    ProfileMeta meta = ProfileStore.Get(entry.Path);
                    meta.ScreenshotPath = saved;
                    meta.AskScreenshot = true;
                    ProfileStore.Save();
                    // El dialogo se abre desde el doble clic de una tarjeta.
                    // Recargar aqui destruiria esa tarjeta dentro de su propio
                    // evento; se deja para el siguiente ciclo del mensaje.
                    if (_profilesView != null && !IsDisposed)
                    {
                        BeginInvoke(new Action(delegate
                        {
                            if (!IsDisposed && _profilesView != null) _profilesView.Reload();
                        }));
                    }
                }
            }
        }

        private void TrackProfileHours()
        {
            if (_inProfiles || string.IsNullOrWhiteSpace(_hoursProfilePath)) return;
            if (_rootLayout == null || !_rootLayout.Visible) return;
            ProfileStore.AddSeconds(_hoursProfilePath, 1);
            if ((DateTime.UtcNow - _lastHoursSave).TotalSeconds >= 60)
            {
                _lastHoursSave = DateTime.UtcNow;
                ProfileStore.Save();
            }
        }

        private void CommitProfileHours()
        {
            CommitProfileHours(true);
        }

        private void CommitProfileHours(bool clearPath)
        {
            if (!string.IsNullOrWhiteSpace(_hoursProfilePath)) ProfileStore.Save();
            if (clearPath) _hoursProfilePath = "";
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.AutoSize = true;
            label.BackColor = Color.Transparent;
            label.Margin = new Padding(0, 0, 0, 2);
            return label;
        }

        private static TableLayoutPanel MakeCardStack(int columns, int rows)
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Margin = new Padding(0);
            table.Padding = new Padding(0);
            table.BackColor = Palette.Card;
            table.ColumnCount = columns;
            table.RowCount = rows;
            for (int i = 0; i < columns; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < rows; i++) table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return table;
        }

        private void BuildSidebar()
        {
            _sidebar = new Panel();
            _sidebar.Name = "_sidebar";
            _sidebar.Dock = DockStyle.Fill;
            _sidebar.Margin = new Padding(0);
            _sidebar.BackColor = Palette.Sidebar;

            Panel logo = new Panel();
            logo.Dock = DockStyle.Bottom;
            logo.Height = Dp(78);
            logo.BackColor = Palette.Sidebar;

            Panel logoMark = new Panel();
            logoMark.BackColor = Color.Transparent;
            logoMark.SetBounds(Dp(22), Dp(12), Dp(40), Dp(40));
            logoMark.Paint += delegate(object s, PaintEventArgs e) { DrawSidebarMark(e.Graphics); };
            _logoMark = logoMark;

            _logoTitle = new Label();
            _logoTitle.Text = Brand.NameUpper;
            _logoTitle.Font = Fonts.UI(11f, FontStyle.Bold);
            _logoTitle.ForeColor = Palette.Text;
            _logoTitle.AutoSize = true;
            _logoTitle.BackColor = Color.Transparent;
            _logoTitle.Location = new Point(Dp(70), Dp(14));

            _logoSub = new Label();
            _logoSub.Text = ActiveServerFolderName() + " Server Control";
            _logoSub.Font = Fonts.UI(8.5f);
            _logoSub.ForeColor = Palette.Muted;
            _logoSub.AutoSize = false;
            _logoSub.AutoEllipsis = true;
            _logoSub.BackColor = Color.Transparent;
            _logoSub.SetBounds(Dp(70), Dp(36), Dp(204), Dp(18));

            logo.Controls.Add(logoMark);
            logo.Controls.Add(_logoTitle);
            logo.Controls.Add(_logoSub);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = Dp(38);
            bottom.BackColor = Palette.Sidebar;
            _versionLabel = new Label();
            _versionLabel.Text = "MC -- - Java --";
            _versionLabel.Font = Fonts.UI(8.5f);
            _versionLabel.ForeColor = Palette.Dim;
            _versionLabel.AutoSize = true;
            _versionLabel.BackColor = Color.Transparent;
            _versionLabel.Location = new Point(Dp(22), Dp(10));
            bottom.Controls.Add(_versionLabel);

            Panel navWrap = new Panel();
            navWrap.Dock = DockStyle.Top;
            navWrap.Height = Dp(364);
            navWrap.Padding = new Padding(Dp(10), Dp(14), Dp(10), Dp(8));
            navWrap.BackColor = Palette.Sidebar;

            _navProfiles = new NavButton();
            _navProfiles.Text = "Perfiles";
            _navProfiles.Glyph = "grid";
            _navProfiles.Dock = DockStyle.Top;
            _navProfiles.TabStop = true;
            _navProfiles.Click += delegate { ShowProfiles(); };

            _navStatus = new NavButton();
            _navStatus.Text = "Estado";
            _navStatus.Glyph = "pulse";
            _navStatus.Dock = DockStyle.Top;
            _navStatus.TabStop = true;
            _navStatus.Click += delegate { NavigateTo(0); };

            _navConsole = new NavButton();
            _navConsole.Text = "Consola";
            _navConsole.Glyph = "terminal";
            _navConsole.Dock = DockStyle.Top;
            _navConsole.TabStop = true;
            _navConsole.Click += delegate { NavigateTo(1); };

            _navAddons = new NavButton();
            _navAddons.Text = _addonKind == AddonKind.Mods ? "Mods" : "Plugins";
            _navAddons.Glyph = _addonKind == AddonKind.Mods ? "chip" : "plug";
            _navAddons.Dock = DockStyle.Top;
            _navAddons.TabStop = true;
            _navAddons.Visible = _addonKind != AddonKind.None;
            _navAddons.Click += delegate { NavigateTo(2); };

            _navConfig = new NavButton();
            _navConfig.Text = "Configuracion del Servidor";
            _navConfig.Glyph = "sliders";
            _navConfig.Dock = DockStyle.Top;
            _navConfig.TabStop = true;
            _navConfig.Click += delegate { NavigateTo(3); };

            _navApp = new NavButton();
            _navApp.Text = "Configuracion App";
            _navApp.Glyph = "folder";
            _navApp.Dock = DockStyle.Top;
            _navApp.TabStop = true;
            _navApp.Click += delegate { NavigateTo(7); };

            // Docking: el ultimo agregado queda arriba, por eso se agregan al reves.
            navWrap.Controls.Add(_navApp);
            navWrap.Controls.Add(_navConfig);
            navWrap.Controls.Add(_navAddons);
            navWrap.Controls.Add(_navConsole);
            navWrap.Controls.Add(_navStatus);
            navWrap.Controls.Add(_navProfiles);

            _sidebar.Controls.Add(logo);
            _sidebar.Controls.Add(navWrap);
            _sidebar.Controls.Add(bottom);
        }

        // El logo del sidebar muestra el server-icon del proyecto activo.
        // Si el servidor no tiene icono se conserva el bloque de Minecraft.
        private void DrawSidebarMark(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle target = new Rectangle(Dp(4), Dp(4), Dp(32), Dp(32));
            Image logo = Brand.LogoSmall;
            if (logo != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, target);
                return;
            }
            Draw.MinecraftBlock(g, new RectangleF(4, 4, Dp(32), Dp(32)));
        }

        private string ActiveServerFolderName()
        {
            try
            {
                string dir = _paths.ServerDir.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string name = Path.GetFileName(dir);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            catch { }
            return "Server";
        }

        private void BuildHeader()
        {
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.Padding = new Padding(0);
            grid.BackColor = Color.Transparent;
            grid.ColumnCount = 2;
            grid.RowCount = 1;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            FlowLayoutPanel left = new FlowLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.FlowDirection = FlowDirection.TopDown;
            left.WrapContents = false;
            left.AutoSize = false;
            left.BackColor = Color.Transparent;
            left.Margin = new Padding(0);

            _pageTitle = MakeLabel("Estado del Servidor", Fonts.UI(16f, FontStyle.Bold), Palette.Text);
            _pageSub = MakeLabel("Monitoreo en tiempo real", Fonts.UI(9.5f), Palette.Muted);
            _pageSub.Margin = new Padding(0, 2, 0, 0);

            FlowLayoutPanel badges = new FlowLayoutPanel();
            badges.FlowDirection = FlowDirection.LeftToRight;
            badges.WrapContents = false;
            badges.AutoSize = true;
            badges.BackColor = Color.Transparent;
            badges.Margin = new Padding(0, 10, 0, 0);

            _serverBadge = new StatusBadge();
            _serverBadge.Set("DETENIDO", Palette.Muted);
            _serverBadge.Margin = new Padding(0, 0, 8, 0);

            _playitBadge = new StatusBadge();
            _playitBadge.Set("PLAYIT: --", Palette.Muted);
            _playitBadge.Margin = new Padding(0);

            badges.Controls.Add(_serverBadge);
            badges.Controls.Add(_playitBadge);

            left.Controls.Add(_pageTitle);
            left.Controls.Add(_pageSub);
            left.Controls.Add(badges);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            actions.AutoSize = true;
            actions.Anchor = AnchorStyles.None;
            actions.BackColor = Color.Transparent;
            actions.Margin = new Padding(Dp(20), 0, 0, 0);

            _btnStart = new RoundButton();
            _btnStart.Text = "INICIAR";
            _btnStart.Glyph = "play";
            _btnStart.Width = Dp(148);
            _btnStart.Margin = new Padding(0, 0, Dp(10), 0);

            _btnRestart = new RoundButton();
            _btnRestart.Text = "REINICIAR";
            _btnRestart.Glyph = "restart";
            _btnRestart.Width = Dp(148);
            _btnRestart.FillColor = Palette.Card;
            _btnRestart.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.08f);
            _btnRestart.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.12f);
            _btnRestart.TextColor = Palette.Text;
            _btnRestart.BorderColor = Palette.Border;
            _btnRestart.Margin = new Padding(0);

            actions.Controls.Add(_btnStart);
            actions.Controls.Add(_btnRestart);

            grid.Controls.Add(left, 0, 0);
            grid.Controls.Add(actions, 1, 0);
            _header.Controls.Add(grid);
        }

        private Panel BuildStatus()
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.Padding = new Padding(0);
            grid.BackColor = Color.Transparent;
            grid.ColumnCount = 2;
            grid.RowCount = 2;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(272)));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ---------------- Servidor ----------------
            _cardServer = new Card();
            _cardServer.Dock = DockStyle.Fill;
            _cardServer.Margin = new Padding(0, 0, Dp(7), Dp(7));

            TableLayoutPanel srv = MakeCardStack(2, 10);
            srv.ColumnStyles.Clear();
            srv.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            srv.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            srv.RowStyles[6] = new RowStyle(SizeType.Absolute, Dp(26));
            srv.RowStyles[8] = new RowStyle(SizeType.Absolute, Dp(26));

            _srvTitle = MakeLabel("SERVIDOR", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            _srvState = MakeLabel("Estado: --", Fonts.UI(9.5f), Palette.Muted);
            _lastPlayitDomain = AppConfig.Instance.LastPlayitDomain ?? "";
            _srvIpDomain = MakeLabel("IP/DOMINIO: " + (string.IsNullOrWhiteSpace(_lastPlayitDomain) ? "--" : _lastPlayitDomain), Fonts.UI(9.5f), Palette.Muted);
            _srvIpDomain.AutoSize = false;
            _srvIpDomain.Dock = DockStyle.Fill;
            _srvIpDomain.AutoEllipsis = true;
            _srvIpDomain.TextAlign = ContentAlignment.MiddleLeft;
            _srvIpDomain.Margin = new Padding(0, Dp(4), 0, 0);

            _srvIpLocal = MakeLabel("IP LOCAL: --", Fonts.UI(9.5f), Palette.Muted);
            _srvPid = MakeLabel("PID: --", Fonts.UI(9.5f), Palette.Muted);
            _srvRamVal = MakeLabel("--", Fonts.UI(9.5f, FontStyle.Bold), Palette.Text);
            _sysRamVal = MakeLabel("--", Fonts.UI(9.5f, FontStyle.Bold), Palette.Text);
            _sysFree = MakeLabel("Disponible: --", Fonts.UI(9f), Palette.Dim);
            Label srvRamTitle = MakeLabel("RAM DEL SERVIDOR (uso)", Fonts.UI(9f), Palette.Muted);
            Label sysRamTitle = MakeLabel("RAM DEL SISTEMA (total / uso)", Fonts.UI(9f), Palette.Muted);
            _sysRamTitle = sysRamTitle;

            _copyDomain = new RoundButton();
            _copyDomain.Glyph = "copy";
            _copyDomain.GlyphStroke = 1.7f;
            _copyDomain.Width = Dp(34);
            _copyDomain.Height = Dp(26);
            _copyDomain.Margin = new Padding(Dp(6), 0, 0, 0);
            _copyDomain.Radius = 7;
            _copyDomain.FillColor = Palette.CardAlt;
            _copyDomain.HoverFill = Palette.Mix(Palette.CardAlt, Color.White, 0.08f);
            _copyDomain.PressFill = Palette.Mix(Palette.CardAlt, Color.Black, 0.08f);
            _copyDomain.TextColor = Palette.Muted;
            _copyDomain.BorderColor = Palette.Border;

            _copyDomain.Anchor = AnchorStyles.Left;
            _copyDomain.Enabled = !string.IsNullOrWhiteSpace(_lastPlayitDomain);
            _copyDomain.Click += delegate { CopyPlayitDomain(); };

            _srvRamBar = new UsageBar();
            _srvRamBar.Dock = DockStyle.Fill;
            _srvRamBar.Margin = new Padding(0, Dp(8), 0, Dp(10));

            _sysRamBar = new UsageBar();
            _sysRamBar.Dock = DockStyle.Fill;
            _sysRamBar.Margin = new Padding(0, Dp(8), 0, Dp(10));

            srv.Controls.Add(_srvTitle, 0, 0);
            srv.SetColumnSpan(_srvTitle, 2);
            srv.Controls.Add(_srvIpDomain, 0, 1);
            srv.Controls.Add(_copyDomain, 1, 1);
            srv.Controls.Add(_srvIpLocal, 0, 2);
            srv.SetColumnSpan(_srvIpLocal, 2);
            srv.Controls.Add(_srvState, 0, 3);
            srv.SetColumnSpan(_srvState, 2);
            srv.Controls.Add(_srvPid, 0, 4);
            srv.SetColumnSpan(_srvPid, 2);
            srv.Controls.Add(srvRamTitle, 0, 5);
            srv.Controls.Add(_srvRamVal, 1, 5);
            srv.Controls.Add(_srvRamBar, 0, 6);
            srv.SetColumnSpan(_srvRamBar, 2);
            srv.Controls.Add(sysRamTitle, 0, 7);
            srv.Controls.Add(_sysRamVal, 1, 7);
            srv.Controls.Add(_sysRamBar, 0, 8);
            srv.SetColumnSpan(_sysRamBar, 2);
            srv.Controls.Add(_sysFree, 0, 9);
            srv.SetColumnSpan(_sysFree, 2);

            _cardServer.Controls.Add(srv);

            // ---------------- Jugadores ----------------
            _cardPlayers = new Card();
            _cardPlayers.Dock = DockStyle.Fill;
            _cardPlayers.Margin = new Padding(Dp(7), 0, 0, Dp(7));

            TableLayoutPanel ply = MakeCardStack(2, 2);
            ply.ColumnStyles.Clear();
            ply.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            ply.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ply.RowStyles[1] = new RowStyle(SizeType.Percent, 100f);

            _plyTitle = MakeLabel("JUGADORES CONECTADOS", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            _plyCount = MakeLabel("(0)", Fonts.UI(10f, FontStyle.Bold), Palette.Accent);

            _plyList = new ListBox();
            _plyList.BorderStyle = BorderStyle.None;
            _plyList.BackColor = Palette.Card;
            _plyList.ForeColor = Palette.Text;
            _plyList.Font = Fonts.UI(9.5f);
            _plyList.IntegralHeight = false;
            _plyList.DrawMode = DrawMode.OwnerDrawFixed;
            _plyList.ItemHeight = Dp(32);
            _plyList.Dock = DockStyle.Fill;
            _plyList.Margin = new Padding(0, Dp(8), 0, 0);
            _plyList.DrawItem += PlyList_DrawItemEx;
            _plyList.MouseUp += PlyList_MouseUpEx;

            ply.Controls.Add(_plyTitle, 0, 0);
            ply.Controls.Add(_plyCount, 1, 0);
            ply.Controls.Add(_plyList, 0, 1);
            ply.SetColumnSpan(_plyList, 2);
            _cardPlayers.Controls.Add(ply);

            // ---------------- Playit ----------------
            _cardPlayit = new Card();
            _cardPlayit.Dock = DockStyle.Fill;
            _cardPlayit.Margin = new Padding(0, Dp(7), 0, 0);

            TableLayoutPanel pit = MakeCardStack(1, 6);
            pit.RowStyles[5] = new RowStyle(SizeType.Percent, 100f);

            _pitTitle = MakeLabel("PLAYIT.GG", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            _pitStatus = MakeLabel("Servicio: --", Fonts.UI(9.5f), Palette.Muted);
            _pitTunnel = MakeLabel("Tunel: --", Fonts.UI(9.5f), Palette.Muted);
            _pitClients = MakeLabel("Conexiones activas detectadas: 0", Fonts.UI(9.5f), Palette.Muted);
            Label pitLogTitle = MakeLabel("Log reciente", Fonts.UI(9f), Palette.Dim);
            pitLogTitle.Margin = new Padding(0, 6, 0, 0);

            _pitLog = new TextBox();
            _pitLog.Multiline = true;
            _pitLog.ReadOnly = true;
            _pitLog.BorderStyle = BorderStyle.None;
            _pitLog.BackColor = Palette.Input;
            _pitLog.ForeColor = Palette.Text;
            _pitLog.Font = Fonts.Mono(8.5f);
            _pitLog.ScrollBars = ScrollBars.Vertical;
            _pitLog.WordWrap = true;
            _pitLog.Dock = DockStyle.Fill;
            _pitLog.Margin = new Padding(0, Dp(6), 0, 0);

            pit.Controls.Add(_pitTitle, 0, 0);
            pit.Controls.Add(_pitStatus, 0, 1);
            pit.Controls.Add(_pitTunnel, 0, 2);
            pit.Controls.Add(_pitClients, 0, 3);
            pit.Controls.Add(pitLogTitle, 0, 4);
            pit.Controls.Add(_pitLog, 0, 5);
            _cardPlayit.Controls.Add(pit);

            grid.Controls.Add(_cardServer, 0, 0);
            grid.Controls.Add(_cardPlayers, 1, 0);
            grid.Controls.Add(_cardPlayit, 0, 1);
            grid.SetColumnSpan(_cardPlayit, 2);
            p.Controls.Add(grid);
            return p;
        }

        private Panel BuildConsole()
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;

            Card card = new Card();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0);

            TableLayoutPanel stack = MakeCardStack(1, 5);
            stack.RowStyles[2] = new RowStyle(SizeType.Percent, 100f);
            stack.RowStyles[3] = new RowStyle(SizeType.Absolute, Dp(48));
            stack.RowStyles[4] = new RowStyle(SizeType.AutoSize);

            Label t = MakeLabel("CONSOLA DEL SERVIDOR", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label s = MakeLabel("Envía comandos directamente al servidor. Usa TAB para historial.", Fonts.UI(9f), Palette.Muted);

            _consoleOut = new TextBox();
            _consoleOut.Multiline = true;
            _consoleOut.ReadOnly = true;
            _consoleOut.BorderStyle = BorderStyle.None;
            _consoleOut.BackColor = Palette.Input;
            _consoleOut.ForeColor = Palette.Text;
            _consoleOut.Font = Fonts.Mono(9f);
            _consoleOut.ScrollBars = ScrollBars.Vertical;
            _consoleOut.WordWrap = true;
            _consoleOut.Dock = DockStyle.Fill;
            _consoleOut.Margin = new Padding(0, Dp(10), 0, Dp(10));

            TableLayoutPanel inputRow = new TableLayoutPanel();
            inputRow.Dock = DockStyle.Fill;
            inputRow.Margin = new Padding(0);
            inputRow.Padding = new Padding(0);
            inputRow.BackColor = Palette.Card;
            inputRow.ColumnCount = 2;
            inputRow.RowCount = 1;
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(132)));
            inputRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _consoleIn = new InputBox();
            _consoleIn.Dock = DockStyle.Fill;
            _consoleIn.Margin = new Padding(0, Dp(3), Dp(10), Dp(3));
            _consoleIn.Padding = new Padding(Dp(14), 0, Dp(14), 0);
            _consoleIn.Placeholder = "Escribe un comando (ej: say, list, op, gamemode...)";
            _consoleIn.KeyDownInternal += ConsoleIn_KeyDown;

            _consoleSend = new RoundButton();
            _consoleSend.Text = "ENVIAR";
            _consoleSend.Glyph = "send";
            _consoleSend.Dock = DockStyle.Fill;
            _consoleSend.Margin = new Padding(0, Dp(3), 0, Dp(3));
            _consoleSend.Click += delegate { SendConsole(); };

            inputRow.Controls.Add(_consoleIn, 0, 0);
            inputRow.Controls.Add(_consoleSend, 1, 0);

            _consoleHint = MakeLabel("Los comandos van directamente al servidor..", Fonts.UI(8.5f), Palette.Dim);
            _consoleHint.Margin = new Padding(0, Dp(6), 0, 0);

            stack.Controls.Add(t, 0, 0);
            stack.Controls.Add(s, 0, 1);
            stack.Controls.Add(_consoleOut, 0, 2);
            stack.Controls.Add(inputRow, 0, 3);
            stack.Controls.Add(_consoleHint, 0, 4);
            card.Controls.Add(stack);
            p.Controls.Add(card);
            return p;
        }

        private Panel BuildConfig()
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
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ---------------- RAM + logo ----------------
            // La columna izquierda se desplaza si la ventana es baja: asi el
            // contenido nunca queda cortado por el borde de la tarjeta.
            Panel leftHost = new Panel();
            leftHost.Dock = DockStyle.Fill;
            leftHost.Margin = new Padding(0, 0, Dp(7), 0);
            leftHost.Padding = new Padding(0);
            leftHost.AutoScroll = true;
            leftHost.BackColor = Color.Transparent;
            _configScrollHost = leftHost;

            _cardRam = new Card();
            _cardRam.Dock = DockStyle.Top;
            _cardRam.Margin = new Padding(0);
            _cardRam.Height = Dp(470);

            TableLayoutPanel ram = MakeCardStack(1, 8);
            ram.RowStyles[3] = new RowStyle(SizeType.Absolute, Dp(24));
            ram.RowStyles[4] = new RowStyle(SizeType.Absolute, Dp(42));
            ram.RowStyles[5] = new RowStyle(SizeType.Absolute, Dp(50));
            ram.RowStyles[7] = new RowStyle(SizeType.Absolute, Dp(196));

            _ramTitle = MakeLabel("ASIGNACIÓN DE RAM", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label ramDesc = MakeLabel("Cambia la RAM asignada al servidor.jar", Fonts.UI(9f), Palette.Muted);

            _ramVal = new Label();
            _ramVal.Text = "4096 MB";
            _ramVal.Font = Fonts.UI(22f, FontStyle.Bold);
            _ramVal.ForeColor = Palette.Text;
            _ramVal.AutoSize = true;
            _ramVal.BackColor = Color.Transparent;
            _ramVal.Margin = new Padding(0, Dp(4), 0, Dp(2));

            TableLayoutPanel range = new TableLayoutPanel();
            range.Dock = DockStyle.Fill;
            range.Margin = new Padding(0);
            range.Padding = new Padding(0);
            range.BackColor = Palette.Card;
            range.ColumnCount = 2;
            range.RowCount = 1;
            range.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            range.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            range.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label rmin = MakeLabel("1024 MB", Fonts.UI(9f), Palette.Dim);
            Label rmax = MakeLabel("12288 MB", Fonts.UI(9f), Palette.Dim);
            rmax.Margin = new Padding(Dp(8), 0, 0, 0);
            range.Controls.Add(rmin, 0, 0);
            range.Controls.Add(rmax, 1, 0);

            _ramSlider = new SliderBar();
            _ramSlider.Min = 1024;
            _ramSlider.Max = 12288;
            _ramSlider.Step = 512;
            _ramSlider.Value = 4096;
            _ramSlider.Dock = DockStyle.Fill;
            _ramSlider.Margin = new Padding(0, Dp(6), 0, Dp(6));

            _ramApply = new RoundButton();
            _ramApply.Text = "APLICAR A RUN.BAT";
            _ramApply.Glyph = "save";
            _ramApply.Width = Dp(230);
            _ramApply.Anchor = AnchorStyles.Left;
            _ramApply.Margin = new Padding(0, Dp(4), 0, Dp(4));

            Label rnote = MakeLabel("Edita run.bat para cambiar -Xmx / -Xms", Fonts.UI(8.5f), Palette.Dim);

            Card logoCard = new Card();
            logoCard.Dock = DockStyle.Fill;
            logoCard.Margin = new Padding(0, Dp(12), 0, 0);

            TableLayoutPanel logoStack = MakeCardStack(1, 4);
            logoStack.RowStyles[2] = new RowStyle(SizeType.Absolute, Dp(72));
            logoStack.RowStyles[3] = new RowStyle(SizeType.Percent, 100f);

            Label ltitle = MakeLabel("CAMBIAR LOGO DEL SERVIDOR", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label ldesc = MakeLabel("server-icon.png · 64x64", Fonts.UI(8.5f), Palette.Muted);

            TableLayoutPanel logoRow = new TableLayoutPanel();
            logoRow.Dock = DockStyle.Fill;
            logoRow.Margin = new Padding(0, Dp(8), 0, 0);
            logoRow.Padding = new Padding(0);
            logoRow.BackColor = Palette.Card;
            logoRow.ColumnCount = 2;
            logoRow.RowCount = 1;
            logoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(64)));
            logoRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            logoRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel lprev = new Panel();
            lprev.Size = new Size(Dp(64), Dp(64));
            lprev.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            lprev.BackColor = Palette.Input;
            lprev.Paint += delegate(object s, PaintEventArgs e) { DrawIconPreview(e.Graphics, lprev); };

            RoundButton lbtn = new RoundButton();
            lbtn.Text = "ELEGIR PNG";
            lbtn.Glyph = "folder";
            lbtn.Dock = DockStyle.Fill;
            lbtn.Margin = new Padding(Dp(16), Dp(4), 0, Dp(4));
            lbtn.Click += delegate(object s, EventArgs e) { PickServerIcon(lprev); };

            logoRow.Controls.Add(lprev, 0, 0);
            logoRow.Controls.Add(lbtn, 1, 0);

            logoStack.Controls.Add(ltitle, 0, 0);
            logoStack.Controls.Add(ldesc, 0, 1);
            logoStack.Controls.Add(logoRow, 0, 2);
            logoCard.Controls.Add(logoStack);

            ram.Controls.Add(_ramTitle, 0, 0);
            ram.Controls.Add(ramDesc, 0, 1);
            ram.Controls.Add(_ramVal, 0, 2);
            ram.Controls.Add(range, 0, 3);
            ram.Controls.Add(_ramSlider, 0, 4);
            ram.Controls.Add(_ramApply, 0, 5);
            ram.Controls.Add(rnote, 0, 6);
            ram.Controls.Add(logoCard, 0, 7);
            _cardRam.Controls.Add(ram);

            // ---------------- server.properties ----------------
            _cardProps = new Card();
            _cardProps.Dock = DockStyle.Fill;
            _cardProps.Margin = new Padding(Dp(7), 0, 0, 0);

            TableLayoutPanel props = MakeCardStack(1, 5);
            props.RowStyles[2] = new RowStyle(SizeType.Absolute, Dp(38));
            props.RowStyles[3] = new RowStyle(SizeType.Percent, 100f);
            props.RowStyles[4] = new RowStyle(SizeType.Absolute, Dp(52));

            _propsTitle = MakeLabel("SERVER.PROPERTIES", Fonts.UI(10f, FontStyle.Bold), Palette.Text);
            Label pdes = MakeLabel("Edita todas las opciones con botones/controles", Fonts.UI(9f), Palette.Muted);

            Panel searchWrap = new Panel();
            searchWrap.Dock = DockStyle.Fill;
            searchWrap.Margin = new Padding(0, Dp(10), 0, Dp(6));
            searchWrap.Padding = new Padding(Dp(12), Dp(8), Dp(12), Dp(8));
            searchWrap.BackColor = Palette.Card;
            searchWrap.Paint += delegate(object s, PaintEventArgs e)
            {
                Draw.StrokeRound(e.Graphics, new RectangleF(0.5f, 0.5f, searchWrap.Width - 2f, searchWrap.Height - 2f), 9f, Palette.Border, 1f);
            };

            _propsSearch = new TextBox();
            _propsSearch.BorderStyle = BorderStyle.None;
            _propsSearch.BackColor = Palette.Card;
            _propsSearch.ForeColor = Palette.Text;
            _propsSearch.Font = Fonts.UI(9.5f);
            _propsSearch.Dock = DockStyle.Fill;
            searchWrap.Controls.Add(_propsSearch);

            _propsFlow = new FlowLayoutPanel();
            _propsFlow.Dock = DockStyle.Fill;
            _propsFlow.Margin = new Padding(0, 0, 0, Dp(8));
            _propsFlow.AutoScroll = true;
            _propsFlow.FlowDirection = FlowDirection.TopDown;
            _propsFlow.WrapContents = false;
            _propsFlow.BackColor = Palette.Card;
            _propsFlow.BorderStyle = BorderStyle.None;
            _propsFlow.Resize += delegate { LayoutPropRows(); };

            _propsSave = new RoundButton();
            _propsSave.Text = "GUARDAR SERVER.PROPERTIES";
            _propsSave.Glyph = "save";
            _propsSave.Width = Dp(290);
            _propsSave.Anchor = AnchorStyles.Left;

            props.Controls.Add(_propsTitle, 0, 0);
            props.Controls.Add(pdes, 0, 1);
            props.Controls.Add(searchWrap, 0, 2);
            props.Controls.Add(_propsFlow, 0, 3);
            props.Controls.Add(_propsSave, 0, 4);
            _cardProps.Controls.Add(props);

            leftHost.Controls.Add(_cardRam);
            grid.Controls.Add(leftHost, 0, 0);
            grid.Controls.Add(_cardProps, 1, 0);
            p.Controls.Add(grid);
            return p;
        }

        // Envuelve una seccion de configuracion con una barra superior que
        // permite regresar al indice sin salir de "Configuracion del Servidor".
        private Panel MakeConfigSection(Panel content, string title, int backIndex = 3)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Margin = new Padding(0);
            host.Padding = new Padding(0);
            host.BackColor = Color.Transparent;

            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = Dp(44);
            bar.Margin = new Padding(0);
            bar.Padding = new Padding(0, 0, 0, Dp(6));
            bar.BackColor = Color.Transparent;
            bar.ColumnCount = 2;
            bar.RowCount = 1;
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dp(150)));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            RoundButton back = new RoundButton();
            back.Text = "REGRESAR";
            back.Glyph = "back";
            back.Dock = DockStyle.Fill;
            back.Margin = new Padding(0, 0, Dp(14), 0);
            back.FillColor = Palette.Card;
            back.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.06f);
            back.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.08f);
            back.TextColor = Palette.Text;
            back.BorderColor = Palette.Border;
            back.Click += delegate { NavigateTo(backIndex); };

            Label section = new Label();
            section.Text = (title ?? "").ToUpperInvariant();
            section.Font = Fonts.UI(9f, FontStyle.Bold);
            section.ForeColor = Palette.Dim;
            section.BackColor = Color.Transparent;
            section.Dock = DockStyle.Fill;
            section.TextAlign = ContentAlignment.MiddleLeft;
            section.AutoEllipsis = true;

            bar.Controls.Add(back, 0, 0);
            bar.Controls.Add(section, 1, 0);

            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0);
            host.Controls.Add(content);
            host.Controls.Add(bar);
            return host;
        }

        private Panel BuildConfigHub()
        {
            Panel p = new Panel();
            p.BackColor = Color.Transparent;

            TableLayoutPanel stack = new TableLayoutPanel();
            stack.Dock = DockStyle.Fill;
            stack.Margin = new Padding(0);
            stack.Padding = new Padding(0);
            stack.BackColor = Color.Transparent;
            stack.ColumnCount = 1;
            stack.RowCount = 7;
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(100)));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(100)));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(100)));
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, Dp(100)));
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _cfgEntryRam = new ConfigEntry();
            _cfgEntryRam.Title = "RAM, Server.properties y icono";
            _cfgEntryRam.Description = "Memoria asignada, MTOD propiedades del servidor e imagen del proyecto.";
            _cfgEntryRam.Glyph = "sliders";
            _cfgEntryRam.Dock = DockStyle.Fill;
            _cfgEntryRam.Margin = new Padding(0, Dp(8), 0, Dp(8));
            _cfgEntryRam.Click += delegate { NavigateTo(4); };

            _cfgEntryBackups = new ConfigEntry();
            _cfgEntryBackups.Title = "Copias de seguridad";
            _cfgEntryBackups.Description = "Respaldos automaticos y manuales del mundo y de los jugadores.";
            _cfgEntryBackups.Glyph = "copy";
            _cfgEntryBackups.Dock = DockStyle.Fill;
            _cfgEntryBackups.Margin = new Padding(0, 0, 0, Dp(8));
            _cfgEntryBackups.Click += delegate { NavigateTo(5); };

            _cfgEntryVersions = new ConfigEntry();
            _cfgEntryVersions.Title = "Versiones del servidor";
            _cfgEntryVersions.Description = "Cambiar server.jar entre Vanilla, Snapshots, Plugins y Modded.";
            _cfgEntryVersions.Glyph = "chip";
            _cfgEntryVersions.Dock = DockStyle.Fill;
            _cfgEntryVersions.Margin = new Padding(0);
            _cfgEntryVersions.Click += delegate { NavigateTo(6); };

            _cfgEntryResets = new ConfigEntry();
            _cfgEntryResets.Title = "Resets automaticos";
            _cfgEntryResets.Description = "Reinicia el servidor cada cierto tiempo con avisos y backup previo.";
            _cfgEntryResets.Glyph = "restart";
            _cfgEntryResets.Dock = DockStyle.Fill;
            _cfgEntryResets.Margin = new Padding(0);
            _cfgEntryResets.Click += delegate { NavigateTo(12); };

            Label hint = MakeWrapLabel(
                "Los cambios de RAM, server.properties y server.jar se aplican al iniciar el servidor. " +
                "Las copias de seguridad y las versiones viven ahora dentro de esta seccion.",
                Fonts.UI(8.5f), Palette.Dim, Dp(40));
            hint.Margin = new Padding(0, Dp(16), 0, 0);

            stack.Controls.Add(_cfgEntryRam, 0, 2);
            stack.Controls.Add(_cfgEntryBackups, 0, 3);
            stack.Controls.Add(_cfgEntryVersions, 0, 4);
            stack.Controls.Add(_cfgEntryResets, 0, 5);
            stack.Controls.Add(hint, 0, 6);
            p.Controls.Add(stack);
            return p;
        }

        // Las filas de server.properties son controles de ancho fijo: se
        // reajustan al ancho real del contenedor en cada resize.
        private void LayoutPropRows()
        {
            if (_propsFlow == null) return;
            int w = _propsFlow.ClientSize.Width - Dp(8);
            if (w < Dp(320)) w = Dp(320);
            foreach (Control c in _propsFlow.Controls)
            {
                if (c is PropRow && c.Width != w) c.Width = w;
            }
        }


        private void ConsoleIn_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                SendConsole();
                return;
            }
            if (e.KeyCode == Keys.Tab)
            {
                e.Handled = true;
                TabHistory();
                return;
            }
            if (e.KeyCode == Keys.Up)
            {
                e.Handled = true;
                PrevHistory();
                return;
            }
            if (e.KeyCode == Keys.Down)
            {
                e.Handled = true;
                NextHistory();
                return;
            }
        }

        private void SendConsole()
        {
            string cmd = _consoleIn.Text.Trim();
            if (string.IsNullOrEmpty(cmd)) return;
            AddHistory(cmd);
            _consoleIn.Text = "";
            AppendConsole("> " + cmd);
            if (_bridge.IsRunning)
            {
                _bridge.Send(cmd);
                return;
            }
            AppendConsole("[PANEL] Servidor no arrancado desde este panel de consola. Usa 'Iniciar' o asegúrate que esté corriendo.");
        }

        private void AddHistory(string cmd)
        {
            if (_consoleHistory.Count > 0 && _consoleHistory[_consoleHistory.Count - 1] == cmd) { _historyIndex = _consoleHistory.Count; return; }
            _consoleHistory.Add(cmd);
            if (_consoleHistory.Count > 200) _consoleHistory.RemoveAt(0);
            _historyIndex = _consoleHistory.Count;
        }

        private void TabHistory()
        {
            if (_consoleHistory.Count == 0) return;
            _historyIndex--;
            if (_historyIndex < 0) _historyIndex = _consoleHistory.Count - 1;
            _consoleIn.Text = _consoleHistory[_historyIndex];
            _consoleIn.Inner.SelectionStart = _consoleIn.Text.Length;
        }

        private void PrevHistory()
        {
            if (_consoleHistory.Count == 0) return;
            _historyIndex--;
            if (_historyIndex < 0) _historyIndex = 0;
            _consoleIn.Text = _consoleHistory[_historyIndex];
            _consoleIn.Inner.SelectionStart = _consoleIn.Text.Length;
        }

        private void NextHistory()
        {
            if (_consoleHistory.Count == 0) return;
            _historyIndex++;
            if (_historyIndex >= _consoleHistory.Count) { _historyIndex = _consoleHistory.Count; _consoleIn.Text = ""; return; }
            _consoleIn.Text = _consoleHistory[_historyIndex];
            _consoleIn.Inner.SelectionStart = _consoleIn.Text.Length;
        }

        private void AppendConsole(string line)
        {
            if (line == null) return;
            lock (_consoleQueueLock) { _consoleQueue.Enqueue(line); }
            // En el hilo de la interfaz no hace falta esperar al temporizador.
            if (!InvokeRequired) FlushConsoleQueue();
        }

        // Vacia la cola en un unico AppendText por lote. Antes se llamaba a
        // AppendText/ScrollToCaret por cada linea: con 200 mods la cola crecia
        // mas rapido de lo que la interfaz podia pintarla y la ventana
        // terminaba marcada como "no responde".
        private void FlushConsoleQueue()
        {
            if (_consoleOut == null || IsDisposed || Disposing) return;

            string batch;
            int taken = 0;
            lock (_consoleQueueLock)
            {
                if (_consoleQueue.Count == 0) return;
                StringBuilder builder = new StringBuilder();
                while (_consoleQueue.Count > 0 && taken < 500)
                {
                    builder.Append(_consoleQueue.Dequeue()).Append(Environment.NewLine);
                    taken++;
                }
                batch = builder.ToString();
            }
            if (batch.Length == 0) return;

            try
            {
                if (_consoleOut.TextLength + batch.Length > 80000)
                {
                    // Recortar una sola vez por lote en lugar de por linea.
                    string text = _consoleOut.Text + batch;
                    int keep = Math.Max(10000, text.Length - 70000);
                    int cut = text.IndexOf('\n', keep);
                    _consoleOut.Text = cut >= 0 ? text.Substring(cut + 1) : text.Substring(keep);
                }
                else
                {
                    _consoleOut.AppendText(batch);
                }
                _consoleOut.SelectionStart = _consoleOut.TextLength;
                _consoleOut.ScrollToCaret();
            }
            catch { }
        }


        private void Wire()
        {
            _navStatus.Active = true;
            _btnStart.Click += BtnStart_Click;
            _btnRestart.Click += BtnRestart_Click;
            _ramSlider.ValueChanged += delegate { _selectedRamMb = _ramSlider.Value; _ramVal.Text = _selectedRamMb.ToString() + " MB"; };
            _ramApply.Click += RamApply_Click;
            _propsSave.Click += PropsSave_Click;
            _propsSearch.TextChanged += PropsSearch_TextChanged;

            // Barras delgadas superpuestas en las paginas con scroll. Se
            // ocultan solas cuando no hay contenido suficiente o la vista
            // esta fuera de pantalla.
            _configScroll = ScrollBarBinder.Attach(_configScrollHost, delegate { return Palette.Bg; });
            _propsScroll = ScrollBarBinder.Attach(_propsFlow, delegate { return Palette.Card; });
            _addonScroll = ScrollBarBinder.Attach(_addonList, delegate { return Palette.Card; });
            _cloudScroll = ScrollBarBinder.Attach(_cloudScrollHost, delegate { return Palette.Bg; });

            FormClosing += delegate
            {
                CommitProfileHours();
                ProfileStore.Save();
                if (_sidebarAnim != null) { _sidebarAnim.Stop(); _sidebarAnim.Dispose(); }
                if (_sidebarTip != null) _sidebarTip.Dispose();
                if (_configScroll != null) _configScroll.Dispose();
                if (_propsScroll != null) _propsScroll.Dispose();
                if (_addonScroll != null) _addonScroll.Dispose();
            };

            // La posicion del toast ya se recalcula en InitializeUI/PositionToast.
            Load += delegate { HandleCreated += delegate { WindowFx.DarkTitleBar(Handle); }; };
            Shown += delegate
            {
                // Los binders necesitan el handle del formulario para crear
                // su barra; se sincronizan al mostrarse por primera vez.
                if (_configScroll != null) _configScroll.Sync();
                if (_propsScroll != null) _propsScroll.Sync();
                if (_addonScroll != null) _addonScroll.Sync();
                if (_viewVersions != null && _viewVersions.Visible) LoadVersionList();
            };
        }

        private void SwitchView(int idx)
        {
            if (idx == 2 && _addonKind == AddonKind.None) idx = 0;

            // Si el usuario sale de Plugins/Mods mientras carga, la capa no
            // debe seguir tapando la vista de destino.
            if (idx != 2)
            {
                _markReadyAfterAddonBuild = false;
                if (_loadingOverlay != null && _loadingOverlay.Visible)
                    _loadingOverlay.Cancel();
            }

            _navStatus.Active = idx == 0;
            _navConsole.Active = idx == 1;
            _navAddons.Active = idx == 2;
            _navConfig.Active = (idx >= 3 && idx <= 6) || idx == 12;
            if (_navApp != null) _navApp.Active = idx >= 7 && idx <= 11;
            if (_navProfiles != null) _navProfiles.Active = false;

            _viewStatus.Visible = idx == 0;
            _viewConsole.Visible = idx == 1;
            _viewAddons.Visible = idx == 2;
            _viewConfigHub.Visible = idx == 3;
            _viewConfigRam.Visible = idx == 4;
            _viewBackupsHost.Visible = idx == 5;
            _viewVersionsHost.Visible = idx == 6;
            if (_viewResetsHost != null) _viewResetsHost.Visible = idx == 12;
            if (_viewAppConfigHub != null) _viewAppConfigHub.Visible = idx == 7;
            if (_viewAppTheme != null) _viewAppTheme.Visible = idx == 8;
            if (_viewCloudBackup != null) _viewCloudBackup.Visible = idx == 9;
            if (_viewLanguage != null) _viewLanguage.Visible = idx == 10;
            if (_viewUpdates != null) _viewUpdates.Visible = idx == 11;

            if (idx == 0)
            {
                _viewStatus.BringToFront();
                _pageTitle.Text = LanguageService.T("Estado del Servidor");
                _pageSub.Text = LanguageService.T("Monitoreo en tiempo real");
            }
            else if (idx == 1)
            {
                _viewConsole.BringToFront();
                _pageTitle.Text = LanguageService.T("Consola");
                _pageSub.Text = LanguageService.T("Enviar comandos con autocompletado por historial");
            }
            else if (idx == 2)
            {
                _viewAddons.BringToFront();
                BeginAddonsLoading();
                _pageTitle.Text = LanguageService.T(_addonKind == AddonKind.Mods ? "Mods" : "Plugins");
                _pageSub.Text = LanguageService.T(_addonKind == AddonKind.Mods
                    ? "Mods descargados en la carpeta mods"
                    : "Plugins descargados en la carpeta plugins");
            }
            else if (idx == 3)
            {
                _viewConfigHub.BringToFront();
                _pageTitle.Text = LanguageService.T("Configuracion del Servidor");
                _pageSub.Text = LanguageService.T("RAM, copias de seguridad y versiones del servidor");
            }
            else if (idx == 4)
            {
                _viewConfigRam.BringToFront();
                BuildProps();
                _pageTitle.Text = LanguageService.T("Configuracion del Servidor");
                _pageSub.Text = LanguageService.T("RAM, server.properties e icono del servidor");
            }
            else if (idx == 5)
            {
                _viewBackupsHost.BringToFront();
                RefreshBackupHistory();
                UpdateBackupStatus(true);
                _pageTitle.Text = LanguageService.T("Configuracion del Servidor");
                _pageSub.Text = LanguageService.T("Copias de seguridad del mundo y de los jugadores");
            }
            else if (idx == 6)
            {
                _viewVersionsHost.BringToFront();
                RefreshJarCurrent();
                LoadVersionList();
                _pageTitle.Text = LanguageService.T("Configuracion del Servidor");
                _pageSub.Text = LanguageService.T("Cambiar server.jar (Vanilla, Snapshots, Plugins, Modded)");
            }
            else if (idx == 12)
            {
                _viewResetsHost.BringToFront();
                UpdateResetStatus(true);
                _pageTitle.Text = LanguageService.T("Configuracion del Servidor");
                _pageSub.Text = LanguageService.T("Reinicios programados con aviso y backup previo");
            }
            else if (idx == 7)
            {
                _viewAppConfigHub.BringToFront();
                _pageTitle.Text = LanguageService.T("Configuracion de la App");
                _pageSub.Text = LanguageService.T("Personaliza la apariencia, el idioma y las actualizaciones.");
            }
            else if (idx == 8)
            {
                _viewAppTheme.BringToFront();
                _pageTitle.Text = LanguageService.T("Configuracion de la App");
                _pageSub.Text = LanguageService.T("Temas");
            }
            else if (idx == 9)
            {
                _viewCloudBackup.BringToFront();
                _pageTitle.Text = LanguageService.T("Configuracion de la App");
                _pageSub.Text = LanguageService.T("Backup en la nube");
            }
            else if (idx == 10)
            {
                _viewLanguage.BringToFront();
                _pageTitle.Text = LanguageService.T("Configuracion de la App");
                _pageSub.Text = LanguageService.T("Idioma");
            }
            else if (idx == 11)
            {
                _viewUpdates.BringToFront();
                _pageTitle.Text = LanguageService.T("Configuracion de la App");
                _pageSub.Text = LanguageService.T("Actualizaciones");
            }

            // Solo la vista visible conserva su barra superpuesta.
            if (_configScroll != null) _configScroll.SetActive(idx == 4);
            if (_propsScroll != null) _propsScroll.SetActive(idx == 4);
            if (_addonScroll != null) _addonScroll.SetActive(idx == 2);
            if (_cloudScroll != null) _cloudScroll.SetActive(idx == 9);
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_versionBusy)
            {
                MessageDialog.Info(this, "Servidor", "Espera a que termine el cambio de version.");
                return;
            }
            if (_startBusy) return;
            if (_bridge.IsRunning)
            {
                // stop
                if (MessageDialog.Confirm(this, "Detener Servidor", "¿Detener el servidor de Minecraft?", "Detener", true))
                {
                    AppendConsole("[PANEL] Enviando stop...");
                    SetStartBusy(true);
                    // Guardar un mundo grande puede tardar: nunca en el hilo
                    // de la interfaz o la ventana se queda "no responde".
                    Task.Run(delegate
                    {
                        bool stopped = _bridge.StopGracefully(60000);
                        try { _playit.Stop(); } catch { }
                        FinishStartBusy(delegate
                        {
                            _toast.ShowMessage(
                                stopped ? "Servidor detenido" : "Servidor detenido (forzado)",
                                stopped ? Palette.Red : Palette.Amber, "stop");
                        });
                    });
                }
                return;
            }
            // start
            StartServerFromPanel();
        }

        private void SetStartBusy(bool busy)
        {
            _startBusy = busy;
            try { _btnStart.Enabled = !busy; } catch { }
            try { _btnRestart.Enabled = !busy; } catch { }
        }

        private void FinishStartBusy(Action done)
        {
            try
            {
                BeginInvoke(new Action(delegate
                {
                    SetStartBusy(false);
                    if (done != null) done();
                }));
            }
            catch { }
        }

        private void StartServerFromPanel()
        {
            if (_startBusy) return;
            _serverWasRunning = false;
            _propsFile.Load();
            int ram = _propsFile.GetInt("server-ram-mb", _selectedRamMb);
            if (ram < 1024) ram = 1024;
            if (ram > 12288) ram = 12288;
            _selectedRamMb = ram;
            _ramSlider.Value = ram;
            _ramVal.Text = ram + " MB";
            AppendConsole("[PANEL] Iniciando servidor con " + ram + "MB...");
            SetStartBusy(true);
            int launchRam = ram;
            Task.Run(delegate
            {
                bool started = _bridge.Start(_paths, launchRam);
                if (started)
                {
                    // Playit puede tardar en arrancar: fuera del hilo de la UI.
                    try { _playit.Start(); } catch { }
                }
                FinishStartBusy(delegate
                {
                    if (!started)
                    {
                        _toast.ShowMessage("No se pudo iniciar el servidor", Palette.Red, "warning");
                        return;
                    }
                    ResetOnServerStarted();
                    _toast.ShowMessage("Servidor iniciando", Palette.Accent, "play");
                });
            });
        }

        private void BtnRestart_Click(object sender, EventArgs e)
        {
            if (_versionBusy)
            {
                MessageDialog.Info(this, "Servidor", "Espera a que termine el cambio de version.");
                return;
            }
            if (_startBusy) return;
            if (MessageDialog.Confirm(this, "Reiniciar", "¿Reiniciar servidor y túnel Playit?", "Reiniciar", false))
            {
                AppendConsole("[PANEL] Reiniciando...");
                SetStartBusy(true);
                Task.Run(delegate
                {
                    _bridge.Stop();
                    try { _playit.Restart(); } catch { }
                    Thread.Sleep(1500);
                    int ram = _propsFile.GetInt("server-ram-mb", _selectedRamMb);
                    bool started = _bridge.Start(_paths, ram);
                    if (started) { try { _playit.Start(); } catch { } }
                    FinishStartBusy(delegate
                    {
                        if (started)
                            _toast.ShowMessage("Reiniciando servidor y Playit", Palette.Blue, "restart");
                        else
                            _toast.ShowMessage("No se pudo reiniciar el servidor", Palette.Red, "warning");
                    });
                });
            }
        }

        private void StartPlayit()
        {
            try { _playit.Start(); } catch { }
        }

        private void StopPlayitIfDesired(bool restart)
        {
            // We don't force stop playit unless user wants? Button says "Reiniciar (debe reiniciar el server y el tunnel)"
            try
            {
                if (restart)
                {
                    _playit.Restart();
                    return;
                }
                // For "detener" we can leave playit running or stop? Spec says "Iniciar (Que alterna entre Iniciar y cerrar, el cual debe abrir el server y el tunnel)"
                // To match "abrir el server y el tunnel" on start, on stop maybe stop both? safer to stop playit if stopping server from panel
                _playit.Stop();
            }
            catch { }
        }

        private void RamApply_Click(object sender, EventArgs e)
        {
            try
            {
                int ram = _ramSlider.Value;
                string bat = _paths.RunBat;
                if (!File.Exists(bat)) { MessageDialog.Info(this, "run.bat no encontrado", bat); return; }
                string content = File.ReadAllText(bat);
                System.Text.RegularExpressions.Regex rx = new System.Text.RegularExpressions.Regex(@"-Xmx\d+M\s+-Xms\d+M", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                string rep = string.Format("-Xmx{0}M -Xms{0}M", ram);
                if (rx.IsMatch(content))
                {
                    content = rx.Replace(content, rep);
                }
                else
                {
                    content = content.Replace("java", "java " + rep);
                }
                File.WriteAllText(bat, content);
                _propsFile.SetInt("server-ram-mb", ram);
                _propsFile.Save();
                _toast.ShowMessage("RAM actualizada en run.bat", Palette.Accent, "check");
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Error", ex.Message);
            }
        }

        private void PropsSave_Click(object sender, EventArgs e)
        {
            try
            {
                _propsFile.Save();
                _toast.ShowMessage("server.properties guardado", Palette.Accent, "check");
            }
            catch (Exception ex)
            {
                MessageDialog.Info(this, "Error", ex.Message);
            }
        }

        private void PropsSearch_TextChanged(object sender, EventArgs e)
        {
            string q = _propsSearch.Text.ToLowerInvariant();
            foreach (Control c in _propsFlow.Controls)
            {
                if (!(c is PropRow)) continue;
                PropRow row = (PropRow)c;
                if (string.IsNullOrEmpty(q)) { row.Visible = true; continue; }
                row.Visible = row.Key.ToLowerInvariant().Contains(q) || row.LabelText.ToLowerInvariant().Contains(q);
            }
        }

        private void RefreshAll()
        {
            BuildProps();
            _tick.Tick += Tick_Tick;
        }

        // La recoleccion de estado (enumerar procesos, leer logs completos,
        // consultar el servicio de Playit) puede tardar cientos de
        // milisegundos. Si se hace en el hilo de UI, al restaurar la ventana
        // los controles esperan a que termine y se ve el fondo a medio pintar
        // ("como si estuviera cargando"). Por eso se recoge en segundo plano.
        private bool _statusBusy;

        private sealed class StatusSample
        {
            public ServerStatus Server;
            public RamInfo Ram;
            public List<PlayerInfo> Players;
            public string LocalIp;
            public bool PlayitRunning;
            public PlayitStatus Playit;
            public int ServerPort;
        }

        private void Tick_Tick(object sender, EventArgs e)
        {
            TrackProfileHours();
            ResetTick();
            if (_statusBusy) return;

            string latestLog = _paths.LatestLog;
            string playitLogs = _paths.PlayitLogs;
            int serverPort = _propsFile.GetInt("server-port", 25565);

            _statusBusy = true;
            System.Threading.Tasks.Task.Run(delegate
            {
                StatusSample sample = new StatusSample();
                sample.ServerPort = serverPort;
                try { sample.Server = _scanner.GetServerStatus(); } catch { sample.Server = new ServerStatus(); }
                try { sample.Ram = _ram.Get(); } catch { sample.Ram = new RamInfo(); }
                try { sample.Players = _logTail.GetOnlineFromLog(latestLog); } catch { sample.Players = new List<PlayerInfo>(); }
                try { sample.LocalIp = NetworkInfo.GetLocalIPv4(); } catch { sample.LocalIp = ""; }
                try { sample.PlayitRunning = _playit.IsRunning(); } catch { }
                try
                {
                    sample.Playit = _logTail.ParsePlayit(playitLogs);
                }
                catch
                {
                    sample.Playit = new PlayitStatus();
                    sample.Playit.Recent = new List<string>();
                }
                return sample;
            }).ContinueWith(delegate(System.Threading.Tasks.Task<StatusSample> task)
            {
                _statusBusy = false;
                if (task.Status != System.Threading.Tasks.TaskStatus.RanToCompletion) return;
                StatusSample sample = task.Result;
                if (sample == null) return;
                try
                {
                    if (!IsDisposed && !Disposing && IsHandleCreated)
                        BeginInvoke(new Action(delegate { ApplyStatusSample(sample); }));
                }
                catch { }
            });
        }

        private void ApplyStatusSample(StatusSample sample)
        {
            if (IsDisposed || Disposing) return;
            // Minimizada no hay nada visible que actualizar y cada cambio de
            // texto encola repintados que luego retrasan la restauracion.
            if (WindowState == FormWindowState.Minimized) return;
            ServerStatus st = sample.Server ?? new ServerStatus();
            RamInfo ram = sample.Ram ?? new RamInfo();
            List<PlayerInfo> players = sample.Players ?? new List<PlayerInfo>();
            PlayitStatus ps = sample.Playit ?? new PlayitStatus();
            bool pitRun = sample.PlayitRunning;
            string localIp = sample.LocalIp;
            int serverPort = sample.ServerPort;

            bool running = st.Running || _bridge.IsRunning;
            BackupTick(running);
            if (running)
            {
                _serverBadge.Set("EN LINEA", Palette.Accent);
                _srvState.Text = LanguageService.T("Estado: En ejecución");
                _srvPid.Text = st.Pid > 0 ? "PID: " + st.Pid.ToString() : (_bridge.IsRunning ? LanguageService.T("PID: (administrado por panel)") : "PID: --");
                if (st.RamBytes > 0)
                {
                    _srvRamVal.Text = Utils.HumanBytes(st.RamBytes);
                    // rough
                    _srvRamBar.Value = 0.4;
                }
                else
                {
                    _srvRamVal.Text = "--";
                    _srvRamBar.Value = _bridge.IsRunning ? 0.3 : 0;
                }
                _btnStart.Text = LanguageService.T("DETENER");
                _btnStart.Glyph = "stop";
                _btnStart.FillColor = Palette.Red;
                _btnStart.HoverFill = Palette.RedHover;
                _btnStart.PressFill = ColorTranslator.FromHtml("#D73A49");
                _btnStart.TextColor = Palette.White;
            }
            else
            {
                _serverBadge.Set("DETENIDO", Palette.Muted);
                _srvState.Text = LanguageService.T("Estado: Detenido");
                _srvPid.Text = "PID: --";
                _srvRamVal.Text = "--";
                _srvRamBar.Value = 0;
                _btnStart.Text = LanguageService.T("INICIAR");
                _btnStart.Glyph = "play";
                _btnStart.FillColor = Palette.Accent;
                _btnStart.HoverFill = Palette.AccentHover;
                _btnStart.PressFill = ColorTranslator.FromHtml("#2E9A40");
                _btnStart.TextColor = ColorTranslator.FromHtml("#06120A");
            }

            // System ram
            if (ram.TotalBytes > 0)
            {
                _sysRamVal.Text = Utils.HumanBytes(ram.UsedBytes) + " / " + Utils.HumanBytes(ram.TotalBytes);
                _sysRamBar.Value = ram.Percent / 100.0;
                _sysRamBar.BarColor = ram.Percent > 90 ? Palette.Red : (ram.Percent > 75 ? Palette.Amber : Palette.Accent);
                _sysFree.Text = LanguageService.T("Disponible:") + " " + Utils.HumanBytes(ram.FreeBytes);
            }
            else
            {
                _sysRamVal.Text = "--";
                _sysRamBar.Value = 0;
                _sysFree.Text = LanguageService.T("Disponible:") + " --";
            }

            // Players
            try
            {
                foreach (PlayerInfo pinfo in players)
                {
                    if (!_playerCache.ContainsKey(pinfo.Name))
                    {
                        PlayerSession psess = new PlayerSession();
                        psess.Name = pinfo.Name;
                        psess.Ip = pinfo.Ip;
                        psess.Joined = DateTime.Now;
                        _playerCache[pinfo.Name] = psess;
                        if (!_notified.Contains(psess.Name))
                        {
                            _notified.Add(psess.Name);
                            ShowPlayerJoin(psess.Name);
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(_playerCache[pinfo.Name].Ip) && !string.IsNullOrEmpty(pinfo.Ip))
                            _playerCache[pinfo.Name].Ip = pinfo.Ip;
                    }
                }
                List<string> toRemove = new List<string>();
                foreach (KeyValuePair<string, PlayerSession> kv in _playerCache)
                {
                    bool found = false;
                    foreach (PlayerInfo pi in players) if (string.Equals(pi.Name, kv.Key, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    if (!found)
                    {
                        toRemove.Add(kv.Key);
                        _notified.Remove(kv.Key);
                    }
                }
                foreach (string r in toRemove) _playerCache.Remove(r);
                _plyList.BeginUpdate();
                _plyList.Items.Clear();
                foreach (KeyValuePair<string, PlayerSession> kv in _playerCache)
                {
                    _plyList.Items.Add(kv.Value);
                }
                _plyList.EndUpdate();
                _plyCount.Text = "(" + _playerCache.Count.ToString() + ")";
            }
            catch { }

            // IP local / dominio publico de Playit
            string localText = string.IsNullOrWhiteSpace(localIp)
                ? "IP LOCAL: --"
                : "IP LOCAL: " + localIp + ":" + serverPort.ToString();
            if (_srvIpLocal.Text != localText) _srvIpLocal.Text = localText;

            // Playit
            if (pitRun)
            {
                _playitBadge.Set("PLAYIT: ACTIVO", Palette.Accent);
                _pitStatus.Text = LanguageService.T("Servicio: En ejecucion (playitd)");
            }
            else
            {
                _playitBadge.Set("PLAYIT: INACTIVO", Palette.Muted);
                _pitStatus.Text = LanguageService.T("Servicio: Detenido");
            }
            TickPlayitDomain(pitRun, serverPort);
            _pitClients.Text = LanguageService.T("Conexiones activas detectadas:") + " " + ps.ClientCount.ToString();
            if (ps.Recent != null && ps.Recent.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                int take = ps.Recent.Count;
                if (take > 12) take = 12;
                for (int i = ps.Recent.Count - take; i < ps.Recent.Count; i++) sb.AppendLine(ps.Recent[i]);
                _pitLog.Text = sb.ToString();
                _pitLog.SelectionStart = _pitLog.Text.Length;
                _pitLog.ScrollToCaret();
            }
        }

        // El dominio de Playit cambia al reiniciar el tunel. Se consulta el
        // IPC en segundo plano y se conserva el ultimo valor valido.
        private void TickPlayitDomain(bool running, int serverPort)
        {
            if (string.IsNullOrEmpty(_lastPlayitDomain))
                _lastPlayitDomain = AppConfig.Instance.LastPlayitDomain ?? "";

            string domain = string.IsNullOrWhiteSpace(_lastPlayitDomain) ? "--" : _lastPlayitDomain;
            string domainText = "IP/DOMINIO: " + domain;
            if (_srvIpDomain.Text != domainText) _srvIpDomain.Text = domainText;
            _srvIpDomain.ForeColor = running ? Palette.Text : Palette.Muted;
            if (_copyDomain != null)
            _copyDomain.Enabled = !string.IsNullOrWhiteSpace(_lastPlayitDomain);

            string tunnelText = string.IsNullOrWhiteSpace(_lastPlayitDomain)
                ? "Tunel: --"
                : "Tunel: " + _lastPlayitDomain +
                  (string.IsNullOrWhiteSpace(_lastPlayitDestination) ? "" : " -> " + _lastPlayitDestination);
            if (_pitTunnel.Text != tunnelText) _pitTunnel.Text = tunnelText;
            _pitTunnel.ForeColor = running ? Palette.Muted : Palette.Dim;

            if (!running || _playitPolling) return;
            if ((DateTime.UtcNow - _lastPlayitPoll).TotalSeconds < 4) return;

            _playitPolling = true;
            _lastPlayitPoll = DateTime.UtcNow;
            Task.Run(delegate { return PlayitIpc.GetTunnels(1200); }).ContinueWith(delegate(Task<List<PlayitTunnel>> t)
            {
                try
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        PlayitTunnel tunnel = PlayitIpc.PickForPort(t.Result, serverPort);
                        if (tunnel != null && !string.IsNullOrWhiteSpace(tunnel.DisplayAddress))
                        {
                            if (IsHandleCreated && !IsDisposed)
                            {
                                BeginInvoke(new Action(delegate { ApplyPlayitTunnel(tunnel); }));
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    _playitPolling = false;
                }
            });
        }

        private void ApplyPlayitTunnel(PlayitTunnel tunnel)
        {
            _lastPlayitDomain = tunnel.DisplayAddress ?? "";
            _lastPlayitDestination = tunnel.Destination ?? "";

            _srvIpDomain.Text = "IP/DOMINIO: " +
                (string.IsNullOrWhiteSpace(_lastPlayitDomain) ? "--" : _lastPlayitDomain);
            _srvIpDomain.ForeColor = Palette.Text;

            _pitTunnel.Text = string.IsNullOrWhiteSpace(_lastPlayitDomain)
                ? "Tunel: --"
                : "Tunel: " + _lastPlayitDomain +
                  (string.IsNullOrWhiteSpace(_lastPlayitDestination) ? "" : " -> " + _lastPlayitDestination);
            _pitTunnel.ForeColor = Palette.Muted;
            if (_copyDomain != null)
            _copyDomain.Enabled = !string.IsNullOrWhiteSpace(_lastPlayitDomain);

            if (!string.Equals(AppConfig.Instance.LastPlayitDomain, _lastPlayitDomain, StringComparison.Ordinal))
            {
                AppConfig.Instance.LastPlayitDomain = _lastPlayitDomain;
                AppConfig.Instance.Save();
            }
        }

        private void BuildProps()
        {
            if (_propsFlow == null) return;
            _propsFlow.SuspendLayout();
            _propsFlow.Controls.Clear();
            _propsFile.Load();
            // Common props
            AddProp("motd", "MOTD", PropType.Text, _propsFile.Get("motd", "A Minecraft Server"));
            AddProp("server-port", "Puerto", PropType.Number, _propsFile.Get("server-port", "25565"));
            AddProp("max-players", "Max Jugadores", PropType.Number, _propsFile.Get("max-players", "20"));
            AddProp("gamemode", "Modo de Juego", PropType.Select, new string[] { "survival", "creative", "adventure", "spectator" });
            AddProp("difficulty", "Dificultad", PropType.Select, new string[] { "peaceful", "easy", "normal", "hard" });
            AddProp("pvp", "PVP", PropType.Bool, "");
            AddProp("hardcore", "Hardcore", PropType.Bool, "");
            AddProp("online-mode", "Online Mode", PropType.Bool, "");
            AddProp("white-list", "Whitelist", PropType.Bool, "");
            AddProp("enforce-whitelist", "Forzar Whitelist", PropType.Bool, "");
            AddProp("allow-flight", "Permitir Vuelo", PropType.Bool, "");
            AddProp("spawn-protection", "Proteccion Spawn", PropType.Number, _propsFile.Get("spawn-protection", "0"));
            AddProp("view-distance", "View Distance", PropType.Number, _propsFile.Get("view-distance", "10"));
            AddProp("simulation-distance", "Simulation Distance", PropType.Number, _propsFile.Get("simulation-distance", "10"));
            AddProp("max-tick-time", "Max Tick Time", PropType.Number, _propsFile.Get("max-tick-time", "60000"));
            AddProp("enable-status", "Mostrar en Lista", PropType.Bool, "");
            AddProp("hide-online-players", "Ocultar Jugadores", PropType.Bool, "");
            AddProp("level-name", "Nombre Mundo", PropType.Text, _propsFile.Get("level-name", "world"));
            AddProp("level-seed", "Semilla", PropType.Text, _propsFile.Get("level-seed", ""));
            AddProp("level-type", "Tipo Mundo", PropType.Text, _propsFile.Get("level-type", "minecraft:normal"));
            AddProp("spawn-monsters", "Spawn Monstruos", PropType.Bool, "");
            AddProp("spawn-animals", "Spawn Animales", PropType.Bool, "");
            AddProp("spawn-npcs", "Spawn Aldeanos", PropType.Bool, "");
            AddProp("generate-structures", "Generar Estructuras", PropType.Bool, "");
            AddProp("force-gamemode", "Forzar Gamemode", PropType.Bool, "");
            AddProp("require-resource-pack", "Requerir Resource Pack", PropType.Bool, "");
            _propsFlow.ResumeLayout();
        }

        private void AddProp(string key, string label, PropType type, object opts)
        {
            if (_propsFlow == null) return;
            PropRow row = new PropRow(key, LanguageService.T(label), type, opts, _propsFile);
            int w = _propsFlow.ClientSize.Width - Dp(8);
            if (w < Dp(320)) w = Dp(320);
            row.Width = w;
            row.Margin = new Padding(0, 0, 0, 6);
            _propsFlow.Controls.Add(row);
        }

        private enum PropType { Text, Number, Bool, Select }
         private void CopyPlayitDomain()
            {
                string domain = (_lastPlayitDomain ?? "").Trim();
                if (string.IsNullOrWhiteSpace(domain) || domain == "--")
                {
                    if (_toast != null)
                        _toast.ShowMessage(
                            LanguageService.T("No hay dominio para copiar"),
                            Palette.Amber, "warning");
                    return;
                }

                try
                {
                    Clipboard.SetText(domain);

                    if (_toast != null)
                        _toast.ShowMessage(
                            LanguageService.T("Copiado"),
                            Palette.Accent, "copy");
                }
                catch
                {
                    if (_toast != null)
                        _toast.ShowMessage(
                            LanguageService.T("No se pudo copiar"),
                            Palette.Red, "warning");
                }
            }
        private sealed class PropRow : Panel
        {
            private readonly string _key;
            private readonly Label _label;
            private readonly Label _keyLabel;
            private Control _editor;
            private TextBox _text;
            private readonly PropertiesFile _props;
            public string Key { get { return _key; } }
            public string LabelText { get { return _label.Text; } }

            public PropRow(string key, string label, PropType type, object opts, PropertiesFile props)
            {
                _key = key;
                _props = props;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
                Height = 54;
                BackColor = Color.Transparent;
                _label = new Label();
                _label.Text = label;
                _label.Font = Fonts.UI(9.5f);
                _label.ForeColor = Palette.Text;
                _label.AutoSize = false;
                _keyLabel = new Label();
                _keyLabel.Text = key;
                _keyLabel.Font = Fonts.UI(8.5f);
                _keyLabel.ForeColor = Palette.Dim;
                _keyLabel.AutoSize = false;
                Controls.Add(_label);
                Controls.Add(_keyLabel);
                BuildEditor(type, opts);
                LayoutRow();
            }

            // La fila es responsiva: el editor se ancla a la derecha y el
            // texto de la izquierda aprovecha el ancho restante. Sin esto,
            // al reducir la ventana los controles se salian de la tarjeta.
            private void LayoutRow()
            {
                if (_label == null || _keyLabel == null) return;
                int w = Width;
                int editorW = Math.Max(180, (int)(w * 0.44f));
                int editorX = w - 14 - editorW;
                if (editorX < 170) editorX = 170;
                int labelW = Math.Max(90, editorX - 26);
                _label.SetBounds(14, 10, labelW, 16);
                _keyLabel.SetBounds(14, 30, labelW, 14);
                if (_editor != null)
                {
                    if (_editor is ToggleSwitch) _editor.SetBounds(editorX, 15, 44, 24);
                    else _editor.SetBounds(editorX, 13, editorW, 26);
                }
                if (_text != null) _text.Width = Math.Max(40, _editor.Width - 6);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                LayoutRow();
            }

            private void BuildEditor(PropType type, object opts)
            {
                if (type == PropType.Bool)
                {
                    ToggleSwitch sw = new ToggleSwitch();
                    sw.Checked = _props.GetBool(_key, false);
                    sw.CheckedChanged += delegate { _props.SetBool(_key, sw.Checked); };
                    _editor = sw;
                    Controls.Add(sw);
                    return;
                }
                if (type == PropType.Select)
                {
                    string[] arr = (string[])opts;
                    SelectBox cb = new SelectBox(arr, _props.Get(_key, arr.Length > 0 ? arr[0] : ""));
                    cb.SelectedIndexChanged += delegate { _props.Set(_key, cb.SelectedItem); };
                    _editor = cb;
                    Controls.Add(cb);
                    return;
                }
                // Text/Number
                TextBox tb = new TextBox();
                tb.BorderStyle = BorderStyle.None;
                tb.BackColor = Palette.Input;
                tb.ForeColor = Palette.Text;
                tb.Font = Fonts.UI(9.5f);
                tb.Text = _props.Get(_key, Convert.ToString(opts ?? ""));
                tb.Location = new Point(8, 5);
                Panel wrap = new Panel();
                wrap.BackColor = Palette.Input;
                wrap.Controls.Add(tb);
                wrap.Paint += delegate(object s, PaintEventArgs e)
                {
                    Draw.StrokeRound(e.Graphics, new RectangleF(0.5f, 0.5f, wrap.Width - 2f, wrap.Height - 2f), 9f, Palette.Border, 1f);
                };
                tb.TextChanged += delegate { _props.Set(_key, tb.Text); };
                _text = tb;
                _editor = wrap;
                Controls.Add(wrap);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(BackColor);
                RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
                Draw.FillRound(e.Graphics, r, 12f, Palette.CardAlt);
                Draw.StrokeRound(e.Graphics, r, 12f, Palette.BorderSoft, 1f);
            }
        }
    }
}