using Content.Shared.Construction.Prototypes;
using Content.Shared.Kitchen.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Kitchen.Components;

/// <summary>
/// The combo reagent grinder/juicer. The reason why grinding and juicing are seperate is simple,
/// think of grinding as a utility to break an object down into its reagents. Think of juicing as
/// converting something into its single juice form. E.g, grind an apple and get the nutriment and sugar
/// it contained, juice an apple and get "apple juice".
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedReagentGrinderSystem))]
public sealed partial class ReagentGrinderComponent : Component
{
    [DataField, AutoNetworkedField]
    public int StorageMaxEntities = 6;

    // Frontier
    [DataField]
    public int BaseStorageMaxEntities = 4;

    // Frontier
    [DataField("machinePartStorageMax")]
    public ProtoId<MachinePartPrototype> MachinePartStorageMax = "MatterBin";

    // Frontier
    [DataField]
    public int StoragePerPartRating = 4;

    [DataField, AutoNetworkedField]
    public TimeSpan WorkTime = TimeSpan.FromSeconds(3.5); // Roughly matches the grind/juice sounds.

    [DataField, AutoNetworkedField]
    public float WorkTimeMultiplier = 1;

    // Frontier
    [DataField("machinePartWorkTime")]
    public ProtoId<MachinePartPrototype> MachinePartWorkTime = "Manipulator";

    // Frontier
    [DataField]
    public float PartRatingWorkTimerMulitplier = 0.6f;

    [DataField]
    public SoundSpecifier ClickSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    [DataField]
    public SoundSpecifier GrindSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/blender.ogg");

    [DataField]
    public SoundSpecifier JuiceSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/juicer.ogg");

    [DataField, AutoNetworkedField]
    public GrinderAutoMode AutoMode = GrinderAutoMode.Off;

    public EntityUid? AudioStream;
}

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedReagentGrinderSystem))]
public sealed partial class ActiveReagentGrinderComponent : Component
{
    /// <summary>
    /// Remaining time until the grinder finishes grinding/juicing.
    /// </summary>
    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public GrinderProgram Program;
}
