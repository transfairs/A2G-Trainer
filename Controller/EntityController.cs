using A2G_Trainer_XP.Model;
using Memory;
using System;
using System.ComponentModel;

namespace A2G_Trainer_XP.Controller
{
    public abstract class EntityController<E> : INotifyPropertyChanged where E : Entity
    {
        protected string baseAddress;
        protected string[] settings;
        internal BindingList<E> EntityList { get { return entityList; } set { entityList = value; } }
        private BindingList<E> entityList;

        protected Mem memory;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion

        public PlayerEnums.AddressType Type { get => this.type; set => this.type = value; }
        private PlayerEnums.AddressType type;
        protected bool isGog;

        protected EntityController(Mem memory)
        {
            this.memory = memory;
            this.EntityList = new BindingList<E>();
        }

        public void UpdateBaseAddress(PlayerEnums.AddressType type)
        {
            this.Type = type;
            int selection = (this.Type == PlayerEnums.AddressType.OWN ? 0 : 2) | (this.isGog ? 1 : 0);
            this.baseAddress = settings[selection];
            // Console.WriteLine($"({this.Type}, {this.isGog}): {selection} => {this.baseAddress}");
        }


        protected string GetAddress(Mem memory, Entity entity, string baseOffset)
        {
            return $"{memory.mProc.MainModule.ModuleName}+{this.baseAddress},{Tools.SumHex(new string[] { baseOffset, entity.Offset })}";
        }

        internal abstract E GetEntity(string offset, PlayerEnums.AddressType type);
    }
}
