﻿using OpenNos.Domain;
using OpenNos.Core;

namespace OpenNos.GameObject.Packets.CommandPackets
{
    [PacketHeader("$Act6Percent", PassNonParseablePacket = true, Authority = AuthorityType.GameMaster)]
    public class Act6RaidPacket : PacketDefinition
    {
        #region Properties

        [PacketIndex(0)]
        public string Name { get; set; }

        [PacketIndex(1)]
        public byte? Percent { get; set; }

        public override string ToString()
        {
            return "Act6Percent Name [Percent]";
        }

        #endregion
    }
}
