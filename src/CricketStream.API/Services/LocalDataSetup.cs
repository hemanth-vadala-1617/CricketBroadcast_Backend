using System.Security.Cryptography;

namespace CricketStream.API.Services;

/// <summary>
/// Makes a fresh clone run with nothing installed: the SQLite database lives in a local data folder (App_Data, not in git) and the
/// JWT signing secret is created on first start and kept in that folder. Anything set in configuration (appsettings, user-secrets,
/// environment variables) still wins, so a deployment can point at its own file or secret.
/// </summary>
public static class LocalDataSetup
{
    public static void Apply(ConfigurationManager config, string contentRoot)
    {
        var dataDir = Path.GetFullPath(config["Data:Path"] ?? "App_Data", contentRoot);
        Directory.CreateDirectory(dataDir);

        if (string.IsNullOrWhiteSpace(config["ConnectionStrings:DefaultConnection"]))
            config["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(dataDir, "cricket.db")}";

        var secret = config["JwtSettings:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            config["JwtSettings:Secret"] = LoadOrCreateSecret(Path.Combine(dataDir, "jwt.secret"));
    }

    private static string LoadOrCreateSecret(string file)
    {
        if (File.Exists(file))
        {
            var existing = File.ReadAllText(file).Trim();
            if (existing.Length >= 32) return existing;
        }
        var created = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        File.WriteAllText(file, created);
        return created;
    }
}
