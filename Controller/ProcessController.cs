using A2G_Trainer_XP.Model;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace A2G_Trainer_XP.Controller
{
    public class ProcessController
    {
        private readonly Trainer trainer;
        private Process gameProcess;
        private bool initialised;
        private bool viewsWired;
        internal bool IsGog { get => this.gog; private set => this.gog = value; }
        private bool gog;

        // Loading a savegame in an already-running game briefly makes the own club unreadable/empty
        // while the game swaps its data in memory - watch for that dip to detect a reload happened,
        // even when it's the same club/country as before (so Id/Country alone can't tell them apart).
        private bool sawInvalidClubState;

        public ProcessController(Trainer trainer)
        {
            this.trainer = trainer;
            this.initialised = false;
        }

        private void FindGame()
        {
            // Already tracking a live game process - no need to rescan every process on the system.
            if (this.gameProcess != null && !this.gameProcess.HasExited)
                return;

            var candidates = Process.GetProcesses()
                .Where(p =>
                    p.MainWindowTitle.StartsWith("ANSTOSS 2") &&
                    !p.MainWindowTitle.Contains("Dateneditor"));

            // The game itself can pop up further top-level windows also titled "ANSTOSS 2 ..."
            // (e.g. dialogs), so the title prefix alone can't tell those apart from the real game
            // process - confirm a candidate actually holds readable club data before latching onto
            // it, instead of trusting whichever one happens to be first.
            foreach (Process candidate in candidates)
            {
                if (TryAttach(candidate))
                {
                    this.gameProcess = candidate;
                    return;
                }
            }
        }

        private bool TryAttach(Process candidate)
        {
            if (!this.trainer.Memory.OpenProcess(candidate.Id))
                return false;

#if FORCE_GOG_ADDRESSING
            // 2007er-CD-Release: nutzt dieselben Speicheradressen wie GOG, läuft aber
            // nicht über run.exe, daher greift die Namenserkennung unten nicht - Build
            // erzwingt die GOG-Adressierung unabhängig vom erkannten Prozessnamen.
            bool isGogCandidate = true;
#else
            bool isGogCandidate = candidate.MainModule.ModuleName.Equals("run.exe");
#endif

            Club own;
            try
            {
                own = new ClubController(this.trainer.Memory, isGogCandidate, PlayerEnums.AddressType.OWN).Club;
            }
            catch
            {
                return false;
            }

            if (own == null || own.PlayerCount <= 0)
                return false;

            this.IsGog = isGogCandidate;
            return true;
        }

        internal void Observe()
        {
            try
            {
                while (!this.trainer.ShutDown)
                {
                    this.FindGame();

                    if (this.gameProcess != null)
                    {
                        this.UpdateGameProcess();
                    }
                    else
                    {
                        this.ClearGameProcess();
                    }

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Exception in Observe()", ex);
            }
        }

        private void UpdateGameProcess()
        {
            if (this.trainer.IsHandleCreated)
            {
                this.trainer.Invoke((MethodInvoker)(() =>
                {
                    // Once settled, also watch for the user loading a different savegame in the
                    // still-running game (no process change, so the checks below wouldn't catch it).
                    bool clubChanged = this.initialised && this.DetectReload();

                    // Also retry (not just on a new PID) while !initialised: right after a fresh game
                    // launch, its club/player memory may not be populated yet, so the first read attempt
                    // can fail - keep retrying every tick until it actually succeeds.
                    if (this.trainer.Anstoss.Process == null || this.trainer.Anstoss.Process.Id != this.gameProcess.Id || !this.initialised || clubChanged)
                    {
                        this.trainer.Anstoss.Process = this.gameProcess;
                        this.trainer.Anstoss.IsRunning = true;

                        try
                        {
                            this.trainer.Memory.OpenProcess(this.gameProcess.Id);
                            if (!this.initialised || clubChanged)
                            {
                                this.trainer.PlayerView.RefreshPlayerListView(this.trainer.PlayerView.PlayerController == null ? PlayerEnums.AddressType.OWN : this.trainer.PlayerView.PlayerController.Type);
                                this.trainer.ClubView.RefreshValues(this.trainer.ClubView.ClubController == null ? PlayerEnums.AddressType.OWN : this.trainer.PlayerView.PlayerController.Type);

                                // Event handlers/data bindings must only ever be wired up once per app lifetime,
                                // not on every reconnect - re-adding them would double them up.
                                if (!this.viewsWired)
                                {
                                    this.trainer.PlayerView.InitMainTabControl();
                                    this.trainer.ClubView.InitClubTabControl();
                                    this.viewsWired = true;
                                }
                            }
                            this.initialised = true;
                        }
                        catch (Exception ex)
                        {
                            this.initialised = false;
                            // Expected right after a fresh game launch (see comment above) - retried
                            // every tick, so Warn rather than Error.
                            Logger.Warn("Konnte Prozess nicht öffnen", ex);
                        }
                    }
                }));
            }
        }

        // Returns true exactly once: the tick where the own club goes from unreadable/empty back to
        // valid again - i.e. the moment a savegame load (same club or not) just finished.
        private bool DetectReload()
        {
            bool isValid;
            try
            {
                Club own = new ClubController(this.trainer.Memory, this.IsGog, PlayerEnums.AddressType.OWN).Club;
                isValid = own != null && own.PlayerCount > 0;
            }
            catch
            {
                isValid = false;
            }

            if (!isValid)
            {
                this.sawInvalidClubState = true;
                return false;
            }

            bool justRecovered = this.sawInvalidClubState;
            this.sawInvalidClubState = false;
            return justRecovered;
        }

        public void UpdatePlayerOffsets()
        {
            Club own = new ClubController(this.trainer.Memory, this.IsGog, PlayerEnums.AddressType.OWN).Club;
            new PlayerController(this.trainer.Memory, own, this.IsGog, PlayerEnums.AddressType.OWN);
        }

        private void ClearGameProcess()
        {
            this.initialised = false;

            if (this.trainer.IsHandleCreated && this.trainer.InvokeRequired)
            {
                this.trainer.Invoke((MethodInvoker)(() =>
                {
                    this.trainer.Anstoss.Process = null;
                    this.trainer.Anstoss.IsRunning = false;
                }));
            }
        }
    }
}
