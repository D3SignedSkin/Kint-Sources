﻿using OpenNos.Core.Networking.Communication.Scs.Communication;
using OpenNos.Core.Networking.Communication.Scs.Communication.Channels;
using OpenNos.Core.Networking.Communication.Scs.Communication.EndPoints;
using OpenNos.Core.Networking.Communication.Scs.Communication.Messages;
using OpenNos.Core.Networking.Communication.Scs.Communication.Protocols;
using System;
using System.Threading.Tasks;

namespace OpenNos.Core.Networking.Communication.Scs.Server
{
    /// <summary>
    /// This class represents a client in server side.
    /// </summary>
    public class ScsServerClient : IScsServerClient
    {
        #region Members

        /// <summary>
        /// The communication channel that is used by client to send and receive messages.
        /// </summary>
        private readonly ICommunicationChannel communicationChannel;

        #endregion

        #region Instantiation

        /// <summary>
        /// Creates a new ScsClient object.
        /// </summary>
        /// <param name="communicationChannel">
        /// The communication channel that is used by client to send and receive messages
        /// </param>
        public ScsServerClient(ICommunicationChannel communicationChannel)
        {
            this.communicationChannel = communicationChannel;
            this.communicationChannel.MessageReceived += CommunicationChannel_MessageReceived;
            this.communicationChannel.MessageSent += CommunicationChannel_MessageSent;
            this.communicationChannel.Disconnected += CommunicationChannel_Disconnected;
        }

        #endregion

        #region Events

        /// <summary>
        /// This event is raised when client is disconnected from server.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// This event is raised when a new message is received.
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageReceived;

        /// <summary>
        /// This event is raised when a new message is sent without any error. It does not guaranties
        /// that message is properly handled and processed by remote application.
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageSent;

        #endregion

        #region Properties

        /// <summary>
        /// Unique identifier for this client in server.
        /// </summary>
        public long ClientId { get; set; }

        /// <summary>
        /// Gets the communication state of the Client.
        /// </summary>
        public CommunicationStates CommunicationState => communicationChannel.CommunicationState;

        /// <summary>
        /// Gets the time of the last succesfully received message.
        /// </summary>
        public DateTime LastReceivedMessageTime => communicationChannel.LastReceivedMessageTime;

        /// <summary>
        /// Gets the time of the last succesfully received message.
        /// </summary>
        public DateTime LastSentMessageTime => communicationChannel.LastSentMessageTime;

        /// <summary>
        /// Gets endpoint of remote application.
        /// </summary>
        public ScsEndPoint RemoteEndPoint => communicationChannel.RemoteEndPoint;

        /// <summary>
        /// Gets/sets wire protocol that is used while reading and writing messages.
        /// </summary>
        public IScsWireProtocol WireProtocol
        {
            get { return communicationChannel.WireProtocol; }
            set { communicationChannel.WireProtocol = value; }
        }

        #endregion

        #region Methods

        public async Task ClearLowPriorityQueue()
        {
            await communicationChannel.ClearLowPriorityQueue();
        }

        /// <summary>
        /// Disconnects from client and closes underlying communication channel.
        /// </summary>
        public void Disconnect() => communicationChannel.Disconnect();

        /// <summary>
        /// Sends a message to the client.
        /// </summary>
        /// <param name="message">Message to be sent</param>
        public void SendMessage(IScsMessage message, byte priority)
        {
            communicationChannel.SendMessage(message, priority);
        }

        /// <summary>
        /// Raises MessageSent event.
        /// </summary>
        /// <param name="message">Received message</param>
        protected virtual void OnMessageSent(IScsMessage message)
        {
            MessageSent?.Invoke(this, new MessageEventArgs(message, DateTime.Now));
        }

        /// <summary>
        /// Handles Disconnected event of _communicationChannel object.
        /// </summary>
        /// <param name="sender">Source of event</param>
        /// <param name="e">Event arguments</param>
        private void CommunicationChannel_Disconnected(object sender, EventArgs e) => OnDisconnected();

        /// <summary>
        /// Handles MessageReceived event of _communicationChannel object.
        /// </summary>
        /// <param name="sender">Source of event</param>
        /// <param name="e">Event arguments</param>
        private void CommunicationChannel_MessageReceived(object sender, MessageEventArgs e)
        {
            var message = e.Message;
            if (message is ScsPingMessage)
            {
                communicationChannel.SendMessage(new ScsPingMessage { RepliedMessageId = message.MessageId }, 10);
                return;
            }

            OnMessageReceived(message);
        }

        /// <summary>
        /// Handles MessageSent event of _communicationChannel object.
        /// </summary>
        /// <param name="sender">Source of event</param>
        /// <param name="e">Event arguments</param>
        private void CommunicationChannel_MessageSent(object sender, MessageEventArgs e)
        {
            OnMessageSent(e.Message);
        }

        /// <summary>
        /// Raises Disconnected event.
        /// </summary>
        private void OnDisconnected() => Disconnected?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Raises MessageReceived event.
        /// </summary>
        /// <param name="message">Received message</param>
        private void OnMessageReceived(IScsMessage message)
        {
            MessageReceived?.Invoke(this, new MessageEventArgs(message, DateTime.Now));
        }

        #endregion
    }
}