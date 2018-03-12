﻿using OpenNos.Core.Networking.Communication.Scs.Communication.Channels;
using OpenNos.Core.Networking.Communication.Scs.Communication.Protocols;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace OpenNos.Core.Networking.Communication.Scs.Server
{
    /// <summary>
    /// This class provides base functionality for server Classs.
    /// </summary>
    public abstract class ScsServerBase : IScsServer
    {
        #region Members

        /// <summary>
        /// This object is used to listen incoming connections.
        /// </summary>
        private IConnectionListener connectionListener;

        #endregion

        #region Instantiation

        /// <summary>
        /// Constructor.
        /// </summary>
        protected ScsServerBase()
        {
            Clients = new ConcurrentDictionary<long, IScsServerClient>();
            WireProtocolFactory = WireProtocolManager.GetDefaultWireProtocolFactory();
        }

        #endregion

        #region Events

        /// <summary>
        /// This event is raised when a new client is connected.
        /// </summary>
        public event EventHandler<ServerClientEventArgs> ClientConnected;

        /// <summary>
        /// This event is raised when a client disconnected from the server.
        /// </summary>
        public event EventHandler<ServerClientEventArgs> ClientDisconnected;

        #endregion

        #region Properties

        /// <summary>
        /// A collection of clients that are connected to the server.
        /// </summary>
        public ConcurrentDictionary<long, IScsServerClient> Clients { get; private set; }

        /// <summary>
        /// Gets/sets wire protocol that is used while reading and writing messages.
        /// </summary>
        public IScsWireProtocolFactory WireProtocolFactory { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Starts the server.
        /// </summary>
        public virtual void Start()
        {
            connectionListener = CreateConnectionListener();
            connectionListener.CommunicationChannelConnected += ConnectionListener_CommunicationChannelConnected;
            connectionListener.Start();
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public virtual void Stop()
        {
            if (connectionListener != null)
            {
                connectionListener.Stop();
            }

            foreach (IScsServerClient client in Clients.Select(s => s.Value))
                client.Disconnect();
        }

        /// <summary>
        /// This method is implemented by derived Classs to create appropriate connection listener to
        /// listen incoming connection requets.
        /// </summary>
        /// <returns></returns>
        protected abstract IConnectionListener CreateConnectionListener();

        /// <summary>
        /// Raises ClientConnected event.
        /// </summary>
        /// <param name="client">Connected client</param>
        protected virtual void OnClientConnected(IScsServerClient client)
        {
            ClientConnected?.Invoke(this, new ServerClientEventArgs(client));
        }

        /// <summary>
        /// Raises ClientDisconnected event.
        /// </summary>
        /// <param name="client">Disconnected client</param>
        protected virtual void OnClientDisconnected(IScsServerClient client)
        {
            ClientDisconnected?.Invoke(this, new ServerClientEventArgs(client));
        }

        /// <summary>
        /// Handles Disconnected events of all connected clients.
        /// </summary>
        /// <param name="sender">Source of event</param>
        /// <param name="e">Event arguments</param>
        private void Client_Disconnected(object sender, EventArgs e)
        {
            var client = (IScsServerClient)sender;
            Clients.TryRemove(client.ClientId, out IScsServerClient value);
            OnClientDisconnected(client);
        }

        /// <summary>
        /// Handles CommunicationChannelConnected event of _connectionListener object.
        /// </summary>
        /// <param name="sender">Source of event</param>
        /// <param name="e">Event arguments</param>
        private void ConnectionListener_CommunicationChannelConnected(object sender, CommunicationChannelEventArgs e)
        {
            var client = new NetworkClient(e.Channel)
            {
                ClientId = ScsServerManager.GetClientId(),
                WireProtocol = WireProtocolFactory.CreateWireProtocol()
            };

            client.Disconnected += Client_Disconnected;
            Clients[client.ClientId] = client;
            OnClientConnected(client);
            e.Channel.Start();
        }

        #endregion
    }
}