// Aurora Song - AS Medical Bounty System
// Based on New Frontier Station 14's Medical Bounty System
// Original implementation: https://github.com/new-frontiers-14/frontier-station-14
// This system handles the initialization of medical bounties and their redemption
using System.Linq;
using Content.Server._NF.Bank;
using Content.Server.Administration.Logs;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Server._NF.Traits.Assorted;
using Content.Shared._AS.Medical;
using Content.Shared._AS.Medical.Prototypes;
using Content.Shared._NF.Bank.BUI;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._AS.Medical;

/// <summary>
/// Aurora Song: Main system for AS medical bounties - handles damage application,
/// bounty calculation, and redemption processing
/// </summary>
public sealed partial class ASMedicalBountySystem : EntitySystem
{
    [Dependency] IAdminLogManager _adminLog = default!;
    [Dependency] IRobustRandom _random = default!;
    [Dependency] IPrototypeManager _proto = default!;
    [Dependency] AudioSystem _audio = default!;
    [Dependency] BankSystem _bank = default!;
    [Dependency] BloodstreamSystem _bloodstream = default!;
    [Dependency] DamageableSystem _damageable = default!;
    [Dependency] HandsSystem _hands = default!;
    [Dependency] PopupSystem _popup = default!;
    [Dependency] PowerReceiverSystem _power = default!;
    [Dependency] SharedAppearanceSystem _appearance = default!;
    [Dependency] SharedContainerSystem _container = default!;
    [Dependency] StackSystem _stack = default!;
    [Dependency] TransformSystem _transform = default!;
    [Dependency] UserInterfaceSystem _ui = default!;

    private List<ASMedicalBountyPrototype> _cachedPrototypes = new();

    /// <summary>
    /// Aurora Song: Initialize the AS medical bounty system and subscribe to events
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        _proto.PrototypesReloaded += OnPrototypesReloaded;

        // Aurora Song: Subscribe to bounty component events
        SubscribeLocalEvent<ASMedicalBountyComponent, ComponentStartup>(InitializeMedicalBounty);
        SubscribeLocalEvent<ASMedicalBountyComponent, MobStateChangedEvent>(OnMobStateChanged);

        // Aurora Song: Subscribe to redemption component events
        SubscribeLocalEvent<ASMedicalBountyRedemptionComponent, RedeemASMedicalBountyMessage>(RedeemMedicalBounty);
        SubscribeLocalEvent<ASMedicalBountyRedemptionComponent, EntInsertedIntoContainerMessage>(OnEntityInserted);
        SubscribeLocalEvent<ASMedicalBountyRedemptionComponent, EntRemovedFromContainerMessage>(OnEntityRemoved);
        SubscribeLocalEvent<ASMedicalBountyRedemptionComponent, AfterActivatableUIOpenEvent>(OnActivateUI);
        SubscribeLocalEvent<ASMedicalBountyRedemptionComponent, PowerChangedEvent>(OnPowerChanged);

        CacheBountyPrototypes();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.ByType.ContainsKey(typeof(ASMedicalBountyPrototype))
            || (args.Removed?.ContainsKey(typeof(ASMedicalBountyPrototype)) ?? false))
        {
            CacheBountyPrototypes();
        }
    }

    /// <summary>
    /// Aurora Song: Cache all AS medical bounty prototypes for quick access
    /// </summary>
    private void CacheBountyPrototypes()
    {
        _cachedPrototypes = _proto.EnumeratePrototypes<ASMedicalBountyPrototype>().ToList();
    }

    /// <summary>
    /// Aurora Song: Initialize a medical bounty on an entity - applies damage and reagents
    /// </summary>
    private void InitializeMedicalBounty(EntityUid entity, ASMedicalBountyComponent component, ComponentStartup args)
    {
        if (component.BountyInitialized)
            return;

        if (component.Bounty == null)
        {
            // Try to load specific bounty from BountyId first
            if (!string.IsNullOrEmpty(component.BountyId))
            {
                if (_proto.TryIndex<ASMedicalBountyPrototype>(component.BountyId, out var specificBounty))
                    component.Bounty = specificBounty;
                else
                    Log.Warning($"Failed to find ASMedicalBountyPrototype with ID: {component.BountyId}");
            }

            // If still null, pick random bounty
            if (component.Bounty == null)
            {
                if (_cachedPrototypes.Count > 0)
                    component.Bounty = _random.Pick(_cachedPrototypes);
                else
                    return; // Nothing to do, keep bounty at null.
            }
        }

        // Precondition: check entity can fulfill bounty conditions
        if (!TryComp<DamageableComponent>(entity, out var damageable) ||
            !TryComp<BloodstreamComponent>(entity, out var bloodstream))
            return;

        // Apply damage from prototype, keep track of value
        DamageSpecifier damageToApply = new DamageSpecifier();
        var bountyValueAccum = component.Bounty.BaseReward;
        foreach (var (damageType, damageValue) in component.Bounty.DamageSets)
        {
            if (!_proto.TryIndex<DamageTypePrototype>(damageType, out var damageProto))
                continue;

            var randomDamage = _random.Next(damageValue.MinDamage, damageValue.MaxDamage + 1);
            bountyValueAccum += randomDamage * damageValue.ValuePerPoint;
            damageToApply += new DamageSpecifier(damageProto, randomDamage);
        }
        _damageable.TryChangeDamage((entity, damageable), damageToApply, true);

        // Inject reagents into chemical solution, if any
        foreach (var (reagentType, reagentValue) in component.Bounty.Reagents)
        {
            if (!_proto.HasIndex<ReagentPrototype>(reagentType))
                continue;

            Solution soln = new Solution();
            var reagentQuantity = _random.Next(reagentValue.MinQuantity, reagentValue.MaxQuantity + 1);
            soln.AddReagent(reagentType, reagentQuantity);
            if (_bloodstream.TryAddToBloodstream(entity, soln))
                bountyValueAccum += reagentQuantity * reagentValue.ValuePerPoint;
        }

        // Bounty calculation completed, set output state.
        component.MaxBountyValue = bountyValueAccum;
        component.BountyInitialized = true;
    }

    /// <summary>
    /// Aurora Song: Process the redemption of an AS medical bounty
    /// </summary>
    private void RedeemMedicalBounty(EntityUid uid, ASMedicalBountyRedemptionComponent component, RedeemASMedicalBountyMessage ev)
    {
        // Aurora Song: Check that the medical redeemer has a valid medical bounty inside
        if (!_container.TryGetContainer(uid, component.BodyContainer, out var container) ||
            container.ContainedEntities.Count <= 0)
        {
            _popup.PopupEntity(Loc.GetString("as-medical-bounty-redemption-fail-no-items"), uid);
            _audio.PlayPvs(component.DenySound, uid);
            return;
        }

        // Assumption: only one object can be in the MedicalBountyRedemption
        EntityUid bountyUid = container.ContainedEntities[0];

        if (!TryComp<ASMedicalBountyComponent>(bountyUid, out var medicalBounty) ||
            medicalBounty.Bounty == null ||
            !TryComp<DamageableComponent>(bountyUid, out var damageable))
        {
            _popup.PopupEntity(Loc.GetString("as-medical-bounty-redemption-fail-no-bounty"), uid);
            _audio.PlayPvs(component.DenySound, uid);
            return;
        }

        // Check that the entity inside is alive.
        var bounty = medicalBounty.Bounty;
        if (damageable.TotalDamage > bounty.MaximumDamageToRedeem)
        {
            _popup.PopupEntity(Loc.GetString("as-medical-bounty-redemption-fail-too-much-damage"), uid);
            _audio.PlayPvs(component.DenySound, uid);
            return;
        }

        // Calculate amount of reward to pay out.
        var bountyPayout = medicalBounty.MaxBountyValue;
        foreach (var (damageType, damageVal) in damageable.Damage.DamageDict)
        {
            if (bounty.DamageSets.ContainsKey(damageType))
            {
                bountyPayout -= (int)(bounty.DamageSets[damageType].PenaltyPerPoint * damageVal);
            }
            else
            {
                bountyPayout -= (int)(bounty.PenaltyPerOtherPoint * damageVal);
            }
        }

        string successString = "as-medical-bounty-redemption-success";
        if (TryComp<ASMedicalBountyBankPaymentComponent>(ev.Actor, out var bankPayment))
        {
            successString = "as-medical-bounty-redemption-success-to-station";
            // Find the fractions of the whole to pay out.
            _bank.TrySectorDeposit(bankPayment!.Account, bountyPayout, LedgerEntryType.MedicalBountyTax);
            _adminLog.Add(LogType.MedicalBountyRedeemed, LogImpact.Low, $"{ToPrettyString(ev.Actor):actor} redeemed the AS medical bounty for {ToPrettyString(bountyUid):subject}. Base value: {bountyPayout} (paid to station accounts).");
        }
        else if (bountyPayout > 0)
        {
            var stackUid = _stack.SpawnAtPosition(bountyPayout, "Credit", Transform(uid).Coordinates);
            if (!_hands.TryPickupAnyHand(ev.Actor, stackUid))
                _transform.SetLocalRotation(stackUid, Angle.Zero); // Orient these to grid north instead of map north

            _adminLog.Add(LogType.MedicalBountyRedeemed, LogImpact.Low, $"{ToPrettyString(ev.Actor):actor} redeemed the AS medical bounty for {ToPrettyString(bountyUid):subject}. Base value: {bountyPayout}.");
        }
        // Pay tax accounts
        foreach (var (account, taxCoeff) in component.TaxAccounts)
        {
            _bank.TrySectorDeposit(account, (int)(bountyPayout * taxCoeff), LedgerEntryType.MedicalBountyTax);
        }

        QueueDel(bountyUid);

        _popup.PopupEntity(Loc.GetString(successString), uid);
        _audio.PlayPvs(component.RedeemSound, uid);
        UpdateUserInterface(uid, component);
    }

    private void OnEntityInserted(EntityUid uid, ASMedicalBountyRedemptionComponent component, EntInsertedIntoContainerMessage args)
    {
        UpdateUserInterface(uid, component);
        _appearance.SetData(uid, ASMedicalBountyRedemptionVisuals.Full, true);
    }

    private void OnEntityRemoved(EntityUid uid, ASMedicalBountyRedemptionComponent component, EntRemovedFromContainerMessage args)
    {
        UpdateUserInterface(uid, component);
        _appearance.SetData(uid, ASMedicalBountyRedemptionVisuals.Full, false);
    }

    private void OnActivateUI(EntityUid uid, ASMedicalBountyRedemptionComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnPowerChanged(EntityUid uid, ASMedicalBountyRedemptionComponent component, PowerChangedEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    /// <summary>
    /// Aurora Song: Update the UI state for an AS medical bounty redemption machine
    /// </summary>
    public void UpdateUserInterface(EntityUid uid, ASMedicalBountyRedemptionComponent component)
    {
        if (!_ui.HasUi(uid, ASMedicalBountyRedemptionUiKey.Key))
            return;

        var actor = _ui.GetActors(uid, ASMedicalBountyRedemptionUiKey.Key).FirstOrDefault();

        if (!_power.IsPowered(uid))
        {
            _ui.CloseUis(uid);
            return;
        }

        _ui.SetUiState(uid, ASMedicalBountyRedemptionUiKey.Key, GetUserInterfaceState(uid, component, actor));
    }

    /// <summary>
    /// Aurora Song: Handle mob state changes to remove stinky trait when healed
    /// </summary>
    public void OnMobStateChanged(EntityUid uid, ASMedicalBountyComponent _, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical ||
            args.NewMobState == MobState.Alive)
        {
            RemComp<StinkyTraitComponent>(uid);
        }
    }

    /// <summary>
    /// Aurora Song: Calculate and return the current UI state for the redemption machine
    /// </summary>
    private ASMedicalBountyRedemptionUIState GetUserInterfaceState(EntityUid uid, ASMedicalBountyRedemptionComponent component, EntityUid actor)
    {
        var paidToStation = HasComp<ASMedicalBountyBankPaymentComponent>(actor);
        // Aurora Song: Check that the medical redeemer has a valid medical bounty inside
        if (!_container.TryGetContainer(uid, component.BodyContainer, out var container) ||
            container.ContainedEntities.Count <= 0)
        {
            return new ASMedicalBountyRedemptionUIState(ASMedicalBountyRedemptionStatus.NoBody, 0, paidToStation);
        }

        // Assumption: only one object can be stored in the MedicalBountyRedemption entity
        EntityUid bountyUid = container.ContainedEntities[0];

        // We either have no value or no way to accurately calculate the value of the bounty.
        if (!TryComp<ASMedicalBountyComponent>(bountyUid, out var medicalBounty) ||
            medicalBounty.Bounty == null ||
            !TryComp<DamageableComponent>(bountyUid, out var damageable) ||
            !TryComp<MobStateComponent>(bountyUid, out var mobState))
        {
            return new ASMedicalBountyRedemptionUIState(ASMedicalBountyRedemptionStatus.NoBounty, 0, paidToStation);
        }

        // Check that the entity inside is sufficiently healed.
        var bounty = medicalBounty.Bounty;
        if (damageable.TotalDamage > bounty.MaximumDamageToRedeem)
        {
            return new ASMedicalBountyRedemptionUIState(ASMedicalBountyRedemptionStatus.TooDamaged, 0, paidToStation);
        }

        // Check that the mob is alive.
        if (mobState.CurrentState != Shared.Mobs.MobState.Alive)
        {
            return new ASMedicalBountyRedemptionUIState(ASMedicalBountyRedemptionStatus.NotAlive, 0, paidToStation);
        }

        // Bounty is redeemable, calculate amount of reward to pay out.
        var bountyPayout = medicalBounty.MaxBountyValue;
        foreach (var (damageType, damageVal) in damageable.Damage.DamageDict)
        {
            if (bounty.DamageSets.ContainsKey(damageType))
            {
                bountyPayout -= (int)(bounty.DamageSets[damageType].PenaltyPerPoint * damageVal);
            }
            else
            {
                bountyPayout -= (int)(bounty.PenaltyPerOtherPoint * damageVal);
            }
        }

        return new ASMedicalBountyRedemptionUIState(ASMedicalBountyRedemptionStatus.Valid, int.Max(bountyPayout, 0), paidToStation);
    }
}
