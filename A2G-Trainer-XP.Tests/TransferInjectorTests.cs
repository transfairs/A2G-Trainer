using System;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for TransferInjector: redirecting the market and deadline calls to the stub, the request
    /// arguments and preconditions, refusing foreign patches - and actually running the generated
    /// x86 stub (this test process is x86 like the game) against stand-ins for the game's functions
    /// and clubs.
    /// </summary>
    public class TransferInjectorTests
    {
        private static uint Site(FakeModule fake, uint offset) => fake.ModuleBase + offset;

        private static void WriteCall(FakeModule fake, uint siteOffset, uint targetOffset)
        {
            int rel = unchecked((int)(fake.ModuleBase + targetOffset - (fake.ModuleBase + siteOffset + 5)));
            fake.WriteModuleBytes(siteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
        }

        // The three CALL rel32 the injector redirects, as in the unpatched game.
        private static void WriteOriginalCalls(FakeModule fake)
        {
            foreach (uint site in Settings.TransferMarketCallSiteOffsets)
                WriteCall(fake, site, Settings.TransferMarketFunctionOffset);
            WriteCall(fake, Settings.TransferDeadlineCallSiteOffset, Settings.TransferDeadlineFunctionOffset);
        }

        private static uint CallTarget(FakeModule fake, uint siteOffset)
        {
            byte[] call = fake.ReadModuleBytes(siteOffset, 5);
            return unchecked((uint)(Site(fake, siteOffset) + 5 + BitConverter.ToInt32(call, 1)));
        }

        private static Player BayernPlayer() => new Player { NameRecordId = 0x1234, ClubCountry = PlayerEnums.Country.Deutschland, ClubId = 5 };

        private static byte[] Read(FakeModule fake, uint address, int length) => fake.Memory.ReadBytesAtAddress(address, length);

        private static uint ReadU32(FakeModule fake, uint address) => BitConverter.ToUInt32(Read(fake, address, 4), 0);

        [Fact]
        public void Request_RedirectsAllThreeCallsAndStoresTheMove()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.WriteModuleBytes(Settings.MoneyFactorOffset, BitConverter.GetBytes(100));
                TransferInjector injector = new TransferInjector(fake.Memory);

                TransferInjector.RequestResult result = injector.Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.LoanWithBuyOption, 50, 2000, immediate: true);

                Assert.Equal(TransferInjector.RequestResult.Requested, result);
                uint stub = CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]) - TransferInjector.CodeOffset;
                Assert.Equal(TransferInjector.Signature, Read(fake, stub, TransferInjector.Signature.Length));
                Assert.Equal(stub + TransferInjector.CodeOffset, CallTarget(fake, Settings.TransferMarketCallSiteOffsets[1]));
                Assert.Equal(stub + TransferInjector.DeadlineCodeOffset, CallTarget(fake, Settings.TransferDeadlineCallSiteOffset));

                uint[] arguments = Enumerable.Range(0, 9).Select(i => ReadU32(fake, stub + TransferInjector.ArgumentsOffset + (uint)i * 4)).ToArray();
                // Fee and buy price in money units; "sofort" doesn't apply to loans.
                Assert.Equal(new uint[] { 0x1234, (uint)PlayerEnums.Country.Deutschland, 5, (uint)PlayerEnums.Country.England, 7, 3, 5000, 200000, 0 }, arguments);
                Assert.True(injector.IsRequestPending());
                Assert.Equal(TransferInjector.Outcome.None, injector.TakeOutcome());
            }
        }

        [Fact]
        public void Request_ForATransfer_StoresImmediateAndNoBuyPrice()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                TransferInjector injector = new TransferInjector(fake.Memory);

                injector.Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 1, 2000, immediate: true);

                uint stub = CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]) - TransferInjector.CodeOffset;
                uint Arg(int slot) => ReadU32(fake, stub + TransferInjector.ArgumentsOffset + (uint)slot * 4);
                // Without a readable money factor the game's x100 DM default applies.
                Assert.Equal(100u, Arg(TransferInjector.ArgFee));
                Assert.Equal(0u, Arg(TransferInjector.ArgBuyPrice));
                Assert.Equal(1u, Arg(TransferInjector.ArgImmediate));
            }
        }

        [Fact]
        public void ToMoney_CapsAtTheGamesInt32()
        {
            Assert.Equal(300u, TransferInjector.ToMoney(3, 100));
            Assert.Equal((uint)int.MaxValue, TransferInjector.ToMoney(uint.MaxValue, 100));
        }

        [Fact]
        public void Request_Twice_ReusesTheInstalledStub()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                TransferInjector injector = new TransferInjector(fake.Memory);

                injector.Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 0, 0, false);
                uint first = CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]);
                injector.Request(BayernPlayer(), PlayerEnums.Country.England, 8, TransferInjector.Kind.Transfer, 0, 0, false);

                Assert.Equal(first, CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]));
            }
        }

        [Fact]
        public void Request_FinishesAPartialInstall()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                TransferInjector injector = new TransferInjector(fake.Memory);
                injector.Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 0, 0, false);
                uint stubCode = CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]);
                // e.g. the second market call back to the original after a crash mid-install
                WriteCall(fake, Settings.TransferMarketCallSiteOffsets[1], Settings.TransferMarketFunctionOffset);

                Assert.Equal(TransferInjector.RequestResult.Requested, injector.Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 0, 0, false));
                Assert.Equal(stubCode, CallTarget(fake, Settings.TransferMarketCallSiteOffsets[1]));
            }
        }

        [Fact]
        public void Request_ChecksThePlayersClubBeforeTouchingAnything()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                TransferInjector injector = new TransferInjector(fake.Memory);

                Assert.Equal(TransferInjector.RequestResult.NoClub, injector.Request(new Player(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 0, 0, false));
                Assert.Equal(TransferInjector.RequestResult.SameClub, injector.Request(BayernPlayer(), PlayerEnums.Country.Deutschland, 5, TransferInjector.Kind.Transfer, 0, 0, false));

                Assert.Equal(Site(fake, Settings.TransferMarketFunctionOffset), CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]));
                Assert.False(injector.IsRequestPending());
            }
        }

        [Fact]
        public void Request_WithoutOriginalBuildAnchors_IsNotSupported()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                TransferInjector injector = new TransferInjector(fake.Memory);

                Assert.False(injector.IsSupported);
                Assert.Equal(TransferInjector.RequestResult.NotSupported, injector.Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 0, 0, false));
                Assert.Equal(Site(fake, Settings.TransferMarketFunctionOffset), CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]));
                Assert.False(injector.IsRequestPending());
                Assert.Equal(TransferInjector.Outcome.None, injector.TakeOutcome());
                Assert.Null(injector.EnsureInstalled());
            }
        }

        [Fact]
        public void Request_WhenACallSiteIsNotACall_RefusesToPatch()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.WriteModuleBytes(Settings.TransferDeadlineCallSiteOffset, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 });

                Assert.Equal(TransferInjector.RequestResult.HookFailed, new TransferInjector(fake.Memory).Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 0, 0, false));
                Assert.Equal(Site(fake, Settings.TransferMarketFunctionOffset), CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]));
            }
        }

        [Fact]
        public void Request_WhenACallIsRedirectedByAnotherTool_LeavesEverythingAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                WriteCall(fake, Settings.TransferMarketCallSiteOffsets[1], 0x100040);

                Assert.Equal(TransferInjector.RequestResult.HookFailed, new TransferInjector(fake.Memory).Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 0, 0, false));
                Assert.Equal(Site(fake, Settings.TransferMarketFunctionOffset), CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]));
                Assert.Equal(Site(fake, 0x100040), CallTarget(fake, Settings.TransferMarketCallSiteOffsets[1]));
            }
        }

        [Fact]
        public void Request_WhenTheDeadlineCallIsRedirectedByAnotherTool_LeavesEverythingAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                WriteCall(fake, Settings.TransferDeadlineCallSiteOffset, 0x100040);

                Assert.Equal(TransferInjector.RequestResult.HookFailed, new TransferInjector(fake.Memory).Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 0, 0, false));
                Assert.Equal(Site(fake, Settings.TransferMarketFunctionOffset), CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]));
            }
        }

        [Fact]
        public void TakeOutcome_ReportsOnceAfterTheStubRan()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                TransferInjector injector = new TransferInjector(fake.Memory);
                injector.Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, 0, 0, false);
                uint stub = CallTarget(fake, Settings.TransferMarketCallSiteOffsets[0]) - TransferInjector.CodeOffset;

                // What the stub leaves behind: flag cleared, outcome set.
                fake.Memory.WriteBytesAtAddress(stub + TransferInjector.FlagOffset, BitConverter.GetBytes(0u));
                fake.Memory.WriteBytesAtAddress(stub + TransferInjector.OutcomeOffset, BitConverter.GetBytes((uint)TransferInjector.Outcome.SquadFull));

                Assert.False(injector.IsRequestPending());
                Assert.Equal(TransferInjector.Outcome.SquadFull, injector.TakeOutcome());
                Assert.Equal(TransferInjector.Outcome.None, injector.TakeOutcome());
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StubEntry();

        // Stand-ins for the game: two clubs (old = country 1, new = country 2), one player struct and
        // recording versions of every function the stub calls.
        private sealed class Game : IDisposable
        {
            public readonly FakeModule Fake = new FakeModule();
            public readonly uint Block, Stub, Record, OldClub, NewClub, Player;
            private readonly uint clubTable;

            // Record slots (dwords): call counters, then the move/loan arguments.
            public const int FetchClubCalls = 0, ReleaseClubCalls = 1, FetchPlayerCalls = 2, ReleasePlayerCalls = 3,
                MoveCalls = 4, LoanCalls = 5, MarketCalls = 6, DeadlineAnswer = 7, Arguments = 8;

            public Game()
            {
                this.Block = this.Fake.Memory.AllocateExecutable(0x3000).Value;
                this.Stub = this.Block;
                uint code = this.Block + 0x400;
                this.Record = this.Block + 0x1000;
                this.clubTable = this.Block + 0x1100;
                this.OldClub = this.Block + 0x1200;
                this.NewClub = this.Block + 0x1C00;
                this.Player = this.Block + 0x2800;

                this.Write(this.clubTable + 4, BitConverter.GetBytes(this.OldClub));
                this.Write(this.clubTable + 8, BitConverter.GetBytes(this.NewClub));
                this.Write(this.Player, BitConverter.GetBytes((ushort)0x1234));
                this.SetSquad(this.OldClub, this.Player);

                uint R(int slot) => this.Record + (uint)slot * 4;
                byte[] A(uint a) => BitConverter.GetBytes(a);
                byte[] Inc(int slot) => new byte[] { 0xFF, 0x05 }.Concat(A(R(slot))).ToArray();
                byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
                byte[] StoreArgs(int count) => Concat(Enumerable.Range(0, count).Select(i =>
                    Concat(new byte[] { 0x8B, 0x44, 0x24, (byte)(4 + i * 4), 0xA3 }, A(R(Arguments + i)))).ToArray());

                var t = new TransferInjector.StubTargets();
                uint next = code;
                uint Place(byte[] bytes) { uint at = next; this.Write(at, bytes); next += 0x60; return at; }

                t.Market = Place(Concat(Inc(MarketCalls), new byte[] { 0xC3 }));
                t.Deadline = Place(new byte[] { 0xB8, 1, 0, 0, 0, 0xC3 });                    // after the deadline
                t.FetchClub = Place(Concat(new byte[] { 0x8B, 0x44, 0x24, 0x04, 0x8B, 0x04, 0x85 }, A(this.clubTable), Inc(FetchClubCalls), new byte[] { 0xC3 }));
                t.ReleaseClub = Place(Concat(Inc(ReleaseClubCalls), new byte[] { 0xC3 }));
                t.FetchPlayer = Place(Concat(new byte[] { 0xB8 }, A(this.Player), Inc(FetchPlayerCalls), new byte[] { 0xC3 }));
                t.ReleasePlayer = Place(Concat(Inc(ReleasePlayerCalls), new byte[] { 0xC3 }));
                // Salary 77, one year - the stub must raise the years to 2.
                t.BuildContract = Place(new byte[] { 0x8B, 0x44, 0x24, 0x04, 0x66, 0xC7, 0x00, 77, 0, 0xC6, 0x40, 0x0C, 1, 0xC3 });
                // The move asks the deadline through the redirected call, like FUN_005fb480.
                t.Move = Place(Concat(StoreArgs(5), new byte[] { 0xB8 }, A(this.Stub + TransferInjector.DeadlineCodeOffset),
                    new byte[] { 0xFF, 0xD0, 0xA3 }, A(R(DeadlineAnswer)), Inc(MoveCalls), new byte[] { 0xC3 }));
                t.Loan = Place(Concat(StoreArgs(7), Inc(LoanCalls), new byte[] { 0xC3 }));

                this.Write(this.Stub, TransferInjector.BuildStub(this.Stub, t));
            }

            public void Write(uint address, byte[] bytes) => this.Fake.Memory.WriteBytesAtAddress(address, bytes);
            public uint U32(uint address) => BitConverter.ToUInt32(this.Fake.Memory.ReadBytesAtAddress(address, 4), 0);
            public uint Rec(int slot) => this.U32(this.Record + (uint)slot * 4);

            public void SetSquad(uint club, params uint[] players)
            {
                this.Write(club + Settings.ClubSquadCountOffset, BitConverter.GetBytes((short)players.Length));
                for (int i = 0; i < players.Length; i++)
                    this.Write(club + Settings.ClubSquadOffset + (uint)i * 4, BitConverter.GetBytes(players[i]));
            }

            public void RequestMove(TransferInjector.Kind kind, bool immediate)
            {
                uint[] args = { 0x1234, 1, 9, 2, 7, (uint)kind, 5000, 200000, immediate ? 1u : 0u };
                this.Write(this.Stub + TransferInjector.ArgumentsOffset, args.SelectMany(BitConverter.GetBytes).ToArray());
                this.Write(this.Stub + TransferInjector.FlagOffset, BitConverter.GetBytes(1u));
            }

            public void Run() => Marshal.GetDelegateForFunctionPointer<StubEntry>(new IntPtr(this.Stub + TransferInjector.CodeOffset))();

            public uint Outcome => this.U32(this.Stub + TransferInjector.OutcomeOffset);
            public uint Flag => this.U32(this.Stub + TransferInjector.FlagOffset);
            public uint Contract => this.Stub + TransferInjector.ContractOffset;

            public void Dispose() => this.Fake.Dispose();
        }

        [Fact]
        public void Stub_Transfer_Immediate_MovesThroughTheGameAndSkipsTheDeadline()
        {
            using (Game game = new Game())
            {
                game.RequestMove(TransferInjector.Kind.Transfer, immediate: true);
                game.Run();

                Assert.Equal((uint)TransferInjector.Outcome.Done, game.Outcome);
                Assert.Equal(0u, game.Flag);
                Assert.Equal(1u, game.Rec(Game.MoveCalls));
                Assert.Equal(0u, game.Rec(Game.LoanCalls));
                // FUN_005fb480(player, new, old, fee, &contract)
                Assert.Equal(new[] { game.Player, game.NewClub, game.OldClub, 5000u, game.Contract },
                    Enumerable.Range(0, 5).Select(i => game.Rec(Game.Arguments + i)).ToArray());
                Assert.Equal(0u, game.Rec(Game.DeadlineAnswer));
                Assert.Equal(77, BitConverter.ToInt16(game.Fake.Memory.ReadBytesAtAddress(game.Contract, 2), 0));
                Assert.Equal(2, game.Fake.Memory.ReadBytesAtAddress(game.Contract + 0xC, 1)[0]);
                Assert.Equal(0, game.Fake.Memory.ReadBytesAtAddress(game.Stub + TransferInjector.ImmediateActiveOffset, 1)[0]);
                Assert.Equal(2u, game.Rec(Game.FetchClubCalls));
                Assert.Equal(2u, game.Rec(Game.ReleaseClubCalls));
                Assert.Equal(1u, game.Rec(Game.FetchPlayerCalls));
                Assert.Equal(1u, game.Rec(Game.ReleasePlayerCalls));
                Assert.Equal(1u, game.Rec(Game.MarketCalls));

                // No request: straight on to the market.
                game.Run();
                Assert.Equal(1u, game.Rec(Game.MoveCalls));
                Assert.Equal(2u, game.Rec(Game.MarketCalls));
                Assert.Equal(2u, game.Rec(Game.FetchClubCalls));
            }
        }

        [Fact]
        public void Stub_Transfer_WithoutImmediate_KeepsTheGamesDeadline()
        {
            using (Game game = new Game())
            {
                game.RequestMove(TransferInjector.Kind.Transfer, immediate: false);
                game.Run();

                Assert.Equal(1u, game.Rec(Game.MoveCalls));
                Assert.Equal(1u, game.Rec(Game.DeadlineAnswer));
            }
        }

        [Fact]
        public void Stub_LoanWithBuyOption_CallsTheLoanFunction()
        {
            using (Game game = new Game())
            {
                game.RequestMove(TransferInjector.Kind.LoanWithBuyOption, immediate: false);
                game.Run();

                Assert.Equal((uint)TransferInjector.Outcome.Done, game.Outcome);
                Assert.Equal(0u, game.Rec(Game.MoveCalls));
                Assert.Equal(1u, game.Rec(Game.LoanCalls));
                // FUN_005fb710(player, new, old, fee, buy price, &contract, kind)
                Assert.Equal(new[] { game.Player, game.NewClub, game.OldClub, 5000u, 200000u, game.Contract, 3u },
                    Enumerable.Range(0, 7).Select(i => game.Rec(Game.Arguments + i)).ToArray());
                Assert.Equal(1u, game.Rec(Game.MarketCalls));
            }
        }

        [Fact]
        public void Stub_WhenThePlayerLeftTheOldClub_RefusesAndReleasesTheClubs()
        {
            using (Game game = new Game())
            {
                game.Write(game.Player, BitConverter.GetBytes((ushort)0x4321));
                game.RequestMove(TransferInjector.Kind.Transfer, immediate: true);
                game.Run();

                Assert.Equal((uint)TransferInjector.Outcome.NotInClub, game.Outcome);
                Assert.Equal(0u, game.Rec(Game.MoveCalls));
                Assert.Equal(0u, game.Rec(Game.FetchPlayerCalls));
                Assert.Equal(2u, game.Rec(Game.ReleaseClubCalls));
                Assert.Equal(1u, game.Rec(Game.MarketCalls));
            }
        }

        [Fact]
        public void Stub_FindsThePlayerAnywhereInTheSquad()
        {
            using (Game game = new Game())
            {
                uint other = game.Player + 0x200;
                game.Write(other, BitConverter.GetBytes((ushort)0x0001));
                game.SetSquad(game.OldClub, game.Player, other, other);
                game.RequestMove(TransferInjector.Kind.Transfer, immediate: true);
                game.Run();

                Assert.Equal((uint)TransferInjector.Outcome.Done, game.Outcome);
            }
        }

        [Fact]
        public void Stub_WhenTheNewSquadIsFull_RefusesAndReleasesTheClubs()
        {
            using (Game game = new Game())
            {
                uint other = game.Player + 0x200;
                game.SetSquad(game.NewClub, Enumerable.Repeat(other, Settings.ClubSquadCapacity).ToArray());
                game.RequestMove(TransferInjector.Kind.Transfer, immediate: true);
                game.Run();

                Assert.Equal((uint)TransferInjector.Outcome.SquadFull, game.Outcome);
                Assert.Equal(0u, game.Rec(Game.MoveCalls));
                Assert.Equal(2u, game.Rec(Game.ReleaseClubCalls));

                // One place free: fine.
                game.SetSquad(game.NewClub, Enumerable.Repeat(other, Settings.ClubSquadCapacity - 1).ToArray());
                game.RequestMove(TransferInjector.Kind.Transfer, immediate: true);
                game.Run();
                Assert.Equal((uint)TransferInjector.Outcome.Done, game.Outcome);
            }
        }

        [Fact]
        public void BuildStub_ForTheGameModule_UsesTheGamesFunctions()
        {
            const uint moduleBase = 0x400000;
            byte[] stub = TransferInjector.BuildStub(0x10000000, moduleBase);
            byte[] code = stub.Skip(TransferInjector.CodeOffset).ToArray();

            // mov eax, market; jmp eax at the end of the market entry
            byte[] tail = new byte[] { 0xB8 }.Concat(BitConverter.GetBytes(moduleBase + Settings.TransferMarketFunctionOffset)).Concat(new byte[] { 0xFF, 0xE0 }).ToArray();
            Assert.True(Contains(code, tail));
            byte[] deadline = new byte[] { 0xB8 }.Concat(BitConverter.GetBytes(moduleBase + Settings.TransferDeadlineFunctionOffset)).ToArray();
            Assert.True(Contains(stub.Skip(TransferInjector.DeadlineCodeOffset).Take(TransferInjector.CodeOffset - TransferInjector.DeadlineCodeOffset).ToArray(), deadline));
        }

        private static bool Contains(byte[] data, byte[] pattern) =>
            Enumerable.Range(0, data.Length - pattern.Length + 1).Any(i => data.Skip(i).Take(pattern.Length).SequenceEqual(pattern));
    }
}
