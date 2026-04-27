using Content.Server.StationEvents.Components;
namespace Content.Server._AS.StationEvents.Components;

/// <summary>
/// Aurora Song - Bounty variant of New Frontier's BluespaceErrorRuleComponent
/// Checks for specific mob prototypes and their cuff states for bonus rewards
/// </summary>
[RegisterComponent]
public sealed partial class ToggleEventComponent : Component
{
    /// <summary>
    /// Whether we allow events of the given category to fire. If false, the events will be prevented from triggering
    /// </summary>
    [DataField]
    public bool Active = false;

    /// <summary>
    /// What category of events we want to prevent. The <seealso cref="StationEventComponent"/> needs a matching Category entry in order for this to function
    /// </summary>
    [DataField]
    public string Category = "SLE";
}

