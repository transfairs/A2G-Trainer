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
    /// <summary>In-app help tab; hosts the (German) RTF help text for each roster context.</summary>
    public partial class HelpView : UserControl
    {
        /// <summary>Creates the Help view and populates its RTF help texts.</summary>
        public HelpView()
        {
            InitializeComponent();
            this.InitHelpTexts();
        }

        /// <summary>Creates the Help view, registers it with the given designer container, and populates its RTF help texts.</summary>
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
{\pard\qj Um mit dem Editieren loszulegen, starten Sie {\b Anstoss 2 Gold} und laden Sie einen Spielstand. Der Trainer erkennt das Spiel automatisch - egal ob Sie ihn vor oder nach dem Spiel starten. Auch nach einem Neustart des Spiels oder dem Laden eines anderen Spielstands verbindet er sich von selbst neu, ganz ohne Ihr Zutun. Los geht's!\par}\par

{\fs18\qc Damit Änderungen übernommen werden, das\par {\b\cf1 Spiel nach dem Editieren speichern und den Spielstand neu laden}.\par}
\par
Manchmal hilft es auch, den Verein noch einmal explizit im {\b Transfermarkt} unter {\b Vereine absuchen} aufzurufen.\par
\par
{\fs24\b Werte einfrieren (Team-Tab)}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Im Team-Tab lassen sich {\b Kondition} und {\b Frische} nicht nur einmalig setzen, sondern auch dauerhaft {\b einfrieren}: Haken Sie die Checkbox neben dem jeweiligen Wert an und tragen Sie den gewünschten Wert ein. Solange die Checkbox angehakt bleibt, schreibt der Trainer diesen Wert laufend zurück - auch wenn das Spiel ihn von selbst verändert.\par}
\par

{\fs24\b Spieler dauerhaft umbenennen}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Eine Namensänderung wird nur dann {\b dauerhaft} gespeichert, wenn der neue Name {\b exakt dieselbe Anzahl Zeichen} hat wie der alte. Ein anders langer Name wird bis zum nächsten Tagesabschluss oder Neuladen zwar noch korrekt angezeigt, fällt danach aber wieder auf den alten Namen zurück - in diesem Fall zeigt der Trainer nach dem Speichern eine Warnung mit dem Namen des betroffenen Spielers an. Details dazu landen zusätzlich in der Logdatei unter {\b %LocalAppData%\\A2G-Trainer-XP\\trainer.log}.\par}
\par

{\fs24\b Mehrere Trainer, Aktien & Länder (Hot-Seat)}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Enthält der geladene Spielstand mehr als einen menschlichen Manager, erscheint im Menü {\b Trainer} für jeden ein eigener Eintrag. Neben Name, Alter, Vermögen und Schwierigkeitsgrad lassen sich dort auch die {\b Kompetenzpunkte} (Verhandlungsgeschick, Motivationsfähigkeit, Trainingsgestaltung, Autorität, Fremdsprachenkenntnisse, Ausstrahlung) für jedes Trainer-Level einzeln bearbeiten sowie bis zu sechs {\b Aktienpositionen} (Land, Verein, Stückzahl, Kaufpreis). Neue Positionen lassen sich dabei nicht anlegen, nur bereits vorhandene ändern. Auf der Registerkarte {\b Länderauswahl} wird zusätzlich zum Hauptland festgelegt, welche bis zu vier Bonusländer freigeschaltet sind.\par}\par

{\fs18\qc Wechselt im Hot-Seat-Modus der Zug zu einem anderen Manager, erkennt der Trainer das {\b\cf3 automatisch} und aktualisiert die Ansicht von selbst - ganz ohne manuelles Neuverbinden.\par}
\par
";

            this.TraineeHelp.Rtf = @"{\rtf1\ansi
{\colortbl ;\red255\green0\blue0;\red0\green0\blue255;\red0\green128\blue0;}
{\fonttbl{\f0 Arial;}}
\fs20\b0\cf0
{\fs24\b !! Keine Vereinsansicht !!}\par
-------------------------------------------------------------------------------------------------------\par
Um eine vollständige Liste der {\i Jugendspieler} zu erhalten, navigieren Sie im Spiel auf den {\b Transfermarkt} und klicken dann auf {\b Jugendspieler}.\par
Anschließend verwenden Sie den Menüpunkt {\b Jugendspieler} hier im Trainer oder klicken auf {\b Team neuladen}.\par\par

{\fs24\b Adressbereich}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Die {\i Jugendspieler} teilen sich denselben Adressbereich im Speicher Ihres Rechners wie die {\i Dynamische Mannschaft}. Hat man zuletzt auf einen Verein geklickt, erscheinen diese Spieler eventuell hier.\par}
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
            /*
            Image img = Image.FromFile("Trainer-Logo.ico");
            Clipboard.SetImage(img);
            this.GeneralHelp.Paste();
            */
        }
    }
}
