using A2G_Trainer_XP.Controller;
using AnstossTrainer.Shared;
using System.ComponentModel;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    /// <summary>Base class for the Anstoss 2 tab views (Club/Coach/Player): the controllers they share on top of the common TrainerView helpers.</summary>
    public partial class EntityView : TrainerView
    {
        #region INotifyPropertyChanged
        /// <summary>Raised whenever a bound property on this view changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
        protected BindingSource bindingSource;

        /// <summary>Controller currently backing this view's club data, if any.</summary>
        public ClubController ClubController { get => this.clubController; private set { } }
        protected ClubController clubController;
        /// <summary>Controller currently backing this view's coach data, if any.</summary>
        public CoachController CoachController { get => this.coachController; private set { } }
        protected CoachController coachController;
        /// <summary>Controller currently backing this view's player data, if any.</summary>
        public PlayerController PlayerController { get => this.playerController; private set { } }
        protected PlayerController playerController;
        protected ProcessController processController;

        protected EntityView(ProcessMemory memory, ProcessController processController) : base(memory)
        {
            this.processController = processController;
        }

        protected override string GameDisplayName => "Anstoss-2-Gold";
    }
}
