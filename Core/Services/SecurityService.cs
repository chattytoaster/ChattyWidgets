using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChattyWidgets.Core.Models;

namespace ChattyWidgets.Core.Services;

public enum WidgetTrustLevel
{
    Official,           // ✦ Официальный (ChattyWidgets)
    VerifiedCommunity,  // ✓ @author (GitHub)
    ThirdParty,         // ⚠ Сторонний виджет (не проверен)
    SpoofAttempt        // ⛔ Подделка (недоверенный источник)
}

public class WidgetTrustInfo
{
    public WidgetTrustLevel Level { get; set; } = WidgetTrustLevel.ThirdParty;
    public string DisplayAuthor { get; set; } = "Неизвестный";
    public string BadgeText { get; set; } = "⚠ Сторонний";
    public string BadgeBg { get; set; } = "#451A03";
    public string BadgeFg { get; set; } = "#FBBF24";
    public bool IsVerified { get; set; } = false;
    public string WarningMessage { get; set; } = string.Empty;
}

public class PackageSignatureMeta
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("authorGithub")]
    public string AuthorGithub { get; set; } = string.Empty;

    [JsonPropertyName("isOfficial")]
    public bool IsOfficial { get; set; } = false;

    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public class SecurityService
{
    private static SecurityService? _instance;
    public static SecurityService Instance => _instance ??= new SecurityService();

    // Embedded Official Public Key of ChattyWidgets Platform
    public const string OfficialPublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAoWGXGpo+5KBPU1RbpPc3
ruFa33vm41xhS3Kb2s/YsTO8iT6tzP2QZ3Q6mjJi6YPaCoP+HDoJE+x9wuF/ZT4L
yuc+7gy/T0Nr3G5uSZwNxJT8fPE2aK7HZrpHuNKw6l43POsWQJFMwEPWd2n6AJTW
blKeiXtyvknEZM1GqYEvaArw+VFnkHb7gHr9JebkUSs3Ccf10mihfcDMxgGt9roo
mfwepPh4vLqbYpk5dTJnG45QYI02squK7LXtKLUHF+R9PFspmbwFY6kJNL0i+sAc
eERxo3Zh0OJyr+kP1Dn4FmSeLc/OBskAX/S1xR7P0jSaAVhoOAryJ1GYFwAEF9bN
2wIDAQAB
-----END PUBLIC KEY-----";

    public const string OfficialPrefix = "com.chatty.";

    private SecurityService()
    {
    }

    /// <summary>
    /// Evaluates the authenticity and trust level of an installed widget or package.
    /// </summary>
    public WidgetTrustInfo EvaluateWidget(WidgetManifest manifest, string widgetDir)
    {
        var info = new WidgetTrustInfo();

        bool claimsOfficialId = manifest.Id.StartsWith(OfficialPrefix, StringComparison.OrdinalIgnoreCase);
        bool claimsOfficialAuthor = manifest.Author.Contains("ChattyWidgets", StringComparison.OrdinalIgnoreCase) ||
                                    manifest.Author.Contains("chattytoaster", StringComparison.OrdinalIgnoreCase);

        // 1. Check for bundled starter widget
        string starterDir = Path.Combine(StoreService.StarterPackagesDir, manifest.Id);
        bool isBundledStarter = Directory.Exists(starterDir);

        // 2. Read embedded chatty_signature.json if present
        PackageSignatureMeta? meta = null;
        string sigPath = Path.Combine(widgetDir, "chatty_signature.json");
        if (File.Exists(sigPath))
        {
            try
            {
                string json = File.ReadAllText(sigPath);
                meta = JsonSerializer.Deserialize<PackageSignatureMeta>(json);
            }
            catch { }
        }

        bool hasValidSignature = false;
        if (meta != null && !string.IsNullOrEmpty(meta.Signature) && meta.IsOfficial)
        {
            hasValidSignature = VerifySignature(meta.Id, meta.Version, meta.ContentHash, meta.Signature);
        }

        // --- TIER 1: VERIFIED OFFICIAL ---
        if ((hasValidSignature && claimsOfficialId) || (isBundledStarter && claimsOfficialId))
        {
            info.Level = WidgetTrustLevel.Official;
            info.DisplayAuthor = "ChattyWidgets";
            info.BadgeText = "✦ Официальный";
            info.BadgeBg = "#1E2A4A";
            info.BadgeFg = "#60A5FA";
            info.IsVerified = true;
            return info;
        }

        // --- TIER 0: SPOOF ATTEMPT DETECTED! ---
        // Sideloaded widget falsely claiming com.chatty.* or ChattyWidgets author without official signature!
        if (claimsOfficialId || claimsOfficialAuthor)
        {
            info.Level = WidgetTrustLevel.SpoofAttempt;
            info.DisplayAuthor = "Неизвестный источник (подделка)";
            info.BadgeText = "⛔ Подделка (не проверен)";
            info.BadgeBg = "#450A0A";
            info.BadgeFg = "#EF4444";
            info.IsVerified = false;
            info.WarningMessage = "Внимание: данный виджет заявляет себя как официальный ChattyWidgets, но его цифровая подпись отсутствует или не прошла проверку!";
            return info;
        }

        // --- TIER 2: VERIFIED GITHUB COMMUNITY ---
        string githubUser = ExtractGitHubUsername(manifest.Id);
        if (!string.IsNullOrEmpty(githubUser))
        {
            info.Level = WidgetTrustLevel.VerifiedCommunity;
            info.DisplayAuthor = $"@{githubUser}";
            info.BadgeText = string.Empty;
            info.BadgeBg = "#064E3B";
            info.BadgeFg = "#34D399";
            info.IsVerified = true;
            return info;
        }

        // --- TIER 3: THIRD-PARTY / UNVERIFIED ---
        info.Level = WidgetTrustLevel.ThirdParty;
        info.DisplayAuthor = !string.IsNullOrWhiteSpace(manifest.Author) ? manifest.Author : "Сторонний разработчик";
        info.BadgeText = "⚠ Сторонний виджет";
        info.BadgeBg = "#451A03";
        info.BadgeFg = "#FBBF24";
        info.IsVerified = false;
        return info;
    }

    /// <summary>
    /// Inspects a .chw package file before installation to detect spoofing or security risks.
    /// </summary>
    public (bool isSafe, string warningMessage, PackageSignatureMeta? meta) InspectPackageFile(string chwFilePath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(chwFilePath);
            var manifestEntry = zip.GetEntry("manifest.json");
            if (manifestEntry == null)
            {
                return (false, "Пакет поврежден: отсутствует файл manifest.json.", null);
            }

            using var manifestStream = manifestEntry.Open();
            var manifest = JsonSerializer.Deserialize<WidgetManifest>(manifestStream);
            if (manifest == null)
            {
                return (false, "Не удалось прочитать manifest.json.", null);
            }

            var sigEntry = zip.GetEntry("chatty_signature.json");
            PackageSignatureMeta? meta = null;
            if (sigEntry != null)
            {
                using var sigStream = sigEntry.Open();
                meta = JsonSerializer.Deserialize<PackageSignatureMeta>(sigStream);
            }

            bool claimsOfficial = manifest.Id.StartsWith(OfficialPrefix, StringComparison.OrdinalIgnoreCase) ||
                                  manifest.Author.Contains("ChattyWidgets", StringComparison.OrdinalIgnoreCase);

            bool validSig = false;
            if (meta != null && !string.IsNullOrEmpty(meta.Signature) && meta.IsOfficial)
            {
                validSig = VerifySignature(meta.Id, meta.Version, meta.ContentHash, meta.Signature);
            }

            if (claimsOfficial && !validSig)
            {
                return (false, $"ВНИМАНИЕ! Этот пакет пытается выдать себя за официальный виджет ChattyWidgets ({manifest.Id}), но не имеет подлинной цифровой подписи разработчиков.\n\nУстановка сторонних файлов под видом официальных может быть опасна.", meta);
            }

            return (true, string.Empty, meta);
        }
        catch (Exception ex)
        {
            return (false, $"Ошибка проверки пакета: {ex.Message}", null);
        }
    }

    private bool VerifySignature(string id, string version, string contentHash, string signatureBase64)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(OfficialPublicKeyPem);

            string payload = $"{id}:{version}:{contentHash}:official";
            byte[] dataBytes = Encoding.UTF8.GetBytes(payload);
            byte[] sigBytes = Convert.FromBase64String(signatureBase64);

            return rsa.VerifyData(dataBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    private string ExtractGitHubUsername(string id)
    {
        if (id.StartsWith("com.github.", StringComparison.OrdinalIgnoreCase))
        {
            var parts = id.Split('.');
            if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
            {
                return parts[2];
            }
        }
        return string.Empty;
    }
}
