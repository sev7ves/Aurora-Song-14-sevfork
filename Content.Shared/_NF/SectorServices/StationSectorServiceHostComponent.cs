namespace Content.Shared._NF.SectorServices; // Aurora's Song - Move to shared

/// <summary>
/// A station with this component will host all sector-wide services.
/// </summary>
[RegisterComponent]
[Access(typeof(SectorServiceSystem))]
public sealed partial class StationSectorServiceHostComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid SectorUid = EntityUid.Invalid;
}
