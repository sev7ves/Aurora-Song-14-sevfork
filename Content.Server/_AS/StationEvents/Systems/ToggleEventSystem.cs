using Content.Server.StationEvents.Events;
using Content.Server._AS.StationEvents.Components;
using Content.Shared.Verbs;


namespace Content.Server._AS.StationEvents.Systems
{

    public sealed class ToggleEventSystem : EntitySystem
    {
        public override void Initialize()
        {
            SubscribeLocalEvent<ToggleEventComponent, GetVerbsEvent<InteractionVerb>>(AddSearchVerb);
        }

        private void AddSearchVerb(EntityUid uid, ToggleEventComponent component, GetVerbsEvent<InteractionVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess || args.Hands == null)
                return;

            //here we build our dynamic verb. Using the object's sprite for now to make it more dynamic for the moment.
            InteractionVerb toggleVerb = new()
            {
                IconEntity = GetNetEntity(uid),
                Act = () => ToggleState(component),
                Text = component.Active ? Loc.GetString("verb-deactivate-text") : Loc.GetString("verb-activate-text"),
                Priority = 3
            };

            args.Verbs.Add(toggleVerb);
        }
        private void ToggleState(ToggleEventComponent component)
        {
            component.Active = !component.Active;
        }
    }
}