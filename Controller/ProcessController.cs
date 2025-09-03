using A2G_Trainer_XP.Model;
using Memory;
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
        internal bool IsGog { get => this.gog; private set => this.gog = value; }
        private bool gog;

        public ProcessController(Trainer trainer)
        {
            this.trainer = trainer;
            this.initialised = false;
        }

        private void FindGame()
        {
            this.gameProcess = Process.GetProcesses()
                .FirstOrDefault(p =>
                    p.MainWindowTitle.StartsWith("ANSTOSS 2") &&
                    !p.MainWindowTitle.Contains("Dateneditor"));

            if (this.gameProcess != null)
            {
                this.IsGog = this.gameProcess.MainModule.ModuleName.Equals("run.exe");
                // Console.WriteLine($"{this.gameProcess.MainModule.ModuleName}: { this.gameProcess.MainModule.ModuleName.Equals("run.exe")}, {this.IsGog}");
            }
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
                Console.WriteLine($"Exception in Observe(): {ex.Message}");
            }
        }

        private void UpdateGameProcess()
        {
            if (this.trainer.IsHandleCreated)
            {
                this.trainer.Invoke((MethodInvoker)(() =>
                {
                    if (this.trainer.Anstoss.Process == null || this.trainer.Anstoss.Process.Id != this.gameProcess.Id)
                    {
                        this.trainer.Anstoss.Process = this.gameProcess;
                        this.trainer.Anstoss.IsRunning = true;

                        try
                        {
                            this.trainer.Memory.OpenProcess(this.gameProcess.Id);
                            if (!this.initialised)
                            {
                                // Console.WriteLine($"PC: {this.trainer.PlayerView.PlayerController}");
                                this.trainer.PlayerView.RefreshPlayerListView(this.trainer.PlayerView.PlayerController == null ? PlayerEnums.AddressType.OWN : this.trainer.PlayerView.PlayerController.Type);
                                this.trainer.PlayerView.InitMainTabControl();
                                this.trainer.ClubView.RefreshValues(this.trainer.ClubView.ClubController == null ? PlayerEnums.AddressType.OWN : this.trainer.PlayerView.PlayerController.Type);
                                this.trainer.ClubView.InitClubTabControl();
                            }
                            this.initialised = true;
                        }
                        catch (Exception ex)
                        {
                            this.initialised = false;
                            Console.WriteLine("Konnte Prozess nicht öffnen: " + ex.Message);
                        }
                    }
                }));
            }
        }

        public void UpdatePlayerOffsets()
        {
            Club own = new ClubController(this.trainer.Memory, this.IsGog, PlayerEnums.AddressType.OWN).Club;
            new PlayerController(this.trainer.Memory, own, this.IsGog, PlayerEnums.AddressType.OWN);
        }

        private void ClearGameProcess()
        {
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
