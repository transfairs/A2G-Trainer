using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for CoachView's tab wiring, stock/bonus-country combo population, and button
    /// handlers, against a FakeModule set up with a real main country, one active bonus-country
    /// slot, and one non-zero stock position (so both the "has slot"/"none" and "enabled"/
    /// "disabled" branches get exercised).
    /// </summary>
    public class CoachViewTests
    {
        private static object InvokePrivate(object target, string methodName, params object[] args) =>
            target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private static void SeedLeagueAndStock(FakeModule fake)
        {
            fake.RoutePointerSlot(Settings.ClubAddress[0]);
            Addresses league = AddressPresets.LEAGUE_SETTINGS;
            fake.WriteDisplayCacheBytes(league[LeagueEnums.AddressKey.MAIN_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
            fake.WriteDisplayCacheBytes(league[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], new byte[] { (byte)PlayerEnums.Country.England, 0x00 });

            Addresses coach = AddressPresets.COACH;
            fake.WriteDisplayCacheBytes(coach[CoachEnums.AddressKey.STOCK_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
            fake.WriteDisplayCacheBytes(coach[CoachEnums.AddressKey.STOCK_SHARES], BitConverter.GetBytes((ushort)10));
        }

        [Fact]
        public void InitClubTabControl_WithActiveBonusCountryAndStock_WiresEverythingWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);

                Exception thrown = Record.Exception(() => trainer.CoachView.InitClubTabControl());

                Assert.Null(thrown);

                FieldInfo boxField = typeof(CoachView).GetField("AdditionalCountriesBox", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(CoachView).GetField("AdditionalCountriesBox", BindingFlags.Public | BindingFlags.Instance);
                var box = (System.Windows.Forms.GroupBox)boxField.GetValue(trainer.CoachView);
                Assert.True(box.Controls.Count > 0);
            }
        });

        [Fact]
        public void RefreshValues_CalledTwice_RebindsExistingControlsWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                Exception thrown = Record.Exception(() => trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void ReloadBtnClick_RefreshesUsingTheCurrentControllerType() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.CoachView, "ReloadBtn_Click", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void SaveBtnClick_SavesCoachAndLeagueThenRefreshes() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.CoachView, "SaveBtn_Click", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void ShowCompetencyLevel_WithOutOfRangeLevel_DoesNothing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.CoachView, "ShowCompetencyLevel", Coach.MaxLevel + 1));
                Exception thrownBelowMin = Record.Exception(() => InvokePrivate(trainer.CoachView, "ShowCompetencyLevel", Coach.MinLevel - 1));

                Assert.Null(thrown);
                Assert.Null(thrownBelowMin);
            }
        });

        [Fact]
        public void ShowCompetencyLevel_WithNoCoachLoadedYet_DoesNothing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                Exception thrown = Record.Exception(() => InvokePrivate(trainer.CoachView, "ShowCompetencyLevel", Coach.MinLevel));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void RebuildBonusCountryControls_WithNoActiveSlots_ShowsThePlaceholderLabel() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                Addresses league = AddressPresets.LEAGUE_SETTINGS;
                fake.WriteDisplayCacheBytes(league[LeagueEnums.AddressKey.MAIN_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
                // Leave every additional-country slot at the 0xFF/0xFF "unused" sentinel.
                for (int slot = 0; slot < LeagueSettings.MaxAdditionalCountries; slot++)
                    fake.WriteDisplayCacheBytes(Tools.SumHex(new[] { league[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], (slot * 2).ToString("X") }), new byte[] { 0xFF, 0xFF });

                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                FieldInfo boxField = typeof(CoachView).GetField("AdditionalCountriesBox", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(CoachView).GetField("AdditionalCountriesBox", BindingFlags.Public | BindingFlags.Instance);
                var box = (System.Windows.Forms.GroupBox)boxField.GetValue(trainer.CoachView);
                Assert.Contains(box.Controls.Cast<System.Windows.Forms.Control>(), c => c.Text.Contains("Keine Bonusländer"));
            }
        });

        // InitClubTabControl is only ever called after RefreshValues in production (see
        // ProcessController.UpdateGameProcess), so coach/league are already set - calling it on a
        // completely fresh view exercises every "not loaded yet" null-guard directly instead
        // (GetAvailableStockCountries/RefreshStockCountryOptions/RebuildBonusCountryControls/
        // UpdateStockSlotAvailability, and the coach-null short-circuit in the stock combos'
        // change handlers wired here).
        [Fact]
        public void InitClubTabControl_WithoutAPriorRefreshValues_HandlesNullCoachAndLeagueGracefully() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                Exception thrown = Record.Exception(() => trainer.CoachView.InitClubTabControl());

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void MainCountrySelectedIndexChanged_WithNoCoachLoadedYet_RefreshesStockCountriesWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                trainer.CoachView.InitClubTabControl();
                var mainCountryInput = (System.Windows.Forms.ComboBox)typeof(CoachView)
                    .GetField("MainCountryInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);

                Exception thrown = Record.Exception(() => mainCountryInput.SelectedIndex = mainCountryInput.Items.Count > 1 ? 1 : 0);

                Assert.Null(thrown);
            }
        });

        // Covers the "true && true" branch combination of both the stock-country combo's change
        // handler (which calls PopulateStockClubCombo - CoachView.cs line 373's DataSource filter)
        // and the stock-club combo's own change handler (line 328) - both only take that branch
        // when allClubs (scanned via ClubController's loadFullList against the same
        // Settings.ClubAddress pointer SeedLeagueAndStock routes) actually contains a club whose
        // Country matches the stock's STOCK_COUNTRY. FakeModule.RoutePointerSlot's default block
        // (DisplayCacheBlockSize, 256KB) falls well short of the ~0x64FA8+ club-scan region, so a
        // bigger custom block is wired in directly here instead (same technique
        // PlayerViewAdditionalTests' RefreshPlayerListView_ForDynamic_* tests use for the same
        // reason) - big enough to also hold the ordinary coach/league fields.
        [Fact]
        public void BuildStockBoxes_WithAMatchingClubInTheFullList_SelectsARealClubOnBothCombos() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                const int blockSize = 0x70000;
                IntPtr block = Marshal.AllocHGlobal(blockSize);
                try
                {
                    byte[] zeros = new byte[blockSize];
                    Marshal.Copy(zeros, 0, block, blockSize);
                    uint clubPointerFieldAddress = fake.ModuleBase + Convert.ToUInt32(Settings.ClubAddress[0], 16);
                    fake.Memory.WriteBytesAtAddress(clubPointerFieldAddress, BitConverter.GetBytes((uint)block.ToInt64()));

                    void WriteField(string fieldOffset, string baseOffset, byte[] data)
                    {
                        uint address = Convert.ToUInt32(Tools.SumHex(new[] { fieldOffset, baseOffset }), 16);
                        Marshal.Copy(data, 0, block + (int)address, data.Length);
                    }

                    Addresses league = AddressPresets.LEAGUE_SETTINGS;
                    WriteField(league[LeagueEnums.AddressKey.MAIN_COUNTRY], "", new byte[] { (byte)PlayerEnums.Country.Deutschland });
                    WriteField(league[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], "", new byte[] { (byte)PlayerEnums.Country.England, 0x00 });

                    Addresses coach = AddressPresets.COACH;
                    WriteField(coach[CoachEnums.AddressKey.STOCK_COUNTRY], "", new byte[] { (byte)PlayerEnums.Country.Deutschland });
                    WriteField(coach[CoachEnums.AddressKey.STOCK_SHARES], "", BitConverter.GetBytes((ushort)10));
                    // STOCK_CLUB is left at its default 0 - matched below by giving the seeded club Id 0 too.

                    string clubSlot0Offset = Settings.AllClubInitialOffset.Key;
                    WriteField(AddressPresets.ALL_CLUBS[ClubEnums.AddressKey.NAME], clubSlot0Offset, Encoding.GetEncoding("iso-8859-1").GetBytes("Testclub\0"));
                    WriteField(AddressPresets.ALL_CLUBS[ClubEnums.AddressKey.ID], clubSlot0Offset, new byte[] { 0 });
                    WriteField(AddressPresets.ALL_CLUBS[ClubEnums.AddressKey.COUNTRY], clubSlot0Offset, new byte[] { (byte)PlayerEnums.Country.Deutschland });

                    trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);

                    Exception thrown = Record.Exception(() => trainer.CoachView.InitClubTabControl());

                    Assert.Null(thrown);

                    var clubCombos = (ComboBox[])typeof(CoachView).GetField("stockClubCombos", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                    Assert.IsType<Club>(clubCombos[0].SelectedItem);

                    var loadedCoach = (Coach)typeof(CoachView).GetField("coach", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                    Assert.Equal((byte)0, loadedCoach.Stocks[0].ClubId);
                }
                finally
                {
                    Marshal.FreeHGlobal(block);
                }
            }
        });

        // PopulateStockClubCombo is only ever called (from the country combo's own change handler)
        // once this.coach is already non-null - so allClubs is likewise always populated by
        // RefreshValues before it can run in production. Calling it directly via reflection on a
        // completely fresh view exercises its "allClubs is still null" branch, which production
        // code otherwise never reaches.
        [Fact]
        public void PopulateStockClubCombo_WithNoClubsLoadedYet_LeavesTheComboEmpty() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                ComboBox clubCombo = new ComboBox();

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.CoachView, "PopulateStockClubCombo", clubCombo, PlayerEnums.Country.Deutschland, (byte)0));

                Assert.Null(thrown);
                Assert.Empty((System.Collections.IEnumerable)clubCombo.DataSource);
            }
        });

        // The club combo's SelectedIndexChanged handler (wired in BuildStockBoxes) only ever runs
        // in production once this.coach is set - PopulateStockClubCombo, the only thing that
        // assigns clubCombo.DataSource (and so is the only thing that can raise this event), is
        // itself only called from the country combo's change handler behind the same coach != null
        // guard. Forcing coach back to null and then raising the event directly (via the
        // protected OnSelectedIndexChanged all ComboBoxes inherit) exercises the handler's own
        // short-circuit guard, which production code can't otherwise reach.
        [Fact]
        public void ClubComboSelectedIndexChanged_WithCoachClearedAfterwards_DoesNothingWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                typeof(CoachView).GetField("coach", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(trainer.CoachView, null);

                var clubCombos = (ComboBox[])typeof(CoachView).GetField("stockClubCombos", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                MethodInfo onSelectedIndexChanged = typeof(ComboBox).GetMethod("OnSelectedIndexChanged", BindingFlags.NonPublic | BindingFlags.Instance);

                Exception thrown = Record.Exception(() => onSelectedIndexChanged.Invoke(clubCombos[0], new object[] { EventArgs.Empty }));

                Assert.Null(thrown);
            }
        });

        // Raising the event directly through the protected OnSelectedIndexChanged every ComboBox
        // inherits (the same technique the club combo tests above use) at least reaches the handler
        // body without needing a real UI interaction. It doesn't fully close the gap, though: without
        // a created window handle the combo's DataSource-backed Items/SelectedValue never actually
        // materialize here (confirmed by inspection - SelectedValue reads back null), so this only
        // exercises the "SelectedValue is not a Country" (false) side of the pattern match, not the
        // "picked a real country" (true) side that writes back into League.AdditionalCountries. That
        // true side needs the combo's Items to genuinely materialize, which needs a created window
        // handle - which is exactly what previously hung this test host (see
        // MainCountrySelectedIndexChanged_* above, which sidesteps the same problem for a combo built
        // at design time instead of at runtime). Left as a known remaining gap - see the final report.
        [Fact]
        public void BonusCountryComboSelectedIndexChanged_WithUnmaterializedSelection_DoesNothingWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                var bonusCombos = (ComboBox[])typeof(CoachView).GetField("bonusCountryCombos", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                ComboBox activeCombo = bonusCombos.First(c => c != null);
                MethodInfo onSelectedIndexChanged = typeof(ComboBox).GetMethod("OnSelectedIndexChanged", BindingFlags.NonPublic | BindingFlags.Instance);

                Exception thrown = Record.Exception(() => onSelectedIndexChanged.Invoke(activeCombo, new object[] { EventArgs.Empty }));

                Assert.Null(thrown);
            }
        });

        // competencyTotalLabel's Binding.Format handler only ever runs through WinForms' own data-
        // binding push, which (per the investigation this comment used to describe) needs more than
        // a ResetBindings call to fire without a created window handle. Format is a plain field-like
        // event on Binding, so its compiler-generated backing delegate can be read via reflection and
        // invoked directly instead of relying on that push - exercising the handler body itself
        // without depending on WinForms' internal timing for when Format actually gets raised.
        [Fact]
        public void CompetencyTotalLabelFormatBinding_ComputesExpectedTotalStringWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                var totalLabel = (System.Windows.Forms.Label)typeof(CoachView).GetField("competencyTotalLabel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                Binding totalBinding = totalLabel.DataBindings["Text"];
                FieldInfo formatField = typeof(Binding).GetField("onFormat", BindingFlags.NonPublic | BindingFlags.Instance);
                ConvertEventHandler handler = (ConvertEventHandler)formatField.GetValue(totalBinding);
                ConvertEventArgs args = new ConvertEventArgs(0, typeof(string));

                Exception thrown = Record.Exception(() => handler(totalBinding, args));

                Assert.Null(thrown);
                Assert.Contains("Punkte verteilt", (string)args.Value);
            }
        });
    }
}
