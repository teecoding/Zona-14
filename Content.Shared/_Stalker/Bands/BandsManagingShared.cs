using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using Content.Shared.Eui; // For EuiMessage

namespace Content.Shared._Stalker.Bands
{
    /// <summary>
    /// Represents the state of the Bands Managing UI sent from the server to the client.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BandsManagingBoundUserInterfaceState : BoundUserInterfaceState
    {
        public string? BandName { get; }
        public int MaxMembers { get; }
        public List<BandMemberInfo> Members { get; }
        public bool CanManage { get; } // Indicates if the player opening the UI can add/remove members

        public BandsManagingBoundUserInterfaceState(string? bandName, int maxMembers, List<BandMemberInfo> members, bool canManage)
        {
            BandName = bandName;
            MaxMembers = maxMembers;
            Members = members;
            CanManage = canManage;
        }
    }

    /// <summary>
    /// Holds information about a single band member for display in the UI.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BandMemberInfo
    {
        public Guid PlayerUserId { get; }
        public string PlayerName { get; }
        // Add other relevant info like RoleId or Rank if needed for display

        public BandMemberInfo(Guid playerUserId, string playerName)
        {
            PlayerUserId = playerUserId;
            PlayerName = playerName;
        }
    }

    /// <summary>
    /// Message sent from client to server when requesting to add a member.
    /// Contains the name entered by the user. The server needs to resolve this.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BandsManagingAddMemberMessage : EuiMessageBase // Using EuiMessageBase if this UI uses EuiManager
    {
        public string MemberName { get; }

        public BandsManagingAddMemberMessage(string memberName)
        {
            MemberName = memberName;
        }
    }

    /// <summary>
    /// Message sent from client to server when requesting to remove a member.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class BandsManagingRemoveMemberMessage : EuiMessageBase // Using EuiMessageBase if this UI uses EuiManager
    {
        public Guid MemberUserId { get; }

        public BandsManagingRemoveMemberMessage(Guid memberUserId)
        {
            MemberUserId = memberUserId;
        }
    }

    // TODO: Add other messages as needed (e.g., DisbandBandMessage, PromoteMemberMessage)
}