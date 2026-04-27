using Content.Shared._AS.License; // Aurora
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Mindshield.Components; // Aurora
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed class ShowJobIconsSystem : EquipmentHudSystem<ShowJobIconsComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly LicenseSystem _license = default!; // Aurora

    private static readonly ProtoId<JobIconPrototype> JobIconForNoId = "JobIconNoId";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusIconComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, StatusIconComponent _, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        var iconId = JobIconForNoId;

        if (_accessReader.FindAccessItemsInventory(uid, out var items))
        {
            foreach (var item in items)
            {
                // ID Card
                if (TryComp<IdCardComponent>(item, out var id))
                {
                    iconId = id.JobIcon;
                    break;
                }

                // PDA
                if (TryComp<PdaComponent>(item, out var pda)
                    && pda.ContainedId != null
                    && TryComp(pda.ContainedId, out id))
                {
                    iconId = id.JobIcon;
                    break;
                }
            }
        }

        if (_prototype.Resolve(iconId, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
        else
            Log.Error($"Invalid job icon prototype: {iconPrototype}");

        // Aurora - This is yucky gross and evil but there isnt really another way to do this while keeping my sanity.
        // Don't display if mindshield is active since mindshield roles are kinda an upgrade to a license anyway.
        if (_license.TryGetActiveLicenses(uid, out var licenses) && !HasComp<MindShieldComponent>(uid))
        {
            // for now just grab the first licenses icon since there is no differentiation between them
            // the order of the licenses will be id slot first then hands
            var license = licenses[0];
            if (_prototype.TryIndex(license.LicenseStatusIcon, out var licenseIconPrototype))
                ev.StatusIcons.Add(licenseIconPrototype);
        }
    }
}
