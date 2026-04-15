using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

    public async Task SendAsync(string apnsToken, string title, string body)
    {
        var bundleId = _config["Apns:BundleId"]!;
        var useSandbox = _config["Apns:UseSandbox"] == "true";
        var host = useSandbox ? "api.sandbox.push.apple.com" : "api.push.apple.com";
        var jwtToken = GetOrCreateJwt();

        var client = _httpClientFactory.CreateClient("apns");

        var payload = new
        {
            aps = new { alert = new { title, body }, sound = "default" }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://{host}/3/device/{apnsToken}")
        {
            Content = content,
            Version = new Version(2, 0)
        };

        request.Headers.Add("authorization", $"bearer {jwtToken}");
        request.Headers.Add("apns-topic", bundleId);
        request.Headers.Add("apns-push-type", "alert");
        request.Headers.Add("apns-priority", "10");

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[APNs] Push failed {Status}: {Body}", response.StatusCode, err);
        }
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

            using var ecdsa = ECDsa.Create();
            var keyBytes = Convert.FromBase64String(StripPemHeaders(p8Key));
            ecdsa.ImportPkcs8PrivateKey(keyBytes, out _);

            var dataToSign = Encoding.UTF8.GetBytes(signingInput);
            var signature  = ecdsa.SignData(dataToSign, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            _cachedToken    = $"{signingInput}.{Base64UrlEncode(signature)}";
            _tokenCreatedAt = DateTime.UtcNow;
            return _cachedToken;
        }
    }

    private static string StripPemHeaders(string pem) =>
        pem.Replace("-----BEGIN PRIVATE KEY-----", "")
           .Replace("-----END PRIVATE KEY-----", "")
           .Replace("\n", "").Replace("\r", "").Trim();

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
