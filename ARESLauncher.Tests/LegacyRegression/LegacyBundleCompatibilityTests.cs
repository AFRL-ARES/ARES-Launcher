using ARESLauncher.Configuration;
using ARESLauncher.Tools;
using System.Text.Json;

namespace ARESLauncher.Tests.LegacyRegression;

[TestFixture]
public class LegacyBundleCompatibilityTests
{
  // We used to have Bundle as an option in case we packaged UI and Service separately
  // so if a user still has an older config file, we should handle that gracefully
  [Test]
  public void LauncherConfiguration_DeserializesLegacyBundleField_AndSerializesWithoutIt()
  {
    const string json = """
                        {
                          "CurrentAresRepo": {
                            "Owner": "AFRL-ARES",
                            "Repo": "ARES",
                            "Bundle": false
                          },
                          "AvailableAresRepos": [
                            {
                              "Owner": "AFRL-ARES",
                              "Repo": "ARES",
                              "Bundle": true
                            },
                            {
                              "Owner": "Example",
                              "Repo": "Fork",
                              "Bundle": false
                            }
                          ]
                        }
                        """;

    var configuration = JsonSerializer.Deserialize<LauncherConfiguration>(json);

    Assert.That(configuration, Is.Not.Null);
    Assert.That(configuration!.CurrentAresRepo.Owner, Is.EqualTo("AFRL-ARES"));
    Assert.That(configuration.CurrentAresRepo.Repo, Is.EqualTo("ARES"));
    Assert.That(configuration.AvailableAresRepos, Has.Length.EqualTo(2));
    Assert.That(configuration.AvailableAresRepos[1].Owner, Is.EqualTo("Example"));
    Assert.That(configuration.AvailableAresRepos[1].Repo, Is.EqualTo("Fork"));

    var rewrittenJson = JsonSerializer.Serialize(configuration);

    Assert.That(rewrittenJson, Does.Not.Contain("\"Bundle\""));
  }

  [Test]
  public void BinaryMetadataHelper_ReadMetadata_IgnoresLegacyBundleField()
  {
    var tempRoot = TestPaths.CreateTempDirectory();

    try
    {
      var json = """
                 {
                   "Source": {
                     "Owner": "AFRL-ARES",
                     "Repo": "ARES",
                     "Bundle": true
                   },
                   "Version": "1.2.3"
                 }
                 """;

      File.WriteAllText(Path.Combine(tempRoot, BinaryMetadataHelper.MetadataFileName), json);

      var metadata = BinaryMetadataHelper.ReadMetadata(tempRoot);

      Assert.That(metadata, Is.Not.Null);
      Assert.That(metadata!.Source, Is.Not.Null);
      Assert.That(metadata.Source!.Owner, Is.EqualTo("AFRL-ARES"));
      Assert.That(metadata.Source.Repo, Is.EqualTo("ARES"));
      Assert.That(metadata.Version, Is.EqualTo("1.2.3"));
    }
    finally
    {
      TestPaths.DeleteDirectoryIfExists(tempRoot);
    }
  }
}
