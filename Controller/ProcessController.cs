using A2G_Trainer_XP.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Anstoss 2 Gold's game observer (polling loop in the shared GameObserver): attaches to the
    /// running game process, detects savegame reloads/trainer handoffs, and drives the views'
    /// refresh cycle.
    /// </summary>
    public class ProcessController : GameObserver
    {
        private readonly Trainer trainer;
        private bool initialised;
        private bool viewsWired;
        internal bool IsGog { get => this.gog; private set => this.gog = value; }
        private bool gog;

        // Last process GameBuildDetector ran on, and its result (see IdentifyBuild).
        private int identifiedPid;
        private GameBuild identifiedBuild;

        // Loading a savegame in an already-running game briefly makes the own club unreadable/empty
        // while the game swaps its data in memory - watch for that dip to detect a reload happened,
        // even when it's the same club/country as before (so Id/Country alone can't tell them apart).
        private bool sawInvalidClubState;

        // Ending a turn in a hot-seat savegame swaps the next human manager's club straight into the
        // "own club" slot without ever dipping through the invalid state above - tracked separately so
        // that handoff (a change of Id/Country between two otherwise-valid reads) still triggers a refresh.
        private byte? lastOwnClubId;
        private PlayerEnums.Country? lastOwnClubCountry;

        /// <summary>Creates a controller that will find/attach to the game process and drive the given trainer window's refreshes.</summary>
        public ProcessController(Trainer trainer)
        {
            this.trainer = trainer;
            this.initialised = false;
        }

        protected override bool ShutDownRequested => this.trainer.ShutDown;

        // Extracted so tests can substitute a controlled candidate list - the real one depends on
        // whatever happens to be running on the machine (including a real Anstoss 2 Gold instance),
        // which made tests around this non-deterministic.
        // The title is only a cheap pre-filter: the game pops up further windows titled
        // "ANSTOSS 2 ..." (e.g. dialogs), and other programs (e.g. verl.exe from the same
        // install) title theirs that way too, so TryAttach identifies the actual build and
        // confirms readable club data.
        internal override IEnumerable<Process> GetGameCandidates() =>
            Process.GetProcesses()
                .Where(p =>
                    p.MainWindowTitle.StartsWith("ANSTOSS 2") &&
                    !p.MainWindowTitle.Contains("Dateneditor"));

        /// <summary>
        /// Attempts to attach to a candidate process: identifies its game build from the mapped main
        /// module (see GameBuild), then confirms it actually holds readable club data under that
        /// build's addressing.
        /// </summary>
        protected override bool TryAttach(Process candidate)
        {
            if (!this.trainer.Memory.OpenProcess(candidate.Id))
                return false;

            GameBuild build = this.IdentifyBuild(candidate.Id);
            if (build == null || !build.IsSupported)
                return false;

            foreach (bool isGogCandidate in build.CandidateAddressings)
            {
                if (!this.HasReadableOwnClub(isGogCandidate))
                    continue;

                this.IsGog = isGogCandidate;
                this.trainer.Memory.Layout = PersistentLayout.For(isGogCandidate);
                this.trainer.Memory.Build = build;
                Logger.Debug($"Attached to process {candidate.Id} ({candidate.MainModule.ModuleName}), build: {build.Name}, IsGog={isGogCandidate}");
                return true;
            }
            return false;
        }

        // A candidate stays unattachable for many ticks while the game sits in its menus (no club
        // loaded yet), so the identification - a full image scan for unknown builds - and its log
        // line are done once per process rather than every tick.
        private GameBuild IdentifyBuild(int pid)
        {
            if (this.identifiedPid == pid)
                return this.identifiedBuild;

            GameBuild build = GameBuildDetector.Identify(this.trainer.Memory);
            this.identifiedPid = pid;
            this.identifiedBuild = build;

            if (build == null)
                Logger.Debug($"Process {pid} has a matching window title but is no Anstoss 2 build, ignored");
            else if (!build.IsSupported)
                Logger.Warn($"Process {pid} runs {build.Name}, which the trainer doesn't support");
            else if (!build.IsKnown)
                Logger.Warn($"Process {pid} runs an {build.Name} - trying GOG, then original addressing");
            return build;
        }

        private bool HasReadableOwnClub(bool isGog)
        {
            Club own;
            try
            {
                own = new ClubController(this.trainer.Memory, isGog, PlayerEnums.AddressType.OWN).Club;
            }
            catch
            {
                return false;
            }

            // ClubController.GetEntity always constructs a Club, never null - only PlayerCount
            // distinguishes an unreadable/empty attach attempt.
            return own.PlayerCount > 0;
        }

        protected override void UpdateGameProcess()
        {
            if (this.trainer.IsHandleCreated)
            {
                this.trainer.Invoke((MethodInvoker)(() =>
                {
                    bool clubChanged = this.initialised && this.DetectOwnClubChange();

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
                                // Each view's refresh/init is fault-isolated - a bad address in one
                                // view (e.g. an unmapped field) must not stop the others from ever
                                // getting their data or their one-time bindings wired up.
                                this.RefreshView("PlayerView", () => this.trainer.PlayerView.RefreshPlayerListView(this.trainer.PlayerView.PlayerController == null ? PlayerEnums.AddressType.OWN : this.trainer.PlayerView.PlayerController.Type));
                                this.RefreshView("ClubView", () => this.trainer.ClubView.RefreshValues(this.trainer.ClubView.ClubController == null ? PlayerEnums.AddressType.OWN : this.trainer.PlayerView.PlayerController.Type));
                                this.RefreshView("CoachView", () =>
                                {
                                    var activeTrainers = CoachController.GetActiveTrainers(this.trainer.Memory, this.IsGog, PlayerEnums.AddressType.OWN);
                                    this.trainer.RefreshTrainerMenu(activeTrainers);

                                    // If the currently shown trainer slot no longer holds a manager
                                    // in the (possibly just-loaded) savegame, fall back to the first
                                    // one that does - editing/saving a stale, now-invalid slot must
                                    // become impossible rather than silently keep working on it.
                                    int validTrainerIndex = activeTrainers.Any(t => t.Key == this.trainer.CoachView.CurrentTrainerIndex)
                                        ? this.trainer.CoachView.CurrentTrainerIndex
                                        : activeTrainers.Select(t => t.Key).DefaultIfEmpty(0).First();

                                    this.trainer.CoachView.RefreshValues(this.trainer.CoachView.CoachController == null ? PlayerEnums.AddressType.OWN : this.trainer.CoachView.CoachController.Type, trainerIndex: validTrainerIndex);
                                });

                                // Event handlers/data bindings must only ever be wired up once per app lifetime,
                                // not on every reconnect - re-adding them would double them up.
                                if (!this.viewsWired)
                                {
                                    this.RefreshView("PlayerView.Init", () => this.trainer.PlayerView.InitMainTabControl());
                                    this.RefreshView("ClubView.Init", () => this.trainer.ClubView.InitClubTabControl());
                                    this.RefreshView("CoachView.Init", () => this.trainer.CoachView.InitClubTabControl());
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

        // Returns true on the tick where either (a) the own club goes from unreadable/empty back to
        // valid again - i.e. a savegame load (same club or not) just finished - or (b) two consecutive
        // valid reads show a different club Id/Country - i.e. a turn handoff to another trainer just
        // happened, without ever dipping through the invalid state.
        private bool DetectOwnClubChange()
        {
            bool isValid;
            // ClubController.GetEntity always constructs a Club, never null - only PlayerCount
            // distinguishes an unreadable/empty read.
            Club own = null;
            try
            {
                own = new ClubController(this.trainer.Memory, this.IsGog, PlayerEnums.AddressType.OWN).Club;
                isValid = own.PlayerCount > 0;
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

            bool handoffDetected = this.lastOwnClubId.HasValue &&
                (this.lastOwnClubId.Value != own.Id || this.lastOwnClubCountry.Value != own.Country);
            this.lastOwnClubId = own.Id;
            this.lastOwnClubCountry = own.Country;

            if (justRecovered)
                Logger.Debug("Savegame reload detected, refreshing views");
            if (handoffDetected)
                Logger.Debug("Trainer handoff detected (own club changed), refreshing views");

            return justRecovered || handoffDetected;
        }

        /// <summary>Re-resolves the opponent/dynamic-team roster offsets from the current own club.</summary>
        public void UpdatePlayerOffsets()
        {
            Club own = new ClubController(this.trainer.Memory, this.IsGog, PlayerEnums.AddressType.OWN).Club;
            new PlayerController(this.trainer.Memory, own, this.IsGog, PlayerEnums.AddressType.OWN);
        }

        protected override void ClearGameProcess()
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
