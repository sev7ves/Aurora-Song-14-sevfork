using Robust.Shared.Configuration;

namespace Content.Shared._AS.CCVar;

[CVarDefs]
public sealed class ASCCVars
{
    /// <summary>
    /// The number of Cargo Depots to spawn in every round
    /// </summary>
    public static readonly CVarDef<int> Motels =
        CVarDef.Create("as.worldgen.motels", 2, CVar.SERVERONLY);
}
