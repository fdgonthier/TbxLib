using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System;
using System.Runtime.InteropServices;
using System.IO;
using Tbx.Utils;

namespace TeamboxLib.Utils
{
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

    /// <summary>
    /// Add an ANP interface to the Tunnel.
    /// </summary>
    public class AnpTunnel : IAnpTunnel
    {
        private AnpTransport transport = null;
        private Tunnel tunnel;

        public AnpTunnel(string _host, int _port)
        {
            tunnel = new Tunnel(_host, _port);
        }

        public Socket Sock
        {
            get { return tunnel.Sock; }
        }

        public AnpTransport GetTransport()
        {
            return transport;
        }

        public Tunnel GetTunnel()
        {
            return tunnel;
        }

        public void Connect()
        {
            Connect(null, 0);
        }

        /// <summary>
        /// Connect the tunnel to the remote host. If host != null,
        /// then ktlstunnel reconnects to this host when the local
        /// connection is closed.
        /// This is a blocking method.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        public void Connect(string host, int port)
        {
            Logging.Log("Tunnel.cs : Connect called");
            BeginConnect(host, port);

            int timeout = 2;

            while (!CheckConnect(timeout))
            {
                timeout *= 2;
            }
            Logging.Log("Connect exiting");
        }

        public void BeginConnect()
        {
            BeginConnect(null, 0);
        }

        /// <summary>
        /// Connect the tunnel to the remote host. If host != null,
        /// then ktlstunnel reconnects to this host when the local
        /// connection is closed.
        /// This is a non-blocking method.
        /// </summary>
        public void BeginConnect(string host, int port)
        {
            if (host == null || port == 0)
                tunnel.BeginTls("");
            else
                tunnel.BeginTls("-r " + host + ":" + port.ToString());
        }

        /// <summary>
        /// Block until the tunnel is connected or a timeout occurs. If the tunnel
        /// is connected, a new AnpTransport is created and the method returns true.
        /// Otherwise, the method returns false.
        /// </summary>
        /// <param name="timeout">Microseconds to wait in the Select.</param>
        public bool CheckConnect(int timeout)
        {
            // FIXME : loop to call tunnel.CheckTls()
            // at regular intervals.
            SelectSockets select = new SelectSockets();
            select.Timeout = timeout;
            select.AddRead(tunnel.Sock);
            tunnel.CheckTls();
            select.Select();
            if (select.ReadSockets.Contains(tunnel.Sock))
            {
                CreateTransport();
                return true;
            }
            return false;
        }        

        /// <summary>
        /// Close the connection to ktlstunnel.
        /// </summary>
        public void Disconnect()
        {
            tunnel.Disconnect();
            transport = null;
        }

        /// <summary>
        /// Kill ktlstunnel.
        /// </summary>
        public void Terminate()
        {
            tunnel.Terminate();
            transport = null;
        }

        /// <summary>
        /// Create an AnpTransport when the tunnel is connected.
        /// </summary>
        public void CreateTransport()
        {
            transport = new AnpTransport(tunnel.EndTls());
            transport.beginRecv();
        }

        /// <summary>
        /// Send an AnpMsg. This method blocks if a message is currently being
        /// transferred.
        /// </summary>
        /// <param name="msg"></param>
        public void SendMsg(AnpMsg msg)
        {
            Debug.Assert(transport.isReceiving || transport.doneReceiving);
            while (transport.isSending)
            {
                SelectSockets select = new SelectSockets();
                transport.doXfer();
                UpdateSelect(select);
                if (transport.isSending)
                    select.Select();
            }
            transport.sendMsg(msg);
            transport.doXfer();
        }

        /// <summary>
        /// Get an AnpMsg. This is a blocking method, unless a message has already
        /// been received.
        /// </summary>
        public AnpMsg GetMsg()
        {
            Debug.Assert(transport.isReceiving || transport.doneReceiving);
            while (!transport.doneReceiving)
            {
                SelectSockets select = new SelectSockets();
                transport.doXfer();
                UpdateSelect(select);
                if (!transport.doneReceiving)
                    select.Select();
            }
            AnpMsg msg = transport.getRecv();
            transport.beginRecv();
            return msg;
        }

        /// <summary>
        /// Exchange messages with the server and return true if a message has
        /// been received.
        /// </summary>
        public bool CheckReceive()
        {
            Debug.Assert(transport != null, "transfert can't be null");
            Debug.Assert(transport.isReceiving || transport.doneReceiving);
            if (transport.isSending || transport.isReceiving)
                transport.doXfer();
            return transport.doneReceiving;
        }

        /// <summary>
        /// Exchange messages with the server and return true if a message is
        /// being sent.
        /// </summary>
        public bool CheckSend()
        {
            Debug.Assert(transport.isReceiving || transport.doneReceiving);
            if (transport.isSending || transport.isReceiving)
                transport.doXfer();
            return transport.isSending;
        }

        /// <summary>
        /// Return true if a message has been received.
        /// </summary>
        public bool HasReceivedMessage()
        {
            return transport.doneReceiving;
        }

        /// <summary>
        /// Return true if a message is being sent.
        /// </summary>
        public bool IsSendingMessage()
        {
            return transport.isSending;
        }

        /// <summary>
        /// Execute socket read and write operations.
        /// </summary>
        public void DoXfer()
        {
            transport.doXfer();
        }

        /// <summary>
        /// Update the select set specified with the socket of the tunnel.
        /// </summary>
        public void UpdateSelect(SelectSockets select)
        {
            Debug.Assert(transport.isReceiving || transport.doneReceiving);
            if (transport.isSending)
                select.AddWrite(tunnel.Sock);
            if (transport.isReceiving && !transport.doneReceiving)
                select.AddRead(tunnel.Sock);
        }
    }

    /// <summary>
    /// Wrapper around ktlstunnel.
    /// </summary>
    public class Tunnel
    {
        public string Host;
        public int Port;

        public Socket Sock;

        public Tunnel(string host, int port)
        {
            Host = host;
            Port = port;
        }

        /// <summary>
        /// ktlstunnel.exe process.
        /// </summary>
        private RawProcess TunnelProcess;

        public void BeginTls() { BeginTls(""); }

        /// <summary>
        /// Create a listening socket and spawn ktlstunnel process.
        /// </summary>
        public void BeginTls(string extraParams)
        {

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 0);
            Sock = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Sock.Bind(endPoint);
            Sock.Listen(1);

            /* Create a logging dir for ktlstunnel, if it does not exist */
            if (!Directory.Exists(Misc.GetKtlstunnelLogFilePath()))
            {
                Directory.CreateDirectory(Misc.GetKtlstunnelLogFilePath());
            }

            // Start ktlstunnel as such.
            // ktlstunnel localhost ((IPEndPoint)Listener.LocalEndPoint).Port Host Port [-r host:port]
            String loggingPath = "-L " + "\"" + Misc.GetKtlstunnelLogFilePath() + "ktlstunnel-" + Base.GetLogFileName() + "\" ";
            String loggingLevel = "";

#if false
            if(Misc.ApplicationSettings.ktlstunnelLoggingLevel == 1)
            {
                loggingLevel = "-l minimal ";
                loggingLevel += loggingPath;
            }
            else if (Misc.ApplicationSettings.ktlstunnelLoggingLevel == 2)
            {
                loggingLevel = "-l debug ";
                loggingLevel += loggingPath;
            }
#endif
            loggingLevel = "-l minimal ";
            loggingLevel += loggingPath;

            string startupLine = "\"" + Base.GetKwmInstallationPath() +  @"ktlstunnel\ktlstunnel.exe" + "\" " + 
                                 loggingLevel + 
                                 "localhost " + ((IPEndPoint)Sock.LocalEndPoint).Port.ToString() + " " +
                                 Host + " " + Port + " " + extraParams;

            Logging.Log("Starting ktlstunnel.exe : " + startupLine);

            TunnelProcess = new RawProcess(startupLine);
            TunnelProcess.InheritHandles = false;
            TunnelProcess.CreationFlags = (uint)Syscalls.CREATION_FLAGS.CREATE_NO_WINDOW;
            TunnelProcess.Start();
        }

        /// <summary>
        /// Accept the connection received from ktlstunnel.
        /// </summary>
        /// <returns></returns>
        public Socket EndTls()
        {
            Socket Listener = Sock;
            Sock = Listener.Accept();
            Listener.Close();
            Sock.Blocking = false;
            return Sock;
        }

        /// <summary>
        /// Check if ktlstunnel process is still running.
        /// </summary>
        public void CheckTls()
        {
            if (!TunnelProcess.IsRunning)
                throw new AnpException("Cannot establish connection");
        }

        /// <summary>
        /// Close the socket if it is opened.
        /// </summary>
        public void Disconnect()
        {
            if (Sock != null)
            {
                try
                {
                    Sock.Close();
                }
                catch (Exception)
                {}
                Sock = null;
            }
        }

        /// <summary>
        /// Disconnect and close ktlstunnel.
        /// </summary>
        public void Terminate()
        {
            Disconnect();

            if (TunnelProcess != null)
            {
                TunnelProcess.Terminate();
                TunnelProcess = null;
            }
        }
    }

    /// <summary>
    /// Represent a worker thread that runs a tunnel.
    /// </summary>
    public abstract class KwmTunnelThread : KwmWorkerThread
    {
        /// <summary>
        /// Workspace helper.
        /// </summary>
        protected IAppHelper Helper;

        /// <summary>
        /// Host to connect to. This is "" for the default host.
        /// </summary>
        protected String Host;

        /// <summary>
        /// Port to connect to. This is 0 for the default port.
        /// </summary>
        protected int Port;

        /// <summary>
        /// Internal ANP tunnel.
        /// </summary>
        private IAnpTunnel InternalAnpTunnel;

        public KwmTunnelThread(IAppHelper helper, String host, int port)
        {
            Helper = helper;
            Host = host;
            Port = port;
        }

        /// <summary>
        /// This method is called once the tunnel has been connected.
        /// </summary>
        protected abstract void OnTunnelConnected();

        /// <summary>
        /// Retrieve a message from the tunnel.
        /// </summary>
        protected AnpMsg GetAnpMsg()
        {
            AnpTransport transfer = InternalAnpTunnel.GetTransport();
            Debug.Assert(transfer.isReceiving || transfer.doneReceiving);
            Debug.Assert(!transfer.isSending);

            while (!transfer.doneReceiving)
            {
                transfer.doXfer();

                if (!transfer.doneReceiving)
                {
                    SelectSockets set = new SelectSockets();
                    InternalAnpTunnel.UpdateSelect(set);
                    Block(set);
                }
            }

            AnpMsg msg = transfer.getRecv();
            transfer.beginRecv();
            return msg;
        }

        /// <summary>
        /// Return true if an ANP message has been received.
        /// </summary>
        protected bool AnpMsgReceived()
        {
            return InternalAnpTunnel.CheckReceive();
        }

        /// <summary>
        /// Send a message on the tunnel.
        /// </summary>
        protected void SendAnpMsg(AnpMsg m)
        {
            AnpTransport transfer = InternalAnpTunnel.GetTransport();
            Debug.Assert(transfer.isReceiving || transfer.doneReceiving);
            Debug.Assert(!transfer.isSending);
            transfer.sendMsg(m);

            while (transfer.isSending)
            {
                transfer.doXfer();

                if (transfer.isSending)
                {
                    SelectSockets set = new SelectSockets();
                    InternalAnpTunnel.UpdateSelect(set);
                    Block(set);
                }
            }
        }

        protected override void Run()
        {
            // Connect the tunnel.
            InternalAnpTunnel = Helper.CreateTunnel();
            Tunnel tunnel = InternalAnpTunnel.GetTunnel();
            InternalAnpTunnel.BeginConnect(Host, Port);

            while (true)
            {
                SelectSockets set = new SelectSockets();
                set.Timeout = 100;
                tunnel.CheckTls();
                set.AddRead(tunnel.Sock);
                Block(set);
                if (set.ReadSockets.Contains(tunnel.Sock))
                {
                    InternalAnpTunnel.CreateTransport();
                    break;
                }
            }

            // Handle the tunnel.
            OnTunnelConnected();
        }

        protected override void OnCompletion()
        {
            if (InternalAnpTunnel != null)
                InternalAnpTunnel.Disconnect();
        }
    }
}