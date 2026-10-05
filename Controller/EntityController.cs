using A2G_Trainer_XP.Model;
using System;
using System.ComponentModel;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Base class for controllers that read/write a specific entity type (player, club, coach, ...)
    /// from process memory. Handles the original/GOG base-address split (via <see cref="settings"/>,
    /// a 2-element [original, gog] array set by subclasses) and building CE-style module-relative
    /// address strings for field reads/writes.
    /// </summary>
    public abstract class EntityController<E> : INotifyPropertyChanged where E : Entity
    {
        protected string baseAddress;
        protected string[] settings;
        internal BindingList<E> EntityList { get { return entityList; } set { entityList = value; } }
        private BindingList<E> entityList;

        protected ProcessMemory memory;

        #region INotifyPropertyChanged
        /// <summary>Raised whenever a bound property on this controller changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion

        /// <summary>Lookup context (own/opponent/dynamic/etc.) this controller is currently reading.</summary>
        public PlayerEnums.AddressType Type { get => this.type; set => this.type = value; }
        private PlayerEnums.AddressType type;
        protected bool isGog;

        protected EntityController(ProcessMemory memory)
        {
            this.memory = memory;
            this.EntityList = new BindingList<E>();
        }

        /// <summary>Selects the base address (original or GOG build) for the given lookup context.</summary>
        public void UpdateBaseAddress(PlayerEnums.AddressType type)
        {
            this.Type = type;
            int selection = (this.isGog ? 1 : 0);
            this.baseAddress = settings[selection];
        }


        /// <summary>Builds the module-relative address string for one entity field (base address + baseOffset + entity's own offset).</summary>
        protected string GetAddress(ProcessMemory memory, Entity entity, string baseOffset)
        {
            return $"{memory.mProc.MainModule.ModuleName}+{this.baseAddress},{Tools.SumHex(new string[] { baseOffset, entity.Offset })}";
        }

        /// <summary>Reads a full entity from memory at the given offset within the given lookup context.</summary>
        internal abstract E GetEntity(string offset, PlayerEnums.AddressType type);
    }
}
