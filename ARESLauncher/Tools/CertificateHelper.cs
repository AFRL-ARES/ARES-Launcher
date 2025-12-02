using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;

namespace ARESLauncher.Tools;

internal static class CertificateHelper
{
  /// <summary>
  /// This method generates a server side certificate so that the ares service can declare its
  /// validity. It's also used by the UI to validate its asp.net service as well.
  /// We also use this for grpc client/server comms. Ideally maybe it should be separate certs
  /// (especially client vs service) but for the sake of convenience, we just leav it as is.
  /// </summary>
  public static async Task<string> GenerateCertificate(string certPath, string certPassword)
  {
    if (File.Exists(certPath))
      return certPath;

    using var rsa = RSA.Create(2048);

    // Subject (CN is largely ignored for name matching now; SANs below are what matter)
    var subject = new X500DistinguishedName("CN=localhost");

    var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    // Basic constraints: not a CA
    req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

    // Key usage appropriate for TLS server certs
    req.CertificateExtensions.Add(new X509KeyUsageExtension(
        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, // for RSA TLS
        false));

    // Enhanced Key Usage: Server Authentication
    var eku = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }; // Server Auth
    req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, false));

    // Subject Key Identifier
    req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

    // Subject Alternative Names (what modern clients validate against)
    var san = new SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost");
    san.AddIpAddress(IPAddress.Loopback);      // 127.0.0.1
    san.AddIpAddress(IPAddress.IPv6Loopback);  // ::1
    req.CertificateExtensions.Add(san.Build());

    // Validity window (UTC). Slight backdate to avoid clock skew issues.
    var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
    var notAfter  = notBefore.AddYears(5);

    using var cert = req.CreateSelfSigned(notBefore, notAfter);

    try
    {
      if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        cert.FriendlyName = "ARES Certificate (localhost)";
      }
    }
    catch
    {
      // FriendlyName may throw on non-Windows; safe to ignore. Should be "caught" by the if statement, but just in case
    }

    var dir = Path.GetDirectoryName(certPath);
    if (dir is not null)
    {
      Directory.CreateDirectory(dir);
    }

    // Export as PFX including the private key
    await File.WriteAllBytesAsync(certPath, cert.Export(X509ContentType.Pfx, certPassword));

    return certPath;
  }

  public static async Task AddCertificate(string certPath, string certPassword)
  {
    if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      if(!WindowsCertExists(certPath, certPassword))
        AddWindowsCert(certPath, certPassword);
    }
    else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      if(!await MacOsCertExists(certPath, certPassword))
        await AddMacOsCert(certPath, certPassword);
    }
    else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
      if(!LinuxCertExists(certPath))
        await AddLinuxCert(certPath, certPassword);
    }
  }

  #region Windows

  private static bool WindowsCertExists(string certPath, string certPassword)
  {
    var certToCheck = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
    using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    var exists = store.Certificates.Cast<X509Certificate2>()
        .Any(c => c.Thumbprint == certToCheck.Thumbprint);
    store.Close();
    return exists;
  }

  private static void AddWindowsCert(string certPath, string certPassword)
  {
    using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadWrite);
    var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword, X509KeyStorageFlags.PersistKeySet);
    store.Add(cert);
    store.Close();
  }

  #endregion

  #region macOS

  private static async Task<bool> MacOsCertExists(string certPath, string certPassword)
  {
    // Build the reference hash directly from the PFX (no temp PEM needed)
    using var desiredCert = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
    var desiredHash = desiredCert.GetCertHashString(HashAlgorithmName.SHA256);

    // Dump all trusted certs in user keychain
    var keychainPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      "Library", "Keychains", "login.keychain-db");

    // Prefer explicit keychain path if it exists; otherwise search default keychains
    var cmd = Cli.Wrap("security");
    if (File.Exists(keychainPath))
    {
      cmd = cmd.WithArguments(args => args
        .Add("find-certificate")
        .Add("-a")
        .Add("-p")
        .Add(keychainPath));
    }
    else
    {
      cmd = cmd.WithArguments(args => args
        .Add("find-certificate")
        .Add("-a")
        .Add("-p"));
    }

    var result = await cmd.ExecuteBufferedAsync();

    var output = result.StandardOutput;
    if (string.IsNullOrWhiteSpace(output)) return false;

    foreach (var pem in ExtractPemCertificates(output))
    {
      try
      {
        using var cert = X509Certificate2.CreateFromPem(pem);
        var hash = cert.GetCertHashString(HashAlgorithmName.SHA256);
        if (string.Equals(hash, desiredHash, StringComparison.OrdinalIgnoreCase))
          return true;
      }
      catch
      {
        // ignore malformed blocks and continue
      }
    }

    return false;
  }

  private static async Task AddMacOsCert(string certPath, string certPassword)
  {
    var pemPath = string.Empty;
    try
    {
      pemPath = Path.GetTempFileName();

      // Convert PFX to PEM (public cert only)
      await ConvertPfxToPemAsync(certPath, certPassword, pemPath);

      var result = await Cli.Wrap("security")
          .WithArguments($"add-trusted-cert -d -r trustRoot -k \"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Keychains/login.keychain-db\" \"{pemPath}\"")
          .ExecuteBufferedAsync();
    }
    finally
    {
      if(!string.IsNullOrEmpty(pemPath) && File.Exists(pemPath))
        File.Delete(pemPath);
    }
  }

  #endregion

  #region Linux

  private static bool LinuxCertExists(string certPath)
  {
    try
    {
      using var desiredCert = X509CertificateLoader.LoadCertificateFromFile(certPath);
      var desiredHash = desiredCert.GetCertHashString(HashAlgorithmName.SHA256);

      var dir = "/usr/local/share/ca-certificates";
      if (!Directory.Exists(dir)) return false;

      foreach (var path in Directory.EnumerateFiles(dir, "*.crt"))
      {
        try
        {
          var content = File.ReadAllText(path);
          foreach (var pem in ExtractPemCertificates(content))
          {
            using var cert = X509Certificate2.CreateFromPem(pem);
            var hash = cert.GetCertHashString(HashAlgorithmName.SHA256);
            if (string.Equals(hash, desiredHash, StringComparison.OrdinalIgnoreCase))
              return true;
          }
        }
        catch
        {
          // skip unreadable or malformed files
        }
      }

      return false;
    }
    catch
    {
      return false;
    }
  }

  private static async Task AddLinuxCert(string certPath, string certPassword)
  {
    var crtPath = string.Empty;
    try
    {
      crtPath = Path.ChangeExtension(Path.GetTempFileName(), ".crt");

      // Create a clean PEM certificate from the PFX using managed APIs (no bag attributes)
      using (var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword))
      {
        var der = cert.Export(X509ContentType.Cert);
        var base64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        var pem = $"-----BEGIN CERTIFICATE-----\n{base64}\n-----END CERTIFICATE-----\n";
        await File.WriteAllTextAsync(crtPath, pem);
      }

      var destPath = $"/usr/local/share/ca-certificates/{Path.GetFileName(crtPath)}";

      await Cli.Wrap("sudo")
          .WithArguments(args => args
              .Add("cp")
              .Add(crtPath)
              .Add(destPath))
          .ExecuteAsync();

      await Cli.Wrap("sudo")
          .WithArguments("update-ca-certificates")
          .ExecuteAsync();
    }
    finally
    {
      if(!string.IsNullOrEmpty(crtPath) && File.Exists(crtPath))
        File.Delete(crtPath);
    }
  }

  #endregion

  private static async Task ConvertPfxToPemAsync(string pfxPath, string pfxPassword, string pemPath)
  {
    var arguments = $"pkcs12 -in \"{pfxPath}\" -out \"{pemPath}\" -nokeys -clcerts -passin pass:{pfxPassword}";
    await Cli.Wrap("openssl")
        .WithArguments(arguments)
        .ExecuteAsync();
  }

  private static System.Collections.Generic.IEnumerable<string> ExtractPemCertificates(string pemBundle)
  {
    const string begin = "-----BEGIN CERTIFICATE-----";
    const string end = "-----END CERTIFICATE-----";

    int idx = 0;
    while (true)
    {
      var start = pemBundle.IndexOf(begin, idx, StringComparison.Ordinal);
      if (start < 0) yield break;
      var finish = pemBundle.IndexOf(end, start, StringComparison.Ordinal);
      if (finish < 0) yield break;
      finish += end.Length;
      var pem = pemBundle[start..finish];
      yield return pem;
      idx = finish;
    }
  }
}
