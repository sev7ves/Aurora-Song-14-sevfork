// Aurora's Song - Basically this whole thing is rewritten

using Content.Shared._NF.Explosion.Components;
using Content.Shared.Implants;
using Content.Shared.Body.Components;
using Content.Shared._NF.Interaction.Events;
using Content.Shared.Body.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Content.Shared.Station;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Containers;

namespace Content.Shared.Trigger.Systems;

public sealed partial class TriggerSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;


    private void NFInitialize()
    {
        SubscribeLocalEvent<GibOnTriggerComponent, TriggerEvent>(HandleGibTrigger);
        SubscribeLocalEvent<TriggerOnBeingGibbedComponent, BeingGibbedEvent>(OnBeingGibbed);
        SubscribeLocalEvent<TriggerOnBeingGibbedComponent, ImplantRelayEvent<BeingGibbedEvent>>(OnBeingGibbedRelay);
        SubscribeLocalEvent<TriggerOnInteractionPopupUseComponent, InteractionPopupOnUseFailureEvent>(OnPopupInteractionFailure);
        SubscribeLocalEvent<TriggerOnInteractionPopupUseComponent, InteractionPopupOnUseSuccessEvent>(OnPopupInteractionSuccess);

        SubscribeLocalEvent<ReplaceOnTriggerComponent, TriggerEvent>(OnReplaceTrigger);
        SubscribeLocalEvent<TriggerOnProjectileHitComponent, ProjectileHitEvent>(OnProjectileHitEvent);
    }

    private void HandleGibTrigger(EntityUid uid, GibOnTriggerComponent component, TriggerEvent args)
    {
        EntityUid ent;
        if (component.UseArgumentEntity)
        {
            ent = uid;
        }
        else
        {
            if (!TryComp(uid, out TransformComponent? xform))
                return;
            ent = xform.ParentUid;
        }

        if (component.DeleteItems)
        {
            var items = _inventory.GetHandOrInventoryEntities(ent);
            foreach (var item in items)
            {
                PredictedQueueDel(item);
            }
        }

        if (component.Gib)
            _gibbing.Gib(ent);
        args.Handled = true;
    }

    private void OnBeingGibbed(EntityUid uid, TriggerOnBeingGibbedComponent component,  ref BeingGibbedEvent args)
    {
        // Aurora's Song - This *sucks* but seems to be the only reliable way to not cause testing errors
        if (TryComp<GibOnTriggerComponent>(uid, out var comp))
            args.dropGiblets = !comp.DeleteOrgans;

        Trigger(uid);
    }

    private void OnBeingGibbedRelay(EntityUid uid, TriggerOnBeingGibbedComponent component, ref ImplantRelayEvent<BeingGibbedEvent> args)
    {
        Trigger(uid);
    }

    private void OnPopupInteractionFailure(EntityUid uid, TriggerOnInteractionPopupUseComponent component, InteractionPopupOnUseFailureEvent args)
    {
        if (component.TriggerOnFailure)
            Trigger(uid);
    }

    private void OnPopupInteractionSuccess(EntityUid uid, TriggerOnInteractionPopupUseComponent component, InteractionPopupOnUseSuccessEvent args)
    {
        if (component.TriggerOnSuccess)
            Trigger(uid);
    }

    private void OnReplaceTrigger(Entity<ReplaceOnTriggerComponent> ent, ref TriggerEvent args)
    {
        var xform = Transform(ent);

        if (_container.TryGetContainingContainer((ent, xform), out var container))
        {
            _container.Remove(ent.Owner, container, force: true);
            SpawnInContainerOrDrop(ent.Comp.Proto, container.Owner, container.ID);
        }
        else
        {
            Spawn(ent.Comp.Proto, xform.Coordinates);
        }
        QueueDel(ent);
    }

    private void OnProjectileHitEvent(EntityUid uid, TriggerOnProjectileHitComponent component, ref ProjectileHitEvent args)
    {
        Trigger(uid, args.Target);
    }
}
