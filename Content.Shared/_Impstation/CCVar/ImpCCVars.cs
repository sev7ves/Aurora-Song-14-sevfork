using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._Impstation.CCVar;

// ReSharper disable once InconsistentNaming
[CVarDefs]
public sealed class ImpCCVars : CVars
{
    /// <summary>
    /// If the player has the accessibility notifier turned on
    /// </summary>
    public static readonly CVarDef<bool> NotifierOn =
        CVarDef.Create("accessibility.notifier_on", false, CVar.ARCHIVE | CVar.REPLICATED | CVar.CLIENT, "if the notifier system is active");

    /// <summary>
    /// the contents of a players accessibility notifier
    /// </summary>
    public static readonly CVarDef<string> NotifierExamine =
        CVarDef.Create("accessibility.notifier_examine", "", CVar.ARCHIVE | CVar.REPLICATED | CVar.CLIENT, "content of accessibility issue notifier.");
}
