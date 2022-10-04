using System.Collections.Generic;

namespace CustomShipLogModes;

public class ModeA : ShipLogMode
{
    private OWAudioSource _oneShotSource;

    public override void Initialize(ScreenPromptList centerPromptList, ScreenPromptList upperRightPromptList, OWAudioSource oneShotSource)
    {
        _oneShotSource = oneShotSource;
    }

    public override void EnterMode(string entryID = "", List<ShipLogFact> revealQueue = null)
    {
        _oneShotSource.PlayOneShot(AudioType.TravelerPrisoner);
    }

    public override void ExitMode()
    {
        _oneShotSource.Stop();
    }

    public override void OnEnterComputer()
    {
    }

    public override void OnExitComputer()
    {
    }

    public override void UpdateMode()
    {
    }

    public override bool AllowModeSwap()
    {
        return true;
    }

    public override bool AllowCancelInput()
    {
        return true;
    }

    public override string GetFocusedEntryID()
    {
        return "";
    }
}