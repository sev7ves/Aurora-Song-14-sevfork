using Content.Shared.Radio.Components;
using Content.Shared._DV.AACTablet; // Aurora's Song - Change namespace from DeltaV -> _DV
using Content.Shared._starcup.AACTablet;
using Content.Shared.Radio; // Aurora's Song
using Robust.Shared.Prototypes; // Aurora's Song

namespace Content.Server._DV.AACTablet; // Aurora's Song - Change namespace from DeltaV -> _DV

public sealed partial class AACTabletSystem
{
    private HashSet<ProtoId<RadioChannelPrototype>> GetAvailableChannels(EntityUid entity) // Aurora's Song - Change string>ProtoId<RadioChannelPrototype>
    {
        var channels = new HashSet<ProtoId<RadioChannelPrototype>>(); // Aurora's Song - Change string>ProtoId<RadioChannelPrototype>

        // Get all the intrinsic radio channels (IPCs, implants)
        if (TryComp(entity, out ActiveRadioComponent? intrinsicRadio))
            channels.UnionWith(intrinsicRadio.Channels);

        // Get the user's headset channels, if any
        if (TryComp(entity, out WearingHeadsetComponent? headset)
            && TryComp(headset.Headset, out ActiveRadioComponent? headsetRadio))
            channels.UnionWith(headsetRadio.Channels);

        return channels;
    }

    private void OnBoundUIOpened(Entity<AACTabletComponent> ent, ref BoundUIOpenedEvent args)
    {
        var state = new AACTabletBuiState(GetAvailableChannels(args.Actor));
        _userInterface.SetUiState(args.Entity, AACTabletKey.Key, state);
    }
}
