using Content.Shared._Floof.Consent;
using Content.Shared.EntityConditions;
using Content.Shared.Mind;
using Robust.Shared.Prototypes;

namespace Content.Shared._AS.Consent.EntityEffects;

public sealed class ConsentEntityConditionSystem : EntityConditionSystem<MindComponent, Consent>
{
    [Dependency] private static readonly SharedConsentSystem _consent = default!;

    protected override void Condition(Entity<MindComponent> ent, ref EntityConditionEvent<Consent> args)
    {
        if (ent.Comp.Session is not { } session)
            return;

        if (_consent.TryGetConsent(session.UserId, out var settings))
            return;

        foreach (var effect in args.Condition.EffectTypes)
        {
            if (settings is not null && _consent.HasConsent(settings, effect))
            {
                args.Result = true;
            }
        }
    }
}

public sealed partial class Consent : EntityConditionBase<Consent>
{
    [DataField(required: true)]
    public List<ProtoId<ConsentTogglePrototype>> EffectTypes;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        return string.Empty;
    }
}
