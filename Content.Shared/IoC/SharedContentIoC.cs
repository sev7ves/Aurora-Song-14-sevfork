using Content.Shared._AS.Consent; // Aurora's Song
using Content.Shared.Humanoid.Markings;
using Content.Shared.Localizations;
using Content.Shared.Tag; // Aurora's Song
using Content.Shared.Whitelist; // Aurora's Song

namespace Content.Shared.IoC
{
    public static class SharedContentIoC
    {
        public static void Register(IDependencyCollection deps)
        {
            deps.Register<MarkingManager, MarkingManager>();
            deps.Register<ContentLocalizationManager, ContentLocalizationManager>();
            // Start Aurora's Song
            deps.Register<TagSystem>();
            deps.Register<EntityWhitelistSystem>();
            deps.Register<SharedConsentCardSystem>();
            // End Aurora's Song
        }
    }
}
