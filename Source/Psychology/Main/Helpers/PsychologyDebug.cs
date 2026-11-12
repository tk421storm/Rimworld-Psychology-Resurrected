using Verse;

namespace Psychology;

// Optional debug output for Psychology's decisions (romance, dates, hookups), behind the "Print debug info" setting.
// Callers that need extra work just to build the message should check Enabled first.
public static class PsychologyDebug
{
    public static bool Enabled => PsychologySettings.printDebugInfo;

    public static void Log(string message)
    {
        if (Enabled)
        {
            Verse.Log.Message("[Psychology] " + message);
        }
    }
}
