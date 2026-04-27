namespace Content.Shared.Emp; // Aurora's Song - Move to shared

/// <summary>
/// Generates an EMP description for an entity that won't otherwise get one.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedEmpSystem))] // Aurora's Song - Move to shared
public sealed partial class EmpDescriptionComponent : Component
{
    /// <summary>
    /// The range of the EMP blast, in meters
    /// </summary>
    [DataField]
    public float Range = 1.0f;

    /// <summary>
    /// How much energy will be consumed per battery in range
    /// </summary>
    [DataField]
    public float EnergyConsumption;

    /// <summary>
    /// How long it disables targets in seconds
    /// </summary>
    [DataField]
    public float DisableDuration = 60f; // Aurora's Song - NF value
}
