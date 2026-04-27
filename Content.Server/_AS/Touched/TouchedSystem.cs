
using Content.Server.Storage.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Storage.Components;

namespace Content.Server._AS.Touched;

/// <summary>
/// Used to mark entities as having been touched by a player
/// </summary>
public sealed class TouchedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ItemComponent, ContactInteractionEvent>(OnItemInteract);
        SubscribeLocalEvent<EntityStorageComponent, ContactInteractionEvent>(OnStorageInteract);
    }

    private void OnItemInteract(EntityUid uid, ItemComponent component, ContactInteractionEvent args)
    {
        ApplyTouch(uid);
    }

    private void OnStorageInteract(EntityUid uid, EntityStorageComponent component, ContactInteractionEvent args)
    {
        ApplyTouch(uid);
    }

    private void ApplyTouch(EntityUid item)
    {
        var component = EnsureComp<TouchedComponent>(item);
        component.Touched = true;
    }
}
