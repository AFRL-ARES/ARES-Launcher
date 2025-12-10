namespace ARESLauncher.Models;

/// <summary>
///   A source from where we can grab ARES Launcher
/// </summary>
/// <param name="Owner">Owner of the repo</param>
/// <param name="Repo">The repo name itself</param> 
public record LauncherSource(string Owner, string Repo);
