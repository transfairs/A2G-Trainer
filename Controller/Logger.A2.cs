using A2G_Trainer_XP.Controller;

namespace AnstossTrainer.Shared
{
    // Anstoss 2 Gold's settings for the shared Logger (Shared/Logger.cs).
    internal static partial class Logger
    {
        static partial void GetAppFolderName(ref string name) => name = "A2G-Trainer-XP";

        static partial void GetDebugEnabled(ref bool enabled) => enabled = Settings.IsDebug;
    }
}
