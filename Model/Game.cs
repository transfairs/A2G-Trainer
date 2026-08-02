using System.Diagnostics;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// Tracks the attached game process and whether it is currently running.
    /// </summary>
    class Game : Entity
    {
        /// <summary>The OS process for the running game, once attached.</summary>
        public Process Process { get; set; }
        bool running = false;
        /// <summary>Whether the game process is currently attached and running.</summary>
        internal bool IsRunning { get { return running; } set { running = value; OnPropertyChanged("IsRunning"); } }
    }
}
