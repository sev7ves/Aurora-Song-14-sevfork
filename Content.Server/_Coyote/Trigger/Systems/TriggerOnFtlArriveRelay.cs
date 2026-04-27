using Content.Shared.Implants;
using Content.Shared.Mobs; // Aurora's Song
using Content.Shared.Mobs.Components; // Aurora's Song
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Triggers;

namespace Content.Server._Coyote.Trigger.Systems;

public sealed class TriggerOnFtlArriveRelay : TriggerOnXSystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TriggerOnMobstateChangeComponent, ImplantRelayEvent<ReTriggerRattleImplantEvent>>(OnFtlArriveRelay);
    }

    /// <summary>
    /// When ftl arrives, try to retrigger their medical alerts
    /// </summary>
    private void OnFtlArriveRelay(EntityUid uid,
        TriggerOnMobstateChangeComponent component,
        ImplantRelayEvent<ReTriggerRattleImplantEvent> args)
    {
        // Aurora's Song - Prevent retriggering when alive, it causes death acidifiers to activate
        if (!TryComp<MobStateComponent>(args.Event.Implanted, out var mobstate)
            || mobstate.CurrentState == MobState.Alive)
            return;

        Trigger.Trigger(uid, args.Event.Implanted, component.KeyOut);
    }
}
