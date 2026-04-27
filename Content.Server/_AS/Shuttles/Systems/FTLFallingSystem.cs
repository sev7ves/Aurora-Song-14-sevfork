using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Events;
using Content.Shared.StepTrigger.Systems;
using Content.Shared._AS.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Damage;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;
using Content.Shared.Ghost;
using Content.Shared.Silicons.StationAi;
using Content.Server.GameTicking;
using Content.Server.Pointing.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Map.Components;
using Robust.Shared.Map;
using Robust.Shared.Log;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Physics.Systems;
using Robust.Server.GameObjects;


namespace Content.Server._AS.Shuttles.Systems;

/// <summary>
///     Handles making entities fall into FTL Space when stepping into space during transit
/// </summary>
public sealed class FTLFallingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntParentChangedMessage>(OnEntityParentChanged);
        SubscribeLocalEvent<FTLFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FTLFallingComponent, MindContainerComponent>();
        while (query.MoveNext(out var uid, out var falling, out _))
        {
            if (_timing.CurTime < falling.NextFallingTime)
                continue;

            RemComp<FTLFallingComponent>(uid);
            if (!TryComp<TransformComponent>(uid, out var xform))
                return;

            if (!_mapSystem.TryGetMap(_gameTicker.DefaultMap, out var mapUid))
            {
                Log.Error($"Could not get DefaultMap EntityUID, entity {uid} may be stuck in FTL Space.");
                return;
            }

            var destination = new EntityCoordinates(mapUid.Value, _random.NextVector2(falling.MinimumDistance, falling.MaximumDistance));
            var damageAmount = new DamageSpecifier()
            {
                DamageDict = { ["Slash"] = 15, ["Blunt"] = 25 }  // A small brick
            };
            // Make them stop moving
            if (TryComp<PhysicsComponent>(uid, out var physics))
            {
                var fixtures = Comp<FixturesComponent>(uid);
                _physics.SetLinearVelocity(uid, Vector2.Zero, manager: fixtures, body: physics);
            }
            // Let them move again
            _blocker.UpdateCanMove(uid);
            // Then teleport them back to reality
            _transform.SetCoordinates(uid, xform, destination);
            _transform.AttachToGridOrMap(uid, xform);
            // Then hit them with a brick
            _damageable.TryChangeDamage(uid, damageAmount, false);
        }
    }

    private void OnEntityParentChanged(ref EntParentChangedMessage args)
    {
        if (!HasComp<FTLMapComponent>(args.Transform.ParentUid) || HasComp<MapGridComponent>(args.Entity))
            return;
        if (HasComp<GhostComponent>(args.Entity) || HasComp<PointingArrowComponent>(args.Entity) || HasComp<StationAiEyeComponent>(args.Entity))
            return;
        Log.Debug($"{args.Entity} went onto the FTL Map");
        StartFalling(args.Entity);
    }

    public void StartFalling(EntityUid tripper, bool playSound = true)
    {
        var falling = AddComp<FTLFallingComponent>(tripper);

        falling.NextFallingTime = _timing.CurTime + falling.FallingTime;
        _blocker.UpdateCanMove(tripper);

        if (playSound)
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/falling.ogg"), tripper);
    }

    private void OnUpdateCanMove(EntityUid uid, FTLFallingComponent component, UpdateCanMoveEvent args)
    {
        args.Cancel();
    }
}
