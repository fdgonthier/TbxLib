using System.Collections.Generic;
using System;
using System.Net.Sockets;
using System.Diagnostics;
using Tbx.Utils;
using TeamboxLib.Utils;

namespace TeamboxLib.Tcm
{
    /// <summary>
    /// Connection status of a KAS.
    /// </summary>
    public enum TcmAppServerConStatus
    {
        /// <summary>
        /// Scheduled for connection,
        /// </summary>
        Scheduled,

        /// <summary>
        /// TCP connection being established.
        /// </summary>
        Connecting,

        /// <summary>
        /// Waiting for KCD role negociation reply.
        /// </summary>
        RoleReply,

        /// <summary>
        /// Connected in workspace mode.
        /// </summary>
        Connected,

        /// <summary>
        /// Connection lost.
        /// </summary>
        Disconnected
    };

    /// <summary>
    /// This class represents a KAS managed by the KCM.
    /// </summary>
    public class TbxAppServer
    {
        /// <summary>
        /// KAS identifier.
        /// </summary>
        public TbxAppServerId KasID;

        /// <summary>
        /// Connection status.
        /// </summary>
        public TcmAppServerConStatus ConnStatus = TcmAppServerConStatus.Scheduled;

        /// <summary>
        /// Represent the tunnel made with ktlstunnel.
        /// </summary>
        public AnpTunnel Tunnel;

        /// <summary>
        /// Queue of messages to send to the KAS.
        /// </summary>
        public Queue<AnpMsg> SendQueue = new Queue<AnpMsg>();

        /// <summary>
        /// Exception caught when the connection fails.
        /// </summary>
        public Exception Ex;

        /// <summary>
        /// Transport object of the ANP tunnel. This is null if the tunnel is
        /// not connected.
        /// </summary>
        public AnpTransport Transport { get { return Tunnel.GetTransport(); } }

        public TbxAppServer(TbxAppServerId kasID)
        {
            KasID = kasID;
            Tunnel = new AnpTunnel(KasID.Host, (int)KasID.Port);
        }

        /// <summary>
        /// This method verifies that no message has been received when it is 
        /// called.
        /// </summary>
        public void CheckNoMessageReceivedInvariant()
        {
            Debug.Assert(Tunnel == null || Transport == null || !Tunnel.HasReceivedMessage());
        }

        /// <summary>
        /// Send an ANP message. Side effect free.
        /// </summary>
        public void SafeSend(AnpMsg msg)
        {
            Transport.sendMsg(msg);
        }

        /// <summary>
        /// Send the next message queued in SendQueue if possible.
        /// </summary>
        public void SendNextQueuedMsgIfNeeded()
        {
            if (!Tunnel.IsSendingMessage() && SendQueue.Count != 0) SafeSend(SendQueue.Dequeue());
        }

        /// <summary>
        /// Send the select role command message.
        /// </summary>
        public void SendSelectRoleMsg()
        {                
            AnpMsg msg = new AnpMsg();
            msg.Major = KAnp.Major;
            msg.Minor = KAnp.Minor;
            msg.ID = 0;          
            msg.Type = KAnpType.KANP_CMD_MGT_SELECT_ROLE;
            msg.AddUInt32(KAnpType.KANP_KCD_ROLE_WORKSPACE);
            SafeSend(msg);
        }
    }

    /// <summary>
    /// This class implements the logic to communicate with the KCD in 
    /// workspace mode.
    /// </summary>
    public class TbxCommManager : KwmWorkerThread
    {
        /// <summary>
        /// Reference to the KCM broker.
        /// </summary>
        private TbxAppServerBroker m_broker;

        /// <summary>
        /// Delegate called when the KCM completes.
        /// </summary>
        private TcmCompletionDelegate m_completionDelegate;

        /// <summary>
        /// Tree of KASes indexed by KAS ID.
        /// </summary>
        private SortedDictionary<TbxAppServerId, TbxAppServer> m_kasTree = new SortedDictionary<TbxAppServerId, TbxAppServer>();

        /// <summary>
        /// List of KASes that have been disconnected. There is one
        /// disconnection notice per KAS in this list.
        /// </summary>
        private List<TbxAppServer> m_disconnectedList = new List<TbxAppServer>();

        /// <summary>
        /// List of control messages to send to the WM.
        /// </summary>
        private List<TcmControlMsg> m_toWmControlMsgList = new List<TcmControlMsg>();

        /// <summary>
        /// List of ANP messages to send to the WM.
        /// </summary>
        private List<TcmAnpMsg> m_toWmAnpMsgList = new List<TcmAnpMsg>();

        /// <summary>
        /// True if a notification has been received from the WM.
        /// </summary>
        private bool m_wmNotifFlag = false;

        /// <summary>
        /// Quench deadline last obtained from the WM.
        /// </summary>
        private DateTime m_quenchDeadline;

        public TbxCommManager(TbxAppServerBroker broker, TcmCompletionDelegate completionDelegate)
        {
            m_broker = broker;
            m_completionDelegate = completionDelegate;
        }

        protected override void Run()
        {
            m_wmNotifFlag = true;
            m_quenchDeadline = DateTime.MinValue;
            ReplyToWm();
            MainLoop();
        }

        protected override void OnCompletion()
        {
            if (Status == WorkerStatus.Failed) m_completionDelegate(false, FailException);
            else m_completionDelegate(true, null);
        }

        /// <summary>
        /// Called when the WM has notified us that it has sent us messages.
        /// </summary>
        public void HandleWmTcmNotification()
        {
            m_wmNotifFlag = true;
        }

        /// <summary>
        /// Return true if some messages need to be sent to the WM.
        /// </summary>
        private bool MustReplyToWm()
        {
            Debug.Assert(m_disconnectedList.Count == 0 || m_toWmControlMsgList.Count > 0);
            return (m_toWmAnpMsgList.Count > 0 || m_toWmControlMsgList.Count > 0);
        }

        /// <summary>
        /// Dispatch the ANP and control messages to the WM, retrieve the
        /// latest quench deadline and clear disconnected KASes.
        /// </summary>
        private void ReplyToWm()
        {
            if (!MustReplyToWm()) return;
            m_broker.SendMessagesToWm(m_toWmControlMsgList, m_toWmAnpMsgList, out m_quenchDeadline);
            foreach (TbxAppServer kas in m_disconnectedList) m_kasTree.Remove(kas.KasID);
            m_toWmControlMsgList.Clear();
            m_toWmAnpMsgList.Clear();
            m_disconnectedList.Clear();
        }

        /// <summary>
        /// Process messages sent by the WM, update the quench deadline.
        /// </summary>
        private void ProcessIncomingWmMessages()
       { 
            // Process the incoming messages.
            List<TcmControlMsg> controlArray;
            List<TcmAnpMsg> anpArray;
            m_broker.GetMessagesForTcm(out controlArray, out anpArray, out m_quenchDeadline);
            foreach (TcmControlMsg msg in controlArray) ProcessWmControlMsg(msg);
            foreach (TcmAnpMsg msg in anpArray) ProcessWmAnpMsg(msg);

            // Send back control replies, if any.
            ReplyToWm();
        }

        /// <summary>
        /// Process a control message received from the WM.
        /// </summary>
        private void ProcessWmControlMsg(TcmControlMsg msg)
        {
            Debug.Assert(msg is TcmConnectionRequest);
            TcmConnectionRequest req = (TcmConnectionRequest)msg;

            // Handle new KAS to connect.
            if (req.ConnectFlag)
            {
                Debug.Assert(!m_kasTree.ContainsKey(req.KasID));
                m_kasTree[req.KasID] = new TbxAppServer(req.KasID);
            }

            // Disconnect the KAS, if we didn't disconnect it yet.
            else
            {
                if (!m_kasTree.ContainsKey(req.KasID)) return;
                TbxAppServer kas = m_kasTree[req.KasID];
                if (kas.ConnStatus == TcmAppServerConStatus.Disconnected) return;
                HandleDisconnectedKas(kas, null);
            }
        }

        /// <summary>
        /// Process an ANP message received from the WM.
        /// </summary>
        private void ProcessWmAnpMsg(TcmAnpMsg msg)
        {
            // Ignore messages not destined to connected KASes.
            if (!m_kasTree.ContainsKey(msg.KasID)) return;
            TbxAppServer kas = m_kasTree[msg.KasID];
            if (kas.ConnStatus != TcmAppServerConStatus.Connected) return;

            // Enqueue the message.
            kas.SendQueue.Enqueue(msg.Msg);
        }

        /// <summary>
        /// Mark the KAS as connected and add a control message for the KAS in
        /// the control message list.
        /// </summary>
        private void HandleConnectedAppServer(TbxAppServer k, UInt32 minor)
        {
            k.ConnStatus = TcmAppServerConStatus.Connected;
            m_toWmControlMsgList.Add(new TcmConnectionNotice(k.KasID, minor));
        }

        /// <summary>
        /// Mark the KAS as disconnected, add a control message for the KAS in 
        /// the control message list and add the KAS to the disconnected list.
        /// </summary>
        private void HandleDisconnectedKas(TbxAppServer k, Exception ex)
        {
            if (ex != null) Logging.Log(2, "KAS " + k.KasID.Host + " exception: " + ex.Message);
            if (k.Tunnel != null) k.Tunnel.Disconnect();
            k.ConnStatus = TcmAppServerConStatus.Disconnected;
            k.Ex = ex;
            m_toWmControlMsgList.Add(new TcmDisconnectionNotice(k.KasID, k.Ex));
            m_disconnectedList.Add(k);
        }

        /// <summary>
        /// Loop processing KASes.
        /// </summary>
        private void MainLoop()
        {
            while (true)
            {
                Debug.Assert(!MustReplyToWm());

                // Refresh the quench deadline if it depends on the amount of 
                // time elapsed.
                if (m_quenchDeadline != DateTime.MinValue && m_quenchDeadline != DateTime.MaxValue)
                    m_wmNotifFlag = true;

                // If we were notified, process the WM messages. This refreshes
                // the quench deadline.
                if (m_wmNotifFlag)
                {
                    m_wmNotifFlag = false;
                    ProcessIncomingWmMessages();
                }

                Debug.Assert(!MustReplyToWm());

                // Determine whether we are quenched and the value of the
                // select timeout. By default we wait forever in select().
                bool quenchFlag = false;
                int timeout = -2;

                // Be quenched until we are notified.
                if (m_quenchDeadline == DateTime.MaxValue)
                    quenchFlag = true;

                // Be quenched up to the deadline we were given.
                else if (m_quenchDeadline != DateTime.MinValue)
                {
                    DateTime now = DateTime.Now;
                    if (m_quenchDeadline > now)
                    {
                        quenchFlag = true;
                        timeout = (int)(m_quenchDeadline - now).TotalMilliseconds;
                    }
                }

                // Prepare for call to select.
                bool connWatchFlag = false;
                SelectSockets selectSockets = new SelectSockets();
                foreach (TbxAppServer k in m_kasTree.Values) PrepareStateForSelect(k, selectSockets,
                                                                             quenchFlag, ref connWatchFlag);

                // Our state has changed. Notify the WM and recompute our state.
                if (MustReplyToWm())
                {
                    ReplyToWm();
                    continue;
                }

                // Reduce the timeout to account for ktlstunnel.exe.
                if (connWatchFlag) DecrementTimeout(ref timeout, 300);

                // Block in the call to select(). Note that we receive notifications
                // here.
                selectSockets.Timeout = timeout * 1000;
                Block(selectSockets);

                // If we are not quenched, perform transfers.
                if (!quenchFlag)
                {
                    foreach (TbxAppServer k in m_kasTree.Values) UpdateStateAfterSelect(k, selectSockets);
                    ReplyToWm();
                }
            }
        }

        /// <summary>
        /// Add the socket of the KAS in the select sets as needed and manage
        /// ktlstunnel.exe processes.
        /// </summary>
        private void PrepareStateForSelect(TbxAppServer k, SelectSockets selectSockets, bool quenchFlag,
                                           ref bool connWatchFlag)
        {
            // Note: the KAS should never have received a message when this function
            // is called. The function UpdateStateAfterSelect() is responsible for
            // doing all the transfers and handling any message received after these
            // transfers.
            try
            {
                k.CheckNoMessageReceivedInvariant();

                if (k.ConnStatus == TcmAppServerConStatus.Scheduled)
                {
                    // Start ktlstunnel.exe.
                    k.ConnStatus = TcmAppServerConStatus.Connecting;
                    k.Tunnel.BeginConnect();
                }

                if (k.ConnStatus == TcmAppServerConStatus.Connecting)
                {
                    // The TCP connection is now open.
                    if (k.Tunnel.CheckConnect(0))
                    {
                        // Send the select role command.
                        k.SendSelectRoleMsg();

                        // Wait for the reply to arrive.
                        k.ConnStatus = TcmAppServerConStatus.RoleReply;
                    }

                    // Wait for the TCP connection to be established. We busy wait
                    // to monitor the status of ktlstunnel.exe regularly, to detect
                    // the case where the connection fails.
                    else connWatchFlag = true;
                }

                if (k.ConnStatus == TcmAppServerConStatus.RoleReply)
                {
                    // Wait for the reply to arrive.
                    if (!quenchFlag) k.Tunnel.UpdateSelect(selectSockets);
                }

                if (k.ConnStatus == TcmAppServerConStatus.Connected)
                {
                    // Send the next message, if possible.
                    k.SendNextQueuedMsgIfNeeded();
                    if (!quenchFlag) k.Tunnel.UpdateSelect(selectSockets);
                }

                k.CheckNoMessageReceivedInvariant();
            }

            catch (Exception ex)
            {
                HandleDisconnectedKas(k, ex);
            }
        }

        /// <summary>
        /// Analyse the result of the select() call for the specified KAS.
        /// </summary>
        private void UpdateStateAfterSelect(TbxAppServer k, SelectSockets selectSockets)
        {
            try
            {
                k.CheckNoMessageReceivedInvariant();

                // We have nothing to do if we don't have an established TCP
                // connection.
                if (k.ConnStatus != TcmAppServerConStatus.Connected &&
                    k.ConnStatus != TcmAppServerConStatus.RoleReply) return;

                // Perform transfers only if the socket is ready.
                Debug.Assert(k.Tunnel.Sock != null);
                if (!selectSockets.InReadOrWrite(k.Tunnel.Sock)) return;

                // Do up to 20 transfers (the limit exists for quenching purposes).
                for (int i = 0; i < 20; i++)
                {
                    // Send a message if possible.
                    k.SendNextQueuedMsgIfNeeded();

                    // Remember if we are sending a message.
                    bool sendingFlag = k.Tunnel.IsSendingMessage();

                    // Do transfers.
                    k.Tunnel.DoXfer();

                    // Stop if no message has been received and no message has been sent.
                    if (!k.Tunnel.HasReceivedMessage() &&
                        (!sendingFlag || k.Tunnel.IsSendingMessage())) break;
                        
                    // Process the message received.
                    if (k.Tunnel.HasReceivedMessage())
                        ProcessIncomingAppServerMessage(k, k.Tunnel.GetMsg());
                }

                k.CheckNoMessageReceivedInvariant();
            }

            catch (Exception ex)
            {
                HandleDisconnectedKas(k, ex);
            }
        }

        /// <summary>
        /// Handle a message received from a KAS.
        /// </summary>
        private void ProcessIncomingAppServerMessage(TbxAppServer k, AnpMsg msg)
        {
            if (k.ConnStatus == TcmAppServerConStatus.RoleReply)
            {
                if (msg.Type == KAnpType.KANP_RES_FAIL_MUST_UPGRADE)
                    throw new Exception("The KWM is too old, it needs to be upgraded to communicate with " +
                                         k.KasID.Host + " which has protocol version " + msg.Minor);

                else if (msg.Type != KAnpType.KANP_RES_OK)
                    throw new Exception(msg.Elements[1].String);

                else if (msg.Minor < KAnp.LastCompMinor)
                    throw new Exception("The KCD at " + k.KasID.Host +
                                        " is too old and needs to be upgraded.");

                else HandleConnectedAppServer(k, Math.Min(msg.Minor, KAnp.Minor));
            }

            else m_toWmAnpMsgList.Add(new TcmAnpMsg(msg, k.KasID));
        }

        /// <summary>
        /// Reduce the value of the timeout specified to the value
        /// specified. -2 means infinity.
        /// </summary>
        private void DecrementTimeout(ref int timeout, int value)
        {
            Debug.Assert(value >= 0);
            if (timeout == -2) timeout = value;
            else timeout = Math.Min(timeout, value);
        }
    }
}