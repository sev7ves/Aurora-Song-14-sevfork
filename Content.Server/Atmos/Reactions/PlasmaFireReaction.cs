using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class PlasmaFireReaction : IGasReactionEffect
    {
        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
            var energyReleased = 0f;
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            var temperature = mixture.Temperature;
            var location = holder as TileAtmosphere;
            mixture.ReactionResults[(byte)GasReaction.Fire] = 0;

            // More plasma released at higher temperatures.
            var temperatureScale = 0f;

            if (temperature > Atmospherics.PlasmaUpperTemperature)
                temperatureScale = 1f;
            else
            {
                temperatureScale = (temperature - Atmospherics.PlasmaMinimumBurnTemperature) /
                                   (Atmospherics.PlasmaUpperTemperature - Atmospherics.PlasmaMinimumBurnTemperature);
            }

            if (temperatureScale > 0)
            {
                var oxygenBurnRate = Atmospherics.OxygenBurnRateBase - temperatureScale;
                var plasmaBurnRate = 0f;

                var initialOxygenMoles = mixture.GetMoles(Gas.Oxygen);
                var initialPlasmaMoles = mixture.GetMoles(Gas.Plasma);

                // Supersaturation makes tritium.
                var oxyRatio = initialOxygenMoles / initialPlasmaMoles;
                // Efficiency of reaction decreases from 1% Plasma to 3% plasma:
                var supersaturation = Math.Clamp((oxyRatio - Atmospherics.SuperSaturationEnds) /
                                                 (Atmospherics.SuperSaturationThreshold -
                                                  Atmospherics.SuperSaturationEnds), 0.0f, 1.0f);

                if (initialOxygenMoles > initialPlasmaMoles * Atmospherics.PlasmaOxygenFullburn)
                    plasmaBurnRate = initialPlasmaMoles * temperatureScale / Atmospherics.PlasmaBurnRateDelta;
                else
                    plasmaBurnRate = temperatureScale * (initialOxygenMoles / Atmospherics.PlasmaOxygenFullburn) / Atmospherics.PlasmaBurnRateDelta;

                if (plasmaBurnRate > Atmospherics.MinimumHeatCapacity)
                {
                    plasmaBurnRate = MathF.Min(plasmaBurnRate, MathF.Min(initialPlasmaMoles, initialOxygenMoles / oxygenBurnRate));
                    mixture.AdjustMoles(Gas.Plasma, -plasmaBurnRate); // Aurora's Song | AdjustedMoles instead of SetMoles to better respect the context of the operation
                    mixture.AdjustMoles(Gas.Oxygen, -(plasmaBurnRate * oxygenBurnRate)); // Aurora's Song | As above, AdjustMoles instead of SetMoles

                    var totalInput = plasmaBurnRate + (plasmaBurnRate * oxygenBurnRate); //Aurora's Song | total reactant moles used per second, used to calculate final output
                    // supersaturation adjusts the ratio of produced tritium to unwanted CO2
                    mixture.AdjustMoles(Gas.Tritium, totalInput * supersaturation); // Aurora's Song | total moles instead of plasmaBurnRate in order to ensure mass conservation
                    mixture.AdjustMoles(Gas.CarbonDioxide, totalInput * (1.0f - supersaturation)); // Aurora's Song | total moles instead of plasmaBurnRate in order to ensure
                                                                                // mass conservation, outputting more CO2 in order to respect Oxygen as a reactant and not just fuel

                    energyReleased += Atmospherics.FirePlasmaEnergyReleased * plasmaBurnRate;
                    energyReleased /= heatScale; // adjust energy to make sure speedup doesn't cause mega temperature rise
                    mixture.ReactionResults[(byte)GasReaction.Fire] += plasmaBurnRate * (1 + oxygenBurnRate);
                }
            }

            if (energyReleased > 0)
            {
                var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
                if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                    mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;
            }

            if (location != null)
            {
                var mixTemperature = mixture.Temperature;
                if (mixTemperature > Atmospherics.FireMinimumTemperatureToExist)
                {
                    atmosphereSystem.HotspotExpose(location, mixTemperature, mixture.Volume);
                }
            }

            return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
        }
    }
}
