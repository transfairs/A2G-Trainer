using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// An opened savegame file and the entries the editor shows for it: the clubs of each league
    /// country, their squads, the human trainers and the savegame-wide values.
    /// </summary>
    internal sealed class SaveGameEditor
    {
        // Static tables of the game (not in the savegame): first club record and number of club
        // records per league system (+2C17F0 / +2C1BE8, identical in every build - see
        // Settings.LeagueBasePattern). Which system a country uses is savegame data
        // (Settings.LeagueSystemByCountryOffset).
        private static readonly short[] LeagueBases = { 0, 124, 199, 245, 269 };
        private static readonly short[] LeagueClubCounts = { 124, 75, 46, 24, 26 };

        // LeagueSettings' MAIN_COUNTRY, right after the bonus-country count.
        private const uint MainCountryOffset = 2;

        private SaveGameEditor(SaveGameFile file)
        {
            this.File = file;
            this.Calendar = new SaveGameCalendar(file);
        }

        /// <summary>The savegame file.</summary>
        public SaveGameFile File { get; }

        /// <summary>The savegame's calendar.</summary>
        public SaveGameCalendar Calendar { get; }

        /// <summary>Opens a savegame file; throws <see cref="System.IO.InvalidDataException"/> if it isn't one.</summary>
        public static SaveGameEditor Open(string path) => new SaveGameEditor(SaveGameFile.Load(path));

        /// <summary>Wraps an already read savegame.</summary>
        public static SaveGameEditor For(SaveGameFile file) => new SaveGameEditor(file);

        /// <summary>The country the savegame is played in.</summary>
        public PlayerEnums.Country MainCountry => (PlayerEnums.Country)this.File.ReadInt16(SaveGameFile.ModuleBase + Settings.BonusCountryCountOffset + MainCountryOffset);

        /// <summary>Savegame-wide values.</summary>
        public SaveGameOverview Overview => new SaveGameOverview(this);

        /// <summary>The league countries, main country first.</summary>
        public IEnumerable<PlayerEnums.Country> Countries =>
            new[] { this.MainCountry }.Concat(LeagueSettings.LeagueCountries.Where(c => c != this.MainCountry)).Where(c => LeagueSettings.LeagueCountries.Contains(c));

        /// <summary>All clubs of a country that have a record, by ID.</summary>
        public IList<SaveGameClub> Clubs(PlayerEnums.Country country)
        {
            List<SaveGameClub> clubs = new List<SaveGameClub>();
            byte system = this.File.ReadByte(SaveGameFile.ModuleBase + Settings.LeagueSystemByCountryOffset + (uint)country);
            if (system >= LeagueBases.Length)
                return clubs;
            for (int id = 0; id < LeagueClubCounts[system]; id++)
            {
                SaveGameClub club = new SaveGameClub(this, LeagueBases[system] + id);
                if (club.Name.Length > 0 && club.Country == country)
                    clubs.Add(club);
            }
            return clubs;
        }

        /// <summary>Names of the country's clubs by ID.</summary>
        public IDictionary<byte, string> ClubNames(PlayerEnums.Country country)
        {
            Dictionary<byte, string> names = new Dictionary<byte, string>();
            foreach (SaveGameClub club in this.Clubs(country))
                names[club.Id] = club.Name;
            return names;
        }

        /// <summary>Name of a club, "-" if there is none with that country and ID.</summary>
        public string ClubName(PlayerEnums.Country country, byte id) =>
            this.ClubNames(country).TryGetValue(id, out string name) ? name : "-";

        /// <summary>The human trainers of the savegame (Mitspieler IDs 1..count).</summary>
        public IList<SaveGameCoach> Coaches
        {
            get
            {
                int count = this.File.ReadInt16(SaveGameFile.ModuleBase + Settings.ActiveTrainerCountOffset);
                count = Math.Max(0, Math.Min(Coach.MaxTrainers, count));
                return Enumerable.Range(1, count).Select(id => new SaveGameCoach(this, id)).ToList();
            }
        }

        /// <summary>The player with the given ID.</summary>
        public SaveGamePlayer Player(ushort id)
        {
            if (id >= SaveGamePlayer.RecordCount)
                throw new ArgumentOutOfRangeException(nameof(id));
            return new SaveGamePlayer(this.File, this.Calendar, id);
        }

        /// <summary>Players whose "first last" name contains <paramref name="text"/> (ignoring case), at most <paramref name="limit"/>.</summary>
        public IList<SaveGamePlayer> FindPlayers(string text, int limit)
        {
            List<SaveGamePlayer> found = new List<SaveGamePlayer>();
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
                return found;
            for (ushort id = 1; id < SaveGamePlayer.RecordCount && found.Count < limit; id++)
            {
                SaveGamePlayer player = this.Player(id);
                if (player.IsUsed && player.ToString().IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    found.Add(player);
            }
            return found;
        }
    }
}
