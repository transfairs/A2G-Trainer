using A2G_Trainer_XP.Model;
using Memory;
using System.ComponentModel;

namespace A2G_Trainer_XP.Controller
{
    public abstract class EntityController<E> : INotifyPropertyChanged where E : Entity
    {
        protected string baseAddress;
        internal BindingList<E> EntityList { get { return entityList; } set { entityList = value; } }
        private BindingList<E> entityList;

        protected Mem memory;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion

        protected EntityController(Mem memory)
        {
            this.memory = memory;
            this.EntityList = new BindingList<E>();
        }


        protected string GetAddress(Mem memory, Entity entity, string baseOffset)
        {
            return $"{memory.mProc.MainModule.ModuleName}+{this.baseAddress},{Tools.SumHex(new string[] { baseOffset, entity.Offset })}";
        }

        internal abstract E GetEntity(string offset);
    }
}
