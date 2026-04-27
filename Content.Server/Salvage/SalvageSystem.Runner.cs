using System.Numerics;
using System.Diagnostics.CodeAnalysis; // Aurora's Song
using Content.Server._NF.Salvage; // Aurora's Song
using Content.Server.Salvage.Expeditions;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Chat;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC; // Aurora's Song
using Content.Shared.Damage; // Aurora's Song
using Content.Shared.NPC.Components; // Aurora's Song
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Shuttles.Components;
using Content.Shared.Localizations;
using Content.Shared.Mind.Components; // Aurora's Song
using Content.Shared.Warps; // Aurora's Song
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Map; // Frontier
using Content.Server.GameTicking; // Frontier
using Content.Server._NF.Salvage.Expeditions.Structure; // Frontier
using Content.Server._NF.Salvage.Expeditions;
using Content.Shared.Salvage; // Aurora's Song
using Content.Shared.Buckle; // Aurora's Song
using Content.Shared.Buckle.Components; // Aurora's Song
using Content.Shared.Damage.Systems; // Aurora's Song
using Content.Shared.Implants; // Aurora's Song
using Content.Shared.Station.Components; // Aurora's Song
using Robust.Server.Player; // Coyote
using Robust.Shared.Enums; // Frontier

namespace Content.Server.Salvage;

public sealed partial class SalvageSystem
{
    /*
     * Handles actively running a salvage expedition.
     */

    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!; // Frontier
    [Dependency] private readonly DamageableSystem _damageable = default!; // AS
    [Dependency] private readonly IPlayerManager _players = default!; // Coyote
    [Dependency] private readonly SharedBuckleSystem _buckle = default!; // AS

    private void InitializeRunner()
    {
        SubscribeLocalEvent<FTLRequestEvent>(OnFTLRequest);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<ConsoleFTLAttemptEvent>(OnConsoleFTLAttempt);
    }

    private void OnConsoleFTLAttempt(ref ConsoleFTLAttemptEvent ev)
    {
        if (!TryComp(ev.Uid, out TransformComponent? xform) ||
            !TryComp<SalvageExpeditionComponent>(xform.MapUid, out var salvage))
        {
            return;
        }

        // TODO: This is terrible but need bluespace harnesses or something.
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, MobStateComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var mobState, out var mobXform))
        {
            if (mobXform.MapUid != xform.MapUid)
                continue;

            // Don't count unidentified humans (loot) or anyone you murdered so you can still maroon them once dead.
            if (_mobState.IsDead(uid, mobState))
                continue;

            // Okay they're on salvage, so are they on the shuttle.
            if (mobXform.GridUid != ev.Uid)
            {
                ev.Cancelled = true;
                ev.Reason = Loc.GetString("salvage-expedition-not-all-present");
                return;
            }
        }
    }

    /// <summary>
    /// Announces status updates to salvage crewmembers on the state of the expedition.
    /// </summary>
    private void Announce(EntityUid mapUid, string text)
    {
        var mapId = Comp<MapComponent>(mapUid).MapId;

        // I love TComms and chat!!!
        _chat.ChatMessageToManyFiltered(
            Filter.BroadcastMap(mapId),
            ChatChannel.Radio,
            text,
            text,
            _mapSystem.GetMapOrInvalid(mapId),
            false,
            true,
            null);
    }

    private void OnFTLRequest(ref FTLRequestEvent ev)
    {
        if (!HasComp<SalvageExpeditionComponent>(ev.MapUid) ||
            !TryComp<FTLDestinationComponent>(ev.MapUid, out var dest))
        {
            return;
        }

        // Only one shuttle can occupy an expedition.
        dest.Enabled = false;
        _shuttleConsoles.RefreshShuttleConsoles();
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        if (!TryComp<SalvageExpeditionComponent>(args.MapUid, out var component))
            return;

        // Someone FTLd there so start announcement
        if (component.Stage != ExpeditionStage.Added)
            return;

        // Frontier: early finish
        if (TryComp<SalvageExpeditionDataComponent>(component.Station, out var data))
        {
            data.CanFinish = true;
            UpdateConsoles((component.Station, data));
        }
        // End Frontier: early finish

        Announce(args.MapUid, Loc.GetString("salvage-expedition-announcement-countdown-minutes", ("duration", (component.EndTime - _timing.CurTime).Minutes)));

        var directionLocalization = ContentLocalizationManager.FormatDirection(component.DungeonLocation.GetDir()).ToLower();

        if (component.DungeonLocation != Vector2.Zero)
            Announce(args.MapUid, Loc.GetString("salvage-expedition-announcement-dungeon", ("direction", directionLocalization)));

        // Frontier: type-specific announcement
        switch (component.MissionParams.MissionType)
        {
            case SalvageMissionType.Destruction:
                if (TryComp<SalvageDestructionExpeditionComponent>(args.MapUid, out var destruction)
                    && destruction.Structures.Count > 0
                    && TryComp(destruction.Structures[0], out MetaDataComponent? structureMeta)
                    && structureMeta.EntityPrototype != null)
                {
                    var name = structureMeta.EntityPrototype.Name;
                    if (string.IsNullOrWhiteSpace(name))
                        name = Loc.GetString("salvage-expedition-announcement-destruction-entity-fallback");
                    // Assuming all structures are of the same type.
                    Announce(args.MapUid, Loc.GetString("salvage-expedition-announcement-destruction", ("structure", name), ("count", destruction.Structures.Count)));
                }
                break;
            case SalvageMissionType.Elimination:
                if (TryComp<SalvageEliminationExpeditionComponent>(args.MapUid, out var elimination)
                    && elimination.Megafauna.Count > 0
                    && TryComp(elimination.Megafauna[0], out MetaDataComponent? targetMeta)
                    && targetMeta.EntityPrototype != null)
                {
                    var name = targetMeta.EntityPrototype.Name;
                    if (string.IsNullOrWhiteSpace(name))
                        name = Loc.GetString("salvage-expedition-announcement-elimination-entity-fallback");
                    // Assuming all megafauna are of the same type.
                    Announce(args.MapUid, Loc.GetString("salvage-expedition-announcement-elimination", ("target", name), ("count", elimination.Megafauna.Count)));
                }
                break;
            default:
                break; // No announcement
        }
        // End Frontier

        component.Stage = ExpeditionStage.Running;
        Dirty(args.MapUid, component);
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        if (!TryComp<SalvageExpeditionComponent>(ev.FromMapUid, out var expedition) ||
            !TryComp<SalvageExpeditionDataComponent>(expedition.Station, out var station))
        {
            return;
        }

        station.CanFinish = false; // Frontier

        // Check if any shuttles remain.
        var query = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();

        while (query.MoveNext(out _, out var xform))
        {
            if (xform.MapUid == ev.FromMapUid)
                return;
        }

        // Last shuttle has left so finish the mission.
        QueueDel(ev.FromMapUid.Value);
    }

    // Runs the expedition
    private void UpdateRunner()
    {
        // Generic missions
        var query = EntityQueryEnumerator<SalvageExpeditionComponent>();

        // Run the basic mission timers (e.g. announcements, auto-FTL, completion, etc)
        while (query.MoveNext(out var uid, out var comp))
        {
            var remaining = comp.EndTime - _timing.CurTime;
            var audioLength = _audio.GetAudioLength(comp.SelectedSong);

            AbortIfWiped(uid, comp); // Coyote

            if (comp.Stage < ExpeditionStage.FinalCountdown && remaining < TimeSpan.FromSeconds(45))
            {
                comp.Stage = ExpeditionStage.FinalCountdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("salvage-expedition-announcement-countdown-seconds", ("duration", TimeSpan.FromSeconds(45).Seconds)));
            }
            else if (comp.Stage < ExpeditionStage.MusicCountdown && remaining < audioLength) // Frontier
            {
                // Frontier: handled client-side.
                // var audio = _audio.PlayPvs(comp.Sound, uid);
                // comp.Stream = audio?.Entity;
                // _audio.SetMapAudio(audio);
                // End Frontier
                comp.Stage = ExpeditionStage.MusicCountdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("salvage-expedition-announcement-countdown-minutes", ("duration", audioLength.Minutes)));
            }
            else if (comp.Stage < ExpeditionStage.Countdown && remaining < TimeSpan.FromMinutes(5)) // Frontier: 4<5
            {
                comp.Stage = ExpeditionStage.Countdown;
                Dirty(uid, comp);
                Announce(uid, Loc.GetString("salvage-expedition-announcement-countdown-minutes", ("duration", TimeSpan.FromMinutes(5).Minutes)));
            }
            // Auto-FTL out any shuttles
            else if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime) + TimeSpan.FromSeconds(0.5))
            {
                var ftlTime = (float)remaining.TotalSeconds;

                if (remaining < TimeSpan.FromSeconds(_shuttle.DefaultStartupTime))
                {
                    ftlTime = MathF.Max(0, (float)remaining.TotalSeconds - 0.5f);
                }

                ftlTime = MathF.Min(ftlTime, _shuttle.DefaultStartupTime);
                var shuttleQuery = AllEntityQuery<ShuttleComponent, TransformComponent>();

                if (TryComp<StationDataComponent>(comp.Station, out var data))
                {
                    foreach (var member in data.Grids)
                    {
                        while (shuttleQuery.MoveNext(out var shuttleUid, out var shuttle, out var shuttleXform))
                        {
                            if (shuttleXform.MapUid != uid || HasComp<FTLComponent>(shuttleUid))
                                continue;

                            // Frontier: try to find a potential destination for ship that doesn't collide with other grids.
                            var mapId = _gameTicker.DefaultMap;
                            if (!_mapSystem.TryGetMap(mapId, out var mapUid))
                            {
                                Log.Error($"Could not get DefaultMap EntityUID, shuttle {shuttleUid} may be stuck on expedition.");
                                continue;
                            }

                            // Destination generator parameters (move to CVAR?)
                            int numRetries = 20; // Maximum number of retries
                            float minDistance = 200f; // Minimum distance from another object, in meters
                            float minRange = 750f; // Minimum distance from sector centre, in meters
                            float maxRange = 3500f; // Maximum distance from sector centre, in meters

                            // Get a list of all grid positions on the destination map
                            List<Vector2> gridCoords = new();
                            var gridQuery = EntityManager.AllEntityQueryEnumerator<MapGridComponent, TransformComponent>();
                            while (gridQuery.MoveNext(out var _, out _, out var xform))
                            {
                                if (xform.MapID == mapId)
                                    gridCoords.Add(_transform.GetWorldPosition(xform));
                            }

                            Vector2 dropLocation = _random.NextVector2(minRange, maxRange);
                            for (int i = 0; i < numRetries; i++)
                            {
                                bool positionIsValid = true;
                                foreach (var station in gridCoords)
                                {
                                    if (Vector2.Distance(station, dropLocation) < minDistance)
                                    {
                                        positionIsValid = false;
                                        break;
                                    }
                                }

                                if (positionIsValid)
                                    break;

                                // No good position yet, pick another random position.
                                dropLocation = _random.NextVector2(minRange, maxRange);
                            }

                            _shuttle.FTLToCoordinates(
                                shuttleUid,
                                shuttle,
                                new EntityCoordinates(mapUid.Value, dropLocation),
                                0f,
                                ftlTime,
                                TravelTime);
                            // End Frontier:  try to find a potential destination for ship that doesn't collide with other grids.
                            //_shuttle.FTLToDock(shuttleUid, shuttle, member, ftlTime); // Frontier: use above instead
                        }

                        break;
                    }
                }
            }

            if (remaining < TimeSpan.FromSeconds(2.5) && comp.Warped == false) // Begin Aurora's Song: Get players and non-hostile ghost roles left on the expedition and yeet them onto the shuttle before we delete the map
            {
                if (TryFindShuttle(uid, comp, out var shuttleUid) && shuttleUid is { } shuttleGrid)
                {
                    FindPlayers(uid, shuttleGrid, out var players);
                    foreach (var entity in players)
                    {
                        ReturnToShuttle(entity, shuttleGrid);
                    }
                }
                else
                {
                    FindPlayers(uid, null, out var players);
                    if (players.Count > 0)
                    {
                        foreach (var entity in players)
                        {
                            Log.Debug($"Trying to warp {entity}");
                            if (!_mapSystem.TryGetMap(_gameTicker.DefaultMap, out var mapUid))
                            {
                                Log.Error($"Could not get DefaultMap EntityUID, entity {entity} may be deleted.");
                                break;
                            }
                            var fallback = new EntityCoordinates(mapUid.Value, _random.NextVector2(2000f, 2000f));
                            SafetyWarp(entity, fallback);
                        }
                    }
                }
                comp.Warped = true;
            } // End Aurora's Song

            if (remaining < TimeSpan.Zero)
                QueueDel(uid);
        }

        // Frontier: mission-specific logic
        // Destruction
        var structureQuery = EntityQueryEnumerator<SalvageDestructionExpeditionComponent, SalvageExpeditionComponent>();

        while (structureQuery.MoveNext(out var uid, out var structure, out var comp))
        {
            if (comp.Completed)
                continue;

            var structureAnnounce = false;

            for (var i = structure.Structures.Count - 1; i >= 0; i--)
            {
                var objective = structure.Structures[i];

                if (Deleted(objective))
                {
                    structure.Structures.RemoveAt(i);
                    structureAnnounce = true;
                }
            }

            if (structureAnnounce)
                Announce(uid, Loc.GetString("salvage-expedition-structure-remaining", ("count", structure.Structures.Count)));

            if (structure.Structures.Count == 0)
            {
                comp.Completed = true;
                Announce(uid, Loc.GetString("salvage-expedition-completed"));
            }
        }

        // Elimination
        var eliminationQuery = EntityQueryEnumerator<SalvageEliminationExpeditionComponent, SalvageExpeditionComponent>();
        while (eliminationQuery.MoveNext(out var uid, out var elimination, out var comp))
        {
            if (comp.Completed)
                continue;

            var announce = false;

            for (var i = elimination.Megafauna.Count - 1; i >= 0; i--)
            {
                var mob = elimination.Megafauna[i];

                if (Deleted(mob) || _mobState.IsDead(mob))
                {
                    elimination.Megafauna.RemoveAt(i);
                    announce = true;
                }
            }

            if (announce)
                Announce(uid, Loc.GetString("salvage-expedition-megafauna-remaining", ("count", elimination.Megafauna.Count)));

            if (elimination.Megafauna.Count == 0)
            {
                comp.Completed = true;
                Announce(uid, Loc.GetString("salvage-expedition-completed"));
            }
        }
        // End Frontier: mission-specific logic
    }

    // Coyote
    /// <summary>
    /// Checks if everyone on the map worth caring about is dead, and aborts the expedition if so.
    /// </summary>
    // Honestly, as long as one person is not in crit and not SSD, we consider the expedition salvageable.
    private void AbortIfWiped(EntityUid mapUid, SalvageExpeditionComponent component)
    {
        // give it a 30 second grade after first check to avoid instant aborts
        if (component.NextAutoAbortCheck == TimeSpan.Zero)
        {
            component.NextAutoAbortCheck = _timing.CurTime + TimeSpan.FromSeconds(30);
            return;
        }
        // only check frequently in case of some method of revival and/or performance methods
        if (_timing.CurTime < component.NextAutoAbortCheck)
            return;
        component.NextAutoAbortCheck = _timing.CurTime + TimeSpan.FromSeconds(15);

        var query =
            EntityQueryEnumerator<
                HumanoidAppearanceComponent,
                MindContainerComponent,
                MobStateComponent,
                TransformComponent>();
        // prevent abort if:
        // - anyone is alive AND connected
        while (query.MoveNext(
                   out var uid,
                   out _,
                   out var mindC,
                   out var mobState,
                   out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;
            // unidentified humans (loot) dont count
            if (!mindC.HasMind)
                continue;
            // if anyone is alive and not in crit, we are good
            if (_mobState.IsAlive(uid, mobState))
            {
                // okay weve got something alive, is their session?
                _players.TryGetSessionByEntity(uid, out var session);
                // if no session, check if they are SSD
                if (session == null)
                    continue;
                if (session.Status == SessionStatus.Disconnected)
                    continue;
                component.ChecksFailed = 0; // Aurora's Song
                return; // alive and connected player found, expedition is salvageable
            }
        }
        component.ChecksFailed += 1; // Aurora's Song
        if (component.ChecksFailed < component.AllowedFailures) // Aurora's Song
            return;

        // everyone is dead or ssd, abort the expedition
        const int departTime = 20;
        Announce(mapUid, Loc.GetString("salvage-expedition-abort-wipe", ("departTime", departTime)));
        component.NextAutoAbortCheck = _timing.CurTime + TimeSpan.FromDays(1); // prevent further checks
        var newEndTime = _timing.CurTime + TimeSpan.FromSeconds(departTime);

        if (component.EndTime <= newEndTime)
            return;

        component.Stage = ExpeditionStage.FinalCountdown;
        component.EndTime = newEndTime;
    }

    // Aurora's Song
    /// <summary>
    /// Attempts to warp an entity to a given coordinates before damaging them and producing teleportation effects
    /// </summary>
    /// <param name="mobUid">The entity to be warped</param>
    /// <param name="warpDestination">The coordinates to warp them too</param>
    private void SafetyWarp(EntityUid mobUid, EntityCoordinates warpDestination)
    {
        Log.Debug($"Attempting to teleport {mobUid} to {warpDestination}");

        // first we teleport them
        var mobXform = Transform(mobUid);
        _transform.SetCoordinates(mobUid, mobXform, warpDestination);
        _transform.AttachToGridOrMap(mobUid, mobXform);
        Spawn("EffectFlashBluespaceQuiet", mobXform.Coordinates);

        // then we ensure they are dead
        if (_mobState.IsAlive(mobUid))
        {
            // Apply a large bricks worth of damage
            var damageAmount = new DamageSpecifier()
            {
                DamageDict = { ["Slash"] = 75, ["Blunt"] = 75, ["Heat"] = 75 }  // If you are still alive after this you deserve it
            };
            _damageable.TryChangeDamage(mobUid, damageAmount, true);
        }
        else if (TryComp<MobStateComponent>(mobUid, out var mobState))
        {

            var deathrattleEvent = new ReTriggerRattleImplantEvent(mobUid, mobState.CurrentState);
            RaiseLocalEvent(mobUid, deathrattleEvent);
        }
    }

    // Aurora's Song
    /// <summary>
    /// Attempts to find a valid shuttle on an expedition map
    /// </summary>
    /// <param name="mapUid">Uid of the Expedition Map</param>
    /// <param name="component">SalvageExpeditionComponent of the map</param>
    /// <param name="shuttleUid">uid of the found shuttle</param>
    /// <returns>Returns true if a shuttle with an expeditionary console was found</returns>
    private bool TryFindShuttle(EntityUid mapUid, SalvageExpeditionComponent component, [NotNullWhen(true)] out EntityUid? shuttleUid)
    {
        shuttleUid = null;

        if (!TryComp<StationDataComponent>(component.Station, out var data)) // If the ExpeditionComponent doesn't have a station, then we probably
            return false;

        HashSet<EntityUid> grids = new HashSet<EntityUid>(data.Grids);
        var query = AllEntityQuery<SalvageExpeditionConsoleComponent, TransformComponent>(); // First we want to find an expedition console on any of the grids on the expedition.
        while (query.MoveNext(out var consoleUID, out var _, out var xform))
        {
            if (xform.MapUid != mapUid) // Continue if the console isn't on the expedition map
                continue;

            if (xform.GridUid is { } grid && grids.Contains(grid) && HasComp<ShuttleComponent>(grid)) // The console is on one of our grids, we can stop looking
            {
                Log.Debug($"Shuttle found: {grid}");
                shuttleUid = grid;
                return true;
            }
        }
        return false;
    }

    // Aurora's Song
    /// <summary>
    /// Creates a HashSet of all players we want to recover on the given map that are not parented to the given grid.
    /// </summary>
    /// <param name="mapUid">The Expedition map</param>
    /// <param name="gridUid">The grid we want to check that players are not on</param>
    /// <param name="players">A HashSet of the player EnityUid's that need to be returned</param>
    private void FindPlayers(EntityUid mapUid, EntityUid? gridUid, out HashSet<EntityUid> players)
    {
        players = new HashSet<EntityUid>();
        var playerQuery = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        while (playerQuery.MoveNext(out var playerQUID, out var mindContainer, out var mobXform))
        {
            // They aren't on the expedition, continue
            if (mobXform.MapUid != mapUid)
                continue;

            // They are on the shuttle already, nothing needs to be done with them. Continue.
            if (mobXform.GridUid == gridUid)
                continue;

            // Doesn't have a mind, not a player. Continue
            if (!mindContainer.HasMind) // If a player dies and they ghost, they should have a mind still
                continue;

            // NPC, definitely not a player. Continue
            if (HasComp<ActiveNPCComponent>(playerQUID) || HasComp<NFSalvageMobRestrictionsComponent>(playerQUID))
                continue;

            // Hostile ghost role. Continue
            if (TryComp(playerQUID, out NpcFactionMemberComponent? npcFaction))
            {
                var hostileFactions = npcFaction.HostileFactions;
                if (hostileFactions.Contains("NanoTrasen")) // TODO: move away from hardcoded faction
                    continue;
            }
            players.Add(playerQUID);
        }
    }

    // Aurora's Song
    /// <summary>
    /// Attempts to warp a given entity to somewhere on their shuttle. Desitination priority: Beds/Chairs > Shuttle Warp Point > Expedition Console > Some Random Location in space on the main map
    /// </summary>
    /// <param name="player"></param>
    /// <param name="shuttle"></param>
    private void ReturnToShuttle(EntityUid player, EntityUid shuttle)
    {
        var strapQuery = EntityQueryEnumerator<StrapComponent, TransformComponent>();
        while (strapQuery.MoveNext(out var strapuid, out var strapComp, out var xform)) // 1. Try to find an unnocupied bed/chair and send them to it
        {

            if (Transform(player).GridUid == shuttle)
                continue;

            if (xform.GridUid != shuttle)
                continue;

            if (strapComp.BuckledEntities.Count > 0)
                continue;

            Log.Debug($"Strap point found: {strapuid}");
            SafetyWarp(player, xform.Coordinates);
            _buckle.TryBuckle(strapuid, null, strapuid);
            return;
        }

        var warpQuery = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
        while (warpQuery.MoveNext(out var warpuid, out var _, out var xform)) // 2. Try to find the shuttles warp point and send them to it
        {
            if (Transform(player).GridUid == shuttle)
                continue;

            if (xform.GridUid != shuttle)
                continue;

            Log.Debug($"Warp point found: {warpuid}");
            SafetyWarp(player, xform.Coordinates);
            return;
        }

        var consoleQuery = EntityQueryEnumerator<SalvageExpeditionConsoleComponent, TransformComponent>();
        while (consoleQuery.MoveNext(out var consoleuid, out var _, out var xform)) // 3. Try to find the shuttles expedition console and send them to it
        {
            if (Transform(player).GridUid == shuttle)
                continue;

            if (xform.GridUid != shuttle)
                continue;

            Log.Debug($"Warp point found: {consoleuid}");
            SafetyWarp(player, xform.Coordinates);
            return;
        }

        if (!_mapSystem.TryGetMap(_gameTicker.DefaultMap, out var mapUid)) // 4. Send them to a point in space on the default map.
        {
            Log.Error($"Could not get DefaultMap EntityUID, entity {player} may be deleted.");
            return;
        }

        Log.Debug("Resorting to fallback");
        var fallback = new EntityCoordinates(mapUid.Value, _random.NextVector2(2000f, 2000f));
        SafetyWarp(player, fallback);
    }
}
