using NUnit.Framework;
using System;
using WowPacketParser.Enums;
using WowPacketParser.Enums.Version;
using WowPacketParser.Misc;

namespace WowPacketParser.Tests.Misc
{
    [TestFixture]
    public class ClientVersionTest
    {
        [SetUp]
        public void Initialize()
        {
            ClientVersion.SetVersion(ClientVersionBuild.V3_3_3_11685);
        }

        [Test]
        public void TestAddedInVersion()
        {
            Assert.That(ClientVersion.AddedInVersion(ClientType.WrathOfTheLichKing), Is.True);
            Assert.That(ClientVersion.AddedInVersion(ClientType.Cataclysm), Is.False);

            Assert.That(ClientVersion.AddedInVersion(ClientVersionBuild.V2_4_3_8606), Is.True);
            Assert.That(ClientVersion.AddedInVersion(ClientVersionBuild.V3_3_5a_12340), Is.False);
        }

        [Test]
        public void TestRemovedInVersion()
        {
            Assert.That(ClientVersion.RemovedInVersion(ClientType.WrathOfTheLichKing), Is.False);
            Assert.That(ClientVersion.RemovedInVersion(ClientType.Cataclysm), Is.True);

            Assert.That(ClientVersion.RemovedInVersion(ClientVersionBuild.V2_4_3_8606), Is.False);
            Assert.That(ClientVersion.RemovedInVersion(ClientVersionBuild.V3_3_5a_12340), Is.True);
        }

        [Test]
        public void TestIsUndefined()
        {
            Assert.That(ClientVersion.IsUndefined(), Is.False);

            ClientVersion.SetVersion(ClientVersionBuild.Zero);

            Assert.That(ClientVersion.IsUndefined(), Is.True);
        }

        [Test]
        public void TestBuildGetters()
        {
            Assert.That(ClientVersionBuild.V3_3_3_11685, Is.EqualTo(ClientVersion.Build));
            Assert.That(11685, Is.EqualTo(ClientVersion.BuildInt));
            Assert.That("V3_3_3_11685", Is.EqualTo(ClientVersion.VersionString));
        }

        [Test]
        public void TestSetVersion()
        {
            ClientVersion.SetVersion(new DateTime(1991, 1, 1));
            Assert.That(ClientVersion.IsUndefined(), Is.True);

            ClientVersion.SetVersion(new DateTime(2025, 1, 1));
            Assert.That(ClientVersion.IsUndefined(), Is.False);

            ClientVersion.SetVersion(new DateTime(2010, 6, 29));
            Assert.That(ClientVersionBuild.V3_3_5a_12340, Is.EqualTo(ClientVersion.Build));

            ClientVersion.SetVersion(new DateTime(2015, 7, 9));
            Assert.That(ClientVersionBuild.V6_2_0_20253, Is.EqualTo(ClientVersion.Build));

            ClientVersion.SetVersion(ClientVersionBuild.V1_12_1_5875);
            Assert.That(ClientVersionBuild.V1_12_1_5875, Is.EqualTo(ClientVersion.Build));

            ClientVersion.SetVersion(ClientVersionBuild.V6_2_0_20253);
            Assert.That(ClientVersionBuild.V6_2_0_20253, Is.EqualTo(ClientVersion.Build));
        }

        [Test]
        public void TestBuild70961Registration()
        {
            // 12.1.5.70961 is registered upstream (TrinityCore 62422dd) with the release date
            // 2026-11-18; earlier dates must keep resolving to 70783.
            ClientVersion.SetVersion(new DateTime(2026, 11, 17));
            Assert.That(ClientVersionBuild.V12_1_5_70783, Is.EqualTo(ClientVersion.Build));

            ClientVersion.SetVersion(new DateTime(2026, 11, 18));
            Assert.That(ClientVersionBuild.V12_1_5_70961, Is.EqualTo(ClientVersion.Build));

            // 70961 reuses the same parser module and opcode/update-field tables as 70340/70783.
            Assert.That(ClientVersionBuild.V12_0_0_65390, Is.EqualTo(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V12_1_5_70961)));
            Assert.That(ClientVersionBuild.V12_1_5_70340, Is.EqualTo(Opcodes.GetOpcodeDefiningBuild(ClientVersionBuild.V12_1_5_70961)));

            // The neighbouring build keeps its own, already verified mapping.
            Assert.That(ClientVersionBuild.V12_0_0_65390, Is.EqualTo(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V12_1_5_70783)));
            Assert.That(ClientVersionBuild.V12_1_5_70340, Is.EqualTo(Opcodes.GetOpcodeDefiningBuild(ClientVersionBuild.V12_1_5_70783)));
        }
    }
}
