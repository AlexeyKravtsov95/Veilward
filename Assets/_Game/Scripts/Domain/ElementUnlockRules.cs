using System;
using System.Collections.Generic;

namespace Veilward.Domain
{
    public sealed class ElementUnlockRules
    {
        public ElementUnlockRules(IReadOnlyList<ElementKind> startingOptions, IReadOnlyList<int> choiceWaves)
        {
            if (startingOptions == null) throw new ArgumentNullException(nameof(startingOptions));
            if (choiceWaves == null) throw new ArgumentNullException(nameof(choiceWaves));
            if (startingOptions.Count == 0) throw new ArgumentException("Starting options cannot be empty.", nameof(startingOptions));
            var options = new ElementKind[startingOptions.Count];
            var unique = new HashSet<ElementKind>();
            for (var index = 0; index < options.Length; index++)
            {
                var option = startingOptions[index];
                if (!Enum.IsDefined(typeof(ElementKind), option) || !unique.Add(option))
                    throw new ArgumentException("Starting options must be known and unique.", nameof(startingOptions));
                options[index] = option;
            }
            var waves = new int[choiceWaves.Count];
            for (var index = 0; index < waves.Length; index++)
            {
                var wave = choiceWaves[index];
                if (wave <= 0 || (index > 0 && wave <= waves[index - 1]))
                    throw new ArgumentException("Choice waves must be positive, unique and ascending.", nameof(choiceWaves));
                waves[index] = wave;
            }
            StartingOptions = Array.AsReadOnly(options);
            ChoiceWaves = Array.AsReadOnly(waves);
        }

        public IReadOnlyList<ElementKind> StartingOptions { get; }
        public IReadOnlyList<int> ChoiceWaves { get; }

        internal void ValidateWaveCount(int waveCount)
        {
            foreach (var wave in ChoiceWaves)
                if (wave >= waveCount)
                    throw new ArgumentException("Element choices require an existing non-final wave.", nameof(waveCount));
            if (ChoiceWaves.Count > 3)
                throw new ArgumentException("Unlock-only choices cannot exceed the remaining elements.");
        }

        internal bool IsChoiceWave(int wave)
        {
            foreach (var milestone in ChoiceWaves)
                if (milestone == wave) return true;
            return false;
        }
    }
}
