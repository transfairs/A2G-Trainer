using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    public partial class HelpView : UserControl
    {
        public HelpView()
        {
            InitializeComponent();
            this.InitHelpTexts();
        }

        public HelpView(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
            this.InitHelpTexts();
        }

        private void InitHelpTexts()
        {
            int padding = 2;
            foreach (RichTextBox rtb in new RichTextBox[] { this.GeneralHelp, this.DynamicHelp, this.TraineeHelp})
            {
                rtb.SelectionIndent = padding;
                rtb.SelectionRightIndent = padding;
            }

            this.GeneralHelp.Rtf = @"{\rtf1\ansi
{\colortbl ;\red255\green0\blue0;\red0\green0\blue255;\red0\green128\blue0;}
{\fonttbl{\f0 Arial;}}
\fs20\b0\cf0
{\fs24\b Willkommen zu A2G-Trainer-XP!}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Um mit dem Editieren loszulegen, starten Sie {\b Anstoss 2 Gold} und laden Sie einen Spielstand. Jetzt öffnen Sie den Trainer oder starten ihn neu, wenn er bereits läuft. Los geht's!\par}\par

{\fs18\qc Damit Änderungen übernommen werden, das\par {\b\cf1 Spiel nach dem Editieren speichern und den Spielstand neu laden}.\par}
\par
Manchmal hilft es auch, den Verein noch einmal explizit im {\b Transfermarkt} unter {\b Vereine absuchen} aufzurufen.\par
";

            this.TraineeHelp.Rtf = @"{\rtf1\ansi
{\colortbl ;\red255\green0\blue0;\red0\green0\blue255;\red0\green128\blue0;}
{\fonttbl{\f0 Arial;}}
\fs20\b0\cf0
{\fs24\b Jugendspieler}\par
-------------------------------------------------------------------------------------------------------\par
Die {\i Jugendspieler} lassen sich im Trainer anzeigen, jedoch {\b nicht bearbeiten}. Jegliche Änderungen werden vom Spiel ignoriert.\par\par

{\fs24\b !! Keine Vereinsansicht !!}\par
-------------------------------------------------------------------------------------------------------\par
Um eine vollständige Liste der {\i Jugendspieler} zu erhalten, navigieren Sie im Spiel auf den {\b Transfermarkt} und klicken dann auf {\b Jugendspieler}.\par
Anschließend verwenden Sie den Menüpunkt {\b Jugendspieler} hier im Trainer oder klicken auf {\b Team neuladen}.\par\par

{\fs24\b Adressbereich}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qc Die {\i Jugendspieler} teilen sich denselben Adressbereich im Speicher Ihres Rechners wie die {\i Dynamische Mannschaft}. Hat man zuletzt auf einen Verein geklickt, erscheinen diese Spieler eventuell hier.\par}
Da dieser Adressbereich dynamisch gefüllt wird, sollten Sie immer den richtigen Kontext beachten.\par";

            this.DynamicHelp.Rtf = @"{\rtf1\ansi
{\colortbl ;\red255\green0\blue0;\red0\green0\blue255;\red0\green128\blue0;}
{\fonttbl{\f0 Arial;}}

\fs20\b0\cf0
{\fs24\b !! Keine Vereinsansicht !!}\par
-------------------------------------------------------------------------------------------------------\par
Nachdem Sie Ihren Spielstand in {\i Anstoss 2 Gold} geladen haben, besuchen Sie den {\b Transfermarkt}, klicken Sie auf {\b Vereine absuchen} und wählen Sie einen Verein aus.\par
Anschließend können Sie hier im Trainer die {\i Dynamische Mannschaft} sehen, nachdem Sie sie vom Menü aus neu aufgerufen oder auf {\b Team neuladen} geklickt haben.\par\par

{\fs24\b Hinweise zur Bearbeitung}\par
-------------------------------------------------------------------------------------------------------\par
\fs20\b0 Es können {\b nur ganze Vereine} bearbeitet werden, da es sonst zu {\cf1 unvorhersehbarem Verhalten\cf0} im Spiel kommen kann.\par
Sie können im Spiel zwar {\b Spieler suchen}, wenn Sie {\i NORACSA} haben, diese werden dann im Trainer auch angezeigt, lassen sich aber {\b nicht bearbeiten}. Das Bearbeiten funktioniert nur zuverlässig über {\b Vereine absuchen}.\par\par

{\fs24\b Adressbereich}\par
-------------------------------------------------------------------------------------------------------\par
Die {\i Dynamische Mannschaft} belegt denselben Adressbereich im Speicher Ihres Rechners wie die Jugendspieler. Hat man zuletzt auf diese geklickt, erscheinen sie hier. Besser ist es jedoch, den Menüpunkt {\b Jugendspieler} zu verwenden, da dort eine {\cf3 vollständige Liste\cf0} generiert wird.\par
Da dieser Adressbereich vom Spiel dynamisch gefüllt wird, achten Sie bitte immer darauf, den richtigen Verein zu bearbeiten.\par";

            this.TraineeHelp.Rtf = @"{\rtf1\ansi
{\colortbl ;\red255\green0\blue0;\red0\green0\blue255;\red0\green128\blue0;}
{\fonttbl{\f0 Arial;}}
\fs20\b0\cf0
{\fs24\b Jugendspieler}\par
-------------------------------------------------------------------------------------------------------\par
Die {\i Jugendspieler} lassen sich im Trainer anzeigen, jedoch {\b nicht bearbeiten}. Jegliche Änderungen werden vom Spiel ignoriert.\par\par

{\fs24\b !! Keine Vereinsansicht !!}\par
-------------------------------------------------------------------------------------------------------\par
Um eine vollständige Liste der {\i Jugendspieler} zu erhalten, navigieren Sie im Spiel auf den {\b Transfermarkt} und klicken dann auf {\b Jugendspieler}.\par
Anschließend verwenden Sie den Menüpunkt {\b Jugendspieler} hier im Trainer oder klicken auf {\b Team neuladen}.\par\par

{\fs24\b Adressbereich}\par
-------------------------------------------------------------------------------------------------------\par
Die {\i Jugendspieler} teilen sich denselben Adressbereich im Speicher Ihres Rechners wie die {\i Dynamische Mannschaft}. Hat man zuletzt auf einen Verein geklickt, erscheinen diese Spieler eventuell hier.\par
Da dieser Adressbereich dynamisch gefüllt wird, sollten Sie immer den richtigen Kontext beachten.\par";
            /*
            Image img = Image.FromFile("Trainer-Logo.ico");
            Clipboard.SetImage(img);
            this.GeneralHelp.Paste();
            */
        }
    }
}
