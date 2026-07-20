using System;
using System.Collections.Generic;

namespace A2G_Trainer_XP.Model
{
    public class Coach : Entity
    {
        public bool Initilisation { get => this.initilisation; set => this.initilisation = value; }
        private bool initilisation = true;

        public string Firstname
        {
            get => this.firstname; set
            {
                if (Tools.LimitedStringEquals(value, this.firstname, 9))
                {
                    this.firstname = value != null && value.Length > 9 ? value.Substring(0, 9) : value; this.OnPropertyChanged(nameof(this.Firstname));
                }
            }
        }
        private string firstname = string.Empty;
        public string Lastname
        {
            get => this.lastname; set
            {
                if (Tools.LimitedStringEquals(value, this.lastname, 15))
                {
                    this.lastname = value != null && value.Length > 15 ? value.Substring(0, 15) : value; this.OnPropertyChanged(nameof(this.Lastname));
                }
            }
        }
        private string lastname = string.Empty;
        public byte Age { get => this.age; set { this.age = value; this.OnPropertyChanged(nameof(this.Age)); } }
        private byte age = 0;
        public byte Level { get => this.level; set { this.level = value; this.OnPropertyChanged(nameof(this.Level)); } }
        private byte level = 0;

        // Game supports up to 4 human-controlled managers per savegame; a save can use fewer.
        public const int MaxTrainers = 4;

        // Game caps club stock holdings at 6 slots.
        public const int MaxStocks = 6;
        public Stock[] Stocks { get; } = new Stock[MaxStocks] { new Stock(), new Stock(), new Stock(), new Stock(), new Stock(), new Stock() };

        public int Wealth { get => this.wealth; set { this.wealth = value; this.OnPropertyChanged(nameof(this.Wealth)); } }
        private int wealth;

        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;

    }
}
