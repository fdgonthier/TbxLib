using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using Tbx.Utils;
using TeamboxLib.Utils;
using TeamboxLib.Core;

namespace TeamboxLib.App
{
    /// <summary>
    /// Status of an application of the workspace.
    /// </summary>
    public enum KwsAppStatus
    {
        /// <summary>
        /// The application is stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// The application is stopping.
        /// </summary>
        Stopping,

        /// <summary>
        /// The application is started.
        /// </summary>
        Started,

        /// <summary>
        /// The application is starting.
        /// </summary>
        Starting
    }

    /// <summary>
    /// Method called when the KAS query results are ready.
    /// </summary>
    public delegate void KasQueryDelegate(KasQuery query); 

    /// <summary>
    /// Represent a query made on a KAS.
    /// </summary>
    public class KasQuery
    {
        /// <summary>
        /// ANP command of this query.
        /// </summary>
        public AnpMsg Cmd = null;

        /// <summary>
        /// ANP reply of this query.
        /// </summary>
        public AnpMsg Res = null;

        /// <summary>
        /// Meta data associated to this query.
        /// </summary>
        public Object[] MetaData = null;

        /// <summary>
        /// Message ID associated to this query.
        /// </summary>
        public UInt64 MsgID { get { return Cmd.ID; } }

        /// <summary>
        /// Callback called when the query has completed.
        /// </summary>
        public KasQueryDelegate Callback = null;

        public KasQuery(AnpMsg cmd, Object[] metaData, KasQueryDelegate callback)
        {
            Debug.Assert(cmd.ID != 0);
            Cmd = cmd;
            MetaData = metaData;
            Callback = callback;
        }

        /// <summary>
        /// Cancel the execution of this query if required.
        /// </summary>
        public virtual void Cancel()
        {
        }
    }

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
        TbxUser KwmUser { get; }

        /// <summary>
        /// Reference to the workspace creator, if any.
        /// </summary>
        TbxUser KwsCreator { get; }

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

    public interface IAnpTunnel
    {
        AnpTransport GetTransport();
        Tunnel GetTunnel();
        void BeginConnect(string host, int port);
        void Connect();
        void Connect(string host, int port);
        void CreateTransport();
        void SendMsg(AnpMsg msg);
        AnpMsg GetMsg();
        bool CheckReceive();
        void UpdateSelect(SelectSockets select);
        bool CheckSend();
        void Disconnect();
        void Terminate();
    }

    public class OnKwsUserChangedEventArgs : EventArgs
    {
        private UInt32 m_ID;

        public UInt32 ID
        {
            get { return m_ID; }
        }

        public OnKwsUserChangedEventArgs(UInt32 id)
        {
            m_ID = id;
        }
    }

    public class OnKwmFocusChanged : EventArgs
    {
        private bool m_isFocused;

        public bool IsFocused
        {
            get { return m_isFocused; }
        }

        public OnKwmFocusChanged(bool isFocused)
        {
            m_isFocused = isFocused;
        }
    }

    /// <summary>
    /// Represent an application in a workspace.
    /// </summary>
    [Serializable]
    public abstract class KwsApp
    {
        /// <summary>
        /// Application helper of the workspace.
        /// </summary>
        [NonSerialized]
        public IAppHelper Helper;

        /// <summary>
        /// Status of the application.
        /// </summary>
        [NonSerialized]
        public KwsAppStatus AppStatus;

        /// <summary>
        /// Run level the workspace had when it last sent us a notification.
        /// This is used to update the application control when the run level
        /// changes.
        /// </summary>
        [NonSerialized]
        public TbxWsRunLevel NotifiedRunLevel;

        /// <summary>
        /// Value of the catch up flag at the last notification.
        /// </summary>
        [NonSerialized]
        public bool NotifiedCaughtUpFlag;

        /// <summary>
        /// True if the workspace state has been modified since it has been
        /// serialized. Note: use SetDirty() to set the flag.
        /// </summary>
        [NonSerialized]
        public bool DirtyFlag;

        /// <summary>
        /// Reference to the control settings of the application, if any.
        /// </summary>
        public ControlSettings Settings = null;

        /// <summary>
        /// Fired when the application control needs to update itself because
        /// the application state has changed.
        /// </summary>
        [field: NonSerialized]
        public event EventHandler<EventArgs> OnNeedToUpdateControl;

        /// <summary>
        /// ID of the application.
        /// </summary>
        public abstract UInt32 AppID { get; }

        /// <summary>
        /// Non-deserializating constructor.
        /// </summary>
        public KwsApp(IAppHelper appHelper)
        {
            Initialize(appHelper);
        }

        /// <summary>
        /// WARNING: Except for AppKfs, no other app makes use of this base class
        /// functionnality of serialization. If you ever need m_settings in other classes, 
        /// make sure to include a call to base(...) in the constructor / GetObjectData methods.
        /// </summary>
        public KwsApp(SerializationInfo info, StreamingContext context)
        {
            Settings = info.GetValue("m_settings", typeof(ControlSettings)) as ControlSettings;
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("m_settings", Settings);
        }

        /// <summary>
        /// Initialization code common to both the deserialized and
        /// non-deserialized cases. This method must be called explicitly
        /// when the workspace is deserialized.
        /// </summary>
        public virtual void Initialize(IAppHelper appHelper)
        {
            Helper = appHelper;
            Helper.OnKwsStatusChanged += OnKwsStatusChangedInternal;
            AppStatus = KwsAppStatus.Stopped;
            DirtyFlag = false;
        }

        public virtual void ProcessCommand(AnpMsg msg)
        {
            Logging.Log("Process Command Unhandled");
        }

        /// <summary>
        /// Return true if the application is started.
        /// </summary>
        public bool IsStarted()
        {
            return (AppStatus == KwsAppStatus.Started);
        }

        /// <summary>
        /// Mark the application, the workspace and the WM dirty.
        /// </summary>
        public void SetDirty(String reason)
        {
            if (DirtyFlag) return;
            DirtyFlag = true;
            Helper.SetAppDirty(this, reason);
        }

        /// <summary>
        /// Post a command to the KAS of the workspace. A query object having
        /// the meta-data specified is returned. The query object will be
        /// re-supplied when the reply to the command is received. The reply of
        /// the command will be ignored if the workspace logs out for any
        /// reason.
        /// </summary>
        public KasQuery PostKasQuery(AnpMsg cmd, Object[] metaData, KasQueryDelegate callback)
        {
            return Helper.PostAppKasQuery(cmd, metaData, callback, this);
        }

        /// <summary>
        /// Handle an ANP event associated to this application.
        /// </summary>
        public virtual KwsAnpEventStatus HandleAnpEvent(AnpMsg msg)
        {
            return KwsAnpEventStatus.Unprocessed;
        }

        /// <summary>
        /// Start the application. When the application is started, call 
        /// Helper.OnAppStarted().
        /// </summary>
        public virtual void RequestStart()
        {
            Helper.OnAppStarted(this);
        }

        /// <summary>
        /// Stop the application. When the application is stopped, call 
        /// Helper.OnAppStopped().
        /// </summary>
        public virtual void RequestStop()
        {
            Helper.OnAppStopped(this);
        }

#if false
        /// <summary>
        /// Prepare the application to be rebuilt according to the
        /// parameters specified. This is called by the workspace state machine.
        /// </summary>
        public virtual void PrepareForRebuild(KwsRebuildInfo rebuildInfo)
        {
            SetDirty("rebuild");
            if (OnNeedToUpdateControl != null) OnNeedToUpdateControl(this, null);
        }
#endif

        /// <summary>
        /// Delete the application data stored in the database.
        /// This method is called explicitly when a stale workspace is being
        /// purged from the DB.
        /// 
        /// IMPORTANT: Do not call any method of the workspace helper aside
        /// of GetLocalDB() and GetInternalKwsID(). Throw an exception on
        /// failure.
        /// </summary>
        public virtual void DeleteDbData()
        {
        }

        /// <summary>
        /// Delete all the data related to the application. This includes data
        /// in files, in the database and in RAM. This method is called when the
        /// workspace is being removed.
        /// </summary>
        public virtual void DeleteAllData()
        {
        }

        /// <summary>
        /// Update the state of the application when the workspace is being 
        /// initialized. Throw an exception on failure.
        /// </summary>
        public virtual void NormalizeState()
        {
        }

        /// <summary>
        /// This method is called when the state of the workspace changes.
        /// </summary>
        private void OnKwsStatusChangedInternal(object _sender, EventArgs _args)
        {
            OnKwsStatusChanged(Helper.GetRunLevel(), Helper.HasCaughtUpWithKasEvents());
        }

        /// <summary>
        /// This method is called when the state of the workspace changes.
        /// </summary>
        public virtual void OnKwsStatusChanged(TbxWsRunLevel newRunLevel, bool newCaughtUpFlag)
        {
            // Update the application control.
            if (newRunLevel != NotifiedRunLevel || newCaughtUpFlag != NotifiedCaughtUpFlag)
            {
                NotifiedRunLevel = newRunLevel;
                NotifiedCaughtUpFlag = newCaughtUpFlag;
                if (OnNeedToUpdateControl != null) OnNeedToUpdateControl(this, null);
            }
        }

        /// <summary>
        /// Handle the focus change. Only used by the KFS application
        /// for now.
        /// </summary>
        public virtual void HandleOnKwmFocusChanged(bool isFocused)
        {
        }
    }

    /// <summary>
    /// This class represents a request to execute a method within the context
    /// of the UI and outside the context of any state machine (wm or workspace).
    /// It is expected that the method will display dialogs.
    /// </summary>
    public abstract class GuiExecRequest
    {
        /// <summary>
        /// True if the request has been cancelled.
        /// </summary>
        public bool CancelFlag = false;

        /// <summary>
        /// This method is invoked to execute the request in the context
        /// of the UI. You do not need to check for the CancelFlag as the
        /// first step of your implementation as it is assured by the system
        /// that at the moment your Run() is called, the Ger has not been 
        /// canceled. If you were to prompt the user for some input before 
        /// continuing your operation, you should check the cancel flag when
        /// your dialog prompt returns since you are exposed to reentrance and
        /// the flag could've changed under your feet.
        /// </summary>
        public abstract void Run();

        /// <summary>
        /// Mark the request as cancelled. The request won't be executed if
        /// the Run() method hasn't been called yet. Otherwise, the Run()
        /// method may check if the request was cancelled during its execution
        /// by checking the value of CancelFlag.
        /// </summary>
        public virtual void Cancel()
        {
            CancelFlag = true;
        }
    }

    /// <summary>
    /// This object allows the AppControl to store information specific to a
    /// workspace. For example, the KFS application uses this class to save
    /// the selected directory in the treeview and the last directory in 
    /// which a file was added to the share.
    /// </summary>
    [Serializable]
    public abstract class ControlSettings
    {
    }
}