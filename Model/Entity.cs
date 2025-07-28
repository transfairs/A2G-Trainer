using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    public abstract class Entity : INotifyPropertyChanged
    {
        internal string Offset { get => this.offset; set { this.offset = value; this.OnPropertyChanged(nameof(this.Offset)); } }
        private string offset = String.Empty;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
    }
}
