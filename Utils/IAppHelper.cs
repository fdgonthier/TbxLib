using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tbx.Utils;
using TeamboxLib.App;
using TeamboxLib.Core;

namespace TeamboxLib.Utils
{
    public interface IAppHelper
    {
        /// <summary>
        /// Path to the directory containing the workspace state located in a
        /// Roaming part of the user's profile. Do not store big files in there.
        /// </summary>
        String KwsRoamingStatePath { get; }

        /// <summary>
        /// Reference to the user using the workspace with this KWM. Do not cache
        /// this object since it may change under your feet.
        /// </summary>
        TbxUser WmUser { get; }

        /// <summary>
        /// Reference to the workspace creator, if any.
        /// </summary>
        TbxUser WsCreator { get; }

        /// <summary>
        /// Return true if KwmUser is the workspace creator.
        /// </summary>
        bool IsCreatorKwmUser { get; }

        /// <summary>
        /// Create a new KAS ANP message having a unique ID.
        /// </summary>
        AnpMsg NewKAnpMsg(UInt32 type);

        /// <summary>
        /// Create a new KAS ANP command message having a unique ID and
        /// containing the ID of the workspace.
        /// </summary>
        AnpMsg NewKAnpCmd(UInt32 type);

        /// <summary>
        /// Post a command to the KAS of the workspace. A query object having
        /// the meta-data specified is returned. The query object will be
        /// re-supplied when the reply to the command is received. The reply of
        /// the command will be ignored if the workspace logs out for any
        /// reason.
        /// </summary>
        KasQuery PostAppKasQuery(AnpMsg cmd, Object[] metaData, KasQueryDelegate callback, KwsApp app);

        /// <summary>
        /// Post a GUI execution request associated to the workspace.
        /// </summary>
        void PostGuiExecRequest(GuiExecRequest req);

        /// <summary>
        /// Create a tunnel object suitable for communicating with the KAS of
        /// the workspace.
        /// </summary>
        IAnpTunnel CreateTunnel();

#if false
        /// <summary>
        /// Return a reference to the local database.
        /// </summary>
        WmLocalDb GetLocalDb();
#endif

        /// <summary>
        /// Get the internal identifier of the workspace used by the WM.
        /// </summary>
        UInt64 GetInternalKwsID();

        /// <summary>
        /// Get the ID of the workspace as known to the KAS.
        /// </summary>
        UInt64 GetExternalKwsID();

        /// <summary>
        /// Return true if the workspace is the public workspace of the user.
        /// </summary>
        bool IsPublicKws();

        /// <summary>
        /// Return the display name of the workspace.
        /// </summary>
        String GetKwsName();

        /// <summary>
        /// Return a name uniquely identifying the workspace (display name with
        /// the internal ID appended).
        /// </summary>
        String GetKwsUniqueName();

        /// <summary>
        /// Return the LoginLatestEventId property of the workspace.
        /// </summary>
        UInt64 KwsLoginLatestEventId { get; }

        /// <summary>
        /// Display this workspace fully in the UI.
        /// </summary>
        void ActivateKws();

        /// <summary>
        /// Return true if the workspace is selected in the UI.
        /// </summary>
        bool IsKwsSelected();

        /// <summary>
        /// Return the user having the ID specified, if any. Virtual users
        /// may be returned.
        /// </summary>
        TbxUser GetUserByID(UInt32 ID);

        /// <summary>
        /// Return the non-virtual user having the ID specified, if any.
        /// </summary>
        TbxUser GetNonVirtualUserByID(UInt32 ID);

        /// <summary>
        /// Select the specified user in the user list.
        /// </summary>
        void SelectUser(UInt32 userID);

        /// <summary>
        /// Notify the user about the occurrence of an event.
        /// </summary>
        void NotifyUser(NotificationItem item);

        /// <summary>
        /// Mark the application dirty for the reason specified. 
        /// </summary>
        void SetAppDirty(KwsApp app, String reason);

        /// <summary>
        /// This method must be called by the application when it has
        /// started.
        /// </summary>
        void OnAppStarted(KwsApp app);

        /// <summary>
        /// This method must be called by the application when it has
        /// stopped.
        /// </summary>
        void OnAppStopped(KwsApp app);

        /// <summary>
        /// This method must be called when the application fails.
        /// </summary>
        void HandleAppFailure(KwsApp app, Exception ex);

        /// <summary>
        /// Return the current run level of the workspace.
        /// </summary>
        TbxWsRunLevel GetRunLevel();

        /// <summary>
        /// Return true if the workspace has a level of functionality greater
        /// or equal to the offline mode.
        /// </summary>
        bool IsOfflineCapable();

        /// <summary>
        /// Return true if the workspace has a level of functionality equal to
        /// the online mode.
        /// </summary>
        bool IsOnlineCapable();

        /// <summary>
        /// Return true if the KWM has caught up with the events of the 
        /// workspace stored on the KAS. 
        /// </summary>
        bool HasCaughtUpWithKasEvents();

        /// <summary>
        /// Fired when the status of the workspace has changed.
        /// </summary>
        event EventHandler<EventArgs> OnKwsStatusChanged;

        /// <summary>
        /// Fired when the user selected in the UI has been changed.
        /// </summary>
        event EventHandler<OnKwsUserChangedEventArgs> OnKwsUserChanged;
    }
}
