using System.Threading.Tasks;

namespace ARESLauncher.Services;
public interface IConflictManager
{
  bool FindPotentialAres();
  void TakeOverAres();
  Task Kill(); //._.
  public bool IsCurrentUserProcessOwner { get; set; }
}
