using Content.Shared.Radio.Components;
using Content.Shared.NameModifier.Components;
using Content.Server.Silicons.Laws;
using Content.Server.Chat.Managers;
using Content.Server.Ghost.Roles; // AS
using Content.Server.Ghost.Roles.Components; // AS
using Content.Shared._Corvax.Silicons.Borgs;
using Content.Shared._Corvax.Silicons.Borgs.Components;
using Content.Shared.Actions;
using Content.Shared.Mind;
using Content.Shared.Mind.Components; // AS
using Content.Shared.Chat;
using Content.Shared.NameModifier.Components; // AS
using Content.Shared.Database; // AS
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Corvax.Silicons.Borgs;

public sealed class AiRemoteControlSystem : SharedAiRemoteControlSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAiSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!; // AS
    [Dependency] private readonly GhostRoleSystem _ghostSystem = default!; // AS
    private EntityCoordinates? _coordinates;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AiRemoteControllerComponent, ReturnMindIntoAiEvent>(OnReturnMindIntoAi);
        SubscribeLocalEvent<AiRemoteControllerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AiRemoteControllerComponent, MindRemovedMessage>(OnMindRemoved); // AS
        SubscribeLocalEvent<AiRemoteControllerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AiRemoteControllerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<StationAiHeldComponent, AiRemoteControllerComponent.RemoteDeviceActionMessage>(OnUiRemoteAction);
        SubscribeLocalEvent<StationAiHeldComponent, ToggleRemoteDevicesScreenEvent>(OnToggleRemoteDevicesScreen);
    }

    private void OnMapInit(Entity<AiRemoteControllerComponent> entity, ref MapInitEvent args)
    {
        var visionComp = EnsureComp<StationAiVisionComponent>(entity.Owner);
        EntityUid? actionEnt = null;

        _actions.AddAction(entity.Owner, ref actionEnt, entity.Comp.BackToAiAction);

        if (actionEnt != null)
            entity.Comp.BackToAiActionEntity = actionEnt.Value;
    }

    private void OnShutdown(Entity<AiRemoteControllerComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.BackToAiActionEntity);

        var backArgs = new ReturnMindIntoAiEvent();
        backArgs.Performer = entity;

        if (TryComp(entity, out IntrinsicRadioTransmitterComponent? transmitter)
            && entity.Comp.PreviouslyTransmitterChannels != null)
            transmitter.Channels = [.. entity.Comp.PreviouslyTransmitterChannels];

        if (TryComp(entity, out ActiveRadioComponent? activeRadio)
            && entity.Comp.PreviouslyActiveRadioChannels != null)
            activeRadio.Channels = [.. entity.Comp.PreviouslyActiveRadioChannels];

        ReturnMindIntoAi(entity);
    }

    private void OnGetVerbs(Entity<AiRemoteControllerComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;

        if (!TryComp<StationAiHeldComponent>(user, out var stationAiHeldComp))
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("ai-remote-control"),
            Act = () => AiTakeControl(user, entity)
        };
        args.Verbs.Add(verb);
    }

    private void OnReturnMindIntoAi(Entity<AiRemoteControllerComponent> entity, ref ReturnMindIntoAiEvent args)
    {
        ReturnMindIntoAi(entity);
        _metaSystem.SetEntityName(entity, "Empty Remote Chassis"); // AS
    }
    public void AiTakeControl(EntityUid ai, EntityUid entity)
    {
        if (!_mind.TryGetMind(ai, out var mindId, out var mind))
            return;

        if (!TryComp<StationAiHeldComponent>(ai, out var stationAiHeldComp))
            return;

        if (!TryComp<AiRemoteControllerComponent>(entity, out var aiRemoteComp))
            return;
        if (_stationAiSystem.TryGetCore(ai, out var stationAiCore) && stationAiCore.Comp?.RemoteEntity != null
                && (Transform(stationAiCore).WorldPosition - Transform(entity).WorldPosition).Length() > 256 // AS: Changed range and made this use world position
            )
        {
            var msg = Loc.GetString("ai-remote-out-of-range");
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
            _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, Comp<ActorComponent>(ai).PlayerSession.Channel, colorOverride: Color.FromHex("#f30707"));
            // _audio.PlayEntity(new SoundPathSpecifier("Audio/Machines/buzz-sigh.ogg"), Comp<ActorComponent>(ai).PlayerSession, stationAiCore.Comp.RemoteEntity.Value); # Todo: Figure out how to make this work
            return;
        }

        if (TryComp(entity, out IntrinsicRadioTransmitterComponent? transmitter))
        {
            aiRemoteComp.PreviouslyTransmitterChannels = [.. transmitter.Channels];

            if (TryComp(ai, out IntrinsicRadioTransmitterComponent? stationAiTransmitter))
                transmitter.Channels = [.. stationAiTransmitter.Channels];
        }

        if (TryComp(entity, out ActiveRadioComponent? activeRadio))
        {
            aiRemoteComp.PreviouslyActiveRadioChannels = [.. activeRadio.Channels];

            if (TryComp(ai, out ActiveRadioComponent? stationAiActiveRadio))
                activeRadio.Channels = [.. stationAiActiveRadio.Channels];
        }

        stationAiHeldComp.CurrentConnectedEntity = entity; // AS: Moved because it was causing problems with ghost roles
        _mind.ControlMob(ai, entity);
        aiRemoteComp.AiHolder = ai;
        aiRemoteComp.LinkedMind = mindId;
        if (TryComp<NameModifierComponent>(ai, out var nameModifierComponent)) // AS: Make it rename things to represent its being remoted.
        {
            _metaSystem.SetEntityName(entity, nameModifierComponent.BaseName + " Remote Chassis");
        }
        else
        {
            _metaSystem.SetEntityName(entity, Comp<MetaDataComponent>(ai).EntityName + " Remote Chassis");
        }

        _stationAiSystem.SwitchRemoteEntityMode(stationAiCore, false);

        RewriteLaws(ai, entity);
    }

    private void OnToggleRemoteDevicesScreen(EntityUid uid, StationAiHeldComponent component, ToggleRemoteDevicesScreenEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor))
            return;
        args.Handled = true;

        _userInterface.TryToggleUi(uid, RemoteDeviceUiKey.Key, actor.PlayerSession);

        var query = EntityManager.EntityQueryEnumerator<AiRemoteControllerComponent>();
        var remoteDevices = new List<RemoteDevicesData>();

        while (query.MoveNext(out var queryUid, out var comp))
        {
            var data = new RemoteDevicesData
            {
                NetEntityUid = GetNetEntity(queryUid),
                DisplayName = Comp<MetaDataComponent>(queryUid).EntityName,
                DevicePosX = Transform(queryUid).WorldPosition.X,
                DevicePosY = Transform(queryUid).WorldPosition.Y,
                DeviceDistance = (Transform(uid).WorldPosition - Transform(queryUid).WorldPosition).Length() // AS: World position over relative and device distance.
            };
            if (_stationAiSystem.TryGetCore(uid, out var stationAiCore) && stationAiCore.Comp?.RemoteEntity != null
                    && (Transform(stationAiCore).WorldPosition - Transform(queryUid).WorldPosition).Length() < 4096 // AS: World position over relative
                )
            {
                remoteDevices.Add(data);
            };
        }

        var state = new RemoteDevicesBuiState(remoteDevices);
        _userInterface.SetUiState(uid, RemoteDeviceUiKey.Key, state);
    }

    private void OnUiRemoteAction(EntityUid uid, StationAiHeldComponent component, AiRemoteControllerComponent.RemoteDeviceActionMessage msg)
    {
        if (msg.RemoteAction == null)
            return;

        var target = GetEntity(msg.RemoteAction?.Target);

        if (!HasComp<AiRemoteControllerComponent>(target))
            return;

        switch (msg.RemoteAction?.ActionType)
        {
            case RemoteDeviceActionEvent.RemoteDeviceActionType.MoveToDevice:
                if (!_stationAiSystem.TryGetCore(uid, out var stationAiCore)
                    || stationAiCore.Comp?.RemoteEntity == null)
                    return;
                _xformSystem.SetCoordinates(stationAiCore.Comp.RemoteEntity.Value, Transform(target.Value).Coordinates);
                break;

            case RemoteDeviceActionEvent.RemoteDeviceActionType.TakeControl:
                AiTakeControl(uid, target.Value);
                break;
        }
    }

    private void RewriteLaws(EntityUid from, EntityUid to)
    {
        if (!TryComp<SiliconLawProviderComponent>(from, out var fromLawsComp))
            return;

        if (!TryComp<SiliconLawProviderComponent>(to, out var toLawsComp))
            return;

        if (fromLawsComp.Lawset == null)
            return;

        var fromLaws = _lawSystem.GetLaws(from);
        _lawSystem.SetLawsSilent(fromLaws.Laws, to);
    }

    private void OnMindRemoved(EntityUid uid, AiRemoteControllerComponent component, MindRemovedMessage args) // AS: Logic to handle ghosting while connected to a borg
    {
        if (component.AiHolder == null || component.LinkedMind == null) // If these are null, then the mind removal was likely from the AI returning to their core and we don't need to do anything
            return;

        if (!TryComp<StationAiHeldComponent>(component.AiHolder.Value, out var stationAiHeldComp)) // Somehow, what we were connected to wasn't an AI. Don't want to mess with it
            return;

        if (stationAiHeldComp.CurrentConnectedEntity == uid) // The AI still shows as connected to us, which means we probably ghosted, so we should try and re-register the ghost role if it exists.
        {
            if (!TryComp(component.AiHolder.Value, out GhostRoleComponent? ghostRole)) // Same logic as OnMindRemoved from GhostRoleSystem
                return;

            if (!ghostRole.ReregisterOnGhost || component.LifeStage > ComponentLifeStage.Running)
                return;

            _ghostSystem.ReRegisterGhostRole(component.AiHolder.Value, ghostRole);

            component.AiHolder = null;
            component.LinkedMind = null; // Null these out to set them up for later
        }
    }
}
