using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml;
using Tbx.Utils;
using TeamboxLib.Utils;

namespace TeamboxLib.Tcm
{
    /// <summary>
    /// Status of the connection with the KAS.
    /// </summary>
    public enum TbxAppServerConnStatus
    {
        /// <summary>
        /// The workspace is not connected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// A request has been sent to disconnect the workspace.
        /// </summary>
        Disconnecting,

        /// <summary>
        /// The workspace is connected.
        /// </summary>
        Connected,

        /// <summary>
        /// A request has been sent to connect the workspace.
        /// </summary>
        Connecting
    }

    /// <summary>
    /// This class provides an identifier for a KAS server. The identifier
    /// consists of the host name and the port of the KAS server. Since this
    /// object is shared between threads without locking, it is immutable.
    /// </summary>
    [Serializable]
    public class TbxAppServerId : IComparable
    {
        private String m_host;
        private UInt16 m_port;

        public String Host { get { return m_host; } }
        public UInt16 Port { get { return m_port; } }

        public TbxAppServerId(String host, UInt16 port)
        {
            m_host = host;
            m_port = port;
        }

        public int CompareTo(Object obj)
        {
            TbxAppServerId appServerId = (TbxAppServerId)obj;

            int r = appServerId.Host.CompareTo(Host);
            if (r != 0) return r;

            return appServerId.Port.CompareTo(Port);
        }
    }
}
