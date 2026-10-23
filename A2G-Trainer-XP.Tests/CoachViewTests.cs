using System;
using System.Collections.Generic;
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
            fake.WriteDisplayCacheBytes(league[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT], new byte[] { 1, 0x00 });
            fake.WriteDisplayCacheBytes(league[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], new byte[] { (byte)PlayerEnums.Country.England, 0x00 });

            // On the original build trainers are read from their fixed working copy, not through the
            // club pointer the league settings above still use.
            Addresses coach = AddressPresets.COACH;
            CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.STOCK_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
            CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.STOCK_SHARES], BitConverter.GetBytes((ushort)10));
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

        // The release clause and its announcement are two bits of one byte, each bound to its own
        // checkbox (see CoachView.InitClubTabControl).
        [Fact]
        public void InitClubTabControl_BindsReleaseClauseCheckboxesToBothFlags() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);

                trainer.CoachView.InitClubTabControl();

                var clauseInput = (CheckBox)typeof(CoachView).GetField("ReleaseClauseInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                var publishedInput = (CheckBox)typeof(CoachView).GetField("ReleaseClausePublishedInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);

                Assert.Equal(nameof(Coach.HasReleaseClause), clauseInput.DataBindings["Checked"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.IsReleaseClausePublished), publishedInput.DataBindings["Checked"].BindingMemberInfo.BindingField);
            }
        });

        [Fact]
        public void InitClubTabControl_ShowsTheCurrentClubByName() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.CLUB], new byte[] { 55 });
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                typeof(CoachView).GetField("allClubs", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(trainer.CoachView,
                    new System.ComponentModel.BindingList<Club> { new Club { Country = PlayerEnums.Country.Deutschland, Id = 55, ClubName = "Testverein" } });

                trainer.CoachView.InitClubTabControl();

                var value = (Label)typeof(CoachView).GetField("CurrentClubValue", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                Assert.Equal("Testverein (Deutschland)", value.Text);
            }
        });

        [Fact]
        public void InitClubTabControl_EnablesJobOfferBoxForTheOriginalBuild() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);

                trainer.CoachView.InitClubTabControl();

                var box = (GroupBox)typeof(CoachView).GetField("JobOfferBox", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                var countryInput = (ComboBox)typeof(CoachView).GetField("JobOfferCountryInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                Assert.True(box.Enabled);
                Assert.NotEmpty(((System.Collections.IEnumerable)countryInput.DataSource).Cast<object>());
            }
        });

        [Theory]
        [InlineData(JobOfferInjector.RequestResult.Requested, "Tagesabschluss")]
        [InlineData(JobOfferInjector.RequestResult.NotEmployed, "vereinslos")]
        [InlineData(JobOfferInjector.RequestResult.ChangePending, "Kein Wechsel")]
        [InlineData(JobOfferInjector.RequestResult.SameClub, "aktuelle Verein")]
        [InlineData(JobOfferInjector.RequestResult.NotSupported, "Spielversion")]
        [InlineData(JobOfferInjector.RequestResult.HookFailed, "Logdatei")]
        public void JobOfferMessage_ExplainsEveryResult(JobOfferInjector.RequestResult result, string expected)
        {
            Assert.Contains(expected, CoachView.JobOfferMessage(result, "Testverein"));
        }

        [Fact]
        public void JobOfferMessage_MentionsTheClauseOnlyWhenItWasSet()
        {
            Assert.Contains("Freigabeklausel", CoachView.JobOfferMessage(JobOfferInjector.RequestResult.Requested, "Testverein", clauseSet: true));
            Assert.DoesNotContain("Freigabeklausel", CoachView.JobOfferMessage(JobOfferInjector.RequestResult.Requested, "Testverein"));
        }

        [Fact]
        public void InitClubTabControl_WithoutCurrentClub_ShowsVereinslos() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);

                trainer.CoachView.InitClubTabControl();

                var value = (Label)typeof(CoachView).GetField("CurrentClubValue", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                Assert.Equal("vereinslos", value.Text);
            }
        });

        private static T Field<T>(CoachView view, string name) =>
            (T)typeof(CoachView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private static void SetField(CoachView view, string name, object value) =>
            typeof(CoachView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, value);

        private static System.ComponentModel.BindingList<Club> TwoGermanClubs() => new System.ComponentModel.BindingList<Club>
        {
            new Club { Country = PlayerEnums.Country.Deutschland, Id = 1, ClubName = "Aachen" },
            new Club { Country = PlayerEnums.Country.Deutschland, Id = 2, ClubName = "Bremen" },
        };

        // A build whose league tables aren't confirmed: every club passes the eligibility filter
        // (IsEligibleNextSeasonClub is null) and the job offer isn't supported.
        private static readonly PersistentLayout UnconfirmedLayout = new PersistentLayout(null, null, null, null, isVerified: true);

        [Fact]
        public void InitClubTabControl_WithACurrentClubMissingFromTheClubList_ShowsItsId() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.CLUB], new byte[] { 55 });
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                SetField(trainer.CoachView, "allClubs", new System.ComponentModel.BindingList<Club>());

                trainer.CoachView.InitClubTabControl();
                Assert.Equal("Verein 55 (Deutschland)", Field<Label>(trainer.CoachView, "CurrentClubValue").Text);

                // No club scan loaded at all.
                SetField(trainer.CoachView, "allClubs", null);
                InvokePrivate(trainer.CoachView, "UpdateCurrentClubDisplay");
                Assert.Equal("Verein 55 (Deutschland)", Field<Label>(trainer.CoachView, "CurrentClubValue").Text);
            }
        });

        [Fact]
        public void RequestJobOffer_ShowsTheOutcomeInTheStatusLine() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                fake.Memory.Layout = UnconfirmedLayout;
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();
                SetField(trainer.CoachView, "allClubs", TwoGermanClubs());
                InvokePrivate(trainer.CoachView, "PopulateJobOfferCountryCombo");

                InvokePrivate(trainer.CoachView, "RequestJobOffer");

                Assert.Equal(CoachView.JobOfferMessage(JobOfferInjector.RequestResult.NotSupported, "Aachen"), Field<Label>(trainer.CoachView, "JobOfferStatus").Text);
            }
        });

        // Germany's only league holds club 1 (Aachen) - club 2 (Bremen) isn't playing in any league.
        private static void SeedGermanLeagueWithAachenOnly(FakeModule fake)
        {
            fake.WriteModuleBytes(Settings.LeagueRangeTableOffset, new byte[] { 0, 0, 1, 0 });
            fake.WriteModuleBytes(Settings.LeagueTableOffset, BitConverter.GetBytes((short)1));
            fake.WriteModuleBytes(Settings.LeagueTableOffset + Settings.LeagueClubIdsOffset, BitConverter.GetBytes((short)1));
        }

        [Fact]
        public void RequestJobOffer_WhenTheGameTakesTheRequest_SaysWhenTheOfferComes() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                SeedGermanLeagueWithAachenOnly(fake);
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.CLUB], new byte[] { 55 });
                // The game's unpatched CALL to its offer presentation.
                uint callSite = fake.ModuleBase + Settings.JobOfferPresentCallSiteOffset;
                int rel = unchecked((int)(fake.ModuleBase + Settings.JobOfferPresentFunctionOffset - (callSite + 5)));
                fake.WriteModuleBytes(Settings.JobOfferPresentCallSiteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();
                SetField(trainer.CoachView, "allClubs", TwoGermanClubs());
                InvokePrivate(trainer.CoachView, "PopulateJobOfferCountryCombo");

                InvokePrivate(trainer.CoachView, "RequestJobOffer");

                Assert.StartsWith("Aachen bietet beim nächsten Tagesabschluss", Field<Label>(trainer.CoachView, "JobOfferStatus").Text);
            }
        });

        [Fact]
        public void RequestJobOffer_WithoutCoach_LeavesTheStatusAlone() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                InvokePrivate(trainer.CoachView, "RequestJobOffer");

                Assert.Equal("", Field<Label>(trainer.CoachView, "JobOfferStatus").Text);
            }
        });

        [Fact]
        public void PopulateNextSeasonClubCombo_DropsClubsTheSeasonChangeWouldRefuse() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                SeedGermanLeagueWithAachenOnly(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();
                SetField(trainer.CoachView, "allClubs", TwoGermanClubs());

                InvokePrivate(trainer.CoachView, "PopulateNextSeasonClubCombo", PlayerEnums.Country.Deutschland);

                Assert.Equal(new[] { "Aachen" }, ((List<Club>)Field<ComboBox>(trainer.CoachView, "NextSeasonClubInput").DataSource).Select(c => c.ClubName));
            }
        });

        // RefreshValues blanks the combos while the binding source may have nothing to read a
        // fallback value from; the empty selection is then passed on untouched.
        [Fact]
        public void EnumComboBinding_WithoutACurrentItem_LeavesTheEmptySelectionAlone() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();
                Binding binding = Field<ComboBox>(trainer.CoachView, "SeasonGoalInput").DataBindings["SelectedValue"];
                BindingSource source = Field<BindingSource>(trainer.CoachView, "bindingSource");
                source.DataSource = new List<Coach>();
                Assert.Same(source, binding.DataSource);
                Assert.Null(source.Current);
                ConvertEventArgs args = new ConvertEventArgs(null, typeof(CoachEnums.SeasonGoal));

                MethodInfo onParse = typeof(Binding).GetMethod("OnParse", BindingFlags.NonPublic | BindingFlags.Instance);

                onParse.Invoke(binding, new object[] { args });
                Assert.Null(args.Value);

                // A real selection passes through as it is.
                ConvertEventArgs selected = new ConvertEventArgs(CoachEnums.SeasonGoal.Klassenerhalt, typeof(CoachEnums.SeasonGoal));
                onParse.Invoke(binding, new object[] { selected });
                Assert.Equal(CoachEnums.SeasonGoal.Klassenerhalt, selected.Value);
            }
        });

        [Fact]
        public void PopulateJobOfferCountryCombo_KeepsTheCountryChosenBefore() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();
                ComboBox countryInput = Field<ComboBox>(trainer.CoachView, "JobOfferCountryInput");
                countryInput.SelectedValue = PlayerEnums.Country.England;

                InvokePrivate(trainer.CoachView, "PopulateJobOfferCountryCombo");

                Assert.Equal(PlayerEnums.Country.England, countryInput.SelectedValue);
            }
        });

        [Fact]
        public void PopulateJobOfferClubCombo_WithoutCoach_OffersEveryClubOfTheCountry() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                fake.Memory.Layout = UnconfirmedLayout;
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();
                SetField(trainer.CoachView, "allClubs", TwoGermanClubs());
                SetField(trainer.CoachView, "coach", null);

                InvokePrivate(trainer.CoachView, "PopulateJobOfferClubCombo");

                Assert.Equal(2, ((List<Club>)Field<ComboBox>(trainer.CoachView, "JobOfferClubInput").DataSource).Count);
            }
        });

        [Fact]
        public void PopulateNextSeasonClubCombo_WhereEligibilityIsUnknown_KeepsEveryClub() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                fake.Memory.Layout = UnconfirmedLayout;
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();
                SetField(trainer.CoachView, "allClubs", TwoGermanClubs());

                InvokePrivate(trainer.CoachView, "PopulateNextSeasonClubCombo", PlayerEnums.Country.Deutschland);

                Assert.Equal(2, ((List<Club>)Field<ComboBox>(trainer.CoachView, "NextSeasonClubInput").DataSource).Count);
            }
        });

        // A stored next-season country that's no longer an active bonus country stays selectable, so
        // the next save doesn't silently change it.
        [Fact]
        public void PopulateNextSeasonCountryCombo_WithAnInactiveStoredCountry_KeepsItSelectable() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                InvokePrivate(trainer.CoachView, "PopulateNextSeasonCountryCombo", PlayerEnums.Country.Italien);

                Assert.Equal(PlayerEnums.Country.Italien, Field<ComboBox>(trainer.CoachView, "NextSeasonCountryInput").SelectedValue);
            }
        });

        [Fact]
        public void RefreshStockCountryOptions_WithNoCoachLoadedYet_DoesNothing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                Exception thrown = Record.Exception(() => InvokePrivate(trainer.CoachView, "RefreshStockCountryOptions"));

                Assert.Null(thrown);
            }
        });

        // NextSeasonCountryInput deliberately does NOT use a DataBindings "SelectedValue" binding
        // (see the comment on its setup in InitClubTabControl for why that races with this combo's
        // own DataSource rebuild) - it's restricted to GetAvailableStockCountries(), the same
        // main+bonus-country set the stock combos use, and written directly in the change handler.
        [Fact]
        public void PopulateNextSeasonCountryCombo_RestrictsChoicesToMainAndBonusCountries() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                var countryInput = (ComboBox)typeof(CoachView).GetField("NextSeasonCountryInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                var expected = (List<PlayerEnums.Country>)InvokePrivate(trainer.CoachView, "GetAvailableStockCountries");

                List<PlayerEnums.Country> actual = ((System.Collections.IEnumerable)countryInput.DataSource)
                    .Cast<object>()
                    .Select(item => (PlayerEnums.Country)item.GetType().GetProperty("Value").GetValue(item))
                    .ToList();

                // "Kein Wechsel" (country 0) always comes first, then the main + bonus countries.
                Assert.Equal(PlayerEnums.Country.Sonstige, actual[0]);
                Assert.Equal(new[] { PlayerEnums.Country.Sonstige }.Concat(expected).Distinct().OrderBy(c => c), actual.OrderBy(c => c));
                Assert.DoesNotContain(PlayerEnums.Country.Frankreich, actual);
                Assert.Null(countryInput.DataBindings["SelectedValue"]);
            }
        });

        // Country 0 for next season means "no change", and the club scan's country-0 entries are
        // unused slots with garbage names - so instead of those, the club combo offers a single
        // placeholder that keeps ClubIdNextSeason at 0.
        [Fact]
        public void PopulateNextSeasonCountryCombo_WithNoChange_OffersOnlyThePlaceholderClub() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                InvokePrivate(trainer.CoachView, "PopulateNextSeasonCountryCombo", PlayerEnums.Country.Sonstige);

                var countryInput = (ComboBox)typeof(CoachView).GetField("NextSeasonCountryInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                var clubInput = (ComboBox)typeof(CoachView).GetField("NextSeasonClubInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                object firstCountry = ((System.Collections.IEnumerable)countryInput.DataSource).Cast<object>().First();
                List<Club> clubs = ((System.Collections.IEnumerable)clubInput.DataSource).Cast<Club>().ToList();

                Assert.Equal("Kein Wechsel", firstCountry.GetType().GetProperty("Text").GetValue(firstCountry));
                Assert.Single(clubs);
                Assert.Equal((byte)0, clubs[0].Id);
                Assert.Equal("–", clubs[0].ClubName);
            }
        });

        // Direct write, no DataBindings round-trip - so unlike the (unmaterializable) SelectedValue-
        // binding tests elsewhere in this file, calling the handler explicitly is enough to exercise
        // the "picked a real country" branch without needing a created window handle.
        [Fact]
        public void PopulateNextSeasonCountryCombo_WithAKnownSelection_WritesCountryNextSeasonDirectly() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                InvokePrivate(trainer.CoachView, "PopulateNextSeasonCountryCombo", PlayerEnums.Country.England);

                var coach = (Coach)typeof(CoachView).GetField("coach", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                Assert.Equal(PlayerEnums.Country.England, coach.CountryNextSeason);
            }
        });

        // Mirrors the stock club combo's own change handler test below (ClubComboSelectedIndexChanged_
        // WithCoachClearedAfterwards_DoesNothingWithoutThrowing) - NextSeasonClubInput writes
        // Coach.ClubIdNextSeason the same way, guarded by the same coach != null check.
        [Fact]
        public void NextSeasonClubComboSelectedIndexChanged_WithCoachClearedAfterwards_DoesNothingWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                typeof(CoachView).GetField("coach", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(trainer.CoachView, null);

                var clubInput = (ComboBox)typeof(CoachView).GetField("NextSeasonClubInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                MethodInfo onSelectedIndexChanged = typeof(ComboBox).GetMethod("OnSelectedIndexChanged", BindingFlags.NonPublic | BindingFlags.Instance);

                Exception thrown = Record.Exception(() => onSelectedIndexChanged.Invoke(clubInput, new object[] { EventArgs.Empty }));

                Assert.Null(thrown);
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

        // One checkbox per league country except the main country. SeedLeagueAndStock leaves the
        // game's league tables empty, so only the active England can be switched; the inactive
        // countries are disabled with the reason next to them.
        [Fact]
        public void RebuildBonusCountryControls_ShowsOneCheckboxPerLeagueCountryWithTheBlockReason() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                var checks = (List<CheckBox>)typeof(CoachView).GetField("bonusCountryChecks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                var box = (GroupBox)typeof(CoachView).GetField("AdditionalCountriesBox", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);

                Assert.Equal(new[] { "England", "Frankreich", "Italien", "Spanien" }, checks.Select(c => c.Text));
                Assert.True(checks[0].Checked);
                Assert.True(checks[0].Enabled);
                Assert.All(checks.Skip(1), c => Assert.False(c.Enabled || c.Checked));
                Assert.Contains(box.Controls.Cast<Control>(), c => c is Label && c.Text.Contains("Keine Vereine"));
            }
        });

        // Unticking a country takes it out of the stock / next-season / job-offer country choices
        // right away, before anything is saved.
        [Fact]
        public void BonusCountryCheckbox_Unticked_MarksTheCountryInactiveAndDropsItFromTheChoices() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                var checks = (List<CheckBox>)typeof(CoachView).GetField("bonusCountryChecks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                checks[0].Checked = false;

                var league = (LeagueSettings)typeof(CoachView).GetField("league", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                Assert.Empty(league.ActiveBonusCountries);
                Assert.DoesNotContain(PlayerEnums.Country.England, (List<PlayerEnums.Country>)InvokePrivate(trainer.CoachView, "GetAvailableStockCountries"));
            }
        });

        [Fact]
        public void SaveBtnClick_WithASkippedBonusCountryChange_ShowsTheReasonInTheBox() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                // Switch on a country without league data behind the disabled checkbox's back.
                var league = (LeagueSettings)typeof(CoachView).GetField("league", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                league.BonusCountries.Single(c => c.Country == PlayerEnums.Country.Italien).IsActive = true;
                InvokePrivate(trainer.CoachView, "SaveBtn_Click", null, EventArgs.Empty);

                var box = (GroupBox)typeof(CoachView).GetField("AdditionalCountriesBox", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                // Italien's reason label (fourth row's neighbour) now starts with "Nicht übernommen".
                Assert.Contains(box.Controls.Cast<Control>(), c => c is Label && c.Text.StartsWith("Nicht übernommen: Keine Vereine"));
            }
        });

        // A stock slot without shares holds leftover country/club data; it shows "–" in both combos
        // and keeps the stored values untouched, while a real position still shows its country.
        [Fact]
        public void StockSlotWithoutShares_ShowsPlaceholdersAndLeavesTheModelAlone() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedLeagueAndStock(fake);
                // Slot 1: no shares, leftover country Italien and club 7.
                string country1 = Tools.SumHex(new[] { AddressPresets.COACH[CoachEnums.AddressKey.STOCK_COUNTRY], "10" });
                string club1 = Tools.SumHex(new[] { AddressPresets.COACH[CoachEnums.AddressKey.STOCK_CLUB], "10" });
                CoachMemory.WriteWorkingCopy(fake, country1, new byte[] { (byte)PlayerEnums.Country.Italien });
                CoachMemory.WriteWorkingCopy(fake, club1, new byte[] { 7 });
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                var countryCombos = (ComboBox[])typeof(CoachView).GetField("stockCountryCombos", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                var clubCombos = (ComboBox[])typeof(CoachView).GetField("stockClubCombos", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);
                var coach = (Coach)typeof(CoachView).GetField("coach", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);

                Assert.Equal(CoachView.EmptyStockText, countryCombos[1].SelectedItem);
                Assert.Equal(CoachView.EmptyStockText, clubCombos[1].SelectedItem);
                Assert.Single(countryCombos[1].Items);
                Assert.Equal(PlayerEnums.Country.Italien, coach.Stocks[1].Country);
                Assert.Equal((byte)7, coach.Stocks[1].ClubId);
                Assert.Null(countryCombos[1].DataSource);
                Assert.NotNull(countryCombos[0].DataSource);

                // Refreshing again must still work with the placeholder in place.
                Assert.Null(Record.Exception(() => trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0)));
                Assert.Equal(CoachView.EmptyStockText, countryCombos[1].SelectedItem);
            }
        });

        [Fact]
        public void BonusCountryNote_ShowsTheReasonAndMarksSkippedChanges()
        {
            BonusCountry free = new BonusCountry(PlayerEnums.Country.England, true, 1, null);
            BonusCountry blocked = new BonusCountry(PlayerEnums.Country.Italien, false, 3, "Grund");

            Assert.Null(CoachView.BonusCountryNote(free, skipped: false));
            Assert.Equal("Grund", CoachView.BonusCountryNote(blocked, skipped: false));
            Assert.Equal("Nicht übernommen.", CoachView.BonusCountryNote(free, skipped: true));
            Assert.Equal("Nicht übernommen: Grund", CoachView.BonusCountryNote(blocked, skipped: true));
        }

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

        // Changing the main country mid-game isn't supported (its leagues are loaded at the start).
        [Fact]
        public void MainCountryInput_IsShownOnly() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                trainer.CoachView.InitClubTabControl();
                var mainCountryInput = (ComboBox)typeof(CoachView).GetField("MainCountryInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer.CoachView);

                Assert.False(mainCountryInput.Enabled);
                Assert.Equal(DataSourceUpdateMode.Never, mainCountryInput.DataBindings["SelectedValue"].DataSourceUpdateMode);
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
                    WriteField(league[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT], "", new byte[] { 1, 0x00 });
                    WriteField(league[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], "", new byte[] { (byte)PlayerEnums.Country.England, 0x00 });

                    Addresses coach = AddressPresets.COACH;
                    WriteField(coach[CoachEnums.AddressKey.STOCK_COUNTRY], "", new byte[] { (byte)PlayerEnums.Country.Deutschland });
                    WriteField(coach[CoachEnums.AddressKey.STOCK_SHARES], "", BitConverter.GetBytes((ushort)10));
                    // STOCK_CLUB is left at its default 0 - matched below by giving the seeded club Id 0 too.
                    // Trainers are read from their fixed working copy on the original build, and a slot
                    // without shares shows only a placeholder - so the position goes there as well.
                    CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.STOCK_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
                    CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.STOCK_SHARES], BitConverter.GetBytes((ushort)10));

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
