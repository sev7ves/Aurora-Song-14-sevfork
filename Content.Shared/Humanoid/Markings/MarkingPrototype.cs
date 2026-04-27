using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array; // AuroraSong
using Robust.Shared.Utility;

namespace Content.Shared.Humanoid.Markings
{
    [Prototype]
    // AuroraSong: Make markings inheriting (IInheritingPrototype)
    public sealed partial class MarkingPrototype : IPrototype, IInheritingPrototype
    {
        [IdDataField]
        public string ID { get; private set; } = "uwu";

        // AuroraSong: Make markings inheriting
        [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<MarkingPrototype>))]
        public string[]? Parents { get; private set; }

        [NeverPushInheritance]
        [AbstractDataField]
        public bool Abstract { get; private set; }
        // End AuroraSong

        public string Name { get; private set; } = default!;

        [DataField("bodyPart", required: true)]
        public HumanoidVisualLayers BodyPart { get; private set; }

        [DataField("markingCategory", required: true)]
        public MarkingCategories MarkingCategory { get; private set; }

        [DataField("speciesRestriction")]
        public List<string>? SpeciesRestrictions { get; private set; }

        // DEN - Invert marking restrictions
        [DataField]
        public bool InvertSpeciesRestriction { get; private set; }

        [DataField]
        public Sex? SexRestriction { get; private set; }

        // DEN - Invert marking restrictions
        [DataField]
        public bool InvertSexRestriction { get; private set; }

        [DataField]
        public bool FollowSkinColor { get; private set; }

        [DataField]
        public bool ForcedColoring { get; private set; }

        [DataField]
        public MarkingColors Coloring { get; private set; } = new();

        /// <summary>
        /// Do we need to apply any displacement maps to this marking? Set to false if your marking is incompatible
        /// with a standard human doll, and is used for some special races with unusual shapes
        /// </summary>
        [DataField]
        public bool CanBeDisplaced { get; private set; } = true;

        [DataField("sprites", required: true)]
        public List<SpriteSpecifier> Sprites { get; private set; } = default!;

        // impstation edit - allow markings to support shaders
        [DataField("shader")]
        public string? Shader { get; private set; } = null;
        // end impstation edit

        // Aurora Song: Sort markings to the top for preferred species.

        /// <summary>
        /// A list of species IDs that will prefer to use this marking above others.
        /// Species in this list will have this marking sorted to the top, making them more accessible.
        /// In the future, if marking randomization is added, those will probably use this list too for cohesion.
        /// </summary>
        /// <remarks>
        /// For example: Imagine humans have various ear markings, ranging from regular humanoid ears, to
        /// pointy elf/imp-like ears, to kemonomimi traits that may overlap with other species such as
        /// vulpkanin or tajaran. The humanoid and elf ears may be preferred by humans, but their kemonomimi
        /// ears will be preferred by vulpkanin or tajaran respectively. This floats the elf/humanoid ears to the
        /// top of humans' ear marking lists.
        /// </remarks>
        [DataField]
        public HashSet<ProtoId<SpeciesPrototype>>? PreferredSpecies = null;

        // End Aurora Song

        public Marking AsMarking()
        {
            return new Marking(ID, Sprites.Count);
        }
    }
}
