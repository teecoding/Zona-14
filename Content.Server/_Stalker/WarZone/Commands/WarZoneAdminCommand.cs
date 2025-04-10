using System;
using Content.Server._Stalker.WarZone;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared._Stalker.WarZone;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Console;
using Content.Server.Administration;

namespace Content.Server._Stalker.WarZone.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class WarZoneAdminCommand : IConsoleCommand
{
    public string Command => "warzoneadmin";
    public string Description => "Admin command to modify warzone points and ownership.";
    public string Help => "Usage:\n" +
                          "warzoneadmin setpoints band <dbid> <points>\n" +
                          "warzoneadmin setpoints faction <dbid> <points>\n" +
                          "warzoneadmin setowner <zoneProtoId> band <dbid>\n" +
                          "warzoneadmin setowner <zoneProtoId> faction <dbid>";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 4 && !(args.Length == 3 && args[0] == "setpoints"))
        {
            shell.WriteLine(Help);
            return;
        }

        var warZoneSystem = IoCManager.Resolve<WarZoneSystem>();
        var dbManager = IoCManager.Resolve<IServerDbManager>();
        var protoManager = IoCManager.Resolve<IPrototypeManager>();

        if (args[0] == "setpoints")
        {
            if (args.Length != 3)
            {
                shell.WriteLine(Help);
                return;
            }

            var target = args[1];
            if (!int.TryParse(args[2], out var points))
            {
                shell.WriteLine("Invalid points value.");
                return;
            }

            if (!int.TryParse(args[2], out _))
            {
                shell.WriteLine("Invalid points value.");
                return;
            }

            if (!int.TryParse(args[2], out points))
            {
                shell.WriteLine("Invalid points value.");
                return;
            }

            if (!int.TryParse(args[2], out points))
            {
                shell.WriteLine("Invalid points value.");
                return;
            }

            shell.WriteLine("Invalid command format.");
            return;
        }

        if (args[0] == "setpoints")
        {
            if (args.Length != 3)
            {
                shell.WriteLine(Help);
                return;
            }

            var target = args[1];
            if (!int.TryParse(args[2], out var dbid))
            {
                shell.WriteLine("Invalid dbid.");
                return;
            }

            if (!int.TryParse(args[3], out var points))
            {
                shell.WriteLine("Invalid points value.");
                return;
            }

            if (target == "band")
            {
                warZoneSystem.BandPoints[dbid] = points;
                foreach (var bandProto in protoManager.EnumeratePrototypes<Content.Shared._Stalker.Bands.STBandPrototype>())
                {
                    if (bandProto.DatabaseId == dbid)
                    {
                        await dbManager.SetStalkerBandAsync(bandProto.ID, points);
                        shell.WriteLine($"Set band {dbid} points to {points}");
                        return;
                    }
                }
                shell.WriteLine($"Band with dbid {dbid} not found.");
            }
            else if (target == "faction")
            {
                warZoneSystem.FactionPoints[dbid] = points;
                foreach (var factionProto in protoManager.EnumeratePrototypes<Content.Shared.NPC.Prototypes.NpcFactionPrototype>())
                {
                    if (factionProto.DatabaseId == dbid)
                    {
                        await dbManager.SetStalkerFactionAsync(factionProto.ID, points);
                        shell.WriteLine($"Set faction {dbid} points to {points}");
                        return;
                    }
                }
                shell.WriteLine($"Faction with dbid {dbid} not found.");
            }
            else
            {
                shell.WriteLine(Help);
            }

            return;
        }

        if (args[0] == "setowner")
        {
            if (args.Length != 4)
            {
                shell.WriteLine(Help);
                return;
            }

            var zoneProtoId = args[1];
            var target = args[2];

            if (!int.TryParse(args[3], out var dbid))
            {
                shell.WriteLine("Invalid dbid.");
                return;
            }

            var query = warZoneSystem.EntityQueryEnumerator<WarZoneComponent>();
            WarZoneComponent? foundComp = null;
            while (query.MoveNext(out var uid, out var wzComp))
            {
                if (wzComp.ZoneProto == zoneProtoId)
                {
                    foundComp = wzComp;
                    break;
                }
            }

            if (foundComp == null)
            {
                shell.WriteLine($"Warzone with proto id {zoneProtoId} not found.");
                return;
            }

            ProtoId<Content.Shared._Stalker.Bands.STBandPrototype>? bandProtoId = null;
            ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype>? factionProtoId = null;

            if (target == "band")
            {
                foundComp.DefendingBandId = dbid;
                foundComp.DefendingFactionId = null;

                foreach (var bandProto in protoManager.EnumeratePrototypes<Content.Shared._Stalker.Bands.STBandPrototype>())
                {
                    if (bandProto.DatabaseId == dbid)
                    {
                        bandProtoId = bandProto.ID;
                        break;
                    }
                }

                if (bandProtoId == null)
                {
                    shell.WriteLine($"Band with dbid {dbid} not found.");
                    return;
                }
            }
            else if (target == "faction")
            {
                foundComp.DefendingBandId = null;
                foundComp.DefendingFactionId = dbid;

                foreach (var factionProto in protoManager.EnumeratePrototypes<Content.Shared.NPC.Prototypes.NpcFactionPrototype>())
                {
                    if (factionProto.DatabaseId == dbid)
                    {
                        factionProtoId = factionProto.ID;
                        break;
                    }
                }

                if (factionProtoId == null)
                {
                    shell.WriteLine($"Faction with dbid {dbid} not found.");
                    return;
                }
            }
            else
            {
                shell.WriteLine(Help);
                return;
            }

            await dbManager.SetStalkerZoneOwnershipAsync(zoneProtoId, bandProtoId, factionProtoId);
            shell.WriteLine($"Set owner of warzone {zoneProtoId} to {target} {dbid}");
            return;
        }

        shell.WriteLine(Help);
    }
}