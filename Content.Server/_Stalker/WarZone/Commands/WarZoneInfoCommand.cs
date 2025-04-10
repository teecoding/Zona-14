using System;
using System.Text;
using Content.Server._Stalker.WarZone;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Console;
using Content.Server.Administration;

namespace Content.Server._Stalker.WarZone.Commands;

public sealed class WarZoneInfoCommand : IConsoleCommand
{
    public string Command => "warzoneinfo";
    public string Description => "Lists warzones, their owners, cooldowns, and points for bands and factions.";
    public string Help => "Usage: warzoneinfo";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var warZoneSystem = IoCManager.Resolve<WarZoneSystem>();
        var sb = new StringBuilder();

        sb.AppendLine("=== Warzones ===");
        var query = warZoneSystem.EntityQueryEnumerator<WarZoneComponent>();
        while (query.MoveNext(out var uid, out var wzComp))
        {
            var zoneId = wzComp.ZoneProto;
            var owner = "None";
            if (wzComp.DefendingBandId.HasValue)
                owner = $"Band {wzComp.DefendingBandId.Value}";
            else if (wzComp.DefendingFactionId.HasValue)
                owner = $"Faction {wzComp.DefendingFactionId.Value}";

            var cooldown = wzComp.CooldownEndTime.HasValue
                ? (wzComp.CooldownEndTime.Value - warZoneSystem.GameTiming.CurTime).TotalSeconds
                : 0;

            if (cooldown < 0)
                cooldown = 0;

            sb.AppendLine($"Zone: {zoneId}, Owner: {owner}, Cooldown: {cooldown:F0}s, EntityUid: {uid}");
        }

        sb.AppendLine("\n=== Band Points ===");
        foreach (var kvp in warZoneSystem.BandPoints)
        {
            sb.AppendLine($"Band DB ID: {kvp.Key}, Points: {kvp.Value}");
        }

        sb.AppendLine("\n=== Faction Points ===");
        foreach (var kvp in warZoneSystem.FactionPoints)
        {
            sb.AppendLine($"Faction DB ID: {kvp.Key}, Points: {kvp.Value}");
        }

        shell.WriteLine(sb.ToString());
    }
}