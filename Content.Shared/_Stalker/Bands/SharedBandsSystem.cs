using Content.Shared.Actions;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using System; // Added for Guid
using System.Collections.Generic; // Added for List
using System.Linq; // Added for Linq methods
using System.Threading.Tasks; // Added for Task
using Content.Server.Database; // Added for IServerDbManager
using Content.Shared.Database; // Added for PlayerRecord, RoleWhitelist
using Content.Shared.Roles; // Added for JobPrototype
using Robust.Shared.GameObjects; // Added for EntitySystem base class
using Robust.Shared.IoC; // Added for Dependency attribute

namespace Content.Shared._Stalker.Bands
{
    /// <summary>
    /// Handles assigning and removing <see cref="BandsComponent"/> to remove/assign action on entity.
    /// Also provides methods for querying band information from the database.
    /// </summary>
    public sealed class SharedBandsSystem : EntitySystem
    {
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly IServerDbManager _dbManager = default!; // Added dependency

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BandsComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<BandsComponent, ComponentRemove>(OnRemove);
            SubscribeLocalEvent<BandsComponent, ToggleBandsEvent>(OnToggle);
            SubscribeLocalEvent<BandsComponent, ChangeBandEvent>(OnChange);
        }

        private void OnInit(EntityUid uid, BandsComponent component, ComponentInit args)
        {
            EnsureComp<StatusIconComponent>(uid);

            var proto = _proto.Index<JobIconPrototype>(component.BandStatusIcon);
            //if (proto.HideOnStealth)
            //    return;
            if (component is { AltBand: not null, CanChange: true })
                _actions.AddAction(uid, ref component.ActionChangeEntity, component.ActionChange, uid);

            _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid);
        }

        private void OnChange(Entity<BandsComponent> entity, ref ChangeBandEvent args)
        {
            var comp = entity.Comp;
            if (comp.AltBand == null || !comp.CanChange)
                return;

            (comp.BandStatusIcon, comp.AltBand) = (comp.AltBand, comp.BandStatusIcon);
            Dirty(entity);
            args.Handled = true;
        }

        private void OnRemove(EntityUid uid, BandsComponent component, ComponentRemove args)
        {
            RemComp<StatusIconComponent>(uid);

            var proto = _proto.Index<JobIconPrototype>(component.BandStatusIcon);
            //if (proto.HideOnStealth)
            //    return;

            _actions.RemoveAction(uid, component.ActionEntity);
            if (component.ActionChangeEntity != null)
                _actions.RemoveAction(uid, component.ActionChangeEntity);
        }
        private void OnToggle(EntityUid uid, BandsComponent component, ToggleBandsEvent args)
        {
            if (!_mobState.IsAlive(uid))
                return;

            var proto = _proto.Index<JobIconPrototype>(component.BandStatusIcon);
            //if (proto.HideOnStealth)
            //    return;

            component.Enabled = !component.Enabled;
            Dirty(uid, component);

            args.Handled = true;
        }

        #region Band Management Logic (Added)

        /// <summary>
        /// Gets the band prototype associated with a given player based on their RoleWhitelist.
        /// </summary>
        /// <param name="userId">The NetUserId of the player.</param>
        /// <returns>The STBandPrototype if found, otherwise null.</returns>
        public async Task<STBandPrototype?> GetPlayerBandAsync(NetUserId userId)
        {
            var playerRecord = await _dbManager.GetPlayerRecordByUserId(userId);
            if (playerRecord == null)
                return null;

            // Fetch roles specifically for this player
            var roleWhitelists = await _dbManager.GetRoleWhitelistsAsync(playerRecord.UserId);
            if (!roleWhitelists.Any())
                return null;

            // Use the first role found as the primary identifier for the band.
            // Consider if multiple roles could imply multiple bands or if a priority system is needed.
            var playerRoleId = roleWhitelists.First().RoleId;

            // Iterate through all band prototypes to find which one contains the player's role in its hierarchy.
            foreach (var bandProto in _proto.EnumeratePrototypes<STBandPrototype>())
            {
                // Check if any job ID in the band's hierarchy matches the player's role ID.
                if (bandProto.Hierarchy.Any(kvp => kvp.Value.Id == playerRoleId))
                {
                    return bandProto;
                }
            }

            // Fallback: If the specific role isn't found, check if the band ID itself matches the role ID
            // This handles cases where the RoleId might directly be the Band ID (e.g., "Military" role for "Military" band)
             if (_proto.TryIndex<STBandPrototype>(playerRoleId, out var directBandProto))
             {
                 // Further check if this band prototype actually lists this role ID in its hierarchy,
                 // or if the role ID *is* the band ID itself (implicitly part of the band).
                 // This logic might need refinement based on exact game design.
                 if (directBandProto.Hierarchy.Any(kvp => kvp.Value.Id == playerRoleId) || directBandProto.ID == playerRoleId)
                 {
                    return directBandProto;
                 }
             }


            return null; // Player role not found associated with any band prototype.
        }


        /// <summary>
        /// Gets all members (PlayerRecords) of a specific player's band.
        /// </summary>
        /// <param name="userId">The NetUserId of the player whose band members are to be fetched.</param>
        /// <returns>A list of PlayerRecords representing the band members. Returns empty list if player or band not found.</returns>
        public async Task<List<PlayerRecord>> GetBandMembersAsync(NetUserId userId)
        {
            var bandMembers = new List<PlayerRecord>();
            var playerBand = await GetPlayerBandAsync(userId);

            if (playerBand == null)
            {
                // Log or handle the case where the player isn't in a valid band.
                return bandMembers;
            }

            // Fetch all players and all role whitelists once to avoid multiple DB calls.
            var allPlayers = await _dbManager.GetAllPlayersAsync();
            var allRoleWhitelists = await _dbManager.GetAllRoleWhitelistsMapAsync(); // Using Map for efficiency

            // Get all role IDs associated with this specific band from its hierarchy.
            var bandRoleIds = playerBand.Hierarchy.Values.Select(p => p.Id).ToHashSet();
            // Also consider the band ID itself as a potential role identifier if design allows
            bandRoleIds.Add(playerBand.ID);


            foreach (var player in allPlayers)
            {
                // Check if this player has any roles associated with them in the map.
                if (allRoleWhitelists.TryGetValue(player.UserId, out var playerRoles))
                {
                    // Check if any of the player's roles match the roles defined in the target band.
                    if (playerRoles.Any(roleId => bandRoleIds.Contains(roleId)))
                    {
                        bandMembers.Add(player);
                    }
                }
            }

            return bandMembers;
        }


        /// <summary>
        /// Gets the metadata (STBandPrototype) for a player's band.
        /// </summary>
        /// <param name="userId">The NetUserId of the player.</param>
        /// <returns>The STBandPrototype containing the band's metadata, or null if not found.</returns>
        public async Task<STBandPrototype?> GetBandInfoAsync(NetUserId userId)
        {
            // This is essentially the same as getting the player's band.
            return await GetPlayerBandAsync(userId);
        }

        // TODO: Implement methods for adding/removing players from bands.
        // This will likely involve modifying RoleWhitelist entries in the database.
        // Need to consider permissions (e.g., only band leader with ManagingRankId can manage).
        // Example signatures:
        // public async Task<bool> AddPlayerToBandAsync(NetUserId leaderUserId, NetUserId playerToAddUserId, string roleId)
        // public async Task<bool> RemovePlayerFromBandAsync(NetUserId leaderUserId, NetUserId playerToRemoveUserId)

        #endregion
    }

    #region IServerDbManager Extensions (Placeholders - Implement in DB Layer)

    // Helper methods potentially needed in IServerDbManager (add these to the interface and implementation)
    // It's better practice to have these directly in the IServerDbManager interface and its implementations.
    public static class ServerDbManagerExtensions
    {
        /// <summary>
        /// Placeholder: Gets all RoleWhitelist entries for a specific player.
        /// Implement this method in IServerDbManager and its concrete class(es).
        /// </summary>
        public static async Task<List<RoleWhitelist>> GetRoleWhitelistsAsync(this IServerDbManager dbManager, Guid userId)
        {
            // --- Implementation Needed ---
            // Example using Entity Framework Core (adapt to your DB context):
            // using var db = dbManager.GetContext(); // Assuming a method to get the DbContext
            // return await db.RoleWhitelists.Where(rw => rw.PlayerUserId == userId).ToListAsync();
            await Task.CompletedTask; // Replace with actual DB query
            Console.WriteLine($"Warning: {nameof(GetRoleWhitelistsAsync)} not implemented in DB layer.");
            return new List<RoleWhitelist>(); // Return empty list as placeholder
        }

        /// <summary>
        /// Placeholder: Gets all Player records from the database.
        /// Implement this method in IServerDbManager and its concrete class(es).
        /// </summary>
        public static async Task<List<PlayerRecord>> GetAllPlayersAsync(this IServerDbManager dbManager)
        {
            // --- Implementation Needed ---
            // Example using Entity Framework Core:
            // using var db = dbManager.GetContext();
            // return await db.Player.Select(p => new PlayerRecord { ... }).ToListAsync(); // Map to PlayerRecord DTO
             await Task.CompletedTask; // Replace with actual DB query
             Console.WriteLine($"Warning: {nameof(GetAllPlayersAsync)} not implemented in DB layer.");
            return new List<PlayerRecord>(); // Return empty list as placeholder
        }

        /// <summary>
        /// Placeholder: Gets all RoleWhitelist entries from the database, ideally optimized.
        /// Implement this method in IServerDbManager and its concrete class(es).
        /// Consider returning a structure optimized for lookups, like a Dictionary.
        /// </summary>
        public static async Task<List<RoleWhitelist>> GetAllRoleWhitelistsAsync(this IServerDbManager dbManager)
        {
            // --- Implementation Needed ---
            // Example using Entity Framework Core:
            // using var db = dbManager.GetContext();
            // return await db.RoleWhitelists.ToListAsync();
            await Task.CompletedTask; // Replace with actual DB query
            Console.WriteLine($"Warning: {nameof(GetAllRoleWhitelistsAsync)} not implemented in DB layer.");
            return new List<RoleWhitelist>(); // Return empty list as placeholder
        }

        /// <summary>
        /// Placeholder: Gets all RoleWhitelist entries mapped by PlayerUserId for efficient lookup.
        /// Implement this method in IServerDbManager and its concrete class(es).
        /// </summary>
        public static async Task<Dictionary<Guid, List<string>>> GetAllRoleWhitelistsMapAsync(this IServerDbManager dbManager)
        {
            // --- Implementation Needed ---
            // Example using Entity Framework Core:
            // using var db = dbManager.GetContext();
            // var allWhitelists = await db.RoleWhitelists.ToListAsync();
            // return allWhitelists
            //     .GroupBy(rw => rw.PlayerUserId)
            //     .ToDictionary(g => g.Key, g => g.Select(rw => rw.RoleId).ToList());
             await Task.CompletedTask; // Replace with actual DB query
             Console.WriteLine($"Warning: {nameof(GetAllRoleWhitelistsMapAsync)} not implemented in DB layer.");
            return new Dictionary<Guid, List<string>>(); // Return empty dictionary as placeholder
        }
    }
    #endregion
}
