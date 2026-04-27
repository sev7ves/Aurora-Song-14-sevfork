using System.Collections.Generic;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.NodeGroups;
using Content.Server.Power.Pow3r;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Power.Components;
using Content.Shared.NodeContainer;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests._AS.Power;

public sealed class ShipPowerTests
{
    [Ignore("Takes too long to run")]
    [Test]
    public async Task TestApcLoad()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;

        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var mapSys = entMan.System<MapSystem>();
        var mapLoader = entMan.System<MapLoaderSystem>();
        var xform = entMan.System<TransformSystem>();

        await server.WaitPost(() =>
        {
            Assert.MultipleAsync(async () =>
            {
                foreach (var vessel in protoMan.EnumeratePrototypes<VesselPrototype>())
                {
                    Console.WriteLine(vessel.ID);
                    mapSys.CreateMap(out var mapId);
                    Entity<MapGridComponent>? shuttle = null;

                    try
                    {
                        mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out shuttle);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail(
                            $"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid threw exception {ex}");
                        mapSys.DeleteMap(mapId);
                        continue;
                    }

                    // Wait long enough for power to ramp up, but before anything can trip
                    await pair.RunSeconds(2);

                    // Check that no APCs start overloaded
                    var apcQuery = entMan.EntityQueryEnumerator<ApcComponent, PowerNetworkBatteryComponent>();
                    Assert.Multiple(() =>
                    {
                        while (apcQuery.MoveNext(out var uid, out var apc, out var battery))
                        {
                            // Uncomment the following line to log starting APC load to the console
                            //Console.WriteLine($"ApcLoad:{mapProtoId}:{uid}:{battery.CurrentSupply}");
                            if (xform.TryGetMapOrGridCoordinates(uid, out var coord))
                            {
                                Assert.That(apc.MaxLoad,
                                    Is.GreaterThanOrEqualTo(battery.CurrentSupply),
                                    $"APC {uid} on {vessel.ID} ({coord.Value.X}, {coord.Value.Y}) is overloaded {battery.CurrentSupply} / {apc.MaxLoad}");
                            }
                            else
                            {
                                Assert.That(apc.MaxLoad,
                                    Is.GreaterThanOrEqualTo(battery.CurrentSupply),
                                    $"APC {uid} on {vessel.ID} is overloaded {battery.CurrentSupply} / {apc.MaxLoad}");
                            }
                        }
                    });

                    mapSys.DeleteMap(mapId);
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
