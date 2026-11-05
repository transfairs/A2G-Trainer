using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// The last small branch gaps: a law firm's "other" fee with and without a listener, the
    /// savegame's stadium/release-clause flags, the club map and club selection without a club or
    /// clubs, the stadium block's weeks, the human club shown during a match, the championship tip
    /// and home club combos without a usable selection, the game's costs when a table can't be read
    /// and the process controller's refresh ticks.
    /// </summary>
    public class FinalCoverageTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, Private | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        private static T Field<T>(object target, string name) => (T)FindField(target.GetType(), name).GetValue(target);
        private static void SetField(object target, string name, object value) => FindField(target.GetType(), name).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void Raise(Control control, string onEvent) =>
            control.GetType().GetMethod(onEvent, Private, null, new[] { typeof(EventArgs) }, null).Invoke(control, new object[] { EventArgs.Empty });

        #region Model
        [Fact]
        public void LawFirmFee_Other_RoundsWithOrWithoutAListener()
        {
            LawFirmFee fee = new LawFirmFee();
            fee.Other = 2400;
            Assert.Equal(2000, fee.Other);

            List<string> changed = new List<string>();
            fee.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
            fee.Other = 3600;

            Assert.Equal(4000, fee.Other);
            Assert.Equal(new[] { nameof(LawFirmFee.Other) }, changed);
        }

        [Fact]
        public void SaveGameClub_StadiumFlags_SetAndClear()
        {
            SaveGameClub club = new SaveGameClub(SaveGameEditor.For(SaveGameFile.CreateBlank()), 3);
            Func<bool>[] flags = { () => club.HasFloodLight, () => club.HasGrassHeating, () => club.HasUpperSeats, () => club.HasRunningTrack };

            club.HasFloodLight = true;
            club.HasGrassHeating = true;
            club.HasUpperSeats = true;
            club.HasRunningTrack = true;
            Assert.All(flags, f => Assert.True(f()));

            club.HasFloodLight = false;
            club.HasGrassHeating = false;
            club.HasUpperSeats = false;
            club.HasRunningTrack = false;
            Assert.All(flags, f => Assert.False(f()));
        }

        [Fact]
        public void SaveGameCoach_ReleaseClause_SetAndClear()
        {
            SaveGameCoach coach = new SaveGameCoach(SaveGameEditor.For(SaveGameFile.CreateBlank()), 1);

            coach.HasReleaseClause = true;
            Assert.True(coach.HasReleaseClause);
            coach.HasReleaseClause = false;
            Assert.False(coach.HasReleaseClause);
        }
        #endregion

        #region Controls
        [Fact]
        public void ClubMap_Others_WithoutAClubAreAllClubs_WithOneLeaveItOut() => StaThread.Run(() =>
        {
            using (ClubMap map = new ClubMap())
            {
                map.Clubs = new List<ClubRecords.Entry>
                {
                    new ClubRecords.Entry { Id = 1, Name = "A" },
                    new ClubRecords.Entry { Id = 2, Name = "B" },
                };
                Func<List<byte>> others = () => ((IEnumerable<ClubRecords.Entry>)typeof(ClubMap).GetProperty("Others", Private).GetValue(map)).Select(c => c.Id).ToList();

                Assert.Equal(new byte[] { 1, 2 }, others());

                map.Club = new Club { Country = PlayerEnums.Country.Deutschland, Id = 2 };
                Assert.Equal(new byte[] { 1 }, others());
            }
        });

        [Fact]
        public void ClubSelection_ClubsSetToNull_IsAnEmptyList() => StaThread.Run(() =>
        {
            using (ComboBox country = new ComboBox())
            using (ComboBox club = new ComboBox())
            {
                ClubSelection selection = new ClubSelection(country, club);
                selection.Clubs = new List<ClubSquads.Entry> { new ClubSquads.Entry { Country = PlayerEnums.Country.Deutschland, Id = 1, Name = "A" } };

                selection.Clubs = null;

                Assert.NotNull(selection.Clubs);
                Assert.Empty(selection.Clubs);
                Assert.Null(selection.Find(PlayerEnums.Country.Deutschland, 1));
            }
        });
        #endregion

        #region ClubView
        // Human managers (Mitspieler 1, 2, ...) training these German clubs.
        private static void WriteManagers(FakeModule fake, params byte[] clubIds)
        {
            Addresses coach = AddressPresets.COACH;
            fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new[] { (byte)clubIds.Length });
            for (int slot = 0; slot < clubIds.Length; slot++)
            {
                CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.FIRSTNAME], Encoding.ASCII.GetBytes("Erwin"), slot);
                CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.COUNTRY], new[] { (byte)PlayerEnums.Country.Deutschland }, slot);
                CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.CLUB], new[] { clubIds[slot] }, slot);
            }
        }

        private static void WriteMinimalValidClub(FakeModule fake, byte id, PlayerEnums.Country country, ushort playerCount = 15)
        {
            fake.RoutePointerSlot(Settings.ClubAddress[0]);
            Addresses own = AddressPresets.OWN_CLUB;
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.NAME], Encoding.GetEncoding("iso-8859-1").GetBytes("Test FC\0"));
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.ID], new byte[] { id });
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.COUNTRY], new byte[] { (byte)country });
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], BitConverter.GetBytes(playerCount));
        }

        [Fact]
        public void BlockWeeks_TakesANumber_AndAnEmptyBoxAsZero() => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (ClubView view = new ClubView(fake.Memory, new ProcessController(null)))
            {
                ClubEditorSettingsTests.SeedOwnClub(fake, true);
                view.RefreshValues(PlayerEnums.AddressType.OWN);
                view.InitClubTabControl();
                Club club = Field<Club>(view, "club");
                int block = Field<StadiumPlan>(view, "StadiumPlanView").SelectedBlock;
                TextBox weeks = Field<TextBox>(view, "BlockWeeksInput");

                weeks.Text = "7";
                Assert.Equal(7, club.GetWeeks(block));

                weeks.Text = "";
                Assert.Equal(0, club.GetWeeks(block));
            }
        });

        [Fact]
        public void HumanClubInstead_DuringAMatch_PrefersTheHumanClubShownLast() => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (ClubView view = new ClubView(fake.Memory, new ProcessController(null)))
            {
                WriteManagers(fake, 1, 2);
                ClubSelection selection = Field<ClubSelection>(view, "clubSelection");
                ClubSquads.Entry first = new ClubSquads.Entry { Country = PlayerEnums.Country.Deutschland, Id = 1, Name = "Eins" };
                ClubSquads.Entry second = new ClubSquads.Entry { Country = PlayerEnums.Country.Deutschland, Id = 2, Name = "Zwei" };
                selection.Clubs = new List<ClubSquads.Entry> { first, second };
                // The opponent in the own-club slot: no players of a human's club.
                Club opponent = new Club { Country = PlayerEnums.Country.Deutschland, Id = 9 };

                // No human club shown yet: the first manager's.
                Assert.Same(first, Invoke(view, "HumanClubInstead", opponent));

                // Mitspieler 2's club was shown last: it stays.
                SetField(view, "lastHumanClub", (KeyValuePair<PlayerEnums.Country, byte>?)new KeyValuePair<PlayerEnums.Country, byte>(PlayerEnums.Country.Deutschland, 2));
                Assert.Same(second, Invoke(view, "HumanClubInstead", opponent));

                // A club in another country with the same ID isn't it.
                SetField(view, "lastHumanClub", (KeyValuePair<PlayerEnums.Country, byte>?)new KeyValuePair<PlayerEnums.Country, byte>(PlayerEnums.Country.England, 2));
                Assert.Same(first, Invoke(view, "HumanClubInstead", opponent));
            }
        });
        #endregion

        #region CoachView / PlayerView combos
        [Fact]
        public void ChampionTipClub_WithoutAClubChosen_KeepsTheTipsClub() => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (CoachView view = new CoachView(fake.Memory, new ProcessController(null)))
            {
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                Field<BindingSource>(view, "competencyBindingSource").DataSource = new CoachCompetencyLevel();
                IntPtr handle = view.Handle;
                CoachViewTests.SeedLeagueAndStock(fake);
                view.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                view.InitClubTabControl();

                ChampionTip tip = new ChampionTip { Country = PlayerEnums.Country.England, ClubId = 3 };
                Field<BindingSource>(view, "championTipBindingSource").DataSource = tip;
                ComboBox club = Field<ComboBox>(view, "TipClubInput");
                club.DataSource = null;
                club.Items.Clear();
                Assert.Null(club.SelectedValue);

                Raise(club, "OnSelectionChangeCommitted");

                Assert.Equal((ushort)3, tip.ClubId);
            }
        });

        private static ProcessController ControllerWithMemory(ProcessMemory memory)
        {
            Trainer trainer = (Trainer)FormatterServices.GetUninitializedObject(typeof(Trainer));
            GC.SuppressFinalize(trainer);
            typeof(Trainer).GetField("memory", Private).SetValue(trainer, memory);
            return new ProcessController(trainer);
        }

        [Fact]
        public void HomeCombos_WithoutAPlayerOrAUsableSelection_ChangeNothing() => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (PlayerView view = new PlayerView(fake.Memory, ControllerWithMemory(fake.Memory)))
            {
                IntPtr handle = view.Handle;
                Invoke(view, "AttachHomeClubHandlers");
                ComboBox homeCountry = Field<ComboBox>(view, "HomeCountryInput");
                ComboBox homeClub = Field<ComboBox>(view, "HomeClubInput");
                homeCountry.DataSource = null;
                homeCountry.Items.Clear();
                homeClub.DataSource = null;
                homeClub.Items.Clear();

                // No binding source at all.
                SetField(view, "bindingSource", null);
                Raise(homeClub, "OnSelectedIndexChanged");

                // A player, but nothing chosen in either combo.
                Player player = new Player { HomeCountry = PlayerEnums.Country.Chile, HomeClubId = 4 };
                SetField(view, "bindingSource", new BindingSource { DataSource = player });
                Assert.Null(homeCountry.SelectedValue);
                Assert.Null(homeClub.SelectedValue);
                Raise(homeCountry, "OnSelectedIndexChanged");
                Raise(homeClub, "OnSelectedIndexChanged");

                Assert.Equal(PlayerEnums.Country.Chile, player.HomeCountry);
                Assert.Equal((ushort)4, player.HomeClubId);
            }
        });
        #endregion

        #region GameRates
        [Fact]
        public void ReadCosts_WithAnUnreadableTable_IsNull()
        {
            // Not attached: neither table can be read.
            Assert.Null(GameRates.ReadCosts(new ProcessMemory(), FixedAddresses.Original));

            using (FakeModule fake = new FakeModule())
            {
                // Shifted so the department costs end just before a page the fees start on, then that
                // page of the department costs made unreadable: only the fees can be read.
                const uint shift = 0x100;
                FixedAddresses shifted = new FixedAddresses(0, shift, null, null);
                uint departments = Settings.DepartmentCostTableOffset + shift;
                uint fees = Settings.LawFirmFeeTableOffset + shift;
                Assert.NotEqual(departments & ~0xFFFu, fees & ~0xFFFu);
                fake.MakeModuleRegionUnreadable(departments, 12);
                Assert.NotNull(fake.Memory.ReadBytesAtAddress(shifted.Static(fake.Memory, Settings.LawFirmFeeTableOffset), 20));

                Assert.Null(GameRates.ReadCosts(fake.Memory, shifted));
            }
        }
        #endregion

        #region ProcessController
        // One trainer window (each costs the x86 test host ~20 MB) for every refresh tick. The game
        // process is the protected System process: OpenProcess fails for it and leaves the memory on
        // the fake module, while the controller still treats it as the game.
        [Fact]
        public void UpdateGameProcess_RefreshesOnlyOnANewProcessAnUninitialisedTrainerOrAClubChange() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                IntPtr handle = trainer.Handle;
                WriteMinimalValidClub(fake, id: 1, country: PlayerEnums.Country.Deutschland);
                WriteManagers(fake, 1);
                ProcessController controller = new ProcessController(trainer);
                Process game = Process.GetProcessById(4);
                SetField(controller, "gameProcess", game);
                Func<bool> initialised = () => Field<bool>(controller, "initialised");

                // First tick: no process known yet - the views are refreshed and wired.
                Invoke(controller, "UpdateGameProcess");
                Assert.True(initialised());
                Assert.Same(game, trainer.Anstoss.Process);
                Assert.True(trainer.Anstoss.IsRunning);
                Assert.True(Field<bool>(controller, "viewsWired"));

                // Same process, same club: nothing happens (the process isn't even set again).
                trainer.Anstoss.IsRunning = false;
                Invoke(controller, "UpdateGameProcess");
                Assert.False(trainer.Anstoss.IsRunning);

                // Still uninitialised (a failed read): refreshed again, now with the views' controllers.
                SetField(controller, "initialised", false);
                Invoke(controller, "UpdateGameProcess");
                Assert.True(initialised());
                Assert.True(trainer.Anstoss.IsRunning);

                // No managers any more: the coach view falls back to the first slot.
                fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new byte[] { 0 });
                SetField(controller, "initialised", false);
                Invoke(controller, "UpdateGameProcess");
                Assert.True(initialised());
                Assert.Equal(0, trainer.CoachView.CurrentTrainerIndex);

                // Another process registered than the one found: taken over.
                WriteManagers(fake, 1);
                trainer.Anstoss.Process = Process.GetCurrentProcess();
                trainer.Anstoss.IsRunning = false;
                Invoke(controller, "UpdateGameProcess");
                Assert.Same(game, trainer.Anstoss.Process);
                Assert.True(trainer.Anstoss.IsRunning);
            }
        });
        #endregion
    }
}
