namespace ARESLauncher.Models;
public enum AresState
{
  Unknown,
  Running,
  Ready,
  NeedsDbUpdate,
  DatabaseError,
  NeedsInstall,
  Updating,
  ProcessOwnerConflict
}
