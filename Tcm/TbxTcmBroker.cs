using System.Collections.Generic;
using System;
using System.Diagnostics;
using Tbx.Utils;
using TeamboxLib.Core;

namespace TeamboxLib.Tcm
{
    /// <summary>
    /// This class represents an ANP message delivered to/from a KAS
    /// by the KAS communication manager.
    /// </summary>
    public class TcmAnpMsg
    {
        /// <summary>
        /// The ANP message being delivered.
        /// </summary>
        public AnpMsg Msg;

        /// <summary>
        /// Associated KAS.
        /// </summary>
        public TbxAppServerId KasID;

        public TcmAnpMsg(AnpMsg msg, TbxAppServerId kasID)
        {
            Msg = msg;
            KasID = kasID;
        }

        /// <summary>
        /// Return true if the message is an ANP reply.
        /// </summary>
        public bool IsReply()
        {
            return ((Msg.Type & KAnpType.ROLE_MASK) == KAnpType.KANP_RES);
        }

        /// <summary>
        /// Return true if the message is an ANP event.
        /// </summary>
        public bool IsEvent()
        {
            return ((Msg.Type & KAnpType.ROLE_MASK) == KAnpType.KANP_EVT);
        }
    }

    /// <summary>
    /// This class represents a control message exchanged between the WM and
    /// the KCM.
    /// </summary>
    public class TcmControlMsg { }

    /// <summary>
    /// This class represents a KAS connect/disconnect request.
    /// </summary>
    public class TcmConnectionRequest : TcmControlMsg
    {
        /// <summary>
        /// ID of the KAS to connect/disconnect.
        /// </summary>
        public TbxAppServerId KasID;

        /// <summary>
        /// True if connection is required.
        /// </summary>
        public bool ConnectFlag;

        public TcmConnectionRequest(TbxAppServerId kasID, bool connectFlag)
        {
            KasID = kasID;
            ConnectFlag = connectFlag;
        }
    }

    /// <summary>
    /// This class represents a KAS connection notice. Used when
    /// a KAS is now in the connected state.
    /// </summary>
    public class TcmConnectionNotice : TcmControlMsg
    {
        /// <summary>
        /// ID of the KAS that is now connected.
        /// </summary>
        public TbxAppServerId KasID;

        /// <summary>
        /// Minor version of the protocol spoken with the KAS.
        /// </summary>
        public UInt32 MinorVersion;

        public TcmConnectionNotice(TbxAppServerId kasID, UInt32 minorVersion)
        {
            KasID = kasID;
            MinorVersion = minorVersion;
        }
    }

    /// <summary>
    /// This class represents a KAS disconnection notice. Used when
    /// a KAS is now in the disconnected state.
    /// </summary>
    public class TcmDisconnectionNotice : TcmControlMsg
    {
        /// <summary>
        /// ID of the KAS that is now disconnected.
        /// </summary>
        public TbxAppServerId KasID;

        /// <summary>
        /// If the disconnection was caused by an error, this
        /// is the exception describing the error.
        /// </summary>
        public Exception Ex;

        public TcmDisconnectionNotice(TbxAppServerId kasID, Exception ex)
        {
            KasID = kasID;
            Ex = ex;
        }
    }

    /// <summary>
    /// This message is posted to the UI thread by the broker to wake-up
    /// the WM.
    /// </summary>
    public class WkbWmWakeUpMsg : WorkerThreadMsg
    {
        private TbxAppServerBroker Broker;

        public WkbWmWakeUpMsg(TbxAppServerBroker broker)
        {
            Broker = broker;
        }

        public override void Run()
        {
            Broker.HandleWmWakeUp(this);
        }
    }

    /// <summary>
    /// This message is posted to the KCM thread by the broker to wake-up
    /// the KCM.
    /// </summary>
    public class WkbKcmWakeUpMsg : WorkerThreadMsg
    {
        private TbxAppServerBroker Broker;

        public WkbKcmWakeUpMsg(TbxAppServerBroker broker)
        {
            Broker = broker;
        }

        public override void Run()
        {
            Broker.HandleKcmWakeUp(this);
        }
    }

    /// <summary>
    /// Delegate type called from the OnCompletion() handler of the KCM
    /// thread.
    /// </summary>
    public delegate void TcmCompletionDelegate(bool successFlag, Exception ex);
    
    public delegate void KcmNotifyDelegate(WkbKcmWakeUpMsg wkUpMsg);

    /// <summary>
    /// This class manages the interactions between the KAS communication
    /// manager and the workspace manager. It encapsulates most synchronization
    /// and flow control issues.
    /// </summary>
    public class TbxAppServerBroker
    {
        /// <summary>
        /// Reference to the workspace manager.
        /// </summary>
        private TbxManager m_wm = null;

        /// <summary>
        /// Quench if that many messages are lingering in the WM ANP message
        /// queue.
        /// </summary>
        private const UInt32 m_quenchQueueMaxSize = 50;

        /// <summary>
        /// Number of messages to post to the WM between each quench check.
        /// </summary>
        private const UInt32 m_quenchBatchCount = 100;

        /// <summary>
        /// Rate at which messages will be processed, e.g. 1 message per 2
        /// milliseconds.
        /// </summary>
        private const UInt32 m_quenchProcessRate = 5;

        /// <summary>
        /// Reference to the KAS communication manager.
        /// </summary>
        private TbxCommManager m_tcm = null;

        /// <summary>
        /// Mutex protecting the variables that follow.
        /// </summary>
        private Object m_mutex = new Object();

        /// <summary>
        /// Message posted to wake-up the WM.
        /// </summary>
        private WkbWmWakeUpMsg m_wmWakeUpMsg = null;

        /// <summary>
        /// Message posted to wake-up the KCM.
        /// </summary>
        private WkbKcmWakeUpMsg m_tcmWakeUpMsg = null;

        /// <summary>
        /// Array of control messages posted to the WM.
        /// </summary>
        private List<TcmControlMsg> m_ToWmControlMsgArray = new List<TcmControlMsg>();

        /// <summary>
        /// Array of control messages posted to the KCM.
        /// </summary>
        private List<TcmControlMsg> m_ToKcmControlMsgArray = new List<TcmControlMsg>();

        /// <summary>
        /// Array of ANP message posted to the WM.
        /// </summary>
        private List<TcmAnpMsg> m_ToWmAnpMsgArray = new List<TcmAnpMsg>();

        /// <summary>
        /// Array of ANP message posted to the KCM.
        /// </summary>
        private List<TcmAnpMsg> m_ToKcmAnpMsgArray = new List<TcmAnpMsg>();

        /// <summary>
        /// Number of messages that have been processed in the current batch.
        /// </summary>
        private UInt32 m_currentBatchCount = 0;

        /// <summary>
        /// Date at which the current batch has been started.
        /// </summary>
        private DateTime m_currentBatchStartDate = DateTime.MinValue;

        /// <summary>
        /// Substitute for the constructor to allow KCM to be created
        /// appropriately by the WM.
        /// </summary>
        public void Initialize(TbxCommManager kcm, TbxManager wm)
        {
            m_tcm = kcm;
            m_wm = wm;
        }

        /// <summary>
        /// Notify the WM that something occurred. Assume mutex is locked.
        /// </summary>
        private void NotifyWm()
        {
            if (m_wm != null && m_wmWakeUpMsg == null)
            {
                m_wmWakeUpMsg = new WkbWmWakeUpMsg(this);
                m_wm.PostToWorker(m_wmWakeUpMsg);
            }
        }

        /// <summary>
        /// Notify the KCM that something occurred. Assume mutex is locked.
        /// </summary>
        private void NotifyTcm()
        {
            if (m_tcm != null && m_tcmWakeUpMsg == null)
            {
                m_tcmWakeUpMsg = new WkbKcmWakeUpMsg(this);
                m_tcm.PostToWorker(m_tcmWakeUpMsg);
            }
        }

        /// <summary>
        /// Recompute the quench deadline returned to the KCM.
        /// Assume mutex is locked.
        /// </summary>
        private DateTime RecomputeQuenchDeadline()
        {
            // Too many unprocessed messages.
            if (m_ToWmAnpMsgArray.Count >= m_quenchQueueMaxSize) return DateTime.MaxValue;

            // Batch check count not yet reached.
            if (m_currentBatchCount < m_quenchBatchCount) return DateTime.MinValue;

            // Compute deadline.
            DateTime deadline = m_currentBatchStartDate.AddMilliseconds(m_currentBatchCount * m_quenchProcessRate);
            DateTime now = DateTime.Now;

            // Enough time has passed during the processsing of the batch.
            // Reset the batch statistics.
            if (deadline < now)
            {
                m_currentBatchCount = 0;
                m_currentBatchStartDate = now;
                return DateTime.MinValue;
            }

            // Not enough time has passed to process the batch. Return the
            // deadline.
            return deadline;
        }


        ////////////////////////////////////////////
        // Interface methods for internal events. //
        ////////////////////////////////////////////

        /// <summary>
        /// Internal handler for WkbWmWakeUpMsg.
        /// </summary>
        public void HandleWmWakeUp(WkbWmWakeUpMsg msg)
        {
            lock (m_mutex)
            {
                // Clear the posted message reference.
                Debug.Assert(m_wmWakeUpMsg == msg);
                m_wmWakeUpMsg = null;
            }

            // Notify the WM state machine that we have something for it.
            m_wm.HandleWmTcmNotification();
        }

        /// <summary>
        /// Internal handler for WkbKcmWakeUpMsg.
        /// </summary>
        public void HandleKcmWakeUp(WkbKcmWakeUpMsg msg)
        {
            lock (m_mutex)
            {
                // Clear the posted message reference.
                Debug.Assert(m_tcmWakeUpMsg == msg);
                m_tcmWakeUpMsg = null;
            }

            // Notify the KCM that we have something for it.
            m_tcm.HandleWmTcmNotification();
        }


        ///////////////////////////////////
        // Interface methods for the WM. //
        ///////////////////////////////////

        /// <summary>
        /// Request a KAS to be connected.
        /// </summary>
        public void RequestAppServerConnect(TbxAppServerId kasID)
        {
            lock (m_mutex)
            {
                // The following sequence of events can happen:
                // - KCM posts disconnection event.
                // - WM posts ANP message.
                // - WM receives disconnection event.
                // - WM posts connection request.
                // - KCM receives connection request and ANP message concurrently,
                //   possibly posting the ANP message incorrectly.
                // To prevent this situation, we ensure that we have no lingering
                // ANP message left for that KAS.
                List<TcmAnpMsg> newList = new List<TcmAnpMsg>();

                foreach (TcmAnpMsg m in m_ToKcmAnpMsgArray)
                {
                    if (m.KasID != kasID) newList.Add(m);
                }

                m_ToKcmAnpMsgArray = newList;

                m_ToKcmControlMsgArray.Add(new TcmConnectionRequest(kasID, true));
                NotifyTcm();
            }
        }

        /// <summary>
        /// Request a KAS to be disconnected.
        /// </summary>
        public void RequestKasDisconnect(TbxAppServerId kasID)
        {
            lock (m_mutex)
            {
                m_ToKcmControlMsgArray.Add(new TcmConnectionRequest(kasID, false));
                NotifyTcm();
            }
        }

        /// <summary>
        /// Send an ANP message to a KAS.
        /// </summary>
        public void SendAnpMsgToKcm(TcmAnpMsg m)
        {
            lock (m_mutex)
            {
                m_ToKcmAnpMsgArray.Add(m);
                NotifyTcm();
            }
        }

        /// <summary>
        /// Return the messages posted by the KCM.
        /// </summary>
        public void GetMessagesForWm(out List<TcmControlMsg> controlArray, out List<TcmAnpMsg> anpArray)
        {
            lock (m_mutex)
            {
                // Notify KCM if it was potentially quenched.
                if (m_ToWmAnpMsgArray.Count >= m_quenchQueueMaxSize) NotifyTcm();

                controlArray = m_ToWmControlMsgArray;
                m_ToWmControlMsgArray = new List<TcmControlMsg>();
                anpArray = m_ToWmAnpMsgArray;
                m_ToWmAnpMsgArray = new List<TcmAnpMsg>();
            }
        }


        ////////////////////////////////////
        // Interface methods for the KCM. //
        ////////////////////////////////////

        /// <summary>
        /// Return the messages posted by the KCM and the current quench
        /// deadline.
        /// </summary>
        public void GetMessagesForTcm(out List<TcmControlMsg> controlArray, out List<TcmAnpMsg> anpArray,
                                      out DateTime quenchDeadline)
        {
            lock (m_mutex)
            {
                controlArray = m_ToKcmControlMsgArray;
                m_ToKcmControlMsgArray = new List<TcmControlMsg>();
                anpArray = m_ToKcmAnpMsgArray;
                m_ToKcmAnpMsgArray = new List<TcmAnpMsg>();
                quenchDeadline = RecomputeQuenchDeadline();
            }
        }

        /// <summary>
        /// Send control and ANP messages to the WM, return current quench
        /// deadline.
        /// </summary>
        public void SendMessagesToWm(List<TcmControlMsg> controlArray, List<TcmAnpMsg> anpArray,
                                     out DateTime quenchDeadline)
        {
            lock (m_mutex)
            {
                m_ToWmControlMsgArray.AddRange(controlArray);
                m_ToWmAnpMsgArray.AddRange(anpArray);
                m_currentBatchCount += (UInt32)anpArray.Count;
                quenchDeadline = RecomputeQuenchDeadline();
                NotifyWm();
            }
        }
    }
}