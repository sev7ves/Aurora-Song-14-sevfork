using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._AS.License;
using Content.Shared.Containers.ItemSlots; // Aurora
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.NameIdentifier;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Collections;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Implants; // Frontier
using Content.Shared.Implants.Components; // Frontier
using Content.Shared.Radio.Components; // Frontier
using Robust.Shared.Containers; // Frontier
using Robust.Shared.Network; // Frontier
using Content.Shared.Implants; // Frontier
using Content.Shared.Implants.Components;
using Content.Shared.Mind; // Frontier
using Content.Shared.Radio.Components; // Frontier
using Robust.Shared.Containers; // Frontier
using Robust.Shared.Network; // Frontier
using Content.Shared._AS.IPC;
using Content.Shared.Humanoid;
using Content.Shared.Preferences; // Aurora's Song 14

namespace Content.Shared.Station;

public abstract class SharedStationSpawningSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] protected readonly InventorySystem InventorySystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly INetManager _net = default!; // Frontier
    [Dependency] private readonly SharedContainerSystem _container = default!; // Frontier
    [Dependency] private readonly SharedImplanterSystem _implanter = default!; // Frontier
    [Dependency] private readonly LicenseSystem _license = default!; // Aurora
    [Dependency] private readonly SharedMindSystem _mind = default!; // Aurora
    [Dependency] private readonly InternalEncryptionLoadoutSystem _internalEncryptionLoadout = default!; // Aurora's Song 14

    private EntityQuery<HandsComponent> _handsQuery;
    private EntityQuery<InventoryComponent> _inventoryQuery;
    private EntityQuery<StorageComponent> _storageQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _handsQuery = GetEntityQuery<HandsComponent>();
        _inventoryQuery = GetEntityQuery<InventoryComponent>();
        _storageQuery = GetEntityQuery<StorageComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    /// <summary>
    ///     Equips the data from a `RoleLoadout` onto an entity.
    /// </summary>
    /// <remarks>
    ///     Frontier: must run on the server, requires bank access.
    ///     Frontier: currently not charging the player for this.
    /// </remarks>
    public void EquipRoleLoadout(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto)
    {
        // Order loadout selections by the order they appear on the prototype.
        foreach (var group in loadout.SelectedLoadouts.OrderBy(x => roleProto.Groups.FindIndex(e => e == x.Key)))
        {
            List<ProtoId<LoadoutPrototype>> equippedItems = new(); //Frontier - track purchased items (list: few items)
            foreach (var items in group.Value)
            {
                if (!PrototypeManager.TryIndex(items.Prototype, out var loadoutProto))
                {
                    Log.Error($"Unable to find loadout prototype for {items.Prototype}");
                    continue;
                }

                EquipStartingGear(entity, loadoutProto, raiseEvent: false);
                equippedItems.Add(loadoutProto.ID); // Frontier
            }

            // If a character cannot afford their current job loadout, ensure they have fallback items for mandatory categories.
            if (PrototypeManager.TryIndex(group.Key, out var groupPrototype) &&
                equippedItems.Count < groupPrototype.MinLimit)
            {
                foreach (var fallback in groupPrototype.Fallbacks)
                {
                    // Do not duplicate items in loadout
                    if (equippedItems.Contains(fallback))
                    {
                        continue;
                    }

                    if (!PrototypeManager.TryIndex(fallback, out var loadoutProto))
                    {
                        Log.Error($"Unable to find loadout prototype for fallback {fallback}");
                        continue;
                    }

                    EquipStartingGear(entity, loadoutProto, raiseEvent: false);
                    equippedItems.Add(fallback);
                    // Minimum number of items equipped, no need to load more prototypes.
                    if (equippedItems.Count >= groupPrototype.MinLimit)
                        break;
                }
            }
            // End Frontier
        }

        EquipRoleName(entity, loadout, roleProto);
    }

    /// <summary>
    /// Applies the role's name as applicable to the entity.
    /// </summary>
    public void EquipRoleName(EntityUid entity, RoleLoadout loadout, RoleLoadoutPrototype roleProto)
    {
        string? name = null;

        if (roleProto.CanCustomizeName)
        {
            name = loadout.EntityName;
        }

        if (string.IsNullOrEmpty(name) && PrototypeManager.Resolve(roleProto.NameDataset, out var nameData))
        {
            name = Loc.GetString(_random.Pick(nameData.Values));
        }

        if (!string.IsNullOrEmpty(name))
        {
            _metadata.SetEntityName(entity, name);
        }
    }

    public void EquipStartingGear(EntityUid entity, LoadoutPrototype loadout, bool raiseEvent = true)
    {
        EquipStartingGear(entity, loadout.StartingGear, raiseEvent);
        EquipStartingGear(entity, (IEquipmentLoadout)loadout, raiseEvent);
    }

    /// <summary>
    /// <see cref="EquipStartingGear(Robust.Shared.GameObjects.EntityUid,System.Nullable{Robust.Shared.Prototypes.ProtoId{Content.Shared.Roles.StartingGearPrototype}},bool)"/>
    /// </summary>
    public void EquipStartingGear(EntityUid entity, ProtoId<StartingGearPrototype>? startingGear, bool raiseEvent = true)
    {
        PrototypeManager.Resolve(startingGear, out var gearProto);
        EquipStartingGear(entity, gearProto, raiseEvent);
    }

    /// <summary>
    /// <see cref="EquipStartingGear(Robust.Shared.GameObjects.EntityUid,System.Nullable{Robust.Shared.Prototypes.ProtoId{Content.Shared.Roles.StartingGearPrototype}},bool)"/>
    /// </summary>
    public void EquipStartingGear(EntityUid entity, StartingGearPrototype? startingGear, bool raiseEvent = true)
    {
        EquipStartingGear(entity, (IEquipmentLoadout?)startingGear, raiseEvent);
    }

    /// <summary>
    /// Equips starting gear onto the given entity.
    /// </summary>
    /// <param name="entity">Entity to load out.</param>
    /// <param name="startingGear">Starting gear to use.</param>
    /// <param name="raiseEvent">Should we raise the event for equipped. Set to false if you will call this manually</param>
    public void EquipStartingGear(EntityUid entity, IEquipmentLoadout? startingGear, bool raiseEvent = true)
    {
        if (startingGear == null)
            return;

        var xform = _xformQuery.GetComponent(entity);

        if (InventorySystem.TryGetSlots(entity, out var slotDefinitions))
        {
            foreach (var slot in slotDefinitions)
            {
                var equipmentStr = startingGear.GetGear(slot.Name);
                if (!string.IsNullOrEmpty(equipmentStr))
                {
                    var equipmentEntity = Spawn(equipmentStr, xform.Coordinates);
                    InventorySystem.TryEquip(entity, equipmentEntity, slot.Name, silent: true, force: true);
                }
            }
        }

        if (_handsQuery.TryComp(entity, out var handsComponent))
        {
            var inhand = startingGear.Inhand;
            var coords = xform.Coordinates;
            foreach (var prototype in inhand)
            {
                var inhandEntity = Spawn(prototype, coords);

                if (_handsSystem.TryGetEmptyHand((entity, handsComponent), out var emptyHand))
                {
                    _handsSystem.TryPickup(entity, inhandEntity, emptyHand, checkActionBlocker: false, handsComp: handsComponent);
                }
            }
        }

        if (startingGear.Storage.Count > 0)
        {
            var coords = _xformSystem.GetMapCoordinates(entity);
            _inventoryQuery.TryComp(entity, out var inventoryComp);

            foreach (var (slotName, entProtos) in startingGear.Storage)
            {
                if (entProtos == null || entProtos.Count == 0)
                    continue;

                if (inventoryComp != null &&
                    InventorySystem.TryGetSlotEntity(entity, slotName, out var slotEnt, inventoryComponent: inventoryComp) &&
                    _storageQuery.TryComp(slotEnt, out var storage))
                {

                    foreach (var entProto in entProtos)
                    {
                        var spawnedEntity = Spawn(entProto, coords);

                        _storage.Insert(slotEnt.Value, spawnedEntity, out _, storageComp: storage, playSound: false);
                    }
                }
            }
        }

        // Begin Aurora's Song 14 additions
        if (_net.IsServer)
        {
            _internalEncryptionLoadout.TryEquipLoadoutEquipment(entity, startingGear);
        }
        // End Aurora's Song 14 additions

        // Frontier: extra fields
        // Implants must run on server, container initialization only runs on server, and lobby dummies don't work.
        if (_net.IsServer && startingGear.Implants.Count > 0)
        {
            var coords = _xformSystem.GetMapCoordinates(entity);
            foreach (var entProto in startingGear.Implants)
            {
                var spawnedEntity = Spawn(entProto, coords);
                if (TryComp<ImplanterComponent>(spawnedEntity, out var implanter))
                    _implanter.Implant(entity, entity, spawnedEntity, implanter);
                else
                    DebugTools.Assert(false, $"Entity has an implant for {entProto}, which doesn't have an implanter component!");
                QueueDel(spawnedEntity);
            }
        }

        if (startingGear.EncryptionKeys.Count > 0)
        {
            EquipEncryptionKeysIfPossible(entity, startingGear.EncryptionKeys);
        }

        // PDA cartridges must run on server, installation logic exists server-side.
        if (_net.IsServer && startingGear.Cartridges.Count > 0)
        {
            EquipPdaCartridgesIfPossible(entity, startingGear.Cartridges);
        }
        // End Frontier

        // Aurora - Add licenses
        // Must be run on server, installation logic exists server-side.
        if (_net.IsServer && startingGear.Licenses.Count > 0)
        {
            // Spawn and drop all licenses on the ground and rename them to the owner.
            List<EntityUid> spawnedLicenses = new ();
            foreach (var license in startingGear.Licenses)
            {
                var licenseEnt = SpawnAtPosition(license, xform.Coordinates);
                spawnedLicenses.Add(licenseEnt);
                if (!TryComp<LicenseComponent>(licenseEnt, out var licenseComponent))
                    continue;
                _license.SetName((licenseEnt,licenseComponent), MetaData(entity).EntityName);
            }
            // Try to insert any license first in pda then in bags. The remaining ones will remain on ground.
            InsertLicenses(entity, spawnedLicenses, "id", "PDA-license");
            InsertLicenses(entity, spawnedLicenses, "back", "storagebase");
        }

        if (raiseEvent)
        {
            var ev = new StartingGearEquippedEvent(entity);
            RaiseLocalEvent(entity, ref ev);
        }
    }

    /// <summary>
    ///     Gets all the gear for a given slot when passed a loadout.
    /// </summary>
    /// <param name="loadout">The loadout to look through.</param>
    /// <param name="slot">The slot that you want the clothing for.</param>
    /// <returns>
    ///     If there is a value for the given slot, it will return the proto id for that slot.
    ///     If nothing was found, will return null
    /// </returns>
    public string? GetGearForSlot(RoleLoadout? loadout, string slot)
    {
        if (loadout == null)
            return null;

        foreach (var group in loadout.SelectedLoadouts)
        {
            foreach (var items in group.Value)
            {
                if (!PrototypeManager.Resolve(items.Prototype, out var loadoutPrototype))
                    return null;

                var gear = ((IEquipmentLoadout) loadoutPrototype).GetGear(slot);
                if (gear != string.Empty)
                    return gear;
            }
        }

        return null;
    }


    // Frontier: extra loadout fields
    /// Function to equip an entity with encryption keys.
    /// If not possible, will delete them.
    /// Only called in practice server-side.
    /// </summary>
    /// <param name="entity">The entity to receive equipment.</param>
    /// <param name="encryptionKeys">The encryption key prototype IDs to equip.</param>
    protected void EquipEncryptionKeysIfPossible(EntityUid entity, List<EntProtoId> encryptionKeys)
    {
        // Begin Aurora's Song 14 additions
        if (_internalEncryptionLoadout.TryEquipLoadoutEncryptionKeys(entity, encryptionKeys))
        {
            return;
        }
        // End Aurora's Song 14 additions

        if (!InventorySystem.TryGetSlotEntity(entity, "ears", out var slotEnt))
        {
            DebugTools.Assert(false, $"Entity {entity} has a non-empty encryption key loadout, but doesn't have a headset!");
            return;
        }
        if (!_container.TryGetContainer(slotEnt.Value, EncryptionKeyHolderComponent.KeyContainerName, out var keyContainer))
        {
            DebugTools.Assert(false, $"Entity {entity} has a non-empty encryption key loadout, but their headset doesn't have an encryption key container!");
            return;
        }
        var coords = _xformSystem.GetMapCoordinates(entity);
        foreach (var entProto in encryptionKeys)
        {
            var spawnedEntity = Spawn(entProto, coords);
            if (!_container.Insert(spawnedEntity, keyContainer))
            {
                QueueDel(spawnedEntity);
                DebugTools.Assert(false, $"Entity {entity} could not insert their loadout encryption key {entProto} into their headset!");
            }
        }
    }

    public bool GetProfile(EntityUid? uid, [NotNullWhen(true)] out HumanoidCharacterProfile? profile)
    {
        if (!TryComp(uid, out HumanoidAppearanceComponent? appearance))
        {
            profile = null;
            return false;
        }

        if (appearance.LastProfileLoaded is { } lastProfileLoaded)
        {
            profile = lastProfileLoaded;
            return true;
        }

        profile = HumanoidCharacterProfile.DefaultWithSpecies(appearance.Species);
        return true;
    }

    /// <summary>
    /// Function to equip an entity with PDA cartridges.
    /// If not possible, will delete them.
    /// Only called in practice server-side.
    /// </summary>
    /// <param name="entity">The entity to receive equipment.</param>
    /// <param name="encryptionKeys">The PDA cartridge prototype IDs to equip.</param>
    protected abstract void EquipPdaCartridgesIfPossible(EntityUid entity, List<EntProtoId> encryptionKeys);
    // End Frontier: extra loadout fields

    // Aurora - Licenses
    /// <summary>
    /// Function to insert license into given container.
    /// Only called in practice server-side.
    /// </summary>
    /// <param name="entity">The entity to receive equipment.</param>
    /// <param name="licenses">The license prototype IDs to equip</param>
    /// <param name="slot">slot the container is found in</param>
    /// <param name="container">container id</param>
    /// <exception cref="NotImplementedException"></exception>
    private void InsertLicenses(EntityUid entity, List<EntityUid> licenses, string slot, string container)
    {
        if (licenses.Count <= 0)
            return;
        if (!InventorySystem.TryGetSlotEntity(entity, slot, out var slotEnt))
            return;
        if (!_container.TryGetContainer(slotEnt.Value, container, out var keyContainer))
            return;

        var coords = _xformSystem.GetMapCoordinates(entity);

        foreach (var license in licenses.ToList())
        {
            if (_container.Insert(license, keyContainer))
            {
                licenses.Remove(license);
            }
        }
    }
}
