// Aurora's Song - Rewrote the whole file to use modern ECS based EntityConditions

using System.Linq;
using Content.Shared.EntityConditions;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;

namespace Content.Shared._NF.EntityEffects.Effect;

/// <summary>
/// Requires that the metabolizing body is a humanoid, with an optional whitelist/blacklist.
/// </summary>e
public sealed class NFIsHumanoidEntityConditionSystem : EntityConditionSystem<HumanoidAppearanceComponent, NFIsHumanoid>
{
    protected override void Condition(Entity<HumanoidAppearanceComponent> ent, ref EntityConditionEvent<NFIsHumanoid> args)
    {
        if (args.Condition.Whitelist != null && args.Condition.Whitelist.Contains(ent.Comp.Species) != args.Condition.Inverse)
            return;

        args.Result = true;
    }
}

public sealed partial class NFIsHumanoid : EntityConditionBase<NFIsHumanoid>
{
    /// <summary>
    /// The whitelist (or blacklist if inverse is true) of species to select.
    /// </summary>
    [DataField]
    public List<ProtoId<SpeciesPrototype>>? Whitelist = null;

    /// <summary>
    /// If true, the metabolizer's species must be in the list to process this effect.
    /// If false, the metabolizer's species cannot be in the list.
    /// </summary>
    [DataField]
    public bool Inverse;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        if (Whitelist == null || Whitelist.Count == 0)
        {
            return Loc.GetString("reagent-effect-condition-guidebook-species-type-empty");
        }
        else
        {
            var message = Inverse ? "reagent-effect-condition-guidebook-species-type-blacklist" : "reagent-effect-condition-guidebook-species-type-whitelist";
            var localizedSpecies = Whitelist.Select(p => Loc.GetString("reagent-effect-condition-guidebook-species-type-species", ("species", Loc.GetString(prototype.Index(p).Name)))).ToList();
            var list = ContentLocalizationManager.FormatListToOr(localizedSpecies);
            return Loc.GetString(message, ("species", list));
        }
    }
}
