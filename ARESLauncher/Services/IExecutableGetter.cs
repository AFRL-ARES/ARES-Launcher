namespace ARESLauncher.Services;

public interface IExecutableGetter
{
  string? GetUiExecutablePath();
  string? GetServiceExecutablePath();
}