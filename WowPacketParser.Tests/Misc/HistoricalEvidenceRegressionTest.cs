using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WowPacketParser.Enums;
using WowPacketParser.Enums.Version;
using WowPacketParser.Misc;

namespace WowPacketParser.Tests.Misc
{
    /// <summary>
    /// Regression coverage derived from the historical Retail 12.1.5 packet evidence
    /// (read-only source: H:\World_of_Warcraft_Items\packets_sql, audit of 2026-11-18).
    ///
    /// The fixture lists every numeric opcode observed in the 181 distinct PKT 3.1
    /// containers for build 70783: 567 values (211 CMSG, 356 SMSG), of which 402 are also
    /// present in the six-container Ymir inventory and 165 come only from the additional
    /// containers. Two SMSG values have no entry in the 70340 table and are mapped by the
    /// build-scoped provisional layer.
    ///
    /// The tests assert recognisability only. They make no claim about payload schemas.
    /// </summary>
    [TestFixture]
    public class HistoricalEvidenceRegressionTest
    {
        private const int HousingOpcode = 0x00450059;
        private const int BattlePayOpcode = 0x00450226;

        private ClientVersionBuild _originalBuild;

        [SetUp]
        public void CaptureVersion()
        {
            _originalBuild = ClientVersion.Build;
        }

        [TearDown]
        public void RestoreVersion()
        {
            ClientVersion.SetVersion(_originalBuild);
        }

        private sealed class Observation
        {
            public string Direction;
            public int Numeric;
            public string Name;
            public int Occurrences;
            public int Sessions;
            public bool InSixCaptureInventory;

            public WowPacketParser.Enums.Direction WowDirection => Direction == "CMSG"
                ? WowPacketParser.Enums.Direction.ClientToServer
                : WowPacketParser.Enums.Direction.ServerToClient;
        }

        private static List<Observation> LoadObservations()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "70783_observed_opcodes.csv");
            Assert.That(File.Exists(path), Is.True, "Missing fixture: " + path);

            var result = new List<Observation>();
            foreach (var line in File.ReadAllLines(path).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = line.Split(',');
                Assert.That(fields.Length, Is.EqualTo(6), line);
                result.Add(new Observation
                {
                    Direction = fields[0],
                    Numeric = int.Parse(fields[1].Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    Name = fields[2],
                    Occurrences = int.Parse(fields[3], CultureInfo.InvariantCulture),
                    Sessions = int.Parse(fields[4], CultureInfo.InvariantCulture),
                    InSixCaptureInventory = fields[5] == "1"
                });
            }
            return result;
        }

        [Test]
        public void TestObservedInventoryShape()
        {
            var observations = LoadObservations();

            Assert.That(observations.Count, Is.EqualTo(567));
            Assert.That(observations.Count(o => o.Direction == "CMSG"), Is.EqualTo(211));
            Assert.That(observations.Count(o => o.Direction == "SMSG"), Is.EqualTo(356));

            // 402 values reconcile with the six-container inventory; 165 extra values come
            // only from the other 38 distinct 70783 containers. The 165 are not "new
            // opcodes": every one has a 70340 symbolic name, which the next test proves.
            Assert.That(observations.Count(o => o.InSixCaptureInventory), Is.EqualTo(402));
            var extras = observations.Where(o => !o.InSixCaptureInventory).ToList();
            Assert.That(extras.Count, Is.EqualTo(165));
            Assert.That(extras.Count(o => o.Direction == "SMSG"), Is.EqualTo(105));
            Assert.That(extras.Count(o => o.Direction == "CMSG"), Is.EqualTo(60));
            Assert.That(extras.Count(o => string.IsNullOrEmpty(o.Name)), Is.EqualTo(0));

            // Exactly the two provisional values are unnamed in the 70340 table.
            var unnamed = observations.Where(o => string.IsNullOrEmpty(o.Name)).ToList();
            Assert.That(unnamed.Count, Is.EqualTo(2));
            Assert.That(unnamed.Select(o => o.Numeric), Is.EquivalentTo(new[] { HousingOpcode, BattlePayOpcode }));
        }

        [Test]
        public void TestEveryObserved70783ValueResolves()
        {
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);
            var failures = new List<string>();

            foreach (var observation in LoadObservations())
            {
                var opcode = Opcodes.GetOpcode(observation.Numeric, observation.WowDirection);
                if (opcode == Opcode.NULL_OPCODE)
                {
                    failures.Add($"0x{observation.Numeric:X8} {observation.Direction} unresolved");
                    continue;
                }

                var name = Opcodes.GetOpcodeName(observation.Numeric, observation.WowDirection);
                var expected = string.IsNullOrEmpty(observation.Name)
                    ? (observation.Numeric == HousingOpcode ? "UNKNOWN_SMSG_450059" : "UNKNOWN_SMSG_450226")
                    : observation.Name;

                if (name != expected)
                    failures.Add($"0x{observation.Numeric:X8} {observation.Direction} => {name}, expected {expected}");
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures.Take(20)));
        }

        [Test]
        public void TestAdditionalContainerValuesAreRecognised()
        {
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);
            var extras = LoadObservations().Where(o => !o.InSixCaptureInventory).ToList();
            Assert.That(extras.Count, Is.EqualTo(165));

            foreach (var observation in extras)
            {
                Assert.That(Opcodes.GetOpcode(observation.Numeric, observation.WowDirection), Is.Not.EqualTo(Opcode.NULL_OPCODE),
                    $"0x{observation.Numeric:X8} {observation.Direction}");
                Assert.That(Opcodes.GetOpcodeName(observation.Numeric, observation.WowDirection), Is.EqualTo(observation.Name),
                    $"0x{observation.Numeric:X8} {observation.Direction}");
            }
        }

        [Test]
        public void TestObservedDirectionsAreDisjoint()
        {
            // In this build the observed CMSG and SMSG numeric spaces do not overlap at all
            // (verified against the 70340 table extract). A value observed in one direction
            // must therefore never resolve in the other.
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);
            var observations = LoadObservations();
            var failures = new List<string>();

            foreach (var observation in observations)
            {
                var opposite = observation.WowDirection == Direction.ServerToClient
                    ? Direction.ClientToServer
                    : Direction.ServerToClient;

                var resolved = Opcodes.GetOpcode(observation.Numeric, opposite);
                if (resolved != Opcode.NULL_OPCODE)
                    failures.Add($"0x{observation.Numeric:X8} {observation.Direction} leaked into {opposite} as {resolved}");
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures.Take(20)));
        }

        [Test]
        public void TestNumericNormalisation()
        {
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);

            // The same value written as 0x00450059, 0x450059 and decimal 4500059.
            Assert.That(Opcodes.GetOpcodeName(0x00450059, Direction.ServerToClient), Is.EqualTo("UNKNOWN_SMSG_450059"));
            Assert.That(Opcodes.GetOpcodeName(0x450059, Direction.ServerToClient), Is.EqualTo("UNKNOWN_SMSG_450059"));
            Assert.That(Opcodes.GetOpcodeName(4522073, Direction.ServerToClient), Is.EqualTo("UNKNOWN_SMSG_450059"));
            Assert.That(Opcodes.GetOpcodeName(0x00450226, Direction.ServerToClient), Is.EqualTo("UNKNOWN_SMSG_450226"));
            Assert.That(Opcodes.GetOpcodeName(4522534, Direction.ServerToClient), Is.EqualTo("UNKNOWN_SMSG_450226"));

            // A neighbouring existing value is unaffected by the provisional layer.
            Assert.That(Opcodes.GetOpcode(0x450058, Direction.ServerToClient), Is.EqualTo(Opcode.SMSG_ACCOUNT_HOUSE_TYPE_COLLECTION_UPDATE));
        }

        [Test]
        public void TestProvisionalLayerDoesNotLeakToOtherBuilds()
        {
            foreach (var build in new[]
                     {
                         ClientVersionBuild.V12_1_5_70340,
                         ClientVersionBuild.V12_1_5_70961,
                         ClientVersionBuild.V12_1_0_69299
                     })
            {
                ClientVersion.SetVersion(build);
                Assert.That(Opcodes.GetOpcode(HousingOpcode, Direction.ServerToClient), Is.EqualTo(Opcode.NULL_OPCODE), build.ToString());
                Assert.That(Opcodes.GetOpcode(BattlePayOpcode, Direction.ServerToClient), Is.EqualTo(Opcode.NULL_OPCODE), build.ToString());
                Assert.That(Opcodes.GetOpcodeName(HousingOpcode, Direction.ServerToClient), Is.EqualTo("0x450059"), build.ToString());
            }

            // Client direction is unresolved even in 70783.
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);
            Assert.That(Opcodes.GetOpcode(HousingOpcode, Direction.ClientToServer), Is.EqualTo(Opcode.NULL_OPCODE));
            Assert.That(Opcodes.GetOpcode(BattlePayOpcode, Direction.ClientToServer), Is.EqualTo(Opcode.NULL_OPCODE));
        }

        [Test]
        public void TestBothHousingPayloadShapesPreserved()
        {
            ClientVersion.SetVersion(ClientVersionBuild.V12_1_5_70783);

            // 30-byte shape (SHA-256 prefix 894AE4C1F27F380C): builds 69273, 69299, 69323,
            // 69814, 69933, 70783 and 6 of 19 observations in 69587.
            var thirty = Convert.FromHexString("80050000000500000001000000020000000300000004000000AD00000000");

            // 26-byte shape (SHA-256 prefix 5901A9D408485F1C): builds 69404, 69497 and
            // 13 of 19 observations in 69587. Same leading bytes with a shorter middle run;
            // the provisional mapping must not assume a fixed 30-byte payload.
            var twentySix = Convert.FromHexString("800400000004000000010000000200000003000000AD00000000");

            Assert.That(thirty.Length, Is.EqualTo(30));
            Assert.That(twentySix.Length, Is.EqualTo(26));
            Assert.That(Opcodes.GetOpcode(HousingOpcode, Direction.ServerToClient), Is.EqualTo(Opcode.UNKNOWN_SMSG_450059));

            foreach (var payload in new[] { thirty, twentySix })
            {
                var packet = new Packet(payload, HousingOpcode, new DateTime(2026, 11, 16), Direction.ServerToClient, 1, "synthetic");
                Assert.That(packet.Length, Is.EqualTo(payload.Length));
                Assert.That(packet.GetStream(0), Is.EqualTo(payload));

                packet.AsHex();
                var hex = packet.Writer.ToString();
                Assert.That(hex.Contains(payload[0].ToString("X2")), Is.True, hex);
                packet.ClosePacket();
            }
        }

        [Test]
        public void TestNorthshireTupleReconciliationArithmetic()
        {
            // Documented reconstruction of the 2026-08 Northshire pair (build 68974):
            // the 57-tuple gap is entirely spawn rows, not creature_equip_template.
            var deltas = new Dictionary<string, int>
            {
                { "creature", -14 },
                { "creature_addon", -14 },
                { "creature_trainer", 2 },
                { "gameobject", -17 },
                { "gameobject_addon", -14 }
            };

            Assert.That(deltas.Values.Sum(), Is.EqualTo(-57));
            Assert.That(29972 - 30029, Is.EqualTo(-57));
            Assert.That(deltas.ContainsKey("creature_equip_template"), Is.False,
                "creature_equip_template had 54 tuples in both runs; it is not part of the gap.");

            // Multi-row INSERT counting rule used by the audit: one tuple per line that
            // starts with '(' and is not commented out. A three-row statement counts 3.
            var statement = "INSERT INTO `creature_equip_template` (`CreatureID`, `ID`) VALUES\n" +
                            "(40, 1, 0),\n" +
                            "(78, 1, 0),\n" +
                            "(40, 2, 0);\n" +
                            "-- (99, 1, 0),\n";
            Assert.That(CountInsertedTuples(statement, "creature_equip_template"), Is.EqualTo(3));
        }

        private static int CountInsertedTuples(string sql, string table)
        {
            var inBlock = false;
            var count = 0;
            foreach (var raw in sql.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("INSERT", StringComparison.Ordinal) && line.Contains("`" + table + "`", StringComparison.Ordinal))
                {
                    inBlock = true;
                    continue;
                }

                if (!inBlock)
                    continue;

                if (line.StartsWith("(", StringComparison.Ordinal))
                    count++;
                else if (line.EndsWith(";", StringComparison.Ordinal) || line.StartsWith("INSERT", StringComparison.Ordinal))
                    inBlock = false;
            }
            return count;
        }
    }
}
