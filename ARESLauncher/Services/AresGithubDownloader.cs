using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using ARESLauncher.Models;
using ARESLauncher.Tools;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Octokit;

namespace ARESLauncher.Services;

public class AresGithubDownloader(ILogger<AresGithubDownloader> _logger) : IAresDownloader
{
  private readonly ISubject<double> _progressSubject = new BehaviorSubject<double>(0);

  public async Task<SemanticVersion[]> GetAvailableVersions(AresSource source, string? authToken)
  {
    var client = CreateClient(authToken);
    var versions = new List<SemanticVersion>();
    try
    {
      var releases = await client.Repository.Release.GetAll(source.Owner, source.Repo);

      foreach (var release in releases)
      {
        var normalizedTag = TryNormalizeTag(release.TagName);
        if (string.IsNullOrEmpty(normalizedTag)) continue;

        if (SemanticVersion.TryParse(normalizedTag, out var semanticVersion)) versions.Add(semanticVersion);
      }
    }
    catch (NotFoundException)
    {
      _logger.LogError(
        "ARES repository not found for {SourceOwner}/{SourceRepo}. Maybe you're missing the git auth token?",
        source.Owner, source.Repo);
    }

    return versions.ToArray();
  }

  public async Task<string> Download(AresSource source, SemanticVersion version, AresComponent component,
    string destination, string? authToken, IProgress<double>? progress = null)
  {
    var client = CreateClient(authToken);
    var release = await GetReleaseForVersion(client, source, version);
    var asset = SelectAssetForComponent(release, component) ??
                throw new InvalidOperationException(
                  $"No asset found in release {release.TagName} for component {component}.");

    var downloadUri = new Uri(asset.Url);
    var downloadResult = await Downloader.Download(downloadUri, destination, authToken, progress);

    // Technically ResultingFilePath could be null, but if our download result is a success, there's no reason it should.
    return !downloadResult.Success
      ? throw new InvalidOperationException($"Failed to download {component} {version}: {downloadResult.Error}")
      : downloadResult.ResultingFilePath!;
  }

  private GitHubClient CreateClient(string? authtoken)
  {
    var client = new GitHubClient(new ProductHeaderValue("ares-launcher"));

    if (!string.IsNullOrEmpty(authtoken))
      client.Credentials = new Credentials(authtoken);

    return client;
  }

  private static async Task<Release> GetReleaseForVersion(GitHubClient client, AresSource source,
    SemanticVersion version)
  {
    foreach (var tag in EnumerateTagCandidates(version))
      try
      {
        return await client.Repository.Release.Get(source.Owner, source.Repo, tag);
      }
      catch (NotFoundException)
      {
        // Try next candidate.
      }

    throw new InvalidOperationException($"Could not locate release for version {version}.");
  }

  private static IEnumerable<string> EnumerateTagCandidates(SemanticVersion version)
  {
    var normalized = version.ToNormalizedString();
    yield return normalized;
    yield return $"v{normalized}";

    var original = version.ToString();
    if (!string.Equals(original, normalized, StringComparison.Ordinal)) yield return original;

    var full = version.ToFullString();
    if (!string.Equals(full, normalized, StringComparison.Ordinal) &&
        !string.Equals(full, original, StringComparison.Ordinal)) yield return full;
  }

  private static ReleaseAsset? SelectAssetForComponent(Release release, AresComponent component)
  {
    if (release.Assets is null || release.Assets.Count == 0) return null;
    var os = OsBundleNameGetter.GetName();

    var keywords = component switch
    {
      AresComponent.Ui => ["ui", os],
      AresComponent.Service => ["service", os],
      AresComponent.Both => [os],
      _ => Array.Empty<string>()
    };

    var asset = release.Assets.FirstOrDefault(a =>
      keywords.All(keyword => a.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true));

    if (asset is not null) return asset;

    return release.Assets.Count == 1 ? release.Assets[0] : null;
  }

  private static string? TryNormalizeTag(string? tag)
  {
    if (string.IsNullOrWhiteSpace(tag)) return null;

    var trimmed = tag.Trim();
    if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 1 && char.IsDigit(trimmed[1]))
      trimmed = trimmed[1..];

    return trimmed;
  }
}