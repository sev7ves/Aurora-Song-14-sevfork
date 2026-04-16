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
    [AnyCommand]
    public sealed class LinkConveyorCommand : LocalizedEntityCommands
    {
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly SandboxSystem _sandboxSystem = default!;
        [Dependency] private readonly MapSystem _mapSystem = default!;
        [Dependency] private readonly DeviceLinkSystem _deviceLinkSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override string Command => "linkconveyor";
        public List<CompletionOption> SearchDirections =[
            new CompletionOption("1","East West"),
            new CompletionOption("2","North South"),
            new CompletionOption("3","All")];
        public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            switch (args.Length)
            {
                case 1:
                    return CompletionResult.FromHintOptions(CompletionHelper.Components<ConveyorComponent>(args[0], EntityManager), "Conveyor");
                case 2:
                    return CompletionResult.FromHintOptions(CompletionHelper.Components<DeviceLinkSourceComponent>(args[1], EntityManager), "Source Device");
                case 3:
                    return CompletionResult.FromHintOptions(SearchDirections,"Search Directions");
                case 4:
                    return CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<SourcePortPrototype>(),"Link To Reverse");
                case 5:
                    return CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<SourcePortPrototype>(),"Link To Forward");
                case 6:
                    return CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<SourcePortPrototype>(),"Link To Off");
            }
            return CompletionResult.Empty;
        }
        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var links = new List<(string, string)>();

            if (shell.IsClient || (!_sandboxSystem.IsSandboxEnabled && !_adminManager.HasAdminFlag(shell.Player!, AdminFlags.Mapping)))
            {
                shell.WriteError(Loc.GetString("cmd-colornetwork-no-access"));
            }

            if (args.Length != 6)
            {
                shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
                return;
            }

            if (!int.TryParse(args[0], out var targetId))
            {
                shell.WriteLine(Loc.GetString("shell-argument-must-be-number"));
                return;
            }

            if (!int.TryParse(args[1], out var sourceId))
            {
                shell.WriteLine(Loc.GetString("shell-argument-must-be-number"));
                return;
            }

            if (!int.TryParse(args[2], out var direction))
            {
                shell.WriteLine(Loc.GetString("shell-argument-must-be-number"));
                return;
            }

            if (!_prototypeManager.HasIndex<SourcePortPrototype>(args[3])&&args[3]!="None")
            {
                shell.WriteLine(Loc.GetString($"shell-argument-must-be-prototype",
                    ("index", args[3]),
                    ("prototype", nameof(SourcePortPrototype))));
                return;
            }
            if(args[3]!="None")
                links.Add((args[3],"Reverse"));

            if (!_prototypeManager.HasIndex<SourcePortPrototype>(args[4])&&args[4]!="None")
            {
                shell.WriteLine(Loc.GetString($"shell-argument-must-be-prototype",
                    ("index", args[4]),
                    ("prototype", nameof(SourcePortPrototype))));
                return;
            }
            if(args[4]!="None")
                links.Add((args[4],"Forward"));

            if (!_prototypeManager.HasIndex<SourcePortPrototype>(args[5])&&args[5]!="None")
            {
                shell.WriteLine(Loc.GetString($"shell-argument-must-be-prototype",
                    ("index", args[5]),
                    ("prototype", nameof(SourcePortPrototype))));
                return;
            }
            if(args[5]!="None")
                links.Add((args[5],"Off"));

            var BeltNent = new NetEntity(targetId);

            if (!EntityManager.TryGetEntity(BeltNent, out var BeltEUid))
            {
                shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
                return;
            }

            var lala=TraverseBelt(BeltNent,direction);

            var sourceNent = new NetEntity(sourceId);
            if (!EntityManager.TryGetEntity(sourceNent, out var SourceEUid))
            {
                shell.WriteLine(Loc.GetString("shell-invalid-entity-id"));
                return;
            }
            foreach (var entityUid in lala)
            {
                _deviceLinkSystem.SaveLinks( shell.Player!.AttachedEntity, SourceEUid.Value, entityUid,links);
            }
        }
        private HashSet<EntityUid> TraverseBelt(NetEntity InitialBelt, int Direction)
        {
            var ToProcess = new Queue<EntityUid>();
            var ProcessedBelts = new HashSet<EntityUid>();
            ToProcess.Enqueue(EntityManager.GetEntity(InitialBelt));
            while (ToProcess.Count > 0)
            {
                var currentBelt=ToProcess.Dequeue();
                var xform = EntityManager.GetComponent<TransformComponent>(currentBelt);
                var gridID = xform.GridUid;
                var foundBelts = new List<EntityUid>();
                EntityManager.TryGetComponent<MapGridComponent>(gridID!.Value,out var grid);
                if (Direction == 1 || Direction == 3)
                {
                    foundBelts.AddRange( _mapSystem.GetOffset(gridID!.Value, grid!, xform.Coordinates, (1, 0))
                        .Where(entity => EntityManager.HasComponent<ConveyorComponent>(entity)&&(!ProcessedBelts.Contains(entity))));
                    foundBelts.AddRange(_mapSystem.GetOffset(gridID!.Value, grid!, xform.Coordinates, (-1,0))
                        .Where(entity => EntityManager.HasComponent<ConveyorComponent>(entity)&&(!ProcessedBelts.Contains(entity))));
                }

                if (Direction == 2 || Direction == 3)
                {
                    foundBelts.AddRange( _mapSystem.GetOffset(gridID!.Value, grid!, xform.Coordinates, (0, 1))
                        .Where(entity => EntityManager.HasComponent<ConveyorComponent>(entity)&&(!ProcessedBelts.Contains(entity))));
                    foundBelts.AddRange(_mapSystem.GetOffset(gridID!.Value, grid!, xform.Coordinates, (0,-1))
                        .Where(entity => EntityManager.HasComponent<ConveyorComponent>(entity)&&(!ProcessedBelts.Contains(entity))));
                }

                foreach (var entity in foundBelts)
                {
                    ToProcess.Enqueue(entity);
                }
                ProcessedBelts.Add(currentBelt);
            }
            return ProcessedBelts;
        }

    }

}
