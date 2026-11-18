using System;
using NUnit.Framework;
using WowPacketParser.Enums;
using WowPacketParser.Misc;

namespace WowPacketParser.Tests.Misc
{
    /// <summary>
    /// Dispatch coverage for the unified (all-in-one) Elysium WowPacketParser.
    ///
    /// The unified parser keeps the upstream selection model: the sniff header's build number
    /// alone decides which parser module and which opcode/update-field table are loaded. These
    /// tests pin the module each supported client family resolves to, so an integration that
    /// silently remaps a family fails here instead of during parsing.
    ///
    /// They assert dispatch only; no capture is opened and no payload is interpreted.
    /// </summary>
    [TestFixture]
    public class UnifiedClientDispatchTest
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
            // ClientVersion is process-global; leave it as the suite found it.
            ClientVersion.SetVersion(_originalBuild);
        }

        [Test]
        public void TestRetailFamilyDispatch()
        {
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V12_1_5_70783),
                Is.EqualTo(ClientVersionBuild.V12_0_0_65390));
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V12_1_5_70961),
                Is.EqualTo(ClientVersionBuild.V12_0_0_65390));

            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70961);
            Assert.That(ClientVersion.VersionDefiningBuild, Is.EqualTo(ClientVersionBuild.V12_0_0_65390));
            Assert.That(ClientVersion.Expansion, Is.EqualTo(ClientType.Midnight));
            Assert.That(ClientVersion.Branch, Is.EqualTo(ClientBranch.Retail));
        }

        [Test]
        public void TestPtrBuildRegistrationsUseTheRetailModule()
        {
            // PTR and Retail are the same client family in this source: the PTR builds below
            // share the 12.x parser module and the 70340 opcode/update-field tables, so a PTR
            // sniff needs no separate module and no manual client selection.
            foreach (var build in new[]
                     {
                         ClientVersionBuild.V12_1_5_70575,
                         ClientVersionBuild.V12_1_5_70688,
                         ClientVersionBuild.V12_1_5_70783,
                         ClientVersionBuild.V12_1_5_70961
                     })
            {
                Assert.That(ClientVersion.GetVersionDefiningBuild(build), Is.EqualTo(ClientVersionBuild.V12_0_0_65390), build.ToString());
            }
        }

        [Test]
        public void TestClassicEraDispatch()
        {
            // 1.13.2-1.15.7 resolve to the Classic Era module, 1.15.8+ to the newer table the
            // upstream source points them at. Both mappings are inherited unchanged.
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V1_13_2_31446),
                Is.EqualTo(ClientVersionBuild.V1_13_2_31446));
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V1_15_7_63696),
                Is.EqualTo(ClientVersionBuild.V4_4_0_54481));
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V1_15_8_63829),
                Is.EqualTo(ClientVersionBuild.V5_5_0_61735));

            ClientVersion.SetVersion(ClientVersionBuild.V1_13_2_31446);
            Assert.That(ClientVersion.Expansion, Is.EqualTo(ClientType.Classic));
            Assert.That(ClientVersion.Branch, Is.EqualTo(ClientBranch.Classic));
        }

        [Test]
        public void TestBurningCrusade70577Registration()
        {
            // 2.5.6.70577 is the last BCC build in this source and must stay registered: the
            // Forever fork removes this case, which is why the fork is not the unified baseline.
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V2_5_1_38707),
                Is.EqualTo(ClientVersionBuild.V2_5_1_38707));
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V2_5_6_70577),
                Is.EqualTo(ClientVersionBuild.V5_5_0_61735));

            ClientVersion.SetVersion(ClientVersionBuild.V2_5_6_70577);
            Assert.That(ClientVersion.Expansion, Is.EqualTo(ClientType.BurningCrusadeClassic));
            Assert.That(ClientVersion.Branch, Is.EqualTo(ClientBranch.TBC));
        }

        [Test]
        public void TestMoPClassicDispatch()
        {
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V5_5_0_61735),
                Is.EqualTo(ClientVersionBuild.V5_5_0_61735));
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V5_5_3_67509),
                Is.EqualTo(ClientVersionBuild.V5_5_0_61735));

            ClientVersion.SetVersion(ClientVersionBuild.V5_5_3_67509);
            Assert.That(ClientVersion.Branch, Is.EqualTo(ClientBranch.MoP));

            // ClientVersion.Expansion is derived from build-number thresholds upstream, and MoP
            // Classic build numbers (5.5.x) are larger than several Retail families' numbers, so
            // Expansion reports a later Retail expansion for them (5.5.0.61735 > 11.0.0.55666).
            // Branch - what module and table selection actually use - stays correct. This is
            // pre-existing upstream behaviour and is deliberately not changed here; see the
            // integration report's known limitations.
            ClientVersion.SetVersion(ClientVersionBuild.V5_5_0_61735);
            Assert.That(ClientVersion.Branch, Is.EqualTo(ClientBranch.MoP));
            Assert.That(ClientVersion.Expansion, Is.EqualTo(ClientType.TheWarWithin));
        }

        [Test]
        public void TestMoPClassicKnownGap65703()
        {
            // Documented upstream gap, deliberately not patched: 5.5.3.65703 is declared in
            // ClientVersionBuild and has opcode/update-field cases, but GetVersionDefiningBuild
            // has no case for it, so a 65703 sniff cannot select a module. Inventing a mapping
            // here would be an unverified parser change.
            Assert.That(Enum.IsDefined(typeof(ClientVersionBuild), 65703), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V5_5_3_65703));
        }

        [Test]
        public void TestForeverFamilyIsNotClaimed()
        {
            // Forever is served by the separate Devorvh build. The unified tree keeps only the
            // upstream 1.60.1 beta registration, so no Forever wire layout is claimed here.
            Assert.That(ClientVersion.GetVersionDefiningBuild(ClientVersionBuild.V1_60_1_69876),
                Is.EqualTo(ClientVersionBuild.V1_60_1_69876));

            Assert.That(Enum.IsDefined(typeof(ClientVersionBuild), 70888), Is.False,
                "The Forever module's version-defining build must not be registered in the unified parser.");
            Assert.That(Enum.IsDefined(typeof(ClientVersionBuild), 70338), Is.False,
                "The Forever fork's 70338 registration must not be present in the unified parser.");
        }
    }
}
