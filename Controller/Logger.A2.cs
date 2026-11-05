using A2G_Trainer.Controller;

namespace AnstossTrainer.Shared
{
    // Anstoss 2 Gold's settings for the shared Logger (Shared/Logger.cs).
    internal static partial class Logger
    {
        /// <summary>The log folder's name under %LocalAppData% (also shown in the About dialog).</summary>
        internal const string AppFolder = "A2G-Trainer";

        static partial void GetAppFolderName(ref string name) => name = AppFolder;

        static partial void GetDebugEnabled(ref bool enabled) => enabled = Settings.IsDebug;
    }
}
