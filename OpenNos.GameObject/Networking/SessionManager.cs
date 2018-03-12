﻿using OpenNos.Core;
using OpenNos.Domain;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace OpenNos.GameObject
{
    public class SessionManager
    {
        #region Members

        protected Type _packetHandler;
        protected ConcurrentDictionary<long, ClientSession> _sessions = new ConcurrentDictionary<long, ClientSession>();

        #endregion

        #region Instantiation

        public SessionManager(Type packetHandler, bool isWorldServer)
        {
            _packetHandler = packetHandler;
            IsWorldServer = isWorldServer;
        }

        #endregion

        #region Properties

        public bool IsWorldServer { get; set; }

        #endregion

        #region Methods

        public void AddSession(INetworkClient customClient)
        {
            Logger.Log.Info(Language.Instance.GetMessageFromKey("NEW_CONNECT") + customClient.ClientId);

            var session = IntializeNewSession(customClient);
            customClient.SetClientSession(session);

            if (session != null && IsWorldServer)
            {
                if (!_sessions.TryAdd(customClient.ClientId, session))
                {
                    Logger.Log.WarnFormat(Language.Instance.GetMessageFromKey("FORCED_DISCONNECT"), customClient.ClientId);
                    customClient.Disconnect();
                    _sessions.TryRemove(customClient.ClientId, out session);
                }
            }
        }

        public virtual void StopServer()
        {
            _sessions.Clear();
            ServerManager.Instance.StopServer();
        }

        protected virtual ClientSession IntializeNewSession(INetworkClient client)
        {
            var session = new ClientSession(client);
            client.SetClientSession(session);
            return session;
        }

        protected void RemoveSession(INetworkClient client)
        {
            _sessions.TryRemove(client.ClientId, out ClientSession session);

            // check if session hasnt been already removed
            if (session != null)
            {
                session.IsDisposing = true;

                if (IsWorldServer && session.HasSelectedCharacter)
                {
                    session.Character.Mates.Where(s => s.IsTeamMember).ToList().ForEach(s => session.CurrentMapInstance?.Broadcast(session, s.GenerateOut(), ReceiverType.AllExceptMe));
                    session.CurrentMapInstance?.Broadcast(session, session.Character.GenerateOut(), ReceiverType.AllExceptMe);
                }

                session.Destroy();

                if (IsWorldServer)
                {
                    if (session.HasSelectedCharacter)
                    {
                        if (session.Character.Hp < 1)
                        {
                            session.Character.Hp = 1;
                        }

                        if (ServerManager.Instance.Groups.Any(s => s.IsMemberOfGroup(session.Character.CharacterId)))
                        {
                            ServerManager.Instance.GroupLeave(session);
                        }

                        session.Character.LeaveTalentArena(true);
                        session.Character.Save();
                    }
                }

                client.Disconnect();
                Logger.Log.Info(Language.Instance.GetMessageFromKey("DISCONNECT") + client.ClientId);

                // session = null;
            }
        }

        #endregion
    }
}