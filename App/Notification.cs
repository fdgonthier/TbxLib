using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tbx.Utils;
using TeamboxLib.Utils;

namespace TeamboxLib.App
{
    // FIXME: clean up this code.

    /// <summary>
    /// This class is used to format an ANP message to 
    /// be able to notify gracefully.
    /// </summary>
    public abstract class NotificationItem
    {
        public enum DetailCode : int
        {
            SimpleFormatDetail = 0,
            SimpleDetail,
            FullFormatedDetail,
            FullDetail
        }

        /// <summary>
        /// This enum is to set the notification action to take.
        /// </summary>
        [Flags]
        public enum NotificationEffect
        {
            /// <summary>
            /// No notification desired.
            /// </summary>
            None = 0,

            /// <summary>
            /// Show a Tray Message popup.
            /// </summary>
            ShowPopup = 0x00000001 << 0,

            /// <summary>
            /// Log the notification to the Event List.
            /// </summary>
            LogToEventList = 0x00000001 << 1,

            /// <summary>
            /// Make the Workspace appear in Bold in the 
            /// workspace list.
            /// </summary>
            BoldWsName = 0x00000001 << 2,

            /// <summary>
            /// Make the Tray icon blink.
            /// </summary>
            BlinkTrayIcon = 0x00000001 << 3,

            /// <summary>
            /// Unknown.
            /// </summary>
            BlinkWindowIcon = 0x00000001 << 4,

            /// <summary>
            /// Give focus to the application.
            /// </summary>
            FocusWindows = 0x00000001 << 5,

            /// <summary>
            /// Shake the application's window.
            /// </summary>
            ShakeWindow = 0x00000001 << 6,

            /// <summary>
            /// Play a system Beep.
            /// </summary>
            PlayBeep = 0x00000001 << 7,

            /// <summary>
            /// Play a song.
            /// </summary>
            PlaySong = 0x00000001 << 8
        }

        /// <summary>
        /// The default Notification is popup, log to event list and bold the ws.
        /// </summary>
        protected static NotificationEffect DefaultEffect =
            NotificationEffect.ShowPopup |
            NotificationEffect.LogToEventList |
            NotificationEffect.BoldWsName |
            NotificationEffect.BlinkTrayIcon;

        /// <summary>
        /// Internal Workspace ID concerned by this notification.
        /// </summary>
        public UInt64 InternalWsID { get { return m_helper.GetInternalKwsID(); } }

        /// <summary>
        /// User ID that created the event, if any.
        /// </summary>
        protected String m_eventSourceUserName;

        /// <summary>
        /// KAnpType of the event, if any.
        /// </summary>
        protected UInt32 m_eventType;

        /// <summary>
        /// Application ID associated to the notification, or 0 if none.
        /// </summary>
        protected UInt32 m_appID = 0;

        /// <summary>
        /// Date at which the event occurred on the server.
        /// </summary>
        protected DateTime m_serverEventDate;

        /// <summary>
        /// Date at which we received the event.
        /// </summary>
        protected DateTime m_localEventDate = DateTime.Now;

        /// <summary>
        /// A reference to a Helper object.
        /// </summary>
        protected IAppHelper m_helper;

        /// <summary>
        /// Determine what actions the UI should take when it deals with this notification.
        /// </summary>
        protected NotificationEffect m_notificationToTake = NotificationEffect.None;

        public uint AppId
        {
            get { return m_appID; }
        }

        /// <summary>
        /// Event text to display.
        /// </summary>
        public abstract String EventText
        {
            get;
        }

        /// <summary>
        /// Event text to display, in RTF format. Call this only after checking 
        /// the HasRtfText property.
        /// </summary>
        public virtual String EventRtf
        {
            get
            {
                return @"{\rtf1\ansi{\fonttbl\f0\fswiss Helvetica;}\f0";
            }
        }

        /// <summary>
        /// Return true if the item has some RTF text available for tray icon 
        /// notification.
        /// </summary>
        public virtual bool HasRtfText
        {
            get { return false; }
        }

        /// <summary>
        /// Date at which we received the event.
        /// </summary>
        public DateTime LocalEventDate
        {
            get { return m_localEventDate; }
        }

        public string WorkspaceName
        {
            get { return m_helper.GetKwsName(); }
        }

        public String AppName
        {
            get { return Misc.GetApplicationName(AppId); }
        }

        public String GetShortFormattedDate
        {
            get { return "(" + m_serverEventDate.ToString("d") + ")"; }
        }

        public String GetLongFormattedDate
        {
            get { return "(" + m_serverEventDate.ToString("f") + ")"; }
        }

        public NotificationEffect NotificationToTake
        {
            get { return m_notificationToTake; }
            set { m_notificationToTake = value; }
        }

        public virtual bool WantShowPopup
        {
            get
            {
                return ((m_notificationToTake & NotificationEffect.ShowPopup) == NotificationEffect.ShowPopup);
            }
            set
            {
                if (value)
                    m_notificationToTake |= NotificationItem.NotificationEffect.ShowPopup;
                else
                    m_notificationToTake &= ~NotificationItem.NotificationEffect.ShowPopup;

            }
        }

        public bool WantLogToEventList
        {
            get
            {
                return ((m_notificationToTake & NotificationEffect.LogToEventList) == NotificationEffect.LogToEventList);
            }
        }

        public bool WantBoldWs
        {
            get
            {
                return ((m_notificationToTake & NotificationEffect.BoldWsName) == NotificationEffect.BoldWsName);
            }
        }

        public bool WantBlinkTrayIcon
        {
            get
            {
                return ((m_notificationToTake & NotificationEffect.BlinkTrayIcon) == NotificationEffect.BlinkTrayIcon);
            }
        }

        public bool WantBlinkWindowIcon
        {
            get
            {
                return ((m_notificationToTake & NotificationEffect.BlinkWindowIcon) == NotificationEffect.BlinkWindowIcon);
            }
        }

        public bool WantFocusWindow
        {
            get
            {
                return ((m_notificationToTake & NotificationEffect.FocusWindows) == NotificationEffect.FocusWindows);
            }
        }

        public bool WantShakeWindow
        {
            get
            {
                return ((m_notificationToTake & NotificationEffect.ShakeWindow) == NotificationEffect.ShakeWindow);
            }
        }

        public bool WantPlayBeep
        {
            get
            {
                return ((m_notificationToTake & NotificationEffect.PlayBeep) == NotificationEffect.PlayBeep);
            }
        }

        public bool WantPlaySong
        {
            get
            {
                return ((m_notificationToTake & NotificationEffect.PlaySong) == NotificationEffect.PlaySong);
            }
        }

        public bool WantNothing
        {
            get { return m_notificationToTake == NotificationEffect.None; }
        }

        public NotificationItem(AnpMsg _msg, UInt32 appID, IAppHelper _helper)
        {
            m_helper = _helper;
            m_appID = appID;
            m_notificationToTake = DefaultEffect;

            // Show a popup only if the event occured after we logged on the
            // workspace and we caught up with the KAS events. In some cases we
            // do not add an Anp message to this object, simply check for the 
            // CaughtUp flag.
            WantShowPopup = (_msg == null || _msg.ID > _helper.KwsLoginLatestEventId) &&
                            _helper.HasCaughtUpWithKasEvents();

            if (_msg != null) Init(_msg);
        }

        private void Init(AnpMsg _msg)
        {
            m_serverEventDate = Base.KDateToDateTime(_msg.Elements[1].UInt64);
            m_eventSourceUserName = m_helper.GetUserByID(_msg.Elements[2].UInt32).UiSimpleName;
            m_eventType = _msg.Type;
        }

        /// <summary>
        /// Format details are multi-lined, which is inappropriate for
        /// logging in a list view.
        /// </summary>
        public abstract String GetSimplifiedFormattedDetail();

        /// <summary>
        /// Multi-lined.
        /// </summary>
        public abstract String GetFullFormattedDetail();

        /// <summary>
        /// One-liner information.
        /// </summary>
        public abstract String GetSimpleDetail();

        public abstract String GetFullDetail();

        /// <summary>
        /// Return the detail string according to the desired detail level.
        /// </summary>
        public String GetDetail(DetailCode detailCode)
        {
            if (detailCode == DetailCode.SimpleDetail)
            {
                return GetSimpleDetail();
            }
            else if (detailCode == DetailCode.SimpleFormatDetail)
            {
                return GetSimplifiedFormattedDetail();
            }
            else if (detailCode == DetailCode.FullDetail)
            {
                return GetFullDetail();
            }
            else if (detailCode == DetailCode.FullFormatedDetail)
            {
                return GetFullFormattedDetail();
            }
            else
            {
                return GetSimpleDetail();
            }
        }
    }
}
