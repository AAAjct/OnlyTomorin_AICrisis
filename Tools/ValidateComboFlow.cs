// Run while offline AshenCourt is in Play Mode.
public static class ValidateComboFlow
{
    public static string Main()
    {
        TomorinTrial.Editor.TrialComboFlowValidation.Run();
        return TomorinTrial.Editor.TrialComboFlowValidation.Status;
    }
}
