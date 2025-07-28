using System.Diagnostics;

namespace A2G_Trainer_XP.Model
{
    class Game : Entity
    {
        public Process Process { get; set; }
        bool running = false;
        internal bool IsRunning { get { return running; } set { running = value; OnPropertyChanged("IsRunning"); } }
    }
}
