using Content.Shared._Stalker.Bands; // Assuming shared messages/state are here
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using System; // For Enum

namespace Content.Client._Stalker.Bands.UI
{
    /// <summary>
    /// Initializes a <see cref="BandsManagingWindow"/> and updates it when new server messages are received.
    /// Handles interactions like adding or removing band members.
    /// </summary>
    [UsedImplicitly]
    public sealed class BandsManagingUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private BandsManagingWindow? _window;

        public BandsManagingUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        /// <summary>
        /// Called each time a Bands Managing UI instance is opened. Generates the window.
        /// </summary>
        protected override void Open()
        {
            base.Open();

            // Create the window
            _window = this.CreateWindow<BandsManagingWindow>();
            // Optionally set title or other initial properties if needed
            // _window.Title = ...;

            // Setup button actions
            if (_window != null)
            {
                _window.OnAddMemberButtonPressed += AddMember;
                _window.OnRemoveMemberButtonPressed += RemoveMember;
                // Add handlers for other potential actions like disbanding the band, promoting/demoting, etc.
            }
        }

        /// <summary>
        /// Sends a message to the server to add a member.
        /// </summary>
        private void AddMember(string memberName) // Assuming we add by name for now
        {
            // TODO: Need a way to select the player to add (e.g., from a list of nearby players or by searching)
            // For now, sending a placeholder message with the name.
            // The server will need to resolve this name to a NetUserId/EntityUid.
            if (!string.IsNullOrWhiteSpace(memberName))
            {
                SendMessage(new BandsManagingAddMemberMessage(memberName));
            }
        }

        /// <summary>
        /// Sends a message to the server to remove a member.
        /// </summary>
        private void RemoveMember(Guid memberUserId) // Assuming we identify members by Guid (PlayerUserId)
        {
             SendMessage(new BandsManagingRemoveMemberMessage(memberUserId));
        }


        /// <summary>
        /// Update the UI each time new state data is sent from the server.
        /// </summary>
        /// <param name="state">
        /// Data specific to the Bands Managing UI, sent from the server.
        /// </param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            // Cast the state to the specific type for this UI
            if (state is BandsManagingBoundUserInterfaceState castState)
            {
                // Pass the new state to the window to update its display
                _window?.UpdateState(castState);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing &amp;&amp; _window != null)
            {
                 _window.OnAddMemberButtonPressed -= AddMember;
                 _window.OnRemoveMemberButtonPressed -= RemoveMember;
                 _window.Dispose();
            }
        }
    }
}
