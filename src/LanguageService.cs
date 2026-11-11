#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace McPanel
{
    // Un idioma se guarda como JSON editable en la carpeta "languages".
    // Las claves son el texto base en espanol; el valor es su traduccion.
    internal sealed class LanguagePack
    {
        public string Code = "";
        public string Name = "";
        public string FilePath = "";
        public readonly Dictionary<string, string> Strings =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> ByNorm =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    internal static class LanguageService
    {
        public const string SpanishCode = "es";

        private static readonly List<LanguagePack> Packs = new List<LanguagePack>();
        private static readonly Dictionary<string, string> CanonicalByNorm =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ReverseByNorm =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ExactCanonical =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> ReverseExact =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static event EventHandler Changed;

        public static LanguagePack Current { get; private set; }
        public static string CurrentCode { get { return Current != null ? Current.Code : SpanishCode; } }
        public static IReadOnlyList<LanguagePack> Available { get { return Packs; } }

        public static string LanguagesDirectory
        {
            get
            {
                string local = Path.Combine(AppContext.BaseDirectory, "languages");
                try
                {
                    if (Directory.Exists(local)) return local;
                }
                catch { }

                foreach (string candidate in new string[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "languages")
                })
                {
                    try
                    {
                        if (Directory.Exists(candidate)) return candidate;
                    }
                    catch { }
                }

                try { Directory.CreateDirectory(local); } catch { }
                return local;
            }
        }

        public static void Initialize(string code)
        {
            Reload();
            LanguagePack pack = Find(code) ?? Find(SpanishCode) ?? (Packs.Count > 0 ? Packs[0] : null);
            Current = pack;
        }

        public static void Reload()
        {
            Packs.Clear();
            CanonicalByNorm.Clear();
            ReverseByNorm.Clear();
            ExactCanonical.Clear();
            ReverseExact.Clear();

            string dir = LanguagesDirectory;
            try
            {
                if (Directory.Exists(dir))
                {
                    foreach (string file in Directory.GetFiles(dir, "*.json").OrderBy(f => f))
                        LoadFile(file);
                }
            }
            catch { }

            Packs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

            LanguagePack spanish = Packs.FirstOrDefault(p =>
                string.Equals(p.Code, SpanishCode, StringComparison.OrdinalIgnoreCase));
            if (spanish == null && Packs.Count > 0) spanish = Packs[0];

            foreach (LanguagePack pack in Packs)
            {
                foreach (KeyValuePair<string, string> pair in pack.Strings)
                {
                    string normKey = Normalize(pair.Key);
                    if (string.IsNullOrEmpty(normKey)) continue;
                    if (!CanonicalByNorm.ContainsKey(normKey)) CanonicalByNorm[normKey] = pair.Key;
                    if (!ExactCanonical.ContainsKey(pair.Key)) ExactCanonical[pair.Key] = pair.Key;
                    pack.ByNorm[normKey] = pair.Value;

                    string normValue = Normalize(pair.Value);
                    if (!string.IsNullOrEmpty(normValue) && normValue != normKey &&
                        !ReverseByNorm.ContainsKey(normValue))
                        ReverseByNorm[normValue] = normKey;
                    if (!string.IsNullOrEmpty(pair.Value) &&
                        !string.Equals(pair.Value, pair.Key, StringComparison.OrdinalIgnoreCase) &&
                        !ReverseExact.ContainsKey(pair.Value))
                        ReverseExact[pair.Value] = pair.Key;
                }
            }

            if (Current != null)
                Current = Find(Current.Code) ?? spanish ?? (Packs.Count > 0 ? Packs[0] : null);
            else
                Current = spanish;
        }

        private static void LoadFile(string path)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) return;

                    LanguagePack pack = new LanguagePack();
                    pack.FilePath = path;
                    pack.Code = GetString(root, "code");
                    pack.Name = GetString(root, "name");
                    if (string.IsNullOrWhiteSpace(pack.Code))
                        pack.Code = Path.GetFileNameWithoutExtension(path);
                    if (string.IsNullOrWhiteSpace(pack.Name))
                        pack.Name = pack.Code;

                    JsonElement strings;
                    if (root.TryGetProperty("strings", out strings) && strings.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty prop in strings.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                                pack.Strings[prop.Name] = prop.Value.GetString();
                        }
                    }

                    if (pack.Strings.Count > 0) Packs.Add(pack);
                }
            }
            catch { }
        }

        private static string GetString(JsonElement root, string name)
        {
            JsonElement value;
            if (root.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? "";
            return "";
        }

        public static LanguagePack Find(string codeOrName)
        {
            if (string.IsNullOrWhiteSpace(codeOrName)) return null;
            string wanted = codeOrName.Trim();
            return Packs.FirstOrDefault(p =>
                string.Equals(p.Code, wanted, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, wanted, StringComparison.OrdinalIgnoreCase));
        }

        public static bool SetLanguage(string code, bool persist)
        {
            LanguagePack pack = Find(code);
            if (pack == null) return false;
            Current = pack;

            if (persist)
            {
                try
                {
                    AppConfig.Instance.Language = pack.Code;
                    AppConfig.Instance.Save();
                }
                catch { }
            }

            ApplyToOpenForms();
            if (Changed != null) Changed(null, EventArgs.Empty);
            return true;
        }

        // Traduce un texto base en espanol. Acepta tambien un texto que ya
        // este en otro idioma y lo vuelve a traducir; asi el cambio es
        // reversible sin guardar el texto original de cada control.
        public static string T(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (Current == null) return text;
            if (text.IndexOf(Brand.Name, StringComparison.OrdinalIgnoreCase) >= 0) return text;
            if (text.IndexOf(Brand.NameUpper, StringComparison.OrdinalIgnoreCase) >= 0) return text;

            string norm = Normalize(text);
            if (string.IsNullOrEmpty(norm)) return text;

            // Primero la coincidencia exacta: evita que dos textos que solo
            // difieren en mayusculas (Temas / TEMAS) compartan traduccion.
            string exact;
            if (Current.Strings.TryGetValue(text, out exact) && !string.IsNullOrEmpty(exact))
                return exact;
            if (ExactCanonical.TryGetValue(text, out exact))
            {
                string translatedExact;
                if (Current.Strings.TryGetValue(exact, out translatedExact) &&
                    !string.IsNullOrEmpty(translatedExact))
                    return translatedExact;
            }
            if (ReverseExact.TryGetValue(text, out exact))
            {
                string translatedExact;
                if (Current.Strings.TryGetValue(exact, out translatedExact) &&
                    !string.IsNullOrEmpty(translatedExact))
                    return translatedExact;
            }

            string keyNorm;
            if (!CanonicalByNorm.ContainsKey(norm))
            {
                if (!ReverseByNorm.TryGetValue(norm, out keyNorm)) return text;
            }
            else
            {
                keyNorm = norm;
            }

            string translated;
            if (Current.ByNorm.TryGetValue(keyNorm, out translated) && !string.IsNullOrEmpty(translated))
                return translated;
            return text;
        }

        public static void ApplyToOpenForms()
        {
            try
            {
                Form[] forms = Application.OpenForms.Cast<Form>().ToArray();
                foreach (Form form in forms)
                {
                    if (form == null || form.IsDisposed) continue;
                    ApplyTo(form);
                }
            }
            catch { }
        }

        public static void ApplyTo(Control root)
        {
            if (root == null || root.IsDisposed) return;

            try
            {
                if (root is ConfigEntry)
                {
                    ConfigEntry entry = (ConfigEntry)root;
                    entry.Title = T(entry.Title);
                    entry.Description = T(entry.Description);
                }
                else if (root is StatusBadge)
                {
                    ((StatusBadge)root).RefreshLanguage();
                }
                else if (root is InputBox)
                {
                    InputBox input = (InputBox)root;
                    input.Placeholder = T(input.Placeholder);
                }
                else if (!(root is TextBoxBase) && !(root is ComboBox) &&
                         !(root is ListBox) && !(root is ListView) &&
                         !(root is TreeView) && !(root is UpDownBase) &&
                         !(root is ScrollBar) && !(root is TrackBar) &&
                         !(root is SliderBar) && !(root is Form) &&
                         !string.IsNullOrEmpty(root.Text))
                {
                    root.Text = T(root.Text);
                }
            }
            catch { }

            foreach (Control child in root.Controls) ApplyTo(child);
        }

        public static string Normalize(string text)
        {
            if (text == null) return "";
            string decomposed = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString().Trim();
        }

        public static bool ImportLanguage(string sourceFile, out string importedCode, out string message)
        {
            importedCode = "";
            message = "";
            try
            {
                string json = File.ReadAllText(sourceFile);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        message = "El archivo no contiene un objeto JSON.";
                        return false;
                    }
                    string code = GetString(root, "code");
                    string name = GetString(root, "name");
                    JsonElement strings;
                    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) ||
                        !root.TryGetProperty("strings", out strings) ||
                        strings.ValueKind != JsonValueKind.Object)
                    {
                        message = "Faltan los campos code, name o strings.";
                        return false;
                    }
                    importedCode = code;
                }

                string dir = LanguagesDirectory;
                Directory.CreateDirectory(dir);
                string target = Path.Combine(dir, Path.GetFileName(sourceFile));
                if (File.Exists(target))
                {
                    string stem = Path.GetFileNameWithoutExtension(sourceFile);
                    target = Path.Combine(dir, stem + "-" +
                        DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                }
                File.Copy(sourceFile, target, false);
                Reload();
                message = "Idioma agregado: " + Path.GetFileName(target);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }
    }
}
