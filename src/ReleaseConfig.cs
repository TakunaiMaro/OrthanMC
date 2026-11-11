#nullable disable
namespace McPanel
{
    // Datos del repositorio publico que usa el sistema de actualizaciones.
    // Si el proyecto se mueve a otra cuenta u organizacion solo hay que
    // cambiar RepoOwner/RepoName aqui.
    internal static class ReleaseConfig
    {
        public const string RepoOwner = "TakunaiMaro";
        public const string RepoName = "OrthanMC";

        public static string RepoDisplay
        {
            get { return RepoOwner + "/" + RepoName; }
        }

        public static string RepoUrl
        {
            get { return "https://github.com/" + RepoOwner + "/" + RepoName; }
        }

        public static string ReleasesUrl
        {
            get { return RepoUrl + "/releases"; }
        }

        public static string LatestReleaseApi
        {
            get { return "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest"; }
        }
    }
}
