using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems
{
    public sealed class ScrambledAccentSystem : EntitySystem
    {
        private static readonly Regex RegexLoneI = new(@"(?<=\ )i(?=[\ \.\?]|$)");

        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<ScrambledAccentComponent, AccentGetEvent>(OnAccent);
            SubscribeLocalEvent<ScrambledAccentComponent, StatusEffectRelayedEvent<AccentGetEvent>>(OnAccentRelayed);
        }

        public string Accentuate(string message, ScrambledAccentComponent comp) // Aurora - pass component
        {
            var words = message.ToLower().Split();

            if (words.Length < 2 && comp.ScrambleSingleWords) // Aurora - check if filter is enabled for single word messages.
            {
                var pick = _random.Next(1, 8);
                // If they try to weasel out of it by saying one word at a time we give them this.
                return Loc.GetString($"accent-scrambled-words-{pick}");
            }

            // Scramble the words
            var scrambled = words.OrderBy(x => _random.Next()).ToArray();

            var msg = string.Join(" ", scrambled);

            // First letter should be capital
            msg = msg[0].ToString().ToUpper() + msg.Remove(0, 1);

            // Capitalize lone i's
            msg = RegexLoneI.Replace(msg, "I");
            return msg;
        }

        private void OnAccent(Entity<ScrambledAccentComponent> entity, ref AccentGetEvent args)
        {
            args.Message = Accentuate(args.Message, entity.Comp); // Aurora - pass component
        }

        private void OnAccentRelayed(Entity<ScrambledAccentComponent> entity, ref StatusEffectRelayedEvent<AccentGetEvent> args)
        {
            args.Args.Message = Accentuate(args.Args.Message, entity.Comp); // Aurora - pass component
        }
    }
}
