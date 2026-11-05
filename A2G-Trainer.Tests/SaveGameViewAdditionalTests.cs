using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Further tests for the savegame editor's view and its property-grid converters: values the
    /// converters leave to their base classes, the flags editor without a context, a search that
    /// hits the limit, and selection/edit events without a node or listener.
    /// </summary>
    public class SaveGameViewAdditionalTests
    {
        private const uint Base = SaveGameFile.ModuleBase;

        private static uint PlayerRecord(ushort id) => Base + Settings.PlayerRecordTableOffset + id * Settings.PlayerRecordStride;

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);

        private static void Call(object owner, Type type, string name, params object[] arguments) =>
            type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, arguments);

        [Fact]
        public void DescriptionEnumConverter_LeavesOtherValuesToTheEnumConverter()
        {
            DescriptionEnumConverter converter = new DescriptionEnumConverter(typeof(CoachEnums.Difficulty));

            // Not a string wanted, no value, or a value of the underlying type.
            Assert.Throws<NotSupportedException>(() => converter.ConvertTo(null, null, CoachEnums.Difficulty.LeichtUndLocker, typeof(int)));
            Assert.Equal(string.Empty, converter.ConvertTo(null, null, null, typeof(string)));
            object underlying = Convert.ChangeType((int)CoachEnums.Difficulty.LeichtUndLocker, Enum.GetUnderlyingType(typeof(CoachEnums.Difficulty)));
            Assert.Equal(nameof(CoachEnums.Difficulty.LeichtUndLocker), converter.ConvertTo(null, null, underlying, typeof(string)));

            // Neither a description nor a number: the enum converter reads the member name, or fails.
            Assert.Equal(CoachEnums.Difficulty.LeichtUndLocker, converter.ConvertFrom(null, null, nameof(CoachEnums.Difficulty.LeichtUndLocker)));
            Assert.Throws<FormatException>(() => converter.ConvertFrom(null, null, "gibt es nicht"));
            Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(null, null, 3));
        }

        [Fact]
        public void FlagsEnumConverter_LeavesOtherValuesToTheEnumConverter()
        {
            FlagsEnumConverter converter = new FlagsEnumConverter(typeof(PlayerEnums.Skills));

            Assert.Throws<NotSupportedException>(() => converter.ConvertTo(null, null, (PlayerEnums.Skills)0x3, typeof(int)));
            Assert.Equal(string.Empty, converter.ConvertTo(null, null, null, typeof(string)));
            // A raw number: the enum converter's member name (both groups name bit 0).
            Assert.Contains(converter.ConvertTo(null, null, (ushort)1, typeof(string)), new object[] { "Elfmetertoeter", "Kopfball" });
        }

        [Fact]
        public void FlagsEnumEditor_WithoutContext_UsesTheOutfieldGroup() => StaThread.Run(() =>
        {
            FlagsEnumEditor editor = new FlagsEnumEditor();
            List<string> items = new List<string>();
            EditorService service = new EditorService(list =>
            {
                items.AddRange(list.Items.Cast<string>());
                list.SetItemChecked(2, true);
            });

            object result = editor.EditValue(null, service, (PlayerEnums.Skills)0x1);

            List<string> outfield = new FlagsEnumConverter(typeof(PlayerEnums.Skills)).ConvertTo(null, null, (PlayerEnums.Skills)0x7, typeof(string)).ToString().Split(new[] { ", " }, StringSplitOptions.None).ToList();
            Assert.Equal(outfield, items.Take(3));
            Assert.Equal((PlayerEnums.Skills)0x5, result);
        });

        [Fact]
        public void YesNoConverter_LeavesOtherValuesToTheBooleanConverter()
        {
            YesNoConverter converter = new YesNoConverter();

            Assert.Equal("Nein-Text", converter.ConvertTo(null, null, "Nein-Text", typeof(string)));
            Assert.Equal(string.Empty, converter.ConvertTo(null, null, null, typeof(string)));
            Assert.Throws<NotSupportedException>(() => converter.ConvertTo(null, null, true, typeof(int)));
        }

        [Fact]
        public void View_SearchAtTheLimit_ShowsThePlus() => StaThread.Run(() =>
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            ushort first = (ushort)file.AddName("Hans");
            ushort last = (ushort)file.AddName("Meier");
            for (ushort id = 1; id <= 201; id++)
            {
                file.WriteUInt16(PlayerRecord(id), first);
                file.WriteUInt16(PlayerRecord(id) + 2, last);
            }
            using (SaveGameView view = new SaveGameView())
            {
                view.ShowSaveGame(SaveGameEditor.For(file));
                Field<TextBox>(view, "searchInput").Text = "meier";

                view.Search();

                TreeNode search = view.Tree.Nodes[0];
                Assert.Equal("Suche (200+)", search.Text);
                Assert.Equal(200, search.Nodes.Count);
                Assert.Equal((ushort)1, Assert.IsType<SaveGamePlayer>(view.SelectedEntry).Id);
            }
        });

        [Fact]
        public void View_SelectionWithoutNode_EmptiesTheGrid() => StaThread.Run(() =>
        {
            using (SaveGameView view = new SaveGameView())
            {
                view.ShowSaveGame(SaveGameEditor.For(SaveGameFile.CreateBlank()));
                Assert.IsType<SaveGameOverview>(view.SelectedEntry);

                Call(view.Tree, typeof(TreeView), "OnAfterSelect", new TreeViewEventArgs(null));

                Assert.Null(view.SelectedEntry);
            }
        });

        [Fact]
        public void View_EditWithoutListener_StillRenamesTheNodes() => StaThread.Run(() =>
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            file.WriteInt16(Base + Settings.ActiveTrainerCountOffset, 1);
            using (SaveGameView view = new SaveGameView())
            {
                SaveGameEditor editor = SaveGameEditor.For(file);
                view.ShowSaveGame(editor);
                TreeNode coachNode = view.Tree.Nodes[1].Nodes[0];
                view.Tree.SelectedNode = coachNode;
                Field<PropertyGrid>(view, "grid").SelectedObject = coachNode.Tag;
                editor.Coaches[0].Firstname = "Otto";

                Call(view, typeof(SaveGameView), "Grid_PropertyValueChanged", null, null);

                Assert.Equal(editor.Coaches[0].ToString(), coachNode.Text);
                Assert.StartsWith("Otto", coachNode.Text);
            }
        });

        private sealed class EditorService : IWindowsFormsEditorService, IServiceProvider
        {
            private readonly Action<CheckedListBox> pick;
            public EditorService(Action<CheckedListBox> pick) { this.pick = pick; }
            public object GetService(Type serviceType) => serviceType == typeof(IWindowsFormsEditorService) ? this : null;
            public void CloseDropDown() { }
            public DialogResult ShowDialog(Form dialog) => DialogResult.OK;
            public void DropDownControl(Control control) => this.pick((CheckedListBox)control);
        }
    }
}
