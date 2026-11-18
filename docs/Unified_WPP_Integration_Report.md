# Unified Elysium WowPacketParser — Integration Report

Date: 2026-11-18
Workspace: `H:\World_of_Warcraft_Items\wpp`
New project: `Elysium_wpp_master` (seventh WPP project)
Baseline: `Trinitycore_Wpp_Master\WowPacketParser-master` (8,448 files, read-only, not modified)

## 1. Outcome

| Item | Result |
| --- | --- |
| Unified source tree | Created from the TrinityCore master; 8,452 files (8,448 + 4 new test files) |
| Build | `dotnet build WowPacketParser.slnx -c Release` — 0 warnings, 0 errors |
| Tests | 124 total: **118 passed, 6 skipped (pre-existing `[Ignore]`), 0 failed** |
| Publication | `Elysium_wpp_master\Publish` — 108 files, 56.4 MB, **23 parser modules** |
| Client families included | Retail, PTR, Classic Era, Burning Crusade Classic, Mists of Pandaria Classic |
| Forever | **Not integrated — blocked, pending.** See §6 |
| Independent distributions | All six `Publish` trees byte-identical to their pre-phase baselines |

## 2. Phase 1 — Audit of the six client trees

Each tree was compared against `Trinitycore_Wpp_Master\WowPacketParser-master` with
`diff -rq -x bin -x obj -x Publish -x '*.zip' -x '*.md'` (documentation excluded from the diff,
because every client carries its own `docs\`).

| Tree | Base | Source files differing from the TrinityCore master |
| --- | --- | --- |
| `retail_wpp` | TrinityCore | 5 core files + 2 new test files + 1 test fixture + test `.csproj` |
| `ptr_wpp` | TrinityCore | 4 core files + `ClientVersionTest.cs` |
| `Classic_wpp` | TrinityCore | none |
| `BCC_wpp` | TrinityCore | none |
| `MoPc_wpp` | TrinityCore | none |
| `Forever_wpp` | **Devorvh fork** | 22 core files, 3 parser-module files, 1 new module project, 6 new store objects, 3 Forever-only tests/schema files |

### 2.1 Retail delta (the Elysium Retail improvements)

| File | Change |
| --- | --- |
| `WowPacketParser\Enums\ClientVersionBuild.cs` | +1 member `V12_1_5_70961 = 70961` |
| `WowPacketParser\Enums\Opcode.cs` | +2 members `UNKNOWN_SMSG_450059`, `UNKNOWN_SMSG_450226` (marked provisional) |
| `WowPacketParser\Enums\Version\Opcodes.cs` | Build-scoped provisional dictionary; public `GetOpcodeDictionary` became a wrapper over a new `GetBuildOpcodeDictionary`; exact-build name resolution in `GetOpcodeName`; +1 `V12_1_5_70961` case |
| `WowPacketParser\Enums\Version\UpdateFields.cs` | +1 `V12_1_5_70961` case → `V12_1_5_70340` |
| `WowPacketParser\Misc\ClientVersion.cs` | +1 release-date entry (2026-11-18); +1 `V12_1_5_70961` → `V12_0_0_65390` module case |
| `WowPacketParser.Tests\Misc\ProvisionalOpcodeTest.cs` | new — provisional mapping, build scoping, neighbour integrity, raw payload bytes |
| `WowPacketParser.Tests\Misc\HistoricalEvidenceRegressionTest.cs` + `Fixtures\70783_observed_opcodes.csv` | new — 567 observed 70783 opcodes (211 CMSG / 356 SMSG), all resolve, directions disjoint |
| `WowPacketParser.Tests\WowPacketParser.Tests.csproj` | +1 `ItemGroup` copying `Fixtures\**\*` to the test output |

### 2.2 PTR delta

The same 70961 registration. `ClientVersionBuild.cs`, `UpdateFields.cs` and `ClientVersion.cs` are
**byte-identical** to Retail (`cmp` verified). `Version\Opcodes.cs` differs from Retail only by not
containing the 70783 provisional layer — i.e. it is a strict subset of the Retail change. Its one
unique addition is `TestBuild70961Registration` in `ClientVersionTest.cs`.

### 2.3 Classic / BCC / MoP Classic delta

Zero source differences. They are byte-identical copies of the TrinityCore master apart from their
own `docs\` folder. Nothing needed to be brought into the unified tree from them, and nothing was
duplicated into it.

### 2.4 Forever delta (Devorvh fork)

Not a module-only addition: the fork changes core parser, store and SQL behaviour across the tree.

| Area | Forever-fork change | Consequence for a unified tree |
| --- | --- | --- |
| `Misc\ClientVersion.cs` | **Removes** `V2_5_6_70577` from the BCC→`V5_5_0_61735` mapping (2 places), adds `V1_60_1_70338…70938` → `V1_60_1_70888` | Would regress BCC support that this phase must keep |
| `WowPacketParserModule.V12_0_0_65390\Parsers\HotfixHandler.cs` | `ItemSearchName`/`ItemSparse` `AllowableRace` read as `int64` → 2 × `int32` | Changes Retail 12.x hotfix parsing |
| `…\Parsers\UpdateHandler.cs` | +172 lines: Forever values-block type mask, `ReadUpdateEntityFragmentDataForever`, throws on unread bytes for 70938 | Alters the Retail `SMSG_UPDATE_OBJECT` path |
| `…\Parsers\NpcHandler.cs` | Adds gossip `broadcast_text` fallback that writes `npc_text` rows | Changes SQL for all clients when no DB lookup is available |
| `SQL\SQLFile.cs` | Manual `SET @CGUID := SET_VALUE_MANUALLY_HERE` → `(SELECT IFNULL(MAX(guid),0)+1 …)`; append/`KeepSQLFile` behaviour | Changes SQL generator output for every client |
| `SQL\Builder.cs` | Skips spawn builders when `SplitSQLFile`; adds `ClientType.Forever` targets | Changes SQL generator behaviour |
| `SQL\QueryBuilder.cs` | Empty-WHERE guards (2 places) | Behaviour change in generated statements |
| `Hotfix\IHotfixSerializer.cs` | Delete-by-ID for `broadcast_text` and every table when `TargetedDatabase.Forever` | Changes hotfix SQL for all clients |
| `Enums\TargetedDatabase.cs`, `Enums\SQLOutput.cs`, `Enums\ClientType.cs` | New `Forever` database/flag; `npc_spellclick_spells` reordered; `Forever = 12` | Enum/ordering changes shared by all clients |
| `Misc\Packet.cs`, `PacketReads.cs`, `Settings.cs`, `DynamicUpdateField.cs`, `Enums\WowCSEntityFragments.cs`, `Parsing\Parsers\MovementHandler.cs` | `DeclaredLength` + `WPP_PKTDBG`, public `ReadPackedUInt64`, `KeepSQLFile`, element-count limit, Forever fragment offsets, renamed field | Shared-API divergence |
| `Store\Storage.cs`, `StoreDictionaries.cs`, 6 new `*Forever.cs` store objects | New Forever data bags/types | The module does not compile without them |
| `WowPacketParserModule.V1_60_1_70888` | 16,593 LOC, 33 handlers, references `V12_0_0_65390` | The module itself |

## 3. Phase 2 — Baseline

`Elysium_wpp_master` was created by copying the TrinityCore master **with `bin\`/`obj\` excluded**
(`tar` pipe; `rsync` is not available on this host). No compiled DLL was used as a substitute for
source: the 23 parser modules are built from the copied `.csproj`/`.cs` files. The masters were only
read.

Preserved unchanged: solution layout (`WowPacketParser.slnx`, 3 core projects + 23 module projects),
`default.props`, `COPYING`, `Dockerfile`, `SQL\`, all parser modules, `App.config` → `WowPacketParser.dll.config`,
command-line options and output formats.

## 4. Phases 3–4 — Retail, PTR, Classic-family integration

The five Retail files were copied into the unified tree and verified byte-identical to
`retail_wpp` (`cmp`: all 5 same). Because PTR's four files are identical or a subset, no further
source merge was needed; the only PTR-unique artefact, `TestBuild70961Registration`, was merged into
the unified `ClientVersionTest.cs` (together with its `using WowPacketParser.Enums.Version;`).

Nothing was brought from Classic/BCC/MoP Classic: they have no source delta, and adding copies of
their files would have been duplication of code already present in the baseline.

Exact numeric opcode values are untouched: `0x00450059`, `0x00450226`, and the 70340/70783 tables.
No new packet names were introduced beyond the two provisional symbols that already existed in
Retail, no payload structure was written, and the SQL generator was not modified.

## 5. Phase 6 — Unified client recognition

The upstream detection workflow is kept as-is: `ClientVersion.GetVersion(DateTime)` /
`SetVersion(build)` choose the parser module (`GetVersionDefiningBuild`) and the opcode/update-field
tables. There is no new GUI and no manual client selection.

Ambiguities are preserved and documented rather than resolved silently:

- PTR shares the Retail family — there are no PTR-specific build numbers in this source.
- Classic Era `1.15.8+` and BCC `2.5.5+` resolve to `V5_5_0_61735` through upstream mappings.
- MoP Classic `5.5.3.65703` is declared but has no `GetVersionDefiningBuild` case → it throws instead
  of choosing a possibly wrong module. Left unpatched (no invented mapping).
- `ClientVersion.Expansion` is build-number-ordered upstream, so MoP 5.5.x builds report a later
  Retail expansion (`5.5.0.61735 > 11.0.0.55666` → `TheWarWithin`). `ClientVersion.Branch` — used for
  module/table selection — is correct. Pre-existing in every client copy; not changed.

## 6. Phase 5 — Forever: assessed, not integrated (blocked, pending)

**Decision: the Forever module is NOT part of the unified parser.** Module count stays at 23.

Reasons, all evidenced in §2.4 and in the source:

1. **Direct functional conflict.** The Devorvh `ClientVersion.cs` removes the `V2_5_6_70577` BCC
   registration. Taking the fork's registration wholesale would drop BCC support this phase must
   preserve; keeping both would require rewriting the fork's dispatch block.
2. **Retail regression risk.** The fork rewrites `AllowableRace` parsing in the V12 hotfix handler
   and adds a Forever branch plus new exception paths inside the Retail `SMSG_UPDATE_OBJECT` reader.
   Those are shared files used by every 12.x sniff, not Forever-only code.
3. **SQL behaviour changes for all clients.** `SQLFile.cs`, `Builder.cs`, `QueryBuilder.cs` and
   `IHotfixSerializer.cs` change generated SQL (`@CGUID`/`@OGUID` sources, append semantics,
   spawn-builder skipping, delete-by-ID). Adopting them means changing SQL generator behaviour for
   Retail/Classic without packet evidence — explicitly out of scope.
4. **Shared API/store dependency.** The module needs six Forever store objects plus new `Storage`
   bags and `DataBag.Clear()`, i.e. the fork's core types, so it cannot be added as an isolated
   plugin.
5. **Enum collisions.** `ClientType.Forever = 12` (master still has `Forever = 11`, aliased with
   `Midnight = 11`), a new `TargetedDatabase.Forever`, and a reordering of `SQLOutput`.

No assembly-name collision exists (`WowPacketParserModule.V1_60_1_70888` is unique and the fork's
assembly names match TrinityCore), so the blocker is behavioural, not naming. Forcing the merge would
require a fork-wide port plus a re-verification of Retail and BCC parsing — a rewrite, not an
integration. `Forever_wpp` (112 files, 24 modules) remains the Forever distribution, untouched.

A test documents the boundary: `UnifiedClientDispatchTest.TestForeverFamilyIsNotClaimed` asserts the
unified build does not define builds `70888`/`70338` and that only the upstream `1.60.1` beta
registration exists.

## 7. Phase 7 — Build and tests

```
dotnet restore WowPacketParser.slnx           exit 0
dotnet build   WowPacketParser.slnx -c Release  0 warnings, 0 errors
dotnet test    WowPacketParser.Tests -c Release  118 passed, 6 skipped, 0 failed (124)
```

The 6 skipped tests are the upstream `[Ignore]` cases (`OpcodeTest.TestHasHandler`,
`TestHasValue`, `TestHasHandler6x`, `SQLDatabaseTest.TestGet`, `GuidTest.TestToString`,
`LfgEntryTest.TestToString`); no test was skipped, weakened or added to make verification pass.

Focused coverage added or merged:

| Requirement | Test |
| --- | --- |
| Retail 70783 provisional mappings | `ProvisionalOpcodeTest.TestProvisional70783ServerMappings`, `TestExisting70783MappingsIntact` |
| Cross-build opcode isolation | `ProvisionalOpcodeTest.TestProvisionalMappingsAreBuildScoped`, `HistoricalEvidenceRegressionTest.TestProvisionalLayerDoesNotLeakToOtherBuilds` |
| Raw payload preservation | `ProvisionalOpcodeTest.TestPayloadBytesPreserved`, `HistoricalEvidenceRegressionTest.TestBothHousingPayloadShapesPreserved` |
| Retail 70961 recognition | `ClientVersionTest.TestBuild70961Registration`, `ProvisionalOpcodeTest.Test70961RecognitionAndRouting`, `UnifiedClientDispatchTest.TestRetailFamilyDispatch` |
| PTR version registration | `ClientVersionTest.TestBuild70961Registration`, `UnifiedClientDispatchTest.TestPtrBuildRegistrationsUseTheRetailModule` |
| Classic-family dispatch | `UnifiedClientDispatchTest.TestClassicEraDispatch` |
| BCC 70577 registration | `UnifiedClientDispatchTest.TestBurningCrusade70577Registration` |
| MoP Classic dispatch (+ 65703 gap) | `UnifiedClientDispatchTest.TestMoPClassicDispatch`, `TestMoPClassicKnownGap65703` |
| Forever module loading | Not applicable — not integrated; `TestForeverFamilyIsNotClaimed` pins the boundary |
| Existing SQL generator behaviour | Existing upstream `QueryBuilderTest`, `RowListTest`, `SQLDatabaseTest` — all pass, generator untouched |

One adjustment during development, recorded for honesty: the first version of
`TestMoPClassicDispatch` asserted `ClientVersion.Expansion == MistsOfPandaria` and failed
(`TheWarWithin`). That exposed the upstream `Expansion` threshold quirk described in §5; the test now
pins `Branch` (used for module/table selection) and asserts the documented `Expansion` value. No
parser code was changed.

Not performed, deliberately: no `.pkt`/`.bin`/`.sqlite` capture was opened or parsed, and no
generated SQL was executed.

## 8. Phase 8 — Publication

The existing script was extended with a `Unified` catalog entry (`Elysium_wpp_master`,
23 expected modules, `Elysium_wpp_master\Publish`, executable `WowPacketParser.exe`) and
`Unified` was added to the `-Client` `ValidateSet`. Staged publication, per-file SHA-256 comparison
against the build output, standalone smoke test and rollback behaviour are the script's unchanged
logic — no new publication path was introduced.

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Publish-WPP.ps1 -Client Unified   exit 0
```

Verified:

| Check | Result |
| --- | --- |
| Files / size | 108 files, 56.4 MB (107 runtime output files + `COPYING`) |
| Parser modules | 23 DLLs in `Publish\Parsers\`; no `V1_60_1_70888` |
| Required runtime files | 24/24 present, plus `runtimes\win-x64` and `runtimes\win-x86` SQLite interop |
| Configuration | `WowPacketParser.dll.config` readable, 121 `appSettings` keys, `DumpFormat`/`OutputFlag`/`ClientBuild`/`ShowEndPrompt` present |
| Staged vs build output | 108/108 SHA-256 identical |
| Publication vs build output | `cmp` identical for `WowPacketParser.dll` and `WowPacketParser.exe` |
| Smoke test | staged exe started with a missing probe filename → exit 0, "File … was not found, removed." (no capture opened) |
| Source leakage | 0 `.cs`/`.csproj`/`.slnx` files in `Publish`; no `Trinitycore_Wpp_Master` path strings |
| Unified content in the published binary | `V12_1_5_70961`, `UNKNOWN_SMSG_450059`, `UNKNOWN_SMSG_450226` present in `Publish\WowPacketParser.dll` |
| Previous publication | none existed; nothing was replaced or rolled back |

Hashes:

- `Publish` manifest (SHA-256 of the sorted per-file manifest):
  `a2b3127e25228ac0164c43ee650406f7bbd435395f4bd24c2bb73470dcd12aae` (108 entries)
- `COPYING` (GPLv3, identical to the tree's own copy)
  `8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903`
- `WowPacketParser.dll`
  `400ed6f8b2655fe38c5b834f76ef4a61710a2463523a633e66a76be039425738` (11,073,536 B)
- `WowPacketParser.exe`
  `ccc04415f205f2e76d2d3641536a27f275b6488a6d27680187e1e94e37323e4c` (162,304 B, unchanged apphost)
- `WowPacketParser.dll.config`
  `664b7e469e88139c9b133dc84123273dc23cba7c5a9ff1a34cde28d64c7522cb` (unchanged from upstream)
- Per-file manifests: `backup\Unified_Phase_20261118\Unified_Publish.sha256`

The licence text is part of the build output: `WowPacketParser.csproj` copies the tree's `COPYING`
next to the executable, so the published distribution carries the GPLv3 notice and matches the
build output file-for-file. No other client's project file was changed.

Distribution is framework-dependent (`Microsoft.NETCore.App` 10.0, `net10.0`); the `.pdb` files are
included because they are part of the scripted Release output.

## 9. Root workflow changes

- `WPP.slnx` — added `/Clients/Unified/` and `/Clients/Unified/Parsers/` (26 projects). The solution
  now lists 183 projects (`dotnet sln WPP.slnx list` exits 0; 26 entries reference
  `Elysium_wpp_master`).
- `scripts\Publish-WPP.ps1` — `Unified` added to the `ValidateSet`, a `Unified` catalog entry and a
  `$script:UnifiedModules` list (23 modules) added. No other logic touched.

No other root file was modified.

## 10. Protection of the independent projects

Baseline manifests were taken before any work and re-checked afterwards
(`backup\Unified_Phase_20261118\<client>_Publish.sha256`):

| Client | Files | Status |
| --- | --- | --- |
| Retail | 107 | **UNCHANGED** |
| PTR | 108 | **UNCHANGED** |
| Classic | 107 | **UNCHANGED** |
| BCC | 107 | **UNCHANGED** |
| MoP Classic | 107 | **UNCHANGED** |
| Forever | 112 | **UNCHANGED** |

Both upstream masters were read only; neither was modified, and neither client source tree was
written to. No GitHub release was created and nothing was uploaded.

## 11. Final statement of capability

| Client family | Included | Evidence | Experimental / blocked |
| --- | --- | --- | --- |
| Retail | Yes | 70783 + 70961 registered and routed; 70783 provisional opcodes scoped and mapped | Payload support for 70961 unverified (no capture parsed); provisional names are recognition-only |
| PTR | Yes | 70575/70688/70783/70961 → 12.x module | No PTR-specific build numbers exist; same family as Retail |
| Classic Era | Yes | 1.13.2–1.15.7 → `V1_13_2_31446`; 1.15.8+ → upstream mapping | 1.60.1 beta maps to `V1_60_1_69876` without Forever wire layout |
| BCC | Yes | 2.5.1 → `V2_5_1_38707`; **2.5.6.70577 → `V5_5_0_61735`** | Late 2.5.5/2.5.6 reuse the MoP module (upstream) |
| MoP Classic | Yes | 5.5.0/5.5.3 → `V5_5_0_61735` | `5.5.3.65703` unresolvable (throws); `Expansion` mislabels 5.5.x |
| Forever | **No** | Excluded with the reasons in §6; boundary pinned by a test | Blocked — needs a fork-wide port of Devorvh core/SQL changes |
