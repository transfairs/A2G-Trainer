using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// One trainer level's distribution of the 6 points a coach gets to allocate across the six
    /// competencies when reaching that level (see <see cref="Coach.CompetencyLevels"/>). The game
    /// keeps this table for all 16 levels independently, not just whichever one the coach currently
    /// holds - it re-applies the stored distribution for a level if the coach is later promoted or
    /// demoted back to it, so every level needs to stay a valid 6-point split even while unused.
    /// </summary>
    public class CoachCompetencyLevel : INotifyPropertyChanged
    {
        /// <summary>Raised whenever one of the six competency values (or <see cref="Total"/>) changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Total)));
        }

        /// <summary>
        /// How many points the game grants per level reached, spread across the six competencies.
        /// The pool for a given level N is PointsPerLevel * N (Level 0 -> 0 Punkte, Level 1 -> 6,
        /// ..., Level 15 -> 90) - see the "Punkte verteilt" label in CoachView.BuildCompetencyBox,
        /// which is the only place this constant is combined with a level number.
        /// </summary>
        public const int PointsPerLevel = 6;

        /// <summary>Points allocated to the "Verhandlungsgeschick" (negotiation skill) competency at this level.</summary>
        public ushort Verhandlungsgeschick { get => this.verhandlungsgeschick; set { this.verhandlungsgeschick = value; this.OnPropertyChanged(nameof(this.Verhandlungsgeschick)); } }
        private ushort verhandlungsgeschick = 1;

        /// <summary>Points allocated to the "Motivationsfähigkeit" (motivation ability) competency at this level.</summary>
        public ushort Motivationsfaehigkeit { get => this.motivationsfaehigkeit; set { this.motivationsfaehigkeit = value; this.OnPropertyChanged(nameof(this.Motivationsfaehigkeit)); } }
        private ushort motivationsfaehigkeit = 1;

        /// <summary>Points allocated to the "Trainingsgestaltung" (training design) competency at this level.</summary>
        public ushort Trainingsgestaltung { get => this.trainingsgestaltung; set { this.trainingsgestaltung = value; this.OnPropertyChanged(nameof(this.Trainingsgestaltung)); } }
        private ushort trainingsgestaltung = 1;

        /// <summary>Points allocated to the "Autorität" (authority) competency at this level.</summary>
        public ushort Autoritaet { get => this.autoritaet; set { this.autoritaet = value; this.OnPropertyChanged(nameof(this.Autoritaet)); } }
        private ushort autoritaet = 1;

        /// <summary>Points allocated to the "Fremdsprachenkenntnisse" (foreign language skills) competency at this level.</summary>
        public ushort Fremdsprachenkenntnisse { get => this.fremdsprachenkenntnisse; set { this.fremdsprachenkenntnisse = value; this.OnPropertyChanged(nameof(this.Fremdsprachenkenntnisse)); } }
        private ushort fremdsprachenkenntnisse = 1;

        /// <summary>Points allocated to the "Ausstrahlung" (charisma) competency at this level.</summary>
        public ushort Ausstrahlung { get => this.ausstrahlung; set { this.ausstrahlung = value; this.OnPropertyChanged(nameof(this.Ausstrahlung)); } }
        private ushort ausstrahlung = 1;

        /// <summary>Sum of all six values - should equal <see cref="PointsPerLevel"/> for a valid distribution.</summary>
        public int Total => this.verhandlungsgeschick + this.motivationsfaehigkeit + this.trainingsgestaltung + this.autoritaet + this.fremdsprachenkenntnisse + this.ausstrahlung;

        /// <summary>Reads one competency by key - used by CoachController to loop over all six in a fixed order.</summary>
        public ushort Get(CoachEnums.CompetencyKey key)
        {
            switch (key)
            {
                case CoachEnums.CompetencyKey.Verhandlungsgeschick: return this.Verhandlungsgeschick;
                case CoachEnums.CompetencyKey.Motivationsfaehigkeit: return this.Motivationsfaehigkeit;
                case CoachEnums.CompetencyKey.Trainingsgestaltung: return this.Trainingsgestaltung;
                case CoachEnums.CompetencyKey.Autoritaet: return this.Autoritaet;
                case CoachEnums.CompetencyKey.Fremdsprachenkenntnisse: return this.Fremdsprachenkenntnisse;
                case CoachEnums.CompetencyKey.Ausstrahlung: return this.Ausstrahlung;
                default: throw new System.ArgumentOutOfRangeException(nameof(key));
            }
        }

        /// <summary>Writes one competency by key - used by CoachController to loop over all six in a fixed order.</summary>
        public void Set(CoachEnums.CompetencyKey key, ushort value)
        {
            switch (key)
            {
                case CoachEnums.CompetencyKey.Verhandlungsgeschick: this.Verhandlungsgeschick = value; break;
                case CoachEnums.CompetencyKey.Motivationsfaehigkeit: this.Motivationsfaehigkeit = value; break;
                case CoachEnums.CompetencyKey.Trainingsgestaltung: this.Trainingsgestaltung = value; break;
                case CoachEnums.CompetencyKey.Autoritaet: this.Autoritaet = value; break;
                case CoachEnums.CompetencyKey.Fremdsprachenkenntnisse: this.Fremdsprachenkenntnisse = value; break;
                case CoachEnums.CompetencyKey.Ausstrahlung: this.Ausstrahlung = value; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(key));
            }
        }
    }
}
