using Xunit;

namespace Pulse.Tests.Integration;

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("PULSE_RUN_INTEGRATION") != "1")
            Skip = "Set PULSE_RUN_INTEGRATION=1 with a running Docker engine.";
    }
}
