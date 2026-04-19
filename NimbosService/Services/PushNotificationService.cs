using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace NimbosService.Services;

public class PushNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<PushNotificationService> _logger;

    private string? _cachedToken;
    private DateTime _tokenCreatedAt = DateTime.MinValue;
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(55);
    private readonly object _tokenLock = new();

    public PushNotificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<PushNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    // Returns null on success, or the APNs error detail string on failure.
    // useSandbox param is advisory; Apns:UseSandbox config takes precedence when set.
    public async Task<string?> SendAsync(string apnsToken, string title, string body, string type, bool useSandbox)
    {
        var bundleId = _config["Apns:BundleId"]!;
        var configSandbox = _config["Apns:UseSandbox"];
        if (configSandbox is not null) useSandbox = configSandbox == "true";
        var host = useSandbox ? "api.sandbox.push.apple.com" : "api.push.apple.com";
        var jwtToken = GetOrCreateJwt();

        var client = _httpClientFactory.CreateClient("apns");

        var payload = new
        {
            aps = new { alert = new { title, body }, sound = "default" },
            type
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://{host}/3/device/{apnsToken}")
        {
            Content = content,
            Version = new Version(2, 0),
            VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        request.Headers.Add("authorization", $"bearer {jwtToken}");
        request.Headers.Add("apns-topic", bundleId);
        request.Headers.Add("apns-push-type", "alert");
        request.Headers.Add("apns-priority", "10");

        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("[APNs] Push delivered — type={Type} sandbox={Sandbox}", type, useSandbox);
            return null;
        }

        var err = await response.Content.ReadAsStringAsync();
        _logger.LogWarning("[APNs] Push failed {Status} — type={Type} sandbox={Sandbox}: {Body}",
            response.StatusCode, type, useSandbox, err);
        return $"HTTP {(int)response.StatusCode}: {err}";
    }

    private string GetOrCreateJwt()
    {
        lock (_tokenLock)
        {
            if (_cachedToken is not null && DateTime.UtcNow - _tokenCreatedAt < TokenLifetime)
                return _cachedToken;

            var teamId = _config["Apns:TeamId"]!;
            var keyId  = _config["Apns:KeyId"]!;
            var p8Key  = _config["Apns:PrivateKey"]!;

            var headerJson  = JsonSerializer.Serialize(new { alg = "ES256", kid = keyId });
            var payloadJson = JsonSerializer.Serialize(new { iss = teamId, iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });

            var headerB64  = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            var signingInput = $"{headerB64}.{payloadB64}";

            // Use BouncyCastle for signing — avoids Windows CNG key store access
            // issues that occur under restricted IIS AppPool identities.
            var keyBytes = Convert.FromBase64String(StripPemHeaders(p8Key));
            var privateKey = (ECPrivateKeyParameters)PrivateKeyFactory.CreateKey(keyBytes);
            var signer = SignerUtilities.GetSigner("SHA-256withECDSA");
            signer.Init(true, privateKey);
            var dataBytes = Encoding.UTF8.GetBytes(signingInput);
            signer.BlockUpdate(dataBytes, 0, dataBytes.Length);
            var derSignature = signer.GenerateSignature();

            // APNs requires IEEE P1363 format (r||s, 64 bytes), not DER.
            var signature = DerToP1363(derSignature, 32);

            _cachedToken    = $"{signingInput}.{Base64UrlEncode(signature)}";
            _tokenCreatedAt = DateTime.UtcNow;
            return _cachedToken;
        }
    }

    // Converts a DER-encoded ECDSA signature to fixed-length IEEE P1363 (r||s).
    private static byte[] DerToP1363(byte[] der, int componentLength)
    {
        var seq = (Asn1Sequence)Asn1Object.FromByteArray(der);
        var r = ((DerInteger)seq[0]).PositiveValue.ToByteArrayUnsigned();
        var s = ((DerInteger)seq[1]).PositiveValue.ToByteArrayUnsigned();
        var result = new byte[componentLength * 2];
        r.CopyTo(result, componentLength - r.Length);
        s.CopyTo(result, componentLength * 2 - s.Length);
        return result;
    }

    private static string StripPemHeaders(string pem) =>
        pem.Replace("-----BEGIN PRIVATE KEY-----", "")
           .Replace("-----END PRIVATE KEY-----", "")
           .Replace("\n", "").Replace("\r", "").Trim();

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
