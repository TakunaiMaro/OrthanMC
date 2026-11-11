using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace McPanel
{
    public class AppConfig
    {
        public string ServerJarFolder { get; set; }
        public string PlayitLocation { get; set; }
        public string AdminNickname { get; set; }
        public string Theme { get; set; } = "default";
        public string Language { get; set; } = "es";

        // Copia local del ZIP de panorama custom elegido por el usuario.
        // Se guarda para poder reconstruir el tema CustomPanorama al arrancar.
        public string PanoramaZip { get; set; }

        // Comprobacion automatica de actualizaciones al iniciar.
        public bool CheckUpdatesOnStartup { get; set; } = true;

        // Configuracion experimental de respaldos en la nube.
        public string CloudProvider { get; set; } = "GoogleDrive";
        public Dictionary<string, string> CloudSettings { get; set; } = new Dictionary<string, string>();

        // Raiz de proyectos: cada subcarpeta puede ser un servidor distinto.
        public string RootServerFolder { get; set; }
        public string ActiveServer { get; set; }

        // Dominio publico de Playit (se cachea para mostrarlo si playitd esta apagado).
        public string LastPlayitDomain { get; set; }

        // Copias de seguridad automaticas.
        public bool BackupEnabled { get; set; }
        public int BackupIntervalMinutes { get; set; } = 60;
        public string BackupMode { get; set; } = "world";
        public bool BackupRecentOnly { get; set; } = true;
        public int BackupRetention { get; set; } = 5;
        public DateTime? LastBackupUtc { get; set; }

        // Metadatos del ultimo server.jar instalado desde el panel.
        public string InstalledJarProvider { get; set; }
        public string InstalledJarVersion { get; set; }
        public string InstalledJarBuild { get; set; }
        public long InstalledJarSize { get; set; }
        public long InstalledJarWriteTicks { get; set; }

        // Metadatos de la pantalla "Perfiles": screenshot elegida, horas de
        // uso y decision sobre volver a preguntar por la imagen.
        public Dictionary<string, ProfileMeta> Profiles { get; set; } = new Dictionary<string, ProfileMeta>();
        
        // Las pruebas pueden aislar su configuracion con MCPANEL_CONFIG, para
        // que nunca sobrescriban la config real del usuario.
        private static string ConfigPath = ResolveConfigPath();

        private static string ResolveConfigPath()
        {
            string custom = Environment.GetEnvironmentVariable("MCPANEL_CONFIG");
            if (!string.IsNullOrWhiteSpace(custom))
            {
                try { return Path.GetFullPath(custom); }
                catch { }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "McPanel", "config.json");
        }
        private static AppConfig _instance;
        
        public static AppConfig Instance
        {
            get
            {
                if (_instance == null) _instance = Load();
                return _instance;
            }
        }
        
        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { }
            return new AppConfig();
        }
        
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
        
        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(ServerJarFolder) && !string.IsNullOrEmpty(AdminNickname);
        }
    }
}
