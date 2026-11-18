using System;
using NUnit.Framework;
using WowPacketParser.Enums;
using WowPacketParser.Enums.Version;
using WowPacketParser.Misc;

namespace WowPacketParser.Tests.Misc
{
    [TestFixture]
    public class ProvisionalOpcodeTest
    {
        private ClientVersionBuild _originalBuild;

        [SetUp]
        public void CaptureVersion()
        {
            _originalBuild = ClientVersion.Build;
        }

        [TearDown]
        public void RestoreVersion()
        {
            // ClientVersion.Build is process-global; leave it as the suite found it.
            ClientVersion.SetVersion(_originalBuild);
        }

        private static readonly byte[] HousingPayload =
        {
            0x80, 0x05, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x00,
            0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xAD, 0x00, 0x00, 0x00, 0x00
        };

        [Test]
        public void TestProvisional70783ServerMappings()
        {
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);

            Assert.That(Opcodes.GetOpcode(0x00450059, Direction.ServerToClient), Is.EqualTo(Opcode.UNKNOWN_SMSG_450059));
            Assert.That(Opcodes.GetOpcode(0x00450226, Direction.ServerToClient), Is.EqualTo(Opcode.UNKNOWN_SMSG_450226));

            Assert.That(Opcodes.GetOpcode(Opcode.UNKNOWN_SMSG_450059, Direction.ServerToClient), Is.EqualTo(0x00450059));
            Assert.That(Opcodes.GetOpcode(Opcode.UNKNOWN_SMSG_450226, Direction.ServerToClient), Is.EqualTo(0x00450226));

            Assert.That(Opcodes.GetOpcodeName(0x00450059, Direction.ServerToClient), Is.EqualTo("UNKNOWN_SMSG_450059"));
            Assert.That(Opcodes.GetOpcodeName(0x00450226, Direction.ServerToClient), Is.EqualTo("UNKNOWN_SMSG_450226"));

            // Client direction must stay unresolved: these are observed SMSG-only.
            Assert.That(Opcodes.GetOpcode(0x00450059, Direction.ClientToServer), Is.EqualTo(Opcode.NULL_OPCODE));
        }

        [Test]
        public void TestProvisionalMappingsAreBuildScoped()
        {
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70961);
            Assert.That(Opcodes.GetOpcode(0x00450059, Direction.ServerToClient), Is.EqualTo(Opcode.NULL_OPCODE));
            Assert.That(Opcodes.GetOpcodeName(0x00450059, Direction.ServerToClient), Is.EqualTo("0x450059"));

            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70340);
            Assert.That(Opcodes.GetOpcode(0x00450226, Direction.ServerToClient), Is.EqualTo(Opcode.NULL_OPCODE));

            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);
            Assert.That(Opcodes.GetOpcode(0x00450226, Direction.ServerToClient), Is.EqualTo(Opcode.UNKNOWN_SMSG_450226));
        }

        [Test]
        public void Test70961RecognitionAndRouting()
        {
            ClientVersion.SetVersion(new DateTime(2026, 11, 18));
            Assert.That(ClientVersion.Build, Is.EqualTo(ClientVersionBuild.V12_1_5_70961));

            ClientVersion.SetVersion(new DateTime(2026, 11, 16));
            Assert.That(ClientVersion.Build, Is.EqualTo(ClientVersionBuild.V12_1_5_70783));

            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70961);
            Assert.That(ClientVersion.VersionDefiningBuild, Is.EqualTo(ClientVersionBuild.V12_0_0_65390));
            Assert.That(Opcodes.GetOpcodeDefiningBuild(ClientVersionBuild.V12_1_5_70961), Is.EqualTo(ClientVersionBuild.V12_1_5_70340));
            Assert.That(ClientVersion.Expansion, Is.EqualTo(ClientType.Midnight));
            Assert.That(ClientVersion.Branch, Is.EqualTo(ClientBranch.Retail));
        }

        [Test]
        public void TestExisting70783MappingsIntact()
        {
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);

            Assert.That(Opcodes.GetOpcode(Opcode.SMSG_ACCOUNT_HOUSE_TYPE_COLLECTION_UPDATE, Direction.ServerToClient), Is.EqualTo(0x450058));
            Assert.That(Opcodes.GetOpcode(Opcode.SMSG_BATTLE_PAY_GET_PURCHASE_LIST_RESPONSE, Direction.ServerToClient), Is.EqualTo(0x45021D));
            Assert.That(Opcodes.GetOpcode(Opcode.SMSG_BATTLE_PAY_COLLECTION_ITEM_DELIVERED, Direction.ServerToClient), Is.EqualTo(0x450225));
            Assert.That(Opcodes.GetOpcode(0x450058, Direction.ServerToClient), Is.EqualTo(Opcode.SMSG_ACCOUNT_HOUSE_TYPE_COLLECTION_UPDATE));
            Assert.That(Opcodes.GetOpcode(0x450226, Direction.ServerToClient), Is.EqualTo(Opcode.UNKNOWN_SMSG_450226));
            // 0x450227 is an existing table entry (SMSG_INSTANCE_SAVE_CREATED); the provisional
            // value must not have displaced or aliased its neighbours.
            Assert.That(Opcodes.GetOpcode(0x450227, Direction.ServerToClient), Is.EqualTo(Opcode.SMSG_INSTANCE_SAVE_CREATED));
        }

        [Test]
        public void TestPayloadBytesPreserved()
        {
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);
            Assert.That(Opcodes.GetOpcode(0x00450059, Direction.ServerToClient), Is.EqualTo(Opcode.UNKNOWN_SMSG_450059));

            var packet = new Packet(HousingPayload, 0x00450059, new DateTime(2026, 11, 16), Direction.ServerToClient, 1, "synthetic");

            Assert.That(packet.Length, Is.EqualTo(30));
            Assert.That(packet.GetStream(0), Is.EqualTo(HousingPayload));

            packet.AsHex();
            var hex = packet.Writer.ToString();
            var row1 = "| 80 05 00 00 00 05 00 00 00 01 00 00 00 02 00 00 |";
            var row2 = "| 00 03 00 00 00 04 00 00 00 AD 00 00 00 00";
            Assert.That(hex.Contains(row1) && hex.Contains(row2), Is.True, hex);
            packet.ClosePacket();
        }
    }
}
