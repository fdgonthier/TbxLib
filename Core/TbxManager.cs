using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tbx.Utils;
using TeamboxLib.Tcm;

namespace TeamboxLib.Core
{
    public class TbxManager : KwmWorkerThread
    {
        private TbxAppServerBroker m_broker;

        private bool m_wmNotifFlag;

        protected override void Run()
        {
            while (true)
            {
                SelectSockets s = new SelectSockets();
                Block(s);

                // If we were notified, process the WM messages. This refreshes
                // the quench deadline.
                if (m_wmNotifFlag)
                {
                    m_wmNotifFlag = false;
                    ProcessIncomingWmMessages();
                }
            }
        }

        protected override void OnCompletion()
        {
        }

        /// <summary>
        /// Called when the WM has notified us that it has sent us messages.
        /// </summary>
        public void HandleWmTcmNotification()
        {
            m_wmNotifFlag = true;
        }

        /// <summary>
        /// Process a control message received from the WM.
        /// </summary>
        private void ProcessWmControlMsg(TcmControlMsg msg)
        {
        }

        /// <summary>
        /// Process an ANP message received from the WM.
        /// </summary>
        private void ProcessWmAnpMsg(TcmAnpMsg msg)
        {
        }
        
        public void ProcessIncomingWmMessages()
        {
            // Process the incoming messages.
            List<TcmControlMsg> controlArray;
            List<TcmAnpMsg> anpArray;

            m_broker.GetMessagesForWm(out controlArray, out anpArray);

            foreach (TcmControlMsg msg in controlArray)
                ProcessWmControlMsg(msg);

            foreach (TcmAnpMsg msg in anpArray)
                ProcessWmAnpMsg(msg);
        }

        public TbxManager(TbxAppServerBroker broker)
        {
            m_broker = broker;
        }
    }
}
