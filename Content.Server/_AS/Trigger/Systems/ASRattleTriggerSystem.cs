using Content.Server.Radio.EntitySystems;
using Content.Shared._AS.Traits;
using Content.Shared.Humanoid;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Radio;
using Content.Shared.Station;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;

namespace Content.Server._AS.Trigger.Systems;

public sealed class ASRattleTriggerSystem : XOnTriggerSystem<RattleOnTriggerComponent>
{
    [Dependency] private readonly SharedStationSystem _station = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    // Have old functionality of rattle available for NF and Coyote functionality
    protected override void OnTrigger(Entity<RattleOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        if (!TryComp<SubdermalImplantComponent>(target, out var implanted))
            return;

        if (implanted.ImplantedEntity == null)
            return;
        // Coyote
        if (!TryComp<MobStateComponent>(implanted.ImplantedEntity, out var mobstate)
            || mobstate.CurrentState == MobState.Alive)
            return;


        // Gets location of the implant
        var ownerXform = Transform(target);
        var pos = ownerXform.MapPosition;
        var x = (int)pos.X;
        var y = (int)pos.Y;
        var posText = $"({x}, {y})";

        // Frontier: Gets station location of the implant
        var station = _station.GetOwningStation(target);
        var stationText = station is null ? null : $"{Name(station.Value)} ";

        if (stationText == null)
            stationText = "";

        // Frontier: Gets species of the implant user
        var speciesText = $"";
        if (TryComp<HumanoidAppearanceComponent>(implanted.ImplantedEntity, out var species))
        {

            if (HasComp<ReplicantComponent>(implanted.ImplantedEntity)) // AS: Replika
            {
                speciesText = $" ({Loc.GetString("species-name-replicant", ("species", species!.Species))})";  // AS: Replika
            }
            else
            {
                speciesText = $" ({species!.Species})";
            }
        }
        // Start Coyote
        string localeKey = ent.Comp.Messages[mobstate.CurrentState];

        var message = Loc.GetString(
            localeKey,
            ("user", implanted.ImplantedEntity.Value),
            ("specie", speciesText),
            ("grid", stationText!),
            ("position", posText));

        _radioSystem.SendRadioMessage(
            target,
            message,
            _prototypeManager.Index<RadioChannelPrototype>(ent.Comp.RadioChannel),
            target);
        // End Coyote
        args.Handled = true;
    }
}
