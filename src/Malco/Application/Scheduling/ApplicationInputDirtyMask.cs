using System;

namespace Malco.Application.Scheduling
{
    [Flags]
    internal enum ApplicationInputDirtyMask
    {
        None = 0,
        Semantic = 1,
        Commands = 2,
        ProjectionControl = 4,
        ClearStableState = 32
    }
}
