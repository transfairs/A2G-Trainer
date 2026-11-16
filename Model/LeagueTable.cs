using System;
using System.Collections.Generic;
using System.Linq;

namespace A2G_Trainer.Model
{
    /// <summary>How the game decides a league's season (FUN_00597ba0, FUN_0063c960, FUN_00578da0).</summary>
    public enum LeagueMode
    {
        /// <summary>Played matchday by matchday: every league of the main country, a bonus country's top league.</summary>
        Played,
        /// <summary>
        /// All results rolled at the season start and read again at the season change, which moves
        /// the clubs up and down by this table: the leagues below the top one of a bonus country.
        /// </summary>
        Simulated,
        /// <summary>
        /// Rolled as well, but in a country that is neither main nor bonus country: its season change
        /// moves nobody (the club lists stay as they are), so the table has no consequences.
        /// </summary>
        SimulatedInactive,
        /// <summary>No results at all: the top league of a country that is neither main nor bonus country.</summary>
        NoResults,
    }

    /// <summary>One club's line in a league table, computed like the game's FUN_00615780.</summary>
    public sealed class LeagueTableRow
    {
        /// <summary>The club's place in the league's club list (0-based); 0-9 always beat the others in a rolled league.</summary>
        public int ListPosition { get; set; }
        /// <summary>Club ID within its country.</summary>
        public short ClubId { get; set; }
        /// <summary>Club name.</summary>
        public string Name { get; set; } = "";
        /// <summary>Licence byte (club record +0xF5): bits 0-1 points deducted this season (x3), bits 2-3 next season, bit 7 licence refused.</summary>
        public byte Licence { get; set; }
        /// <summary>Matches played.</summary>
        public int Games { get; set; }
        /// <summary>Wins.</summary>
        public int Wins { get; set; }
        /// <summary>Draws.</summary>
        public int Draws { get; set; }
        /// <summary>Losses.</summary>
        public int Losses { get; set; }
        /// <summary>Goals scored.</summary>
        public int GoalsFor { get; set; }
        /// <summary>Goals conceded.</summary>
        public int GoalsAgainst { get; set; }
        /// <summary>Points after the deduction, never below 0 (as the game shows them).</summary>
        public int Points { get; set; }

        /// <summary>Points deducted this season.</summary>
        public int Deduction => Club.DeductionPoints(this.Licence & 3);
        /// <summary>Whether the licence for the next season was refused: the game puts the club last.</summary>
        public bool LicenceRefused => (this.Licence & 0x80) != 0;
        /// <summary>Goal difference.</summary>
        public int GoalDifference => this.GoalsFor - this.GoalsAgainst;
    }

    /// <summary>
    /// One of the game's 13 leagues: its clubs in list order and its result grid. The table isn't
    /// stored anywhere - <see cref="Standings"/> computes it the way the game does.
    /// </summary>
    public sealed class LeagueTable
    {
        /// <summary>Byte of an empty grid cell.</summary>
        public const byte NotPlayed = 0xFF;
        /// <summary>The list positions 0 to this - 1 always win against the others in a rolled league (FUN_005c91f0).</summary>
        public const int Bracket = 10;

        /// <summary>The league's global index (0-12).</summary>
        public int Index { get; set; }
        /// <summary>The league's country.</summary>
        public PlayerEnums.Country Country { get; set; }
        /// <summary>Level within the country, 0 = top league.</summary>
        public int Level { get; set; }
        /// <summary>The league's name.</summary>
        public string Name { get; set; } = "";
        /// <summary>How the game decides the season.</summary>
        public LeagueMode Mode { get; set; }
        /// <summary>Matchdays of the season.</summary>
        public int Matchdays { get; set; }
        /// <summary>Matchdays played so far (stays 0 in a rolled league).</summary>
        public int Played { get; set; }
        /// <summary>Club IDs in list order.</summary>
        public short[] ClubIds { get; set; } = new short[0];
        /// <summary>Club names in list order.</summary>
        public string[] ClubNames { get; set; } = new string[0];
        /// <summary>Licence bytes in list order.</summary>
        public byte[] Licences { get; set; } = new byte[0];
        /// <summary>The result grid, [home][away] for <see cref="GridSize"/> x <see cref="GridSize"/> clubs, two bytes per cell.</summary>
        public byte[] Grid { get; set; } = new byte[GridSize * GridSize * 2];

        /// <summary>Clubs the grid has room for.</summary>
        public const int GridSize = 24;

        /// <summary>Number of clubs.</summary>
        public int ClubCount => this.ClubIds.Length;

        /// <summary>Byte offset of the cell home x away in <see cref="Grid"/>.</summary>
        public static int Cell(int home, int away) => (home * GridSize + away) * 2;

        /// <summary>Home and away goals of the cell; false where it isn't played.</summary>
        public bool TryResult(int home, int away, out int homeGoals, out int awayGoals)
        {
            byte value = this.Grid[Cell(home, away)];
            homeGoals = value >> 4;
            awayGoals = value & 0xF;
            return value != NotPlayed;
        }

        /// <summary>
        /// The table as the game computes it for the whole season (FUN_00615780 with the deduction,
        /// sorted by FUN_00615f80): 3 points a win, 1 a draw, minus the deduction (not below 0); a
        /// club without licence last, then points, goal difference, goals scored. Without results
        /// (<see cref="LeagueMode.NoResults"/>) the clubs stay in list order.
        /// </summary>
        public List<LeagueTableRow> Standings()
        {
            List<LeagueTableRow> rows = new List<LeagueTableRow>();
            for (int i = 0; i < this.ClubCount; i++)
            {
                LeagueTableRow row = new LeagueTableRow
                {
                    ListPosition = i,
                    ClubId = this.ClubIds[i],
                    Name = i < this.ClubNames.Length ? this.ClubNames[i] : "",
                    Licence = i < this.Licences.Length ? this.Licences[i] : (byte)0,
                };
                int points = 0;
                for (int j = 0; j < this.ClubCount; j++)
                {
                    if (i == j)
                        continue;
                    if (this.TryResult(i, j, out int homeGoals, out int awayGoals))
                        points += Count(row, homeGoals, awayGoals);
                    if (this.TryResult(j, i, out homeGoals, out awayGoals))
                        points += Count(row, awayGoals, homeGoals);
                }
                row.Points = Math.Max(0, points - row.Deduction);
                rows.Add(row);
            }
            if (this.Mode == LeagueMode.NoResults)
                return rows;
            return rows
                .OrderBy(r => r.LicenceRefused)
                .ThenByDescending(r => r.Points)
                .ThenByDescending(r => r.GoalDifference)
                .ThenByDescending(r => r.GoalsFor)
                .ThenBy(r => r.ListPosition)
                .ToList();
        }

        // Adds one match to the row; returns its points.
        private static int Count(LeagueTableRow row, int scored, int conceded)
        {
            row.Games++;
            row.GoalsFor += scored;
            row.GoalsAgainst += conceded;
            if (scored > conceded) { row.Wins++; return 3; }
            if (scored == conceded) { row.Draws++; return 1; }
            row.Losses++;
            return 0;
        }

        /// <summary>
        /// Sets every result so that the table comes out in <paramref name="order"/> (list positions,
        /// best first): each club wins its home match against every club below it and draws away
        /// there, so neighbours are three points apart. Goals vary with the gap so the numbers look
        /// like a season. The deduction and a refused licence still apply on top, as in the game.
        /// </summary>
        public void SetResults(IList<int> order)
        {
            if (order == null || order.Count != this.ClubCount || order.Distinct().Count() != this.ClubCount || order.Any(p => p < 0 || p >= this.ClubCount))
                throw new ArgumentException("Die Reihenfolge muss jeden Verein der Liga genau einmal enthalten.", nameof(order));

            for (int rank = 0; rank < order.Count; rank++)
            {
                for (int lower = rank + 1; lower < order.Count; lower++)
                {
                    int better = order[rank], worse = order[lower];
                    int gap = lower - rank;
                    int winnerGoals = 1 + (gap + rank) % 3 + (gap > order.Count / 2 ? 1 : 0);
                    int loserGoals = (rank + lower) % 2 == 0 && winnerGoals > 1 ? 1 : 0;
                    int drawGoals = (rank + lower) % 3;
                    this.Grid[Cell(better, worse)] = Goals(winnerGoals, loserGoals);
                    this.Grid[Cell(worse, better)] = Goals(drawGoals, drawGoals);
                }
            }
        }

        private static byte Goals(int home, int away) => (byte)((Math.Min(home, 15) << 4) | Math.Min(away, 15));
    }
}
