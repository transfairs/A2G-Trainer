using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the savegame editor's window parts: opening, browsing, searching, saving and
    /// closing a savegame from the main window, and the property-grid converters and editors.
    /// </summary>
    public class SaveGameViewTests : IDisposable
    {
        private const uint Base = SaveGameFile.ModuleBase;
        private readonly string directory = Path.Combine(Path.GetTempPath(), "A2G-SaveGameView-" + Guid.NewGuid().ToString("N"));

        public SaveGameViewTests()
        {
            Directory.CreateDirectory(this.directory);
        }

        public void Dispose() => Directory.Delete(this.directory, recursive: true);

        private static uint PlayerRecord(ushort id) => Base + Settings.PlayerRecordTableOffset + id * Settings.PlayerRecordStride;
        private static uint ClubRecord(int index) => Base + Settings.ClubRecordTableOffset + (uint)index * Settings.ClubRecordStride;

        // Germany as main country with two clubs; Werder has two pros and one amateur.
        private string WriteSaveGame()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            file.WriteInt16(Base + Settings.AgeReferenceYearOffset, 2001);
            file.WriteInt16(Base + Settings.DaysPerYearOffset, 365);
            file.WriteInt16(Base + Settings.ActiveTrainerCountOffset, 1);
            file.WriteInt16(Base + Settings.BonusCountryCountOffset + 2, (short)PlayerEnums.Country.Deutschland);
            file.WriteByte(Base + Settings.LeagueSystemByCountryOffset + (byte)PlayerEnums.Country.Deutschland, 0);
            foreach (byte other in new[] { PlayerEnums.Country.England, PlayerEnums.Country.Frankreich, PlayerEnums.Country.Italien, PlayerEnums.Country.Spanien }.Select(c => (byte)c))
                file.WriteByte(Base + Settings.LeagueSystemByCountryOffset + other, 9);
            string[] clubs = { "Werder Bremen", "Bayern München" };
            for (int i = 0; i < clubs.Length; i++)
            {
                file.WriteString(ClubRecord(i), clubs[i], 20);
                file.WriteByte(ClubRecord(i) + 0xF4, (byte)i);
                file.WriteByte(ClubRecord(i) + 0xF8, (byte)PlayerEnums.Country.Deutschland);
            }
            file.WriteInt16(ClubRecord(0) + 0xDE, 2);
            file.WriteUInt16(ClubRecord(0) + 0x570, 1);
            file.WriteUInt16(ClubRecord(0) + 0x572, 2);
            file.WriteByte(ClubRecord(0) + 0x5BD, 1);
            file.WriteUInt16(ClubRecord(0) + 0x5BE, 3);
            string[][] players = { new[] { "Frank", "Rost" }, new[] { "Frank", "Baumann" }, new[] { "Tim", "Borowski" } };
            for (ushort id = 1; id <= players.Length; id++)
            {
                file.WriteUInt16(PlayerRecord(id), (ushort)file.AddName(players[id - 1][0]));
                file.WriteUInt16(PlayerRecord(id) + 2, (ushort)file.AddName(players[id - 1][1]));
            }
            file.WriteString(Base + Settings.CoachRecordTableOffset + Settings.CoachRecordStride, "Robin", 10);

            string path = Path.Combine(this.directory, "Test.gld");
            File.WriteAllBytes(path, file.Serialize());
            return path;
        }

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);

        private static void Call(object owner, string name, params object[] arguments) =>
            owner.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, arguments);

        private static TreeNode Node(TreeNodeCollection nodes, string text) => nodes.Cast<TreeNode>().First(n => n.Text == text);

        // The views are tested on their own: every main window a test creates stays in memory for
        // the rest of the (32-bit) test run, so only the menu flows below use one.
        private SaveGameView OpenView(string path)
        {
            SaveGameView view = new SaveGameView();
            view.ShowSaveGame(SaveGameEditor.Open(path));
            IntPtr handle = view.Tree.Handle;
            return view;
        }

        [Fact]
        public void View_ShowsTheTree() => StaThread.Run(() =>
        {
            using (SaveGameView view = this.OpenView(this.WriteSaveGame()))
            {
                TreeView tree = view.Tree;
                Assert.Equal(new[] { "Spielstand", "Manager", "Deutschland" }, tree.Nodes.Cast<TreeNode>().Select(n => n.Text));
                Assert.Equal(new[] { "Robin" }, Node(tree.Nodes, "Manager").Nodes.Cast<TreeNode>().Select(n => n.Text));
                Assert.Equal(new[] { "Bayern München", "Werder Bremen" }, Node(tree.Nodes, "Deutschland").Nodes.Cast<TreeNode>().Select(n => n.Text));
                Assert.IsType<SaveGameOverview>(view.SelectedEntry);
            }
        });

        [Fact]
        public void View_ClubNodeLoadsItsSquadWhenOpened() => StaThread.Run(() =>
        {
            using (SaveGameView view = this.OpenView(this.WriteSaveGame()))
            {
                TreeNode werder = Node(Node(view.Tree.Nodes, "Deutschland").Nodes, "Werder Bremen");
                Assert.Single(werder.Nodes);

                werder.Expand();
                werder.Collapse();
                werder.Expand();

                Assert.Equal(new[] { "Frank Rost", "Frank Baumann", "Amateure" }, werder.Nodes.Cast<TreeNode>().Select(n => n.Text));
                Assert.Equal("Tim Borowski", werder.Nodes[2].Nodes[0].Text);
                view.Tree.SelectedNode = werder.Nodes[0];
                Assert.Equal((ushort)1, Assert.IsType<SaveGamePlayer>(view.SelectedEntry).Id);
                view.Tree.SelectedNode = werder;
                Assert.IsType<SaveGameClub>(view.SelectedEntry);
            }
        });

        [Fact]
        public void View_SearchListsMatchingPlayers() => StaThread.Run(() =>
        {
            using (SaveGameView view = this.OpenView(this.WriteSaveGame()))
            {
                TextBox input = Field<TextBox>(view, "searchInput");

                input.Text = "frank";
                view.Search();
                TreeNode search = view.Tree.Nodes[0];
                Assert.Equal("Suche (2)", search.Text);
                Assert.Equal("Frank Rost", ((SaveGamePlayer)view.SelectedEntry).ToString());

                input.Text = "Borowski";
                Call(view, "SearchInput_KeyDown", input, new KeyEventArgs(Keys.Enter));
                Assert.Same(search, view.Tree.Nodes[0]);
                Assert.Equal("Suche (1)", search.Text);

                input.Text = "xyz";
                Call(view, "SearchInput_KeyDown", input, new KeyEventArgs(Keys.A));
                Assert.Equal("Suche (1)", search.Text);
                Call(Field<Button>(view, "searchButton"), "OnClick", EventArgs.Empty);
                Assert.Equal("Suche (0)", search.Text);
            }
        });

        [Fact]
        public void View_SearchWithoutSaveGameDoesNothing() => StaThread.Run(() =>
        {
            using (SaveGameView view = new SaveGameView())
            {
                view.Search();
                Assert.Empty(view.Tree.Nodes);
            }
        });

        [Fact]
        public void View_EditRenamesNodesAndReportsTheChange() => StaThread.Run(() =>
        {
            using (SaveGameView view = this.OpenView(this.WriteSaveGame()))
            {
                int modified = 0;
                view.Modified += (s, e) => modified++;
                TreeNode werder = Node(Node(view.Tree.Nodes, "Deutschland").Nodes, "Werder Bremen");
                werder.Expand();
                Field<TextBox>(view, "searchInput").Text = "Rost";
                view.Search();
                SaveGamePlayer player = (SaveGamePlayer)view.SelectedEntry;

                player.Lastname = "Reinke";
                Call(view, "Grid_PropertyValueChanged", null, null);

                Assert.Equal("Frank Reinke", werder.Nodes[0].Text);
                Assert.Equal("Frank Reinke", view.Tree.Nodes[0].Nodes[0].Text);
                Assert.Equal(1, modified);

                view.Tree.SelectedNode = view.Tree.Nodes[1];
                Call(view, "Grid_PropertyValueChanged", null, null);
                Assert.Equal("Spielstand", view.Tree.Nodes[1].Text);

                view.Clear();
                Assert.Null(view.Editor);
                Assert.Null(view.SelectedEntry);
                Assert.Empty(view.Tree.Nodes);
            }
        });

        [Fact]
        public void Trainer_OpensEditsAndSavesASaveGame() => StaThread.Run(() =>
        {
            string path = this.WriteSaveGame();
            using (Trainer trainer = new Trainer())
            {
                ToolStripMenuItem save = Field<ToolStripMenuItem>(trainer, "spielstandSpeichernToolStripMenuItem");
                ToolStripMenuItem close = Field<ToolStripMenuItem>(trainer, "spielstandSchliessenToolStripMenuItem");
                Call(trainer, "SaveGameToolStripMenuItem_DropDownOpening", null, EventArgs.Empty);
                Assert.False(save.Enabled);
                Assert.Null(trainer.GetType().GetMethod("SaveGameFolder", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trainer, null));

                Assert.True(trainer.OpenSaveGame(path));
                Assert.Equal("A2G - Trainer :: Spielstand Test.gld", trainer.Text);
                Call(trainer, "SaveGameToolStripMenuItem_DropDownOpening", null, EventArgs.Empty);
                Assert.True(save.Enabled);
                Assert.True(close.Enabled);

                trainer.SaveGameView.Editor.Player(2).Level = 9;
                Call(trainer.SaveGameView, "Grid_PropertyValueChanged", null, null);
                Assert.Equal("A2G - Trainer :: Spielstand Test.gld *", trainer.Text);

                Call(trainer, "SaveSaveGameToolStripMenuItem_Click", null, EventArgs.Empty);
                Assert.Equal("A2G - Trainer :: Spielstand Test.gld", trainer.Text);
                Assert.True(File.Exists(path + ".bak"));
                Assert.Equal(9, SaveGameEditor.Open(path).Player(2).Level);

                string copy = Path.Combine(this.directory, "Kopie.gld");
                Assert.True(trainer.SaveSaveGame(copy));
                Assert.False(File.Exists(copy + ".bak"));
                Assert.Equal("A2G - Trainer :: Spielstand Kopie.gld", trainer.Text);

                // Nothing changed since: closing doesn't ask.
                trainer.ShowMessage = (text, caption, buttons, icon) => throw new InvalidOperationException("asked");
                Call(trainer, "CloseSaveGameToolStripMenuItem_Click", null, EventArgs.Empty);
                Assert.Null(trainer.SaveGameView.Editor);
                Assert.Equal("A2G - Trainer :: Eigene Mannschaft", trainer.Text);
            }
        });

        [Fact]
        public void Trainer_ReportsFilesItCannotOpenOrSave() => StaThread.Run(() =>
        {
            string broken = Path.Combine(this.directory, "kaputt.gld");
            File.WriteAllBytes(broken, new byte[] { 1, 2, 3 });
            string path = this.WriteSaveGame();
            using (Trainer trainer = new Trainer())
            {
                List<string> messages = new List<string>();
                trainer.ShowMessage = (text, caption, buttons, icon) => { messages.Add(caption); return DialogResult.OK; };

                Assert.False(trainer.SaveSaveGame(null));
                Assert.False(trainer.OpenSaveGame(broken));
                Assert.False(trainer.OpenSaveGame(Path.Combine(this.directory, "gibtsnicht.gld")));
                Assert.Null(trainer.SaveGameView.Editor);

                trainer.OpenSaveGame(path);
                Assert.False(trainer.SaveSaveGame(Path.Combine(this.directory, "fehlt", "x.gld")));
                Assert.Equal(new[] { "Spielstand öffnen", "Spielstand öffnen", "Spielstand speichern" }, messages);
            }
        });

        [Fact]
        public void Trainer_AsksAboutUnsavedChanges() => StaThread.Run(() =>
        {
            string path = this.WriteSaveGame();
            using (Trainer trainer = new Trainer())
            {
                DialogResult answer = DialogResult.Cancel;
                int asked = 0;
                trainer.ShowMessage = (text, caption, buttons, icon) => { asked++; return answer; };
                void OpenAndChange(byte form)
                {
                    trainer.OpenSaveGame(path);
                    trainer.SaveGameView.Editor.Player(1).Form = form;
                }

                // Cancel: neither closing the savegame, nor the window, nor opening another goes ahead.
                OpenAndChange(5);
                Call(trainer, "CloseSaveGameToolStripMenuItem_Click", null, EventArgs.Empty);
                Assert.NotNull(trainer.SaveGameView.Editor);
                FormClosingEventArgs closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                Call(trainer, "Trainer_FormClosing", trainer, closing);
                Assert.True(closing.Cancel);
                Call(trainer, "OpenSaveGameToolStripMenuItem_Click", null, EventArgs.Empty);
                Assert.Equal(3, asked);

                // No: closes without saving.
                answer = DialogResult.No;
                Call(trainer, "CloseSaveGameToolStripMenuItem_Click", null, EventArgs.Empty);
                Assert.Null(trainer.SaveGameView.Editor);
                Assert.NotEqual(5, SaveGameEditor.Open(path).Player(1).Form);

                // Yes: saves first.
                OpenAndChange(6);
                answer = DialogResult.Yes;
                closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                Call(trainer, "Trainer_FormClosing", trainer, closing);
                Assert.False(closing.Cancel);
                Assert.Equal(6, SaveGameEditor.Open(path).Player(1).Form);
            }
        });

        [Fact]
        public void DescriptionEnumConverter_UsesDescriptions()
        {
            DescriptionEnumConverter converter = new DescriptionEnumConverter(typeof(CoachEnums.Difficulty));

            Assert.Equal("Leicht & Locker", converter.ConvertTo(null, null, CoachEnums.Difficulty.LeichtUndLocker, typeof(string)));
            Assert.Equal(CoachEnums.Difficulty.LeichtUndLocker, converter.ConvertFrom(null, null, "Leicht & Locker"));
            Assert.Equal((CoachEnums.Difficulty)99, converter.ConvertFrom(null, System.Globalization.CultureInfo.InvariantCulture, "99"));
            Assert.Equal("99", converter.ConvertTo(null, null, (CoachEnums.Difficulty)99, typeof(string)));
            Assert.True(converter.GetStandardValuesExclusive(null));
            Assert.Equal(Enum.GetValues(typeof(CoachEnums.Difficulty)).Length, converter.GetStandardValues(null).Count);

            DescriptionEnumConverter signed = new DescriptionEnumConverter(typeof(CoachEnums.VehiclePerk));
            Assert.Equal(CoachEnums.VehiclePerk.Keines, signed.ConvertFrom(null, null, "Keines"));
            Assert.Contains(CoachEnums.VehiclePerk.Keines, signed.GetStandardValues(null).Cast<CoachEnums.VehiclePerk>());
        }

        [Fact]
        public void FlagsEnumConverter_ListsTheGroupThatFitsThePosition()
        {
            SaveGameEditor editor = SaveGameEditor.For(SaveGameFile.CreateBlank());
            SaveGamePlayer keeper = editor.Player(1);
            keeper.Position = PlayerEnums.Position.TO;
            SaveGamePlayer striker = editor.Player(2);
            striker.Position = PlayerEnums.Position.S;
            FlagsEnumConverter converter = new FlagsEnumConverter(typeof(PlayerEnums.Skills));
            PlayerEnums.Skills skills = (PlayerEnums.Skills)0x3;

            Assert.Equal("Elfmetertoeter, StarkeReflexe", converter.ConvertTo(new Context(keeper), null, skills, typeof(string)));
            Assert.Equal("Kopfball, Zweikampf", converter.ConvertTo(new Context(striker), null, skills, typeof(string)));
            Assert.Equal("keine", converter.ConvertTo(null, null, PlayerEnums.Skills.None, typeof(string)));
            Assert.False(converter.CanConvertFrom(null, typeof(string)));
            Assert.False(converter.GetStandardValuesSupported(null));
        }

        [Fact]
        public void FlagsEnumEditor_ChecksTheSetBitsAndKeepsTheOthers() => StaThread.Run(() =>
        {
            FlagsEnumEditor editor = new FlagsEnumEditor();
            EditorService service = new EditorService(list => list.SetItemChecked(0, false));
            SaveGamePlayer striker = SaveGameEditor.For(SaveGameFile.CreateBlank()).Player(2);

            object result = editor.EditValue(new Context(striker), service, (PlayerEnums.Skills)0x8003);

            Assert.Equal(UITypeEditorEditStyle.DropDown, editor.GetEditStyle(null));
            Assert.Equal(11, service.Items.Count);
            Assert.True(service.CheckedBefore[1]);
            Assert.False(service.CheckedBefore[2]);
            // Kopfball cleared; Zweikampf kept; bit 15 (no name) kept.
            Assert.Equal((PlayerEnums.Skills)0x8002, result);
            Assert.Equal(PlayerEnums.Unhappy.None, editor.EditValue(null, null, PlayerEnums.Unhappy.None));
        });

        [Fact]
        public void YesNoConverter_SaysJaAndNein()
        {
            YesNoConverter converter = new YesNoConverter();

            Assert.Equal("Ja", converter.ConvertTo(null, null, true, typeof(string)));
            Assert.Equal("Nein", converter.ConvertTo(null, null, false, typeof(string)));
            Assert.Equal(true, converter.ConvertFrom(null, null, " ja"));
            Assert.Equal(false, converter.ConvertFrom(null, null, "Nein"));
            Assert.Equal(true, converter.ConvertFrom(null, null, "True"));
        }

        [Fact]
        public void SaveGameCategory_SortsInTheEditorsOrder()
        {
            string[] names = { "Aktien", "Allgemein", "Stadion", "Verein", "Unbekannt" };
            List<string> sorted = names.Select(n => new SaveGameCategoryAttribute(n).Category).OrderBy(c => c, StringComparer.Ordinal).ToList();

            Assert.Equal(new[] { "Allgemein", "Verein", "Stadion", "Aktien", "Unbekannt" }, sorted.Select(c => c.TrimStart('\t')));
        }

        private sealed class Context : ITypeDescriptorContext
        {
            public Context(object instance) { this.Instance = instance; }
            public IContainer Container => null;
            public object Instance { get; }
            public PropertyDescriptor PropertyDescriptor => null;
            public object GetService(Type serviceType) => null;
            public void OnComponentChanged() { }
            public bool OnComponentChanging() => true;
        }

        private sealed class EditorService : IWindowsFormsEditorService, IServiceProvider
        {
            private readonly Action<CheckedListBox> pick;
            public EditorService(Action<CheckedListBox> pick) { this.pick = pick; }
            public List<string> Items { get; } = new List<string>();
            public List<bool> CheckedBefore { get; } = new List<bool>();
            public object GetService(Type serviceType) => serviceType == typeof(IWindowsFormsEditorService) ? this : null;
            public void CloseDropDown() { }
            public DialogResult ShowDialog(Form dialog) => DialogResult.OK;
            public void DropDownControl(Control control)
            {
                CheckedListBox list = (CheckedListBox)control;
                for (int i = 0; i < list.Items.Count; i++)
                {
                    this.Items.Add((string)list.Items[i]);
                    this.CheckedBefore.Add(list.GetItemChecked(i));
                }
                this.pick(list);
            }
        }
    }
}
