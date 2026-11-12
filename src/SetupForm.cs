using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace McPanel
{
    // Asistente de primera ejecucion. Recorre Playit.GG y la creacion (o
    // importacion) del primer servidor dentro de RaizServer. Al terminar deja
    // config.json listo para que MainForm arranque sin preguntar de nuevo.
    public sealed class SetupForm : Form
    {
        private enum Step
        {
            Language,
            Intro,
            Download,
            PlayitPath,
            Account,
            Claim,
            ServerMode,
            MultiChoice,
            Move,
            UseExisting,
            Help,
            Completion
        }

        private readonly Panel _content;
        private readonly Panel _footer;
        private readonly RoundButton _back;
        private readonly Stack<Step> _history = new Stack<Step>();
        private Step _step = Step.Language;

        private string _playitPath = "";
        private string _existingSource = "";
        private string _lastSummary = "";

        private List<JarOption> _versionOptions = new List<JarOption>();
        private int _versionsRequest;

        private const int PageWidth = 660;

        public SetupForm()
        {
            Text = Brand.Name + " - Configuracion inicial";
            Icon icon = Brand.AppIcon;
            if (icon != null) Icon = icon;
            ClientSize = new Size(780, 640);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Palette.Bg;
            ForeColor = Palette.Text;
            Font = Fonts.UI(9.5f);

            _playitPath = DetectPlayitPath();

            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.ColumnCount = 1;
            shell.RowCount = 2;
            shell.BackColor = Palette.Bg;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));

            _content = new Panel();
            _content.Dock = DockStyle.Fill;
            _content.BackColor = Palette.Bg;
            _content.Padding = new Padding(38, 26, 38, 12);
            _content.AutoScroll = true;
            _content.Resize += delegate { ResizePage(); };

            _footer = new Panel();
            _footer.Dock = DockStyle.Fill;
            _footer.BackColor = Palette.Bg;
            _footer.Padding = new Padding(38, 8, 38, 16);

            _back = MakeSecondary("ATRAS", "chevron");
            _back.Width = 112;
            _back.Height = 38;
            _back.Visible = false;
            _back.Click += delegate
            {
                if (_history.Count == 0) return;
                _step = _history.Pop();
                BuildStep();
            };
            _footer.Controls.Add(_back);

            shell.Controls.Add(_content, 0, 0);
            shell.Controls.Add(_footer, 0, 1);
            Controls.Add(shell);

            Load += delegate
            {
                WindowFx.DarkTitleBar(Handle);
                BuildStep();
                // La pantalla de idioma se muestra siempre en ingles; el
                // resto del asistente usa el idioma elegido.
                if (_step != Step.Language) LanguageService.ApplyTo(this);
            };
            Shown += delegate { WindowFx.DarkTitleBar(Handle); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowFx.DarkTitleBar(Handle);
        }

        // ------------------------------------------------------------------
        // Navegacion y layout de cada pagina
        // ------------------------------------------------------------------
        private void Go(Step step, bool push = true)
        {
            if (push && _step != step) _history.Push(_step);
            _step = step;
            BuildStep();
        }

        private void BuildStep()
        {
            SuspendLayout();
            while (_content.Controls.Count > 0)
            {
                Control old = _content.Controls[0];
                _content.Controls.RemoveAt(0);
                old.Dispose();
            }

            _back.Visible = _history.Count > 0 && _step != Step.Completion;
            switch (_step)
            {
                case Step.Language: BuildLanguage(); break;
                case Step.Intro: BuildIntro(); break;
                case Step.Download: BuildDownload(); break;
                case Step.PlayitPath: BuildPlayitPath(); break;
                case Step.Account: BuildAccount(); break;
                case Step.Claim: BuildClaim(); break;
                case Step.ServerMode: BuildServerMode(); break;
                case Step.MultiChoice: BuildMultiChoice(); break;
                case Step.Move: BuildMove(); break;
                case Step.UseExisting: BuildUseExisting(); break;
                case Step.Help: BuildHelp(); break;
                case Step.Completion: BuildCompletion(); break;
            }

            ResizePage();
            ResumeLayout();
        }

        private void ResizePage()
        {
            foreach (Control control in _content.Controls)
            {
                if (control is TableLayoutPanel) control.Width = Math.Max(300, _content.ClientSize.Width - _content.Padding.Horizontal - 18);
            }
        }

        private TableLayoutPanel NewPage(string kicker, string title, string description)
        {
            TableLayoutPanel stack = new TableLayoutPanel();
            stack.ColumnCount = 1;
            stack.RowCount = 0;
            stack.Dock = DockStyle.Top;
            stack.AutoSize = true;
            stack.AutoSizeMode = AutoSizeMode.GrowOnly;
            stack.Margin = new Padding(0);
            stack.Padding = new Padding(0);
            stack.BackColor = Color.Transparent;
            stack.Width = PageWidth;
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _content.Controls.Add(stack);

            Add(stack, MakeText(kicker, 8.5f, Palette.Dim, true, false, new Padding(0, 0, 0, 4)));
            Add(stack, MakeText(title, 18f, Palette.Text, true, false, new Padding(0, 0, 0, 6)));
            if (!string.IsNullOrEmpty(description))
                Add(stack, MakeText(description, 9.5f, Palette.Muted, false, true, new Padding(0, 0, 0, 14)));
            return stack;
        }

        private static void Add(TableLayoutPanel stack, Control control)
        {
            int row = stack.RowCount;
            stack.RowCount = row + 1;
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.Fill;
            stack.Controls.Add(control, 0, row);
        }

        private static Label MakeText(string text, float size, Color color, bool bold, bool wrap, Padding margin)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = Fonts.UI(size, bold ? FontStyle.Bold : FontStyle.Regular);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Margin = margin;
            if (wrap)
            {
                label.AutoSize = false;
                label.Width = PageWidth;
                label.Height = MeasureHeight(text, label.Font, PageWidth);
            }
            else
            {
                label.AutoSize = true;
            }
            return label;
        }

        private static int MeasureHeight(string text, Font font, int width)
        {
            Size size = TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return Math.Max(20, size.Height + 4);
        }

        private static RoundButton MakePrimary(string text, string glyph)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Glyph = glyph;
            button.Height = 46;
            button.Margin = new Padding(0, 8, 0, 0);
            button.Font = Fonts.UI(10f, FontStyle.Bold);
            return button;
        }

        private static RoundButton MakeSecondary(string text, string glyph)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Glyph = glyph;
            button.Height = 42;
            button.Margin = new Padding(0, 8, 0, 0);
            button.FillColor = Palette.Card;
            button.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.08f);
            button.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.12f);
            button.TextColor = Palette.Text;
            button.BorderColor = Palette.Border;
            button.Font = Fonts.UI(9.5f, FontStyle.Bold);
            return button;
        }

        private static RoundButton MakeSmallButton(string text)
        {
            RoundButton button = new RoundButton();
            button.Text = text;
            button.Height = 40;
            button.Margin = new Padding(8, 0, 0, 0);
            button.FillColor = Palette.Card;
            button.HoverFill = Palette.Mix(Palette.Card, Color.White, 0.08f);
            button.PressFill = Palette.Mix(Palette.Card, Color.Black, 0.12f);
            button.TextColor = Palette.Text;
            button.BorderColor = Palette.Border;
            button.Font = Fonts.UI(9f, FontStyle.Bold);
            return button;
        }

        private InputBox AddField(TableLayoutPanel stack, string label, string value, string placeholder)
        {
            Add(stack, MakeText(label, 8.5f, Palette.Muted, true, false, new Padding(0, 8, 0, 5)));
            InputBox box = new InputBox();
            box.Text = value ?? "";
            box.Placeholder = placeholder ?? "";
            box.Height = 40;
            box.Margin = new Padding(0, 0, 0, 2);
            Add(stack, box);
            return box;
        }

        private InputBox AddPathField(TableLayoutPanel stack, string label, string value, string placeholder,
            bool folder, Action<string> selected)
        {
            Add(stack, MakeText(label, 8.5f, Palette.Muted, true, false, new Padding(0, 8, 0, 5)));
            TableLayoutPanel row = new TableLayoutPanel();
            row.ColumnCount = 2;
            row.RowCount = 1;
            row.Dock = DockStyle.Fill;
            row.Height = 40;
            row.Margin = new Padding(0, 0, 0, 2);
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            InputBox box = new InputBox();
            box.Text = value ?? "";
            box.Placeholder = placeholder ?? "";
            box.Dock = DockStyle.Fill;
            box.Height = 40;
            box.Margin = new Padding(0);

            RoundButton browse = MakeSmallButton("EXAMINAR");
            browse.Dock = DockStyle.Fill;
            browse.Margin = new Padding(8, 0, 0, 0);
            browse.Click += delegate
            {
                if (folder)
                {
                    using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                    {
                        dialog.Description = "Selecciona una carpeta";
                        if (Directory.Exists(box.Text)) dialog.SelectedPath = box.Text;
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            box.Text = dialog.SelectedPath;
                            if (selected != null) selected(dialog.SelectedPath);
                        }
                    }
                }
                else
                {
                    using (OpenFileDialog dialog = new OpenFileDialog())
                    {
                        dialog.Title = "Selecciona playit.exe";
                        dialog.Filter = "Playit.GG|playit.exe|Ejecutables|*.exe|Todos los archivos|*.*";
                        string current = box.Text.Trim();
                        if (File.Exists(current)) dialog.InitialDirectory = Path.GetDirectoryName(current);
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            box.Text = dialog.FileName;
                            if (selected != null) selected(dialog.FileName);
                        }
                    }
                }
            };

            row.Controls.Add(box, 0, 0);
            row.Controls.Add(browse, 1, 0);
            Add(stack, row);
            return box;
        }

        private static ToggleSwitch AddToggle(TableLayoutPanel stack, string text, bool isChecked)
        {
            TableLayoutPanel row = new TableLayoutPanel();
            row.ColumnCount = 2;
            row.RowCount = 1;
            row.Height = 34;
            row.Margin = new Padding(0, 7, 0, 0);
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label label = MakeText(text, 9.25f, Palette.Text, false, false, new Padding(0));
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AutoEllipsis = true;

            ToggleSwitch toggle = new ToggleSwitch();
            toggle.Checked = isChecked;
            toggle.Dock = DockStyle.Fill;
            toggle.Margin = new Padding(8, 5, 0, 5);

            row.Controls.Add(label, 0, 0);
            row.Controls.Add(toggle, 1, 0);
            Add(stack, row);
            return toggle;
        }

        private static Label AddStatus(TableLayoutPanel stack)
        {
            Label status = MakeText("", 9f, Palette.Muted, false, true, new Padding(0, 8, 0, 0));
            status.Visible = false;
            Add(stack, status);
            return status;
        }

        // ------------------------------------------------------------------
        // Paso 1: Playit.GG
        // ------------------------------------------------------------------
        // Primera pantalla del asistente. Se muestra en ingles a proposito:
        // cualquier usuario debe poder elegir el idioma antes de continuar.
        private void BuildLanguage()
        {
            TableLayoutPanel stack = NewPage("ORTHANC MC  ·  SETUP",
                "Choose your language",
                "Select the language you want to use in the app. You can change it later in " +
                "App Configuration > Language.");

            RoundButton english = MakePrimary("ENGLISH", "check");
            english.Click += delegate { ChooseLanguage("en"); };
            Add(stack, english);

            RoundButton spanish = MakeSecondary("ESPANOL", "globe");
            spanish.Click += delegate { ChooseLanguage("es"); };
            Add(stack, spanish);

            Add(stack, MakeText("The rest of the setup will use the language you select.",
                8.75f, Palette.Dim, false, true, new Padding(0, 12, 0, 0)));
        }

        private void ChooseLanguage(string code)
        {
            bool applied;
            try { applied = LanguageService.SetLanguage(code, true); }
            catch { applied = false; }

            Go(Step.Intro);
            // Cada pagina se construye con textos base; aqui se traducen al
            // idioma recien elegido (incluido el titulo y los botones).
            if (applied) LanguageService.ApplyTo(this);
        }

        private void BuildIntro()
        {
            TableLayoutPanel stack = NewPage("PASO 1 DE 2  ·  PLAYIT.GG",
                "¿Tienes instalado Playit.GG?",
                "Playit.GG crea el tunel publico para que tus jugadores puedan entrar sin abrir puertos. " +
                "Primero configuramos Playit y despues el servidor de Minecraft.");

            RoundButton yes = MakePrimary("SI, YA LO TENGO INSTALADO", "check");
            yes.Click += delegate { Go(Step.PlayitPath); };
            Add(stack, yes);

            RoundButton no = MakeSecondary("NO, AUN NO LO TENGO", "download");
            no.Click += delegate { Go(Step.Download); };
            Add(stack, no);
        }

        private void BuildDownload()
        {
            TableLayoutPanel stack = NewPage("PASO 1 DE 2  ·  PLAYIT.GG",
                "Descarga Playit.GG",
                "Se abrira la pagina oficial de descarga. Instala la version para Windows y vuelve aqui " +
                "cuando termine. No cierres este asistente.");

            RoundButton open = MakePrimary("ABRIR PAGINA DE DESCARGA", "external");
            open.Click += delegate { OpenUrl("https://playit.gg/download/windows"); };
            Add(stack, open);

            RoundButton done = MakeSecondary("YA LO INSTALE, CONTINUAR", "arrow-right");
            done.Click += delegate { Go(Step.PlayitPath); };
            Add(stack, done);

            Add(stack, MakeText("Nota: el panel solo necesita playit.exe; el servicio playitd se instala junto con la app.",
                8.75f, Palette.Dim, false, true, new Padding(0, 14, 0, 0)));
        }

        private void BuildPlayitPath()
        {
            // Si Playit se instala despues de abrir el asistente, se vuelve a
            // detectar al entrar a esta pagina para no pedir reiniciar.
            if (string.IsNullOrWhiteSpace(_playitPath) || !File.Exists(_playitPath))
                _playitPath = DetectPlayitPath();

            TableLayoutPanel stack = NewPage("PASO 1 DE 2  ·  PLAYIT.GG",
                "Ubicacion de Playit.GG",
                "Se usa playit.exe para iniciar el tunel. La carpeta predeterminada es " +
                "C:\\Program Files\\playit_gg\\bin.");

            Label status = MakeText("", 9.5f, Palette.Amber, true, true, new Padding(0, 0, 0, 8));
            Add(stack, status);

            InputBox field = AddPathField(stack, "RUTA DE PLAYIT.EXE", _playitPath,
                @"Ej: C:\Program Files\playit_gg\bin\playit.exe", false,
                delegate(string chosen) { _playitPath = chosen; });
            field.TextChanged += delegate { _playitPath = field.Text.Trim(); };

            Action updateStatus = delegate
            {
                string path = field.Text.Trim();
                bool found = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
                status.ForeColor = found ? Palette.Accent : Palette.Amber;
                status.Text = found
                    ? "Detectado en: " + path
                    : (string.IsNullOrWhiteSpace(path)
                        ? "No se encontro playit.exe. Descargalo o selecciona el archivo."
                        : "No existe el archivo indicado: " + path);
            };
            updateStatus();

            RoundButton use = MakePrimary("CONTINUAR CON ESTA RUTA", "arrow-right");
            use.Click += delegate
            {
                string path = field.Text.Trim();
                if (!File.Exists(path))
                {
                    MessageDialog.Info(this, "Playit.GG",
                        "No se encontro playit.exe en:\r\n\r\n" +
                        (path.Length == 0 ? "(ruta vacia)" : path) +
                        "\r\n\r\nSelecciona el archivo o descarga Playit.GG.");
                    return;
                }
                _playitPath = path;
                Go(Step.Account);
            };
            Add(stack, use);

            RoundButton choose = MakeSecondary("ELEGIR OTRO PLAYIT.EXE", "folder");
            choose.Click += delegate
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Selecciona playit.exe";
                    dialog.Filter = "Playit.GG|playit.exe|Ejecutables|*.exe|Todos los archivos|*.*";
                    string current = field.Text.Trim();
                    if (File.Exists(current)) dialog.InitialDirectory = Path.GetDirectoryName(current);
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        _playitPath = dialog.FileName;
                        field.Text = dialog.FileName;
                        updateStatus();
                    }
                }
            };
            Add(stack, choose);

            RoundButton download = MakeSecondary("DESCARGAR PLAYIT.GG", "download");
            download.Click += delegate { OpenUrl("https://playit.gg/download/windows"); };
            Add(stack, download);

            Add(stack, MakeText("Si eliges otra ubicacion, selecciona directamente el archivo playit.exe. " +
                "El servicio playitd se instala junto con la app.",
                8.75f, Palette.Dim, false, true, new Padding(0, 12, 0, 0)));
        }

        private void BuildAccount()
        {
            TableLayoutPanel stack = NewPage("PASO 1 DE 2  ·  PLAYIT.GG",
                "¿Ya tienes tu cuenta vinculada?",
                "Playit necesita una cuenta vinculada para crear el agente y el tunel de tu servidor.");

            RoundButton yes = MakePrimary("SI, MI CUENTA YA ESTA VINCULADA", "check");
            yes.Click += delegate { Go(Step.ServerMode); };
            Add(stack, yes);

            RoundButton no = MakeSecondary("NO, AUN NO LA HE VINCULADO", "link");
            no.Click += delegate { Go(Step.Claim); };
            Add(stack, no);
        }

        private void BuildClaim()
        {
            TableLayoutPanel stack = NewPage("PASO 1 DE 2  ·  PLAYIT.GG",
                "Vincula tu cuenta de Playit.GG",
                "Abre Playit.GG y sigue el enlace que muestra la app. Tendras que abrir un enlace parecido a " +
                "playit.gg/claim/xxxxx, iniciar sesion y autorizar el dispositivo.");

            RoundButton openApp = MakePrimary("ABRIR PLAYIT.GG", "external");
            openApp.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(_playitPath) && File.Exists(_playitPath))
                {
                    try { Process.Start(new ProcessStartInfo { FileName = _playitPath, UseShellExecute = true }); }
                    catch (Exception ex) { MessageDialog.Info(this, "Playit.GG", ex.Message); }
                }
                else
                {
                    OpenUrl("https://playit.gg/download/windows");
                }
            };
            Add(stack, openApp);

            RoundButton done = MakeSecondary("YA VINCULE MI CUENTA", "check");
            done.Click += delegate { Go(Step.ServerMode); };
            Add(stack, done);

            Add(stack, MakeText("Pasos: 1) abre Playit.GG, 2) copia o abre el enlace playit.gg/claim/xxxxx, " +
                "3) inicia sesion y autoriza el equipo, 4) vuelve aqui y continua.",
                8.75f, Palette.Muted, false, true, new Padding(0, 14, 0, 0)));
        }

        // ------------------------------------------------------------------
        // Paso 2: servidor
        // ------------------------------------------------------------------
        private void BuildServerMode()
        {
            TableLayoutPanel stack = NewPage("PASO 2 DE 2  ·  SERVIDOR",
                "¿Ya tienes un server.jar con su configuracion basica?",
                "Si ya tienes un servidor funcionando podemos importarlo. Si empiezas desde cero, " +
                "te ayudamos a descargar la version, Java y aceptar el EULA.");

            RoundButton yes = MakePrimary("SI, YA TENGO UN SERVIDOR", "folder");
            yes.Click += delegate { Go(Step.MultiChoice); };
            Add(stack, yes);

            RoundButton no = MakeSecondary("NO, QUIERO QUE ME AYUDEN", "download");
            no.Click += delegate { Go(Step.Help); };
            Add(stack, no);

            Add(stack, MakeText("RaizServer: " + ServerProjects.Root,
                9f, Palette.Dim, false, false, new Padding(0, 16, 0, 0)));
        }

        private void BuildMultiChoice()
        {
            TableLayoutPanel stack = NewPage("PASO 2 DE 2  ·  SERVIDOR",
                "¿Quieres una instalacion con varios servidores?",
                "Podemos guardar tus proyectos dentro de " + ServerProjects.Root +
                ". Asi podras cambiar de servidor en cualquier momento desde la barra de titulo.");

            RoundButton yes = MakePrimary("SI, ORGANIZAR MIS SERVIDORES", "folder");
            yes.Click += delegate { Go(Step.Move); };
            Add(stack, yes);

            RoundButton no = MakeSecondary("NO, USAR MI CARPETA ACTUAL", "arrow-right");
            no.Click += delegate { Go(Step.UseExisting); };
            Add(stack, no);
        }

        private void BuildMove()
        {
            TableLayoutPanel stack = NewPage("PASO 2 DE 2  ·  SERVIDOR",
                "Mueve tu servidor a RaizServer",
                "Se movera la carpeta completa (mundo, plugins, configuracion y server.jar) a " +
                ServerProjects.Root + ". Despues podras agregar mas servidores ahi.");

            InputBox source = AddPathField(stack, "CARPETA ACTUAL DEL SERVIDOR", _existingSource,
                @"Ej: C:\Servers\Casual", true, delegate(string chosen)
                {
                    _existingSource = chosen;
                    InputBox nameBox = (InputBox)stack.Tag;
                    if (nameBox != null && string.IsNullOrWhiteSpace(nameBox.Text))
                        nameBox.Text = Path.GetFileName(chosen.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                });
            source.TextChanged += delegate { _existingSource = source.Text.Trim(); };

            InputBox name = AddField(stack, "NOMBRE DEL SERVIDOR (CARPETA DESTINO)",
                string.IsNullOrWhiteSpace(_existingSource) ? "" :
                Path.GetFileName(_existingSource.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                "Ej: Survival");
            stack.Tag = name;

            // Si el usuario escribe la ruta en lugar de usar EXAMINAR, el
            // nombre destino se rellena solo mientras siga vacio.
            source.TextChanged += delegate
            {
                string text = source.Text.Trim();
                if (name == null || !string.IsNullOrWhiteSpace(name.Text) || text.Length == 0) return;
                string leaf = Path.GetFileName(text.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(leaf)) name.Text = leaf;
            };

            Label status = AddStatus(stack);
            RoundButton move = MakePrimary("MOVER SERVIDOR Y CONTINUAR", "folder");
            move.Click += async delegate
            {
                string from = source.Text.Trim();
                string serverName = name.Text.Trim();
                if (!ValidateSource(from, out string sourceError))
                {
                    MessageDialog.Info(this, "Servidor", sourceError);
                    return;
                }
                if (!ValidateName(serverName, out string nameError))
                {
                    MessageDialog.Info(this, "Servidor", nameError);
                    return;
                }

                string root = ServerProjects.Root;
                if (IsUnderTemp(root))
                {
                    MessageDialog.Info(this, "Servidor",
                        "La Raiz de servidores apunta a una carpeta temporal:\r\n\r\n" + root +
                        "\r\n\r\nMover un servidor ahi puede hacer que se borre. " +
                        "Corrige la Raiz de servidores antes de continuar.");
                    return;
                }
                string target = Path.Combine(root, serverName);
                if (PathsEqual(from, target))
                {
                    MessageDialog.Info(this, "Servidor", "La carpeta de origen ya es la carpeta destino.");
                    return;
                }
                if (IsInside(from, target) || IsInside(target, from))
                {
                    MessageDialog.Info(this, "Servidor",
                        "La carpeta de origen no puede ser RaizServer ni contener la carpeta destino.");
                    return;
                }
                if (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length > 0)
                {
                    MessageDialog.Info(this, "Servidor", "Ya existe una carpeta con ese nombre en RaizServer.");
                    return;
                }
                if (!MessageDialog.Confirm(this, "Mover servidor",
                    "Se movera toda la carpeta:\r\n\r\n" + from + "\r\n\r\nhacia:\r\n\r\n" + target +
                    "\r\n\r\n¿Continuar?", "Mover", false))
                    return;

                move.Enabled = false;
                _back.Enabled = false;
                status.Visible = true;
                status.Text = "Moviendo servidor...";
                try
                {
                    await Task.Run(delegate { MoveDirectory(from, target); });
                    SaveConfig(target, serverName, null, null);
                    _lastSummary = "Servidor importado en:\r\n" + target;
                    Go(Step.Completion);
                }
                catch (Exception ex)
                {
                    MessageDialog.Info(this, "Mover servidor", ex.Message);
                }
                finally
                {
                    move.Enabled = true;
                    _back.Enabled = true;
                    if (!status.IsDisposed) status.Visible = false;
                }
            };
            Add(stack, move);
        }

        private void BuildUseExisting()
        {
            TableLayoutPanel stack = NewPage("PASO 2 DE 2  ·  SERVIDOR",
                "Usa tu servidor actual",
                "El panel trabajara directamente en la carpeta donde ya tienes server.jar. " +
                "Tambien puedes importarlo luego desde 'Cambiar de server'.");

            InputBox source = AddPathField(stack, "CARPETA DEL SERVIDOR", _existingSource,
                @"Ej: C:\Servers\Casual", true, delegate(string chosen) { _existingSource = chosen; });
            source.TextChanged += delegate { _existingSource = source.Text.Trim(); };

            RoundButton use = MakePrimary("USAR ESTE SERVIDOR", "check");
            use.Click += delegate
            {
                string folder = source.Text.Trim();
                if (!ValidateSource(folder, out string error))
                {
                    MessageDialog.Info(this, "Servidor", error);
                    return;
                }
                string name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                SaveConfig(folder, name, null, null);
                _lastSummary = "Servidor actual:\r\n" + folder;
                Go(Step.Completion);
            };
            Add(stack, use);
        }

        private void BuildHelp()
        {
            TableLayoutPanel stack = NewPage("PASO 2 DE 2  ·  SERVIDOR",
                "Crea tu primer servidor",
                "Se creara una carpeta dentro de RaizServer (" + ServerProjects.Root +
                "). Elegimos la version, descargamos el server.jar y preparamos el EULA.");

            InputBox name = AddField(stack, "NOMBRE DEL SERVIDOR (CARPETA)", "Survival", "Ej: Survival");

            Add(stack, MakeText("VERSION DE MINECRAFT", 8.5f, Palette.Muted, true, false, new Padding(0, 8, 0, 5)));
            SelectBox version = new SelectBox(new string[] { "Cargando versiones..." }, "Cargando versiones...");
            version.Font = Fonts.UI(9.5f);
            version.Height = 40;
            version.Margin = new Padding(0, 0, 0, 4);
            version.Enabled = false;
            Add(stack, version);

            ToggleSwitch eula = AddToggle(stack,
                "Acepto el EULA de Minecraft (obligatorio para iniciar el servidor).", false);
            RoundButton readEula = MakeSecondary("LEER EULA", "external");
            readEula.Height = 36;
            readEula.Click += delegate { OpenUrl("https://aka.ms/MinecraftEULA"); };
            Add(stack, readEula);

            ToggleSwitch java = AddToggle(stack,
                "Descargar Java automaticamente si esta version lo necesita.", true);

            Label status = AddStatus(stack);
            UsageBar progress = new UsageBar();
            progress.Height = 8;
            progress.Visible = false;
            progress.Margin = new Padding(0, 10, 0, 4);
            Add(stack, progress);

            Action updateCreate = null;
            RoundButton retry = MakeSecondary("REINTENTAR CARGA DE VERSIONES", "restart");
            retry.Visible = false;
            retry.Click += async delegate
            {
                retry.Visible = false;
                await LoadVersionsAsync(version, status, retry);
                if (updateCreate != null) updateCreate();
            };
            Add(stack, retry);

            RoundButton create = MakePrimary("CREAR SERVIDOR Y DESCARGAR", "download");
            create.Enabled = false;

            // El boton solo se habilita cuando hay versiones cargadas y el
            // EULA fue aceptado; sin esto el asistente nunca podia continuar.
            updateCreate = delegate
            {
                if (!create.IsDisposed) create.Enabled = _versionOptions.Count > 0 && eula.Checked;
            };
            eula.CheckedChanged += delegate { if (updateCreate != null) updateCreate(); };

            create.Click += async delegate
            {
                if (version.SelectedIndex < 0 || _versionOptions.Count == 0 ||
                    version.SelectedIndex >= _versionOptions.Count)
                {
                    MessageDialog.Info(this, "Servidor", "Selecciona una version de Minecraft.");
                    return;
                }
                await CreateServerAsync(name.Text.Trim(),
                    _versionOptions[version.SelectedIndex], eula.Checked, java.Checked,
                    create, progress, status);
            };
            Add(stack, create);

            Add(stack, MakeText("El servidor se creara con eula=true, server.properties basico y run.bat. " +
                "Podras cambiarlo todo desde el panel.",
                8.75f, Palette.Dim, false, true, new Padding(0, 10, 0, 0)));

            BeginInvoke(new Action(async delegate
            {
                await LoadVersionsAsync(version, status, retry);
                updateCreate();
            }));
        }

        private void BuildCompletion()
        {
            TableLayoutPanel stack = NewPage("CONFIGURACION COMPLETA",
                "¡Todo listo!",
                "El panel ya tiene todo lo necesario para iniciar tu servidor y abrir el tunel de Playit.");

            Add(stack, MakeText(_lastSummary, 9.5f, Palette.Muted, false, true, new Padding(0, 0, 0, 12)));

            RoundButton open = MakePrimary("ABRIR EL PANEL", "play");
            open.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Add(stack, open);

            RoundButton folder = MakeSecondary("ABRIR CARPETA DEL SERVIDOR", "folder");
            folder.Click += delegate
            {
                try
                {
                    string dir = AppConfig.Instance.ServerJarFolder;
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                }
                catch { }
            };
            Add(stack, folder);
        }

        // ------------------------------------------------------------------
        // Descarga y creacion del servidor
        // ------------------------------------------------------------------
        private async Task LoadVersionsAsync(SelectBox combo, Label status, RoundButton retry)
        {
            if (combo == null || combo.IsDisposed || status == null || status.IsDisposed) return;
            int request = ++_versionsRequest;
            status.Visible = true;
            status.Text = "Consultando versiones de Minecraft...";
            try
            {
                JarProvider provider = JarCatalog.GetVanillaProvider(false);
                List<JarOption> options = await provider.List();
                if (request != _versionsRequest || combo.IsDisposed) return;
                _versionOptions = options ?? new List<JarOption>();
                List<string> names = new List<string>();
                foreach (JarOption option in _versionOptions)
                    names.Add(option.MinecraftVersion);
                combo.SetItems(names.ToArray(), names.Count > 0 ? names[0] : "");
                combo.Enabled = _versionOptions.Count > 0;
                if (_versionOptions.Count > 0)
                {
                    status.Visible = false;
                    retry.Visible = false;
                }
                else
                {
                    combo.SetItems(new string[] { "Sin versiones disponibles" }, "Sin versiones disponibles");
                    status.Text = "No se encontraron versiones. Revisa tu conexion.";
                    retry.Visible = true;
                }
            }
            catch (Exception ex)
            {
                if (request != _versionsRequest || combo.IsDisposed) return;
                combo.SetItems(new string[] { "Error de conexion" }, "Error de conexion");
                combo.Enabled = false;
                status.Visible = true;
                status.Text = "No se pudieron cargar las versiones: " + ex.Message;
                retry.Visible = true;
            }
        }

        private async Task CreateServerAsync(string serverName, JarOption option,
            bool acceptedEula, bool downloadJava, RoundButton create, UsageBar progress, Label status)
        {
            if (!acceptedEula)
            {
                MessageDialog.Info(this, "EULA", "Debes aceptar el EULA de Minecraft para crear el servidor.");
                return;
            }
            if (!ValidateName(serverName, out string nameError))
            {
                MessageDialog.Info(this, "Servidor", nameError);
                return;
            }

            string root = ServerProjects.Root;
            string target = Path.Combine(root, serverName);
            bool targetWasEmpty = !Directory.Exists(target) ||
                Directory.GetFileSystemEntries(target).Length == 0;
            if (!targetWasEmpty)
            {
                MessageDialog.Info(this, "Servidor", "Ya existe una carpeta con ese nombre en RaizServer. Elige otro nombre.");
                return;
            }

            create.Enabled = false;
            _back.Enabled = false;
            progress.Visible = true;
            status.Visible = true;
            SetProgress(progress, status, 0, "Preparando descarga...");

            try
            {
                Directory.CreateDirectory(root);
                Directory.CreateDirectory(target);
                string jarPath = Path.Combine(target, "server.jar");
                JarProvider provider = JarCatalog.GetVanillaProvider(false);
                JarDownload download = await provider.Resolve(option);
                if (download == null || string.IsNullOrWhiteSpace(download.Url))
                    throw new Exception("No se pudo obtener la descarga del servidor.");

                SetProgress(progress, status, 0, "Descargando Vanilla " + option.MinecraftVersion + "...");
                await HttpDownloader.DownloadAsync(download.Url, jarPath, download.Size,
                    download.Hash, download.HashAlgorithm,
                    delegate(double fraction, string text) { SafeProgress(progress, status, fraction, text); },
                    System.Threading.CancellationToken.None);
                SetProgress(progress, status, 1, "server.jar descargado y verificado.");

                int javaMajor = download.JavaMajor;
                if (javaMajor <= 0)
                {
                    try { javaMajor = JarInspector.Inspect(jarPath).JavaMajor; } catch { }
                }

                if (javaMajor > 0 && JavaRuntimeManager.Find(javaMajor) == null)
                {
                    if (downloadJava)
                    {
                        SetProgress(progress, status, 0, "Buscando Java " + javaMajor + "...");
                        JavaAsset asset = await JavaRuntimeManager.GetAssetAsync(javaMajor);
                        if (asset == null)
                            throw new Exception("No se encontro un JDK " + javaMajor + " para Windows x64.");
                        status.Text = "Descargando Java " + javaMajor + " (" + Utils.HumanBytes(asset.Size) + ")...";
                        await JavaRuntimeManager.DownloadAsync(asset,
                            delegate(double fraction, string text) { SafeProgress(progress, status, fraction, text); },
                            System.Threading.CancellationToken.None);
                        SetProgress(progress, status, 1, "Java " + javaMajor + " instalado.");
                    }
                    else
                    {
                        MessageDialog.Info(this, "Java " + javaMajor,
                            "El servidor necesita Java " + javaMajor + ", pero no se descargo. " +
                            "Podras instalarlo despues desde 'Versiones del Servidor'.");
                    }
                }

                WriteInitialFiles(target, serverName, javaMajor, option.MinecraftVersion);
                SaveConfig(target, serverName, option, download);
                _lastSummary = "Servidor: " + serverName + "\r\n" +
                    "Carpeta: " + target + "\r\n" +
                    "Minecraft: " + option.MinecraftVersion + " (Vanilla)\r\n" +
                    "Playit.GG: " + _playitPath;
                Go(Step.Completion);
            }
            catch (Exception ex)
            {
                // No dejar una carpeta a medias que impida reintentar con el
                // mismo nombre. Solo se limpia si estaba vacia antes.
                try
                {
                    if (targetWasEmpty && Directory.Exists(target))
                        Directory.Delete(target, true);
                }
                catch { }
                MessageDialog.Info(this, "Crear servidor", ex.Message);
                SetProgress(progress, status, 0, "No se pudo completar la creacion del servidor.");
            }
            finally
            {
                create.Enabled = true;
                _back.Enabled = true;
                if (!progress.IsDisposed) progress.Visible = false;
                if (!status.IsDisposed) status.Visible = true;
            }
        }

        private void SafeProgress(UsageBar bar, Label status, double fraction, string text)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(delegate { SetProgress(bar, status, fraction, text); }));
            }
            catch { }
        }

        private static void SetProgress(UsageBar bar, Label status, double fraction, string text)
        {
            if (bar != null && !bar.IsDisposed) bar.Value = fraction;
            if (status != null && !status.IsDisposed)
            {
                status.Visible = true;
                status.Text = text ?? "";
            }
        }

        private static void WriteInitialFiles(string dir, string serverName, int javaMajor, string version)
        {
            string eula = Path.Combine(dir, "eula.txt");
            File.WriteAllText(eula, "eula=true\r\n", new UTF8Encoding(false));

            string props = Path.Combine(dir, "server.properties");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("#Minecraft server properties");
            sb.AppendLine("#Generado por " + Brand.Name);
            sb.AppendLine("motd=" + (string.IsNullOrWhiteSpace(serverName) ? "Minecraft Server" : serverName) + " - MC Panel");
            sb.AppendLine("server-port=25565");
            sb.AppendLine("max-players=20");
            sb.AppendLine("gamemode=survival");
            sb.AppendLine("difficulty=easy");
            sb.AppendLine("online-mode=true");
            sb.AppendLine("pvp=true");
            sb.AppendLine("enable-command-block=false");
            File.WriteAllText(props, sb.ToString(), new UTF8Encoding(false));

            string java = "java";
            if (javaMajor > 0)
            {
                try
                {
                    string found = JavaRuntimeManager.Find(javaMajor);
                    if (!string.IsNullOrWhiteSpace(found)) java = "\"" + found + "\"";
                }
                catch { }
            }
            string bat = Path.Combine(dir, "run.bat");
            File.WriteAllText(bat,
                "@echo off\r\ncd /d \"%~dp0\"\r\n" + java + " -Xmx4096M -Xms4096M -jar server.jar nogui\r\npause\r\n",
                new UTF8Encoding(false));
        }

        private void SaveConfig(string serverFolder, string serverName,
            JarOption option, JarDownload download)
        {
            AppConfig config = AppConfig.Instance;
            config.ServerJarFolder = serverFolder;
            config.RootServerFolder = ServerProjects.Root;
            config.ActiveServer = string.IsNullOrWhiteSpace(serverName)
                ? Path.GetFileName(serverFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : serverName;
            config.PlayitLocation = _playitPath;
            if (string.IsNullOrEmpty(config.Theme)) config.Theme = "default";
            if (option != null)
            {
                config.InstalledJarProvider = option.ProviderId;
                config.InstalledJarVersion = option.MinecraftVersion;
                config.InstalledJarBuild = option.Build ?? "";
            }
            try
            {
                FileInfo info = new FileInfo(Path.Combine(serverFolder, "server.jar"));
                if (info.Exists)
                {
                    config.InstalledJarSize = info.Length;
                    config.InstalledJarWriteTicks = info.LastWriteTimeUtc.Ticks;
                }
            }
            catch { }
            config.Save();
        }

        // ------------------------------------------------------------------
        // Utilidades de rutas y validacion
        // ------------------------------------------------------------------
        private static bool ValidateName(string name, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(name))
            {
                error = "Escribe un nombre para el servidor.";
                return false;
            }
            if (name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = "El nombre contiene caracteres no permitidos.";
                return false;
            }
            return true;
        }

        private static bool ValidateSource(string folder, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(folder))
            {
                error = "Selecciona la carpeta del servidor.";
                return false;
            }
            if (!Directory.Exists(folder))
            {
                error = "La carpeta no existe:\r\n" + folder;
                return false;
            }
            if (!File.Exists(Path.Combine(folder, "server.jar")))
            {
                error = "No se encontro server.jar en:\r\n" + folder;
                return false;
            }
            return true;
        }

        private static bool PathsEqual(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // Devuelve true cuando candidate esta dentro de parent.
        private static bool IsInside(string parent, string candidate)
        {
            try
            {
                string p = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string c = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        // Una carpeta temporal (o dentro del perfil temporal) nunca debe usarse
        // como raiz de servidores: se limpia sola y arrastra los mundos.
        private static bool IsUnderTemp(string path)
        {
            try
            {
                string candidate = NormalizePath(path);
                string temp = NormalizePath(Path.GetTempPath());
                return string.Equals(candidate, temp, StringComparison.OrdinalIgnoreCase)
                    || IsInside(temp, candidate);
            }
            catch { return false; }
        }

        private static void MoveDirectory(string source, string target)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                throw new Exception("La ruta de origen o destino esta vacia.");

            string s = NormalizePath(source);
            string t = NormalizePath(target);
            if (string.Equals(s, t, StringComparison.OrdinalIgnoreCase))
                throw new Exception("La carpeta de origen y la de destino son la misma.");
            if (IsInside(s, t))
                throw new Exception("La carpeta destino no puede estar dentro de la carpeta de origen.");
            if (IsInside(t, s))
                throw new Exception("La carpeta de origen no puede estar dentro de la carpeta destino.");
            if (IsUnderTemp(t))
                throw new Exception("La carpeta destino esta en una ubicacion temporal y no es segura:\r\n" + t);

            if (!Directory.Exists(s)) throw new Exception("La carpeta de origen no existe.");
            if (Directory.Exists(t) && Directory.GetFileSystemEntries(t).Length > 0)
                throw new Exception("La carpeta destino ya existe y no esta vacia: " + t);

            string parent = Path.GetDirectoryName(t);
            if (string.IsNullOrWhiteSpace(parent))
                throw new Exception("La ruta destino no es valida: " + t);

            try
            {
                Directory.CreateDirectory(parent);
                Directory.Move(s, t);
                return;
            }
            catch (IOException)
            {
                // Distinto volumen o bloqueo: se copia, se verifica y solo
                // entonces se intenta borrar el origen.
            }

            CopyDirectory(s, t);
            if (!SameContent(s, t))
                throw new Exception("La copia quedo incompleta. No se borro la carpeta de origen:\r\n" + s);

            try
            {
                Directory.Delete(s, true);
            }
            catch (Exception ex)
            {
                throw new Exception("El servidor se copio a:\r\n" + t +
                    "\r\npero no se pudo borrar el origen:\r\n" + s + "\r\n\r\n" + ex.Message);
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), false);
        }

        // Compara rutas relativas y tamanos para no borrar el origen si la
        // copia fallo a medias.
        private static bool SameContent(string source, string target)
        {
            try
            {
                Dictionary<string, long> a = MapFiles(source);
                Dictionary<string, long> b = MapFiles(target);
                if (a.Count != b.Count) return false;
                foreach (KeyValuePair<string, long> pair in a)
                {
                    long size;
                    if (!b.TryGetValue(pair.Key, out size) || size != pair.Value) return false;
                }
                return true;
            }
            catch { return false; }
        }

        private static Dictionary<string, long> MapFiles(string root)
        {
            Dictionary<string, long> map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            string basePath = NormalizePath(root);
            foreach (string file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(basePath.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                map[relative] = new FileInfo(file).Length;
            }
            return map;
        }

        private static string DetectPlayitPath()
        {
            try
            {
                AppConfig config = AppConfig.Instance;
                if (!string.IsNullOrWhiteSpace(config.PlayitLocation) && File.Exists(config.PlayitLocation))
                    return config.PlayitLocation;
            }
            catch { }
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "playit_gg", "bin", "playit.exe"),
                @"C:\Program Files\playit_gg\bin\playit.exe"
            };
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return "";
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch { }
        }
    }
}
