namespace ARESLauncher.Models;
public enum AresState
{
  Unknown,
  OneRunning,
  BothRunning,
  Ready,
  NeedsDbUpdate,
  DatabaseError,
  NeedsInstall,
  Updating,
  ProcessOwnerConflict
}
