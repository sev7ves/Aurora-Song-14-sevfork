namespace Content.Server._AS.GameTicking.Rules.Components;
/// <summary>
/// The purpose of this component is to prevent specific variation passes on stations. These are what add puddles and trash
/// </summary>
[RegisterComponent]
public sealed partial class VariationPassExemptionComponent : Component
{
    // Disables the variation pass which causes device wires to be cut at random
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool CutWireExemption = false;

    // Disables the variation pass which breaks or ages lights
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool PoweredLightExemption = false;

    //Disables the variation pass which causes random puddles to appear
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool PuddleMessExemption = false;

    //Disables the variation pass which spreads trash around the station
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool EntitySpawnExemption = false;
}
