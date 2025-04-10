using Content.Server._Stalker.Bands.Components;
using Content.Server.Database; // For IServerDbManager
using Content.Server.EUI;
using Content.Shared._Stalker.Bands;
using Content.Shared.Database; // For PlayerRecord, RoleWhitelist
using Content.Shared.Eui;
using Content.Shared.Roles; // For JobPrototype
using Robust.Server.GameObjects;
using Robust.Shared.Network; // For NetUserId
using Robust.Shared.Prototypes; // For IPrototypeManager
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // For Task

namespace Content.Server._Stalker.Bands
{
    /// <summary>
    /// Server-side system responsible for handling the Bands Managing UI logic,
    /// processing client messages, and interacting with the database.
    /// </summary>
    public sealed class BandsManagingSystem : EntitySystem
    {
        [Dependency] private readonly EuiManager _euiManager = default!;
        [Dependency] private readonly IServerDbManager _dbManager = default!;
        [Dependency] private readonly SharedBandsSystem _sharedBandsSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly UserDbDataManager _userDb = default!; // To get NetUserId from PlayerSession

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BandsManagingComponent, EuiOpenEvent>(OnEuiOpen);
        }

        private void OnEuiOpen(EntityUid uid, BandsManagingComponent component, EuiOpenEvent args)
        {
            var ui = new BandsManagingEui(this, _dbManager, _sharedBandsSystem, _prototypeManager, _userDb, uid, args.Session);
            _euiManager.OpenEui(ui, args.Session);
        }

        // Internal EUI class to handle state and messages for a specific player session
        private sealed class BandsManagingEui : BaseEui
        {
            private readonly BandsManagingSystem _system;
            private readonly IServerDbManager _dbManager;
            private readonly SharedBandsSystem _sharedBandsSystem;
            private readonly IPrototypeManager _prototypeManager;
            private readonly UserDbDataManager _userDb;
            private readonly EntityUid _owner; // The entity with BandsManagingComponent

            public BandsManagingEui(BandsManagingSystem system, IServerDbManager dbManager, SharedBandsSystem sharedBandsSystem, IPrototypeManager prototypeManager, UserDbDataManager userDb, EntityUid owner, IPlayerSession session)
            {
                _system = system;
                _dbManager = dbManager;
                _sharedBandsSystem = sharedBandsSystem;
                _prototypeManager = prototypeManager;
                _userDb = userDb;
                _owner = owner;

                // Send initial state when UI opens
                SendState();
            }

            public override async void HandleMessage(EuiMessageBase msg)
            {
                base.HandleMessage(msg);

                var userId = await _userDb.GetUserIdBySession(Player);
                if (userId == null) return; // Should not happen for authenticated players

                switch (msg)
                {
                    case BandsManagingAddMemberMessage addMsg:
                        await HandleAddMember(userId.Value, addMsg.MemberName);
                        break;
                    case BandsManagingRemoveMemberMessage removeMsg:
                        await HandleRemoveMember(userId.Value, removeMsg.MemberUserId);
                        break;
                        // Handle other messages like Disband, Promote, etc.
                }

                // Resend state after handling message to reflect changes
                SendState();
            }

            private async Task HandleAddMember(NetUserId leaderUserId, string memberName)
            {
                if (!await CanPlayerManageBand(leaderUserId)) return;

                // 1. Resolve memberName to a PlayerRecord/NetUserId
                var targetPlayerRecord = await _dbManager.GetPlayerRecordByUserName(memberName); // Case-sensitive? Needs check.
                if (targetPlayerRecord == null)
                {
                    // TODO: Send feedback to user (player not found)
                    Logger.WarningS("bands", $"Player '{memberName}' not found for adding to band by {leaderUserId}.");
                    return;
                }
                var targetUserId = targetPlayerRecord.UserId;

                // 2. Get leader's band
                var leaderBand = await _sharedBandsSystem.GetPlayerBandAsync(leaderUserId);
                if (leaderBand == null) return; // Leader not in a band?

                // 3. Check if band is full
                var currentMembers = await _sharedBandsSystem.GetBandMembersAsync(leaderUserId);
                if (currentMembers.Count >= leaderBand.MaxMembers)
                {
                    // TODO: Send feedback to user (band full)
                    Logger.InfoS("bands", $"Band '{leaderBand.ID}' is full. Cannot add {memberName}.");
                    return;
                }

                // 4. Check if target player is already in *this* band
                if (currentMembers.Any(m => m.UserId == targetUserId))
                {
                    // TODO: Send feedback (already in band)
                    return;
                }

                // 5. Check if target player is in *another* band (optional, based on rules)
                var targetBand = await _sharedBandsSystem.GetPlayerBandAsync(targetUserId);
                if (targetBand != null)
                {
                    // TODO: Send feedback (player already in another band)
                    Logger.InfoS("bands", $"Player '{memberName}' is already in band '{targetBand.ID}'. Cannot add to '{leaderBand.ID}'.");
                    return;
                }

                // 6. Add the player to the band (assign the lowest hierarchy role by default?)
                //    This requires adding a RoleWhitelist entry.
                var lowestRankRoleId = leaderBand.Hierarchy.OrderBy(kvp => kvp.Key).FirstOrDefault().Value;
                if (lowestRankRoleId == default)
                {
                     Logger.ErrorS("bands", $"Band '{leaderBand.ID}' has no hierarchy defined. Cannot add member.");
                     return; // Cannot add if no roles defined
                }

                try
                {
                    // Ensure the player exists in the Player table (should already be true if GetPlayerRecordByUserName worked)
                    // Add the RoleWhitelist entry
                    await _dbManager.AddRoleWhitelistAsync(targetUserId, lowestRankRoleId); // Needs implementation in IServerDbManager
                    Logger.InfoS("bands", $"Added player {memberName} ({targetUserId}) to band '{leaderBand.ID}' with role '{lowestRankRoleId}' by {leaderUserId}.");
                }
                catch (Exception e)
                {
                    Logger.ErrorS("bands", $"Failed to add player {memberName} to band '{leaderBand.ID}': {e}");
                    // TODO: Send error feedback to user
                }
            }

            private async Task HandleRemoveMember(NetUserId leaderUserId, Guid memberUserIdToRemove)
            {
                if (!await CanPlayerManageBand(leaderUserId)) return;

                // Cannot remove self via this button (maybe add a 'Leave Band' button?)
                if (leaderUserId.UserId == memberUserIdToRemove) return;

                // 1. Get leader's band
                var leaderBand = await _sharedBandsSystem.GetPlayerBandAsync(leaderUserId);
                if (leaderBand == null) return;

                // 2. Get all roles associated with this band
                var bandRoleIds = leaderBand.Hierarchy.Values.Select(p => p.Id).ToHashSet();
                 bandRoleIds.Add(leaderBand.ID); // Include band ID itself as potential role

                // 3. Find all RoleWhitelist entries for the target member that match the band's roles
                var memberRolesInBand = (await _dbManager.GetRoleWhitelistsAsync(memberUserIdToRemove))
                                        .Where(rw => bandRoleIds.Contains(rw.RoleId))
                                        .ToList();

                if (!memberRolesInBand.Any())
                {
                    Logger.WarningS("bands", $"Player {memberUserIdToRemove} is not in band '{leaderBand.ID}' or has no roles matching it. Cannot remove.");
                    return; // Target player not in this band or has no matching roles
                }

                // 4. Remove those RoleWhitelist entries
                try
                {
                    foreach (var roleWhitelist in memberRolesInBand)
                    {
                        await _dbManager.RemoveRoleWhitelistAsync(memberUserIdToRemove, roleWhitelist.RoleId); // Needs implementation
                    }
                    Logger.InfoS("bands", $"Removed player {memberUserIdToRemove} from band '{leaderBand.ID}' by {leaderUserId}.");
                }
                catch (Exception e)
                {
                    Logger.ErrorS("bands", $"Failed to remove player {memberUserIdToRemove} from band '{leaderBand.ID}': {e}");
                    // TODO: Send error feedback to user
                }
            }

            private async Task<bool> CanPlayerManageBand(NetUserId userId)
            {
                var band = await _sharedBandsSystem.GetPlayerBandAsync(userId);
                if (band?.ManagingRankId == null)
                    return false; // No managing rank defined for this band

                var playerRoles = await _dbManager.GetRoleWhitelistsAsync(userId.UserId);
                if (!playerRoles.Any())
                    return false; // Player has no roles

                // Check if the player has the managing role ID OR a role higher in the hierarchy
                var managingJobProtoId = band.Hierarchy.GetValueOrDefault(band.ManagingRankId.Value);
                if (managingJobProtoId == default)
                    return false; // Managing rank ID doesn't exist in hierarchy

                // Check if player has the exact managing role
                if (playerRoles.Any(r => r.RoleId == managingJobProtoId))
                    return true;

                // Optional: Check if player has a role *higher* than the managing rank
                // This requires comparing hierarchy keys. Lower key = higher rank usually.
                var managingRankKey = band.Hierarchy.FirstOrDefault(kvp => kvp.Value == managingJobProtoId).Key;
                foreach (var playerRole in playerRoles)
                {
                    var playerRank = band.Hierarchy.FirstOrDefault(kvp => kvp.Value.Id == playerRole.RoleId);
                    if (playerRank.Value != default &amp;&amp; playerRank.Key < managingRankKey) // Lower key means higher rank
                    {
                        return true;
                    }
                }


                return false; // Player does not have sufficient rank
            }


            private async void SendState()
            {
                var userId = await _userDb.GetUserIdBySession(Player);
                if (userId == null) return;

                var band = await _sharedBandsSystem.GetPlayerBandAsync(userId.Value);
                List<BandMemberInfo> memberInfos = new();
                bool canManage = false;
                string? bandName = null;
                int maxMembers = 0;

                if (band != null)
                {
                    bandName = band.Name; // Or use localization if available
                    maxMembers = band.MaxMembers;
                    canManage = await CanPlayerManageBand(userId.Value);

                    var members = await _sharedBandsSystem.GetBandMembersAsync(userId.Value);
                    foreach (var member in members)
                    {
                        // Potentially fetch more info like rank/role here if needed for UI
                        memberInfos.Add(new BandMemberInfo(member.UserId, member.LastSeenUserName));
                    }
                }

                var state = new BandsManagingBoundUserInterfaceState(bandName, maxMembers, memberInfos, canManage);
                SendMessage(state);
            }

            public override void Closed()
            {
                base.Closed();
                // Cleanup if needed
            }
        }
    }

    // Extensions needed for IServerDbManager (implement these in the actual DB manager class)
    public static class ServerDbManagerBandExtensions
    {
        public static async Task AddRoleWhitelistAsync(this IServerDbManager dbManager, Guid playerUserId, ProtoId<JobPrototype> roleId)
        {
            // --- Implementation Needed ---
            // Example using EF Core:
            // using var db = dbManager.GetContext();
            // var exists = await db.RoleWhitelists.AnyAsync(rw => rw.PlayerUserId == playerUserId &amp;&amp; rw.RoleId == roleId.Id);
            // if (!exists)
            // {
            //     db.RoleWhitelists.Add(new RoleWhitelist { PlayerUserId = playerUserId, RoleId = roleId.Id });
            //     await db.SaveChangesAsync();
            // }
            await Task.CompletedTask;
            Console.WriteLine($"Warning: {nameof(AddRoleWhitelistAsync)} not implemented in DB layer.");
        }

        public static async Task RemoveRoleWhitelistAsync(this IServerDbManager dbManager, Guid playerUserId, string roleId)
        {
            // --- Implementation Needed ---
            // Example using EF Core:
            // using var db = dbManager.GetContext();
            // var entry = await db.RoleWhitelists.FirstOrDefaultAsync(rw => rw.PlayerUserId == playerUserId &amp;&amp; rw.RoleId == roleId);
            // if (entry != null)
            // {
            //     db.RoleWhitelists.Remove(entry);
            //     await db.SaveChangesAsync();
            // }
            await Task.CompletedTask;
            Console.WriteLine($"Warning: {nameof(RemoveRoleWhitelistAsync)} not implemented in DB layer.");
        }
    }
}