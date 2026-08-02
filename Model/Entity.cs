using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// Base class for savegame-backed model objects: carries the memory offset used to
    /// locate the entity's struct, and raises change notifications for UI data binding.
    /// </summary>
    public abstract class Entity : INotifyPropertyChanged
    {
        /// <summary>Base memory offset (relative to the module) for this entity's struct.</summary>
        internal string Offset { get => this.offset; set { this.offset = value; this.OnPropertyChanged(nameof(this.Offset)); } }
        private string offset = String.Empty;

        #region INotifyPropertyChanged
        /// <summary>Raised whenever a bound property on this entity changes, for UI data binding.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
    }
}
