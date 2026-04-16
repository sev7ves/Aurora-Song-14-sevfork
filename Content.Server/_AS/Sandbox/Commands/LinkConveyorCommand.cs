using System.Collections.Frozen;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Physics.Controllers;
using Content.Server.Sandbox;
using Content.Shared.Administration;
using Content.Shared.Conveyor;
using Content.Shared.DeviceLinking;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._AS.Sandbox.Commands
{
    /// <summary>
    /// A command to link a set of conveyor belts
    /// </summary>
    [AnyCommand]
    public sealed class LinkConveyorCommand : LocalizedEntityCommands
    {
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly SandboxSystem _sandboxSystem = default!;
        [Dependency] private readonly MapSystem _mapSystem = default!;
        [Dependency] private readonly DeviceLinkSystem _deviceLinkSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override string Command => "linkconveyors";

        private List<CompletionOption> searchDirections=new List<CompletionOption>();

        public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (searchDirections.Count==0)
                searchDirections =[
                    new ("1",Loc.GetString("cmd-hint-ew")),
                    new ("2",Loc.GetString("cmd-hint-ns")),
                    new ("3",Loc.GetString("cmd-hint-all"))];

            var sourcePorts = CompletionHelper.PrototypeIDs<SourcePortPrototype>()
                .Append(new(Loc.GetString("cmd-link-none"), Loc.GetString("cmd-hint-none")));

            switch (args.Length)
            {
                case 1:
                    return CompletionResult.FromHintOptions(CompletionHelper.Components<ConveyorComponent>(args[0], EntityManager), Loc.GetString("cmd-hint-conveyor"));
                case 2:
                    return CompletionResult.FromHintOptions(CompletionHelper.Components<DeviceLinkSourceComponent>(args[1], EntityManager), Loc.GetString("cmd-hint-sourcedevice"));
                case 3:
                    return CompletionResult.FromHintOptions(searchDirections,Loc.GetString("cmd-hint-searchdirections"));
                case 4:
                    return CompletionResult.FromHintOptions(sourcePorts,Loc.GetString("cmd-hint-reverse"));
                case 5:
                    return CompletionResult.FromHintOptions(sourcePorts,Loc.GetString("cmd-hint-forward"));
                case 6:
                    return CompletionResult.FromHintOptions(sourcePorts,Loc.GetString("cmd-hint-off"));
            }
            return CompletionResult.Empty;
        }
        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var links = new List<(string, string)>();

            if (shell.IsClient || (!_sandboxSystem.IsSandboxEnabled && !_adminManager.HasAdminFlag(shell.Player!, AdminFlags.Mapping)))//check if we can actually run the function
            {
                shell.WriteError(Loc.GetString("cmd-colornetwork-no-access"));// it's just "You are not currently able to use mapping commands." so im reusing it
            }

            if (args.Length != 6)//must have 6 args
            {
                shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
                return;
            }

            if (!int.TryParse(args[0], out var targetId))//arg 0 needs to be an int
            {
                shell.WriteLine(Loc.GetString("shell-argument-must-be-number"));
                return;
            }

            if (!int.TryParse(args[1], out var sourceId))//arg 1 needs to be an int
            {
                shell.WriteLine(Loc.GetString("shell-argument-must-be-number"));
                return;
            }

            if (!int.TryParse(args[2], out var direction))//arg 2 needs to be an int
            {
                shell.WriteLine(Loc.GetString("shell-argument-must-be-number"));
                return;
            }

            if (!_prototypeManager.HasIndex<SourcePortPrototype>(args[3])&&args[3]!="None")//check if it is a valid port or we are not linking
            {
                shell.WriteLine(Loc.GetString("shell-argument-must-be-prototype",
                    ("index", args[3]),
                    ("prototype", nameof(SourcePortPrototype))));
                return;
            }
            if(args[3]!="None")
                links.Add((args[3],"Reverse"));

            if (!_prototypeManager.HasIndex<SourcePortPrototype>(args[4])&&args[4]!="None")//check if it is a valid port or we are not linking
            {
                shell.WriteLine(Loc.GetString("shell-argument-must-be-prototype",
                    ("index", args[4]),
                    ("prototype", nameof(SourcePortPrototype))));
                return;
            }
            if(args[4]!="None")
                links.Add((args[4],"Forward"));

            if (!_prototypeManager.HasIndex<SourcePortPrototype>(args[5])&&args[5]!="None")//check if it is a valid port or we are not linking
            {
                shell.WriteLine(Loc.GetString("shell-argument-must-be-prototype",
                    ("index", args[5]),
                    ("prototype", nameof(SourcePortPrototype))));
                return;
            }
            if(args[5]!="None")
                links.Add((args[5],"Off"));

            var beltNent = new NetEntity(targetId);

            if (!EntityManager.TryGetEntity(beltNent, out var beltEUid))//check if there is a valid corresponding entity
            {
                shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
                return;
            }

            var conveyors=TraverseBelt(beltNent,direction);

            var sourceNent = new NetEntity(sourceId);

            if (!EntityManager.TryGetEntity(sourceNent, out var sourceEUid))//check if there is a valid corresponding entity
            {
                shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
                return;
            }

            foreach (var entityUid in conveyors)// link all the conveyors
            {
                _deviceLinkSystem.SaveLinks( shell.Player!.AttachedEntity, sourceEUid.Value, entityUid,links);
            }
        }
        private HashSet<EntityUid> TraverseBelt(NetEntity initialBelt, int direction)// traverse the belt to get all the parts
        {
            var toProcess = new Queue<EntityUid>();// holds the ones we want to process
            var processedBelts = new HashSet<EntityUid>();//holds the ones we've processed
            toProcess.Enqueue(EntityManager.GetEntity(initialBelt));

            while (toProcess.Count > 0)// recursive implementation of a flood fill type algorithm
            {

                var currentBelt=toProcess.Dequeue();
                var xform = EntityManager.GetComponent<TransformComponent>(currentBelt);
                var gridId = xform.GridUid;
                var foundBelts = new List<EntityUid>();
                EntityManager.TryGetComponent<MapGridComponent>(gridId!.Value,out var grid);

                if (direction == 1 || direction == 3)//check east and west
                {
                    foundBelts.AddRange( _mapSystem.GetOffset(gridId!.Value, grid!, xform.Coordinates, (1, 0))
                        .Where(entity => EntityManager.HasComponent<ConveyorComponent>(entity)&&(!processedBelts.Contains(entity))));
                    foundBelts.AddRange(_mapSystem.GetOffset(gridId!.Value, grid!, xform.Coordinates, (-1,0))
                        .Where(entity => EntityManager.HasComponent<ConveyorComponent>(entity)&&(!processedBelts.Contains(entity))));
                }

                if (direction == 2 || direction == 3)// check north and south
                {
                    foundBelts.AddRange( _mapSystem.GetOffset(gridId!.Value, grid!, xform.Coordinates, (0, 1))
                        .Where(entity => EntityManager.HasComponent<ConveyorComponent>(entity)&&(!processedBelts.Contains(entity))));
                    foundBelts.AddRange(_mapSystem.GetOffset(gridId!.Value, grid!, xform.Coordinates, (0,-1))
                        .Where(entity => EntityManager.HasComponent<ConveyorComponent>(entity)&&(!processedBelts.Contains(entity))));
                }

                foreach (var entity in foundBelts)
                {
                    toProcess.Enqueue(entity);
                }
                processedBelts.Add(currentBelt);
            }
            return processedBelts;
        }

    }

}
