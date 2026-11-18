namespace Pulse.Domain.Alerts;

public static class AlertState
{
    public static bool CanTransition(string from, string to) => from == "OPEN" && (to == "ACKNOWLEDGED" || to == "RESOLVED") || from == "ACKNOWLEDGED" && to == "RESOLVED";
}
