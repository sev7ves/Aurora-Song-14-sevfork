using Content.Server._NF.Cargo.Systems;
using Content.Server._NF.Contraband.Components;
using Content.Server.Cargo.Components;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Shared._AS.Contraband.Events; // Aurora
using Content.Shared._AS.Contraband.ScuOutput; // Aurora
using Content.Shared._AS.License; // Aurora
using Content.Shared._NF.Contraband.BUI;
using Content.Shared._NF.Contraband.Components;
using Content.Shared._NF.Contraband.Events;
using Content.Shared._NF.Contraband;
using Content.Shared.Access.Systems;
using Content.Shared.Contraband;
using Content.Shared.Coordinates;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Stacks;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map; // Aurora
using Robust.Shared.Prototypes;

namespace Content.Server._NF.Contraband.Systems;

/// <summary>
/// Contraband system. Contraband Pallet UI Console is mostly a copy of the system in cargo. Checkraze Note: copy of my code from cargosystems.shuttles.cs
/// </summary>
public sealed partial class ContrabandTurnInSystem : SharedContrabandTurnInSystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly AudioSystem _audio = default!; // Aurora
    [Dependency] private readonly AccessReaderSystem _reader = default!; // Aurora
    [Dependency] private readonly PopupSystem _popup = default!; // Aurora
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!; // Aurora
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!; // Aurora
    [Dependency] private readonly LicenseSystem _license = default!; // Aurora

    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<CargoSellBlacklistComponent> _blacklistQuery;

    private EntityUid _scuOutput;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();
        _blacklistQuery = GetEntityQuery<CargoSellBlacklistComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<ContrabandPalletConsoleComponent, ContrabandPalletSellMessage>(OnPalletSale);
        SubscribeLocalEvent<ContrabandPalletConsoleComponent, ContrabandPalletAppraiseMessage>(OnPalletAppraise);
        SubscribeLocalEvent<ContrabandPalletConsoleComponent, BoundUIOpenedEvent>(OnPalletUIOpen);
        SubscribeLocalEvent<ContrabandPalletConsoleComponent, ContrabandPalletRegisterMessage>(OnPalletRegister);
        SubscribeLocalEvent<ScuOutputComponent, ComponentInit>(OnScuOutputInit); // Aurora
    }

    private void OnScuOutputInit(Entity<ScuOutputComponent> ent, ref ComponentInit args) // Aurora
    {
        _scuOutput = ent;
    }

    private void UpdatePalletConsoleInterface(EntityUid uid, ContrabandPalletConsoleComponent comp)
    {
        var bui = _uiSystem.HasUi(uid, ContrabandPalletConsoleUiKey.Contraband);
        if (Transform(uid).GridUid is not EntityUid gridUid)
        {
            _uiSystem.SetUiState(uid, ContrabandPalletConsoleUiKey.Contraband,
                new ContrabandPalletConsoleInterfaceState(0, 0, 0, 0, false)); // AS: Allow alt reward currencies
            return;
        }

        GetPalletGoods(gridUid, (uid, comp), out var toSell, out var amount, out var altAmount, out var unregistered); // AS: Allow alt reward currencies

        var totalCount = toSell;
        toSell.UnionWith(unregistered);
        _uiSystem.SetUiState(uid, ContrabandPalletConsoleUiKey.Contraband,
            new ContrabandPalletConsoleInterfaceState((int) amount, (int) altAmount, totalCount.Count, unregistered.Count, true)); // AS: Allow alt reward currencies
    }

    private void OnPalletUIOpen(EntityUid uid, ContrabandPalletConsoleComponent component, BoundUIOpenedEvent args)
    {
        var player = args.Actor;

        UpdatePalletConsoleInterface(uid, component);
    }

    /// <summary>
    /// Ok so this is just the same thing as opening the UI, its a refresh button.
    /// I know this would probably feel better if it were like predicted and dynamic as pallet contents change
    /// However.
    /// I dont want it to explode if cargo uses a conveyor to move 8000 pineapple slices or whatever, they are
    /// known for their entity spam i wouldnt put it past them
    /// </summary>

    private void OnPalletAppraise(EntityUid uid, ContrabandPalletConsoleComponent component, ContrabandPalletAppraiseMessage args)
    {
        var player = args.Actor;

        UpdatePalletConsoleInterface(uid, component);
    }

    private List<(EntityUid Entity, ContrabandPalletComponent Component)> GetContrabandPallets(EntityUid consoleUid, EntityUid gridUid) // Aurora copy over max distance limit
    {
        var pads = new List<(EntityUid, ContrabandPalletComponent)>();

        if (!TryComp(consoleUid, out TransformComponent? consoleXform))
            return pads;

        var query = AllEntityQuery<ContrabandPalletComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var compXform))
        {
            // Short-path easy checks
            if (compXform.ParentUid != gridUid
                || !compXform.Anchored)
            {
                continue;
            }

            // Check distance on pallets
            var distance = CalculateDistance(compXform.Coordinates, consoleXform.Coordinates);
            var maxPalletDistance = 8;

            // Get the mapped checking distance from the console
            if (TryComp<ContrabandPalletConsoleComponent>(consoleUid, out var cargoShuttleComponent))
                maxPalletDistance = cargoShuttleComponent.PalletDistance;

            if (distance > maxPalletDistance)
                continue;

            pads.Add((uid, comp));

        }

        return pads;
    }

    private void SellPallets(EntityUid gridUid, Entity<ContrabandPalletConsoleComponent> component, EntityUid? station, out int amount, out int altAmount) // AS: Allow alt reward currencies
    {
        station ??= _station.GetOwningStation(gridUid);
        GetPalletGoods(gridUid, component, out var toSell, out amount, out altAmount, out _); // AS: Allow alt reward currencies

        Log.Debug($"{component.Comp.Faction} sold {toSell.Count} contraband items for {amount} primary currency and {altAmount} alternate currency");

        if (station != null)
        {
            var ev = new NFEntitySoldEvent(toSell, gridUid);
            RaiseLocalEvent(ref ev);
        }

        foreach (var ent in toSell)
        {
            Del(ent);
        }
    }

    private void GetPalletGoods(EntityUid gridUid, Entity<ContrabandPalletConsoleComponent> console, out HashSet<EntityUid> toSell, out int amount, out int altAmount, out HashSet<EntityUid> unregistered) // AS: Allow alt reward currencies
    {
        amount = 0;
        altAmount = 0;
        toSell = new HashSet<EntityUid>();
        unregistered = new HashSet<EntityUid>(); // Aurora

        foreach (var (palletUid, _) in GetContrabandPallets(console, gridUid))
        {
            foreach (var ent in _lookup.GetEntitiesIntersecting(palletUid,
                         LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate))
            {
                // Dont sell:
                // - anything already being sold
                // - anything anchored (e.g. light fixtures)
                // - anything blacklisted (e.g. players).
                if (_xformQuery.TryGetComponent(ent, out var xform) &&
                    (xform.Anchored || !CanSell(ent, xform)))
                {
                    continue;
                }

                if (_blacklistQuery.HasComponent(ent))
                    continue;

                if (TryComp<ContrabandComponent>(ent, out var comp)
                    && !toSell.Contains(ent)
                    && comp.TurnInValues is { } turnInValues
                    && (console.Comp.RewardType != null && turnInValues.ContainsKey(console.Comp.RewardType) || console.Comp.RewardTypeAlternate != null && turnInValues.ContainsKey(console.Comp.RewardTypeAlternate))) // AS: Allow alt reward currencies
                {
                    toSell.Add(ent);
                    if (console.Comp.RewardTypeAlternate != null && turnInValues.ContainsKey(console.Comp.RewardTypeAlternate)) // Begin AS: Allow alt reward currencies
                    {
                        var altValue = comp.TurnInValues[console.Comp.RewardTypeAlternate];
                        altAmount += altValue;
                    }
                    if (console.Comp.RewardType != null && turnInValues.ContainsKey(console.Comp.RewardType))
                    {
                        var value = comp.TurnInValues[console.Comp.RewardType];
                        amount += value;
                    } // End AS
                }

                // Aurora
                if (MetaData(ent).EntityPrototype is {} proto
                    && console.Comp.RegisterRecipies.ContainsKey(proto))
                {
                    unregistered.Add(ent);
                }
            }
        }
    }

    private bool CanSell(EntityUid uid, TransformComponent xform)
    {
        if (_mobQuery.HasComponent(uid))
        {
            if (_mobQuery.GetComponent(uid).CurrentState == MobState.Dead) // Allow selling alive prisoners
            {
                return false;
            }
            return true;
        }

        // Recursively check for mobs at any point.
        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!CanSell(child, _xformQuery.GetComponent(child)))
                return false;
        }
        // Look for blacklisted items and stop the selling of the container.
        if (_blacklistQuery.HasComponent(uid))
        {
            return false;
        }
        return true;
    }

    private void OnPalletSale(EntityUid uid, ContrabandPalletConsoleComponent component, ContrabandPalletSellMessage args)
    {
        var player = args.Actor;

        if (!CheckLicense(component, player)) // Aurora: add check for chl
        {
            PlayDenyEffect((uid, component));
            return;
        }

        if (Transform(uid).GridUid is not EntityUid gridUid)
        {
            _uiSystem.SetUiState(uid, ContrabandPalletConsoleUiKey.Contraband,
                new ContrabandPalletConsoleInterfaceState(0, 0, 0, 0, false)); // AS: Allow alt reward currencies
            return;
        }

        SellPallets(gridUid, (uid, component), null, out var reward, out var altReward); // AS: Allow alt reward currencies

        var outputQuery = EntityQueryEnumerator<ScuOutputComponent, TransformComponent>(); // Begin AS: Makes pirate contraband selling usable
        var output = uid.ToCoordinates();
        var outputSent = false;
        while (outputQuery.MoveNext(out var _, out var xform)) // If theres an entity with an ScuOutputComponent, we want to try and send the primary currency to that first
        {
            if (xform.GridUid == gridUid)
            {
                output = _scuOutput.ToCoordinates();
                outputSent = true;
            }
        }

        if (component.RewardType != null) // If we have a primary reward, spawn it
        {
            var rewardPrototype = _protoMan.Index<StackPrototype>(component.RewardType);
            var stackUid = _stack.SpawnAtPosition(reward, rewardPrototype, output);
            if (outputSent == false)
            {
                if (!_hands.TryPickupAnyHand(args.Actor, stackUid)) // If there wasn't a a ScuOutputComponent to send these too, try to pick them up
                    _transform.SetLocalRotation(stackUid, Angle.Zero);
            }
            else
            {
                _transform.SetLocalRotation(stackUid, Angle.Zero); // Orient these to grid north instead of map north
            }

        }

        if (component.RewardTypeAlternate != null) // If we have an alternate currency, spawn it
        {
            var altRewardPrototype = _protoMan.Index<StackPrototype>(component.RewardTypeAlternate);
            var altStackUid = _stack.SpawnAtPosition(altReward, altRewardPrototype, args.Actor.ToCoordinates());
            if (!_hands.TryPickupAnyHand(args.Actor, altStackUid))
                _transform.SetLocalRotation(altStackUid, Angle.Zero); // Orient these to grid north instead of map north if we can't pick them up
        } // End AS
        UpdatePalletConsoleInterface(uid, component);
    }

    // Aurora - Contra registering
    private void OnPalletRegister(Entity<ContrabandPalletConsoleComponent> ent, ref ContrabandPalletRegisterMessage args)
    {
        if (!CheckLicense(ent.Comp, args.Actor)) // check for chl
        {
            PlayDenyEffect(ent);
            return;
        }

        if (Transform(ent).GridUid is not EntityUid gridUid)
        {
            _uiSystem.SetUiState(ent.Owner, ContrabandPalletConsoleUiKey.Contraband,
                new ContrabandPalletConsoleInterfaceState(0, 0, 0, 0, false)); // AS: Allow alt reward currencies
            return;
        }
        GetPalletGoods(gridUid, ent, out _, out _, out _, out var toRegister); // AS: Allow alt reward currencies

        // Award Primary currency (Probably SCU's) if we have one
        if (ent.Comp.RewardType != null)
        {
            var stackPrototype = _protoMan.Index<StackPrototype>(ent.Comp.RewardType);
            // 1 SCU per registered item
            var stackUid = _stack.SpawnAtPosition(toRegister.Count, stackPrototype, _scuOutput.ToCoordinates());

            _transform.SetLocalRotation(stackUid, Angle.Zero); // Orient these to grid north instead of map north
        }

        //Exchange each item for their registered counterpart
        foreach (var oldEnt in toRegister)
        {
            if (MetaData(oldEnt).EntityPrototype is not {} oldProto)
                continue;
            ent.Comp.RegisterRecipies.TryGetValue(oldProto, out var newProto);
            var newEnt = SpawnAtPosition(newProto, Transform(oldEnt).Coordinates);
            _transform.SetLocalRotation(newEnt, Angle.Zero);

            // Transfer items into new ent
            if (TryComp<ContainerManagerComponent>(oldEnt, out var oldManager)
                && TryComp<ContainerManagerComponent>(newEnt, out var newManager))
            {
                foreach (var newContainer in newManager.Containers)
                {
                    if (newContainer.Key == "actions" || newContainer.Key == "toggleable-clothing")
                        continue;
                    if (!oldManager.Containers.TryGetValue(newContainer.Key, out var oldContainer))
                        continue;
                    _container.CleanContainer(newContainer.Value);
                    var entsToTransfer = oldContainer.ContainedEntities;
                    foreach (var item in entsToTransfer)
                    {
                        _container.Insert(item, newContainer.Value);
                    }
                }
            }

            Del(oldEnt);
            Log.Debug($"{ent.Comp.Faction} registered {oldEnt} into {newEnt}");
        }

        UpdatePalletConsoleInterface(ent, ent.Comp);
    }

    private bool CheckLicense(ContrabandPalletConsoleComponent console, EntityUid user) // Aurora
    {
        if (console.LicenseRequired == null)
            return true;

        return _license.CheckLicence(console.LicenseRequired, user);
    }

    public void PlayDenyEffect(Entity<ContrabandPalletConsoleComponent> target)
    {
        _popup.PopupCoordinates(Loc.GetString("chl-required"), Transform(target).Coordinates);
        _audio.PlayPvs(_audio.ResolveSound(target.Comp.ErrorSound), target);
    }

    // Aurora - copied from NFCargoSystem
    /// <summary>
    /// Calculates distance between two EntityCoordinates
    /// Used to check for cargo pallets around the console instead of on the grid.
    /// </summary>
    /// <param name="point1">first point to get distance between</param>
    /// <param name="point2">second point to get distance between</param>
    /// <returns></returns>
    public static double CalculateDistance(EntityCoordinates point1, EntityCoordinates point2)
    {
        var xDifference = point2.X - point1.X;
        var yDifference = point2.Y - point1.Y;

        return Math.Sqrt(xDifference * xDifference + yDifference * yDifference);
    }
}
