using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;

namespace ARESLauncher.Tools;

internal static class CertificateHelper
{
  public static async Task<string> GenerateCertificate(string certPath, string certPassword)
  {
    if(File.Exists(certPath))
      return certPath;

    using var rsa = RSA.Create(2048);
    var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    req.CertificateExtensions.Add(
        new X509BasicConstraintsExtension(false, false, 0, false));
    req.CertificateExtensions.Add(
        new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
    req.CertificateExtensions.Add(
        new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

    var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(50));

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
    var certToCheck = new X509Certificate2(certPath, certPassword);
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
    store.Add(new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.PersistKeySet));
    store.Close();
  }

  #endregion

  #region macOS

  private static async Task<bool> MacOsCertExists(string certPath, string certPassword)
  {
    var tempPemPath = Path.GetTempFileName();

    try
    {
      // Convert PFX to PEM for comparison
      await ConvertPfxToPemAsync(certPath, certPassword, tempPemPath);

      // Dump all trusted certs in user keychain
      var result = await Cli.Wrap("security")
          .WithArguments("find-certificate -a -p ~/Library/Keychains/login.keychain-db")
          .ExecuteBufferedAsync();

      var currentCert = await File.ReadAllTextAsync(tempPemPath);
      return result.StandardOutput.Contains(currentCert.Trim());
    }
    finally
    {
      if(File.Exists(tempPemPath))
        File.Delete(tempPemPath);
    }
  }

  private static async Task AddMacOsCert(string certPath, string certPassword)
  {
    var pemPath = string.Empty;
    try
    {
      pemPath = Path.GetTempFileName();

      // Convert PFX to PEM (public cert only)
      await ConvertPfxToPemAsync(certPath, certPassword, pemPath);

      await Cli.Wrap("security")
          .WithArguments($"add-trusted-cert -d -r trustRoot -k \"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Keychains/login.keychain-db\" \"{pemPath}\"")
          .ExecuteAsync();
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
    var fileName = Path.GetFileNameWithoutExtension(certPath) + ".crt";
    var destPath = $"/usr/local/share/ca-certificates/{fileName}";
    return File.Exists(destPath);
  }

  private static async Task AddLinuxCert(string certPath, string certPassword)
  {
    var crtPath = string.Empty;
    try
    {
      crtPath = Path.ChangeExtension(Path.GetTempFileName(), ".crt");

      // Convert PFX to CRT (PEM) public cert only
      await ConvertPfxToPemAsync(certPath, certPassword, crtPath);

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
}