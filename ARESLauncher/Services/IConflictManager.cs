using System.Threading.Tasks;

namespace ARESLauncher.Services;
public interface IConflictManager
{
  bool FindPotentialUi();
  bool FindPotentialService();

  void TakeOverUi();
  void TakeOverService();

  Task Kill(); //._.

  public bool IsCurrentUserProcessOwner { get; set; }
}
