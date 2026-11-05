using System;
using A2G_Trainer.Model;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// The savegame-wide interest rates and corporation tax (see Settings.InterestOverLimitOffset),
    /// and the current index of each kind of investment. Only for builds with known fixed addresses.
    /// </summary>
    internal static class GameRates
    {
        private static readonly uint[] RateOffsets =
        {
            Settings.InterestDepositOffset, Settings.InterestOverdraftOffset, Settings.InterestOverLimitOffset,
            Settings.InterestLoanOffset, Settings.InterestCallMoneyOffset
        };

        /// <summary>Reads the rates, or null without known fixed addresses or if unreadable.</summary>
        internal static InterestRates Read(ProcessMemory memory, FixedAddresses fixedAddresses)
        {
            if (fixedAddresses == null)
                return null;
            decimal[] percent = new decimal[RateOffsets.Length];
            for (int i = 0; i < RateOffsets.Length; i++)
            {
                byte[] bytes = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, RateOffsets[i]), 2);
                if (bytes == null)
                    return null;
                percent[i] = BitConverter.ToInt16(bytes, 0) / 100m;
            }
            byte[] tax = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.CorporationTaxOffset), 1);
            if (tax == null)
                return null;
            return new InterestRates
            {
                Deposit = percent[0], Overdraft = percent[1], OverLimit = percent[2], Loan = percent[3], CallMoney = percent[4],
                CorporationTax = tax[0]
            };
        }

        /// <summary>Writes the rates back (percent rounded to basis points, tax to whole percent).</summary>
        internal static void Write(ProcessMemory memory, FixedAddresses fixedAddresses, InterestRates rates)
        {
            if (fixedAddresses == null || rates == null)
                return;
            decimal[] percent = { rates.Deposit, rates.Overdraft, rates.OverLimit, rates.Loan, rates.CallMoney };
            for (int i = 0; i < RateOffsets.Length; i++)
            {
                short basisPoints = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(percent[i] * 100m)));
                memory.WriteBytesAtAddress(fixedAddresses.Runtime(memory, RateOffsets[i]), BitConverter.GetBytes(basisPoints));
            }
            memory.WriteBytesAtAddress(fixedAddresses.Runtime(memory, Settings.CorporationTaxOffset), new[] { (byte)Math.Max(0, Math.Min(99, Math.Round(rates.CorporationTax))) });
        }

        /// <summary>The law firms' names, cheapest first (from the game's dialogs in RESDLL.dll).</summary>
        internal static readonly string[] LawFirmNames =
        {
            "Norbert Nöler - Rechtsverdr...äh Anwalt", "Dr. Ines Pfeifenrauch - Legal, illegal, ...", "Lothar Liebling, Berlin",
            "Dr. Rentz - Für alle Fälle.", "Reinhard Rundball - Fußballspezialist"
        };


        /// <summary>Reads the costs the game charges every club and the investment indices; null without known fixed addresses.</summary>
        internal static GameCosts ReadCosts(ProcessMemory memory, FixedAddresses fixedAddresses)
        {
            if (fixedAddresses == null)
                return null;
            byte[] fees = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.LawFirmFeeTableOffset), LawFirmNames.Length * 4);
            byte[] departments = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.DepartmentCostTableOffset), 12);
            if (fees == null || departments == null)
                return null;
            GameCosts costs = new GameCosts
            {
                SmallStatistics = BitConverter.ToInt16(departments, 2) * GameCosts.StatisticsUnit,
                LargeStatistics = BitConverter.ToInt16(departments, 4) * GameCosts.StatisticsUnit,
                Computer = BitConverter.ToInt16(departments, 10) * GameCosts.ComputerUnit
            };
            for (int i = 0; i < LawFirmNames.Length; i++)
                costs.LawFirms.Add(new LawFirmFee { Name = LawFirmNames[i], RedCard = BitConverter.ToInt16(fees, i * 4) * LawFirmFee.Unit, Other = BitConverter.ToInt16(fees, i * 4 + 2) * LawFirmFee.Unit });
            for (int kind = 0; kind < ClubInvestment.KindNames.Length; kind++)
                costs.Indices.Add(new A2G_Trainer.Model.InvestmentIndex { Name = ClubInvestment.KindNames[kind], Value = InvestmentIndex(memory, fixedAddresses, kind) });
            return costs;
        }

        // The tables hold Int16 in the game's units (fees in 1,000 DM, statistics in 10,000 DM, computer in
        // 100,000 DM); the model keeps DM already rounded to them (GameCosts.InUnits).
        private static byte[] Units(int amount, int unit) => BitConverter.GetBytes((short)(GameCosts.InUnits(amount, unit) / unit));

        /// <summary>Writes the costs and the investment indices back.</summary>
        internal static void WriteCosts(ProcessMemory memory, FixedAddresses fixedAddresses, GameCosts costs)
        {
            if (fixedAddresses == null || costs == null)
                return;
            uint fees = fixedAddresses.Static(memory, Settings.LawFirmFeeTableOffset);
            for (int i = 0; i < costs.LawFirms.Count && i < LawFirmNames.Length; i++)
            {
                memory.WriteBytesAtAddress(fees + (uint)i * 4, Units(costs.LawFirms[i].RedCard, LawFirmFee.Unit));
                memory.WriteBytesAtAddress(fees + (uint)i * 4 + 2, Units(costs.LawFirms[i].Other, LawFirmFee.Unit));
            }
            uint departments = fixedAddresses.Static(memory, Settings.DepartmentCostTableOffset);
            memory.WriteBytesAtAddress(departments + 2, Units(costs.SmallStatistics, GameCosts.StatisticsUnit));
            memory.WriteBytesAtAddress(departments + 4, Units(costs.LargeStatistics, GameCosts.StatisticsUnit));
            memory.WriteBytesAtAddress(departments + 10, Units(costs.Computer, GameCosts.ComputerUnit));
            uint indices = fixedAddresses.Runtime(memory, Settings.InvestmentIndexOffset);
            for (int kind = 0; kind < costs.Indices.Count; kind++)
                memory.WriteBytesAtAddress(indices + (uint)kind * 4, BitConverter.GetBytes(costs.Indices[kind].Value));
        }

        /// <summary>The current index of an investment kind 0..4; 0 if unknown.</summary>
        internal static int InvestmentIndex(ProcessMemory memory, FixedAddresses fixedAddresses, int kind)
        {
            if (fixedAddresses == null || kind < 0 || kind > 4)
                return 0;
            byte[] bytes = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.InvestmentIndexOffset) + (uint)kind * 4, 4);
            return bytes == null ? 0 : BitConverter.ToInt32(bytes, 0);
        }
    }
}
