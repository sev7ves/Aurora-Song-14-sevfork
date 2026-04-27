namespace Content.Shared.Kitchen;

/// <summary>
/// Raised on an entity when it is inside a microwave and it starts cooking.
/// </summary>
public sealed class BeingMicrowavedEvent(EntityUid microwave, EntityUid? user, bool heating, bool irradiating) : HandledEntityEventArgs // Frontier: added heating, irradiating
{
    public EntityUid Microwave = microwave;
    public EntityUid? User = user;
    // Frontier: fields for whether or not the object is actually being heated or irradiated.
    public bool BeingHeated;
    public bool BeingIrradiated;
    // End Frontier
}
