using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// The savegame editor: a tree of the opened savegame (overview, trainers, the clubs of each
    /// league country with their squads, player search results) and a property grid for the
    /// selected entry. Every change goes straight into the file image (see <see cref="SaveGameEntry"/>);
    /// the main window saves it.
    /// </summary>
    public class SaveGameView : UserControl
    {
        private const int SearchLimit = 200;
        // Placeholder child of a club node until its squad is loaded.
        private const string LazyMarker = "…";

        private readonly TextBox searchInput;
        private readonly Button searchButton;
        private readonly TreeView tree;
        private readonly PropertyGrid grid;
        private TreeNode searchNode;

        /// <summary>Creates the (empty) view.</summary>
        public SaveGameView()
        {
            this.Name = "SaveGameView";
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25F);

            this.searchInput = new TextBox { Name = "SearchInput", Location = new Point(8, 9), Size = new Size(166, 20) };
            this.searchButton = new Button { Name = "SearchButton", Text = "Suchen", Location = new Point(178, 8), Size = new Size(56, 22) };
            this.tree = new TreeView { Name = "SaveGameTree", Location = new Point(8, 36), Size = new Size(226, 379), HideSelection = false };
            this.grid = new PropertyGrid
            {
                Name = "SaveGameGrid",
                Location = new Point(242, 8),
                Size = new Size(380, 407),
                HelpVisible = false,
                ToolbarVisible = false,
                PropertySort = PropertySort.Categorized
            };

            this.Controls.Add(this.searchInput);
            this.Controls.Add(this.searchButton);
            this.Controls.Add(this.tree);
            this.Controls.Add(this.grid);

            this.searchButton.Click += (s, e) => this.Search();
            this.searchInput.KeyDown += this.SearchInput_KeyDown;
            this.tree.BeforeExpand += this.Tree_BeforeExpand;
            this.tree.AfterSelect += (s, e) => this.grid.SelectedObject = e.Node?.Tag as SaveGameEntry;
            this.grid.PropertyValueChanged += this.Grid_PropertyValueChanged;
            this.SetEnabled(false);
        }

        /// <summary>Raised after a value in the savegame was changed.</summary>
        public event EventHandler Modified;

        internal SaveGameEditor Editor { get; private set; }

        /// <summary>The entry shown in the property grid, or null.</summary>
        internal object SelectedEntry => this.grid.SelectedObject;

        internal TreeView Tree => this.tree;

        /// <summary>Shows an opened savegame.</summary>
        internal void ShowSaveGame(SaveGameEditor editor)
        {
            this.Editor = editor;
            this.searchNode = null;
            this.tree.BeginUpdate();
            this.tree.Nodes.Clear();

            TreeNode overview = new TreeNode("Spielstand") { Tag = editor.Overview };
            this.tree.Nodes.Add(overview);

            TreeNode coaches = new TreeNode("Manager");
            foreach (SaveGameCoach coach in editor.Coaches)
                coaches.Nodes.Add(new TreeNode(coach.ToString()) { Tag = coach });
            this.tree.Nodes.Add(coaches);

            foreach (PlayerEnums.Country country in editor.Countries)
            {
                IList<SaveGameClub> clubs = editor.Clubs(country);
                if (clubs.Count == 0)
                    continue;
                TreeNode countryNode = new TreeNode(PlayerEnums.GetDescription(country));
                foreach (SaveGameClub club in clubs.OrderBy(c => c.Name, StringComparer.CurrentCulture))
                {
                    TreeNode clubNode = new TreeNode(club.Name) { Tag = club };
                    clubNode.Nodes.Add(LazyMarker);
                    countryNode.Nodes.Add(clubNode);
                }
                this.tree.Nodes.Add(countryNode);
            }

            coaches.Expand();
            this.tree.EndUpdate();
            this.Select(overview);
            this.SetEnabled(true);
        }

        /// <summary>Empties the view after the savegame was closed.</summary>
        internal void Clear()
        {
            this.Editor = null;
            this.searchNode = null;
            this.grid.SelectedObject = null;
            this.tree.Nodes.Clear();
            this.searchInput.Text = string.Empty;
            this.SetEnabled(false);
        }

        private void SetEnabled(bool enabled)
        {
            this.searchInput.Enabled = enabled;
            this.searchButton.Enabled = enabled;
        }

        private void SearchInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            this.Search();
        }

        /// <summary>Lists the players whose name contains the search text under a "Suche" node.</summary>
        internal void Search()
        {
            if (this.Editor == null)
                return;

            IList<SaveGamePlayer> found = this.Editor.FindPlayers(this.searchInput.Text, SearchLimit);
            this.tree.BeginUpdate();
            if (this.searchNode == null)
            {
                this.searchNode = new TreeNode();
                this.tree.Nodes.Insert(0, this.searchNode);
            }
            this.searchNode.Nodes.Clear();
            this.searchNode.Text = found.Count >= SearchLimit ? $"Suche ({SearchLimit}+)" : $"Suche ({found.Count})";
            foreach (SaveGamePlayer player in found)
                this.searchNode.Nodes.Add(this.PlayerNode(player));
            this.searchNode.Expand();
            this.tree.EndUpdate();
            if (found.Count > 0)
                this.Select(this.searchNode.Nodes[0]);
        }

        // The tree only reports a selection once it has a window, so the grid is set here as well.
        private void Select(TreeNode node)
        {
            this.tree.SelectedNode = node;
            this.grid.SelectedObject = node.Tag as SaveGameEntry;
        }

        private TreeNode PlayerNode(SaveGamePlayer player) => new TreeNode(player.ToString()) { Tag = player };

        // A club's squad is only read when its node is opened.
        private void Tree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (!(e.Node.Tag is SaveGameClub club) || e.Node.Nodes.Count != 1 || e.Node.Nodes[0].Text != LazyMarker || e.Node.Nodes[0].Tag != null)
                return;

            this.tree.BeginUpdate();
            e.Node.Nodes.Clear();
            foreach (ushort id in club.SquadIds)
                e.Node.Nodes.Add(this.PlayerNode(this.Editor.Player(id)));
            IList<ushort> amateurs = club.AmateurIds;
            if (amateurs.Count > 0)
            {
                TreeNode amateurNode = new TreeNode("Amateure");
                foreach (ushort id in amateurs)
                    amateurNode.Nodes.Add(this.PlayerNode(this.Editor.Player(id)));
                e.Node.Nodes.Add(amateurNode);
            }
            this.tree.EndUpdate();
        }

        // Names may have changed: refresh every node showing the edited entry.
        private void Grid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            object entry = this.grid.SelectedObject;
            this.RenameNodes(this.tree.Nodes, entry);
            this.grid.Refresh();
            this.Modified?.Invoke(this, EventArgs.Empty);
        }

        private void RenameNodes(TreeNodeCollection nodes, object entry)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag != null && SameEntry(node.Tag, entry))
                    node.Text = node.Tag is SaveGameOverview ? "Spielstand" : node.Tag.ToString();
                this.RenameNodes(node.Nodes, entry);
            }
        }

        // Entries are cheap views on the file - two objects for the same record are the same entry.
        private static bool SameEntry(object a, object b) =>
            ReferenceEquals(a, b) || (a is SaveGameEntry x && b is SaveGameEntry y && x.GetType() == y.GetType() && x.Address == y.Address);
    }
}
