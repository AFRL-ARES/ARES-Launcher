namespace ARESLauncher.Models;

/// <summary>
///   A source from where we can grab ARES
/// </summary>
/// <param name="Owner">Owner of the repo</param>
/// <param name="Repo">The repo name itself</param>
/// <param name="Bundle">Whether the releases here contain both UI and Service in one package</param>
public record AresSource(string Owner, string Repo, bool Bundle = true);