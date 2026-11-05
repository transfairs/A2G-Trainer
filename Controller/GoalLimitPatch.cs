using A2G_Trainer.Model;
using System;
using System.Linq;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Lifts the match engine's goal limits (see Settings): a team scores only while it has fewer than
    /// 6 goals up to minute 45 and fewer than 10 after, and from 7 goals its chances are halved. The
    /// patch raises the limit to 15 and skips the halving, for every match the game plays. 15 is the
    /// most the game can keep: it stores results and half-time scores a nibble per team (club +0x1442,
    /// FUN_005f5600), so 16 goals end up as 0 in the table and the statistics.
    /// </summary>
    /// <remarks>
    /// The code is rewritten in place in the running process; switching off writes the original
    /// instructions back. Every site must hold either the original or the patched instructions,
    /// otherwise nothing is written. The instructions themselves: GoalLimitBytes. The standalone
    /// A2G-Patch makes the same change outside the trainer, in the exe on disk or by its msvfw32.dll
    /// at game start.
    /// </remarks>
    internal sealed class GoalLimitPatch
    {
        private readonly ProcessMemory memory;

        /// <summary>Creates the patch for the attached game; it changes nothing until <see cref="SetEnabled"/>.</summary>
        internal GoalLimitPatch(ProcessMemory memory) => this.memory = memory;

        private FixedAddresses.GoalLimitCode Code => FixedAddresses.For(this.memory)?.GoalLimit;

        /// <summary>Whether the patch is available for the attached build.</summary>
        internal bool IsSupported => this.Code != null;

        /// <summary>Whether the limits are lifted in the attached game right now.</summary>
        internal bool IsEnabled => this.ReadSites()?.All(s => s.Site.Patched) == true;

        /// <summary>Lifts the limits or restores them. Returns whether that succeeded.</summary>
        internal bool SetEnabled(bool enabled)
        {
            Located[] sites = this.ReadSites();
            if (sites == null)
                return !enabled;
            foreach (Located located in sites.Where(s => s.Site.Patched != enabled))
            {
                if (!this.memory.WriteCode(located.Address, enabled ? located.Site.PatchedBytes : located.Site.OriginalBytes))
                    return false;
            }
            Logger.Debug($"GoalLimitPatch: {(enabled ? "on" : "off")}.");
            return true;
        }

        // A site with the absolute address its bytes are written to.
        private sealed class Located
        {
            public uint Address;
            public GoalLimitBytes.Site Site;
        }

        // The three sites with their current state, or null if any isn't as expected.
        private Located[] ReadSites()
        {
            FixedAddresses.GoalLimitCode code = this.Code;
            if (code == null)
                return null;
            Located[] sites =
            {
                this.Locate(this.memory.ModuleBase + code.LimitSite + (uint)Settings.GoalLimitPattern.Length, GoalLimitBytes.LimitWindow, GoalLimitBytes.Limit),
                this.Locate(this.memory.ModuleBase + code.HomeBrakeSite + (uint)Settings.GoalHomeBrakePattern.Length, 1, b => GoalLimitBytes.Jump(b[0])),
                this.Locate(this.memory.ModuleBase + code.AwayBrakeSite + (uint)Settings.GoalAwayBrakePattern.Length, 1, b => GoalLimitBytes.Jump(b[0])),
            };
            if (sites.Contains(null))
            {
                Logger.Warn("GoalLimitPatch: Code der Torgrenzen nicht wie erwartet - nichts geändert.");
                return null;
            }
            return sites;
        }

        private Located Locate(uint address, int length, Func<byte[], GoalLimitBytes.Site> read)
        {
            byte[] bytes = this.memory.ReadBytesAtAddress(address, length);
            GoalLimitBytes.Site site = bytes == null ? null : read(bytes);
            return site == null ? null : new Located { Address = address + (uint)site.Offset, Site = site };
        }
    }
}
