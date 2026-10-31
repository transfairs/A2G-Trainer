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
{\pard\qj Das Spiel speichert jeden Vor- und Nachnamen nur einmal, alle Spieler mit demselben Namen teilen sich diesen Eintrag. In der {\b Original- und der GOG-Version} wird jede Namensänderung {\b dauerhaft} gespeichert, in beliebiger Länge bis zu 9 Zeichen (Vorname) bzw. 15 Zeichen (Nachname): Kommt der neue Name im Spielstand schon vor, verwendet der Trainer diesen Eintrag, sonst fügt er ihn neu hinzu - so wie das Spiel selbst neue Namen anlegt. Andere Spieler mit dem alten Namen behalten ihn in jedem Fall. In Versionen, deren Adressen der Trainer nicht bestätigen kann (z. B. die {\b 2007er-CD}, solange der Trainer ihre Adressen nicht bestätigen konnte), gilt weiterhin: dauerhaft nur, wenn der neue Name {\b im Spielstand schon vorkommt} oder der alte Name {\b nur diesem einen Spieler} gehört und der neue {\b exakt gleich viele Zeichen} hat. Kann ein Name nicht dauerhaft gespeichert werden, wird er bis zum nächsten Tagesabschluss oder Neuladen zwar noch angezeigt, und der Trainer zeigt nach dem Speichern eine Warnung mit dem Namen des betroffenen Spielers an. Details dazu landen zusätzlich in der Logdatei unter {\b %LocalAppData%\\A2G-Trainer-XP\\trainer.log}.\par}
\par

{\fs24\b Verletzungen, Sperren & Vertrag}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Vertragslaufzeit, {\b Verletzungstage} und Verletzungsart, {\b verletzungsanfällig}, gesperrte Spiele, {\b gedopt} und gelbe Karten schreibt der Trainer in den gespeicherten Spielerdatensatz - sie bleiben also auch nach Tagesabschluss und Neuladen erhalten, auch bei Jugendspielern. Neu angehakt ist ein Spieler so lange verletzungsanfällig, wie das Spiel es üblicherweise festlegt. Ebenso dauerhaft sind der {\b Kaufpreis}, der {\b Stammverein} geliehener Spieler und die Saisonwerte der Registerkarte {\b Statistik} (Einsätze, Ein- und Auswechslungen, rote und gelb-rote Karten, Tore, Vorlagen, Jokertore, Eigentore, {\b Tore für Aufwertung}).\par}
\par

{\fs24\b Spieler wechseln lassen}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Auf der Registerkarte {\b Wechsel} schicken Sie den gewählten Spieler zu einem anderen Verein - als Wechsel, Leihe oder Leihe mit Kaufoption, auch einen Spieler eines anderen Vereins zu Ihrem. Das Spiel führt den Wechsel beim nächsten {\b Tagesabschluss} mit seinen eigenen Funktionen aus, mit Ablöse, Vertrag, Transferhistorie und Meldung. Nach Tag 216 trägt das Spiel einen Wechsel sonst nur zum Saisonende ein; mit {\b sofort} wechselt der Spieler trotzdem direkt. Ein voller Kader (30 Spieler) lehnt den Wechsel ab. Nur in der Original-Version und nicht für Jugendspieler.\par}
\par

{\fs24\b Fenster vergrößern}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Das Fenster lässt sich größer ziehen oder maximieren; alle Felder, Listen, Schriften und diese Hilfe wachsen mit. Kleiner als die Startgröße wird es nicht.\par}
\par

{\fs24\b GOG-Version und 2007er-CD}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Bei der {\b GOG-Version} (und der 2007er-CD) sucht der Trainer beim ersten Laden des eigenen Teams selbst, wo die dauerhaft gespeicherten Spielerdaten liegen. Findet er sie nicht, liest und speichert er die Werte wie früher nur über den Anzeigespeicher. {\i Jugendspieler} lassen sich dann nicht dauerhaft ändern, und Namensänderungen gelten nur bis zum nächsten Laden. Das Ergebnis steht in der Logdatei unter {\b %LocalAppData%\\A2G-Trainer-XP\\trainer.log} - bei Problemen bitte mitschicken.\par}
\par

{\fs24\b Mehrere Trainer, Aktien & Länder (Hot-Seat)}\par
-------------------------------------------------------------------------------------------------------\par
{\pard\qj Enthält der geladene Spielstand mehr als einen menschlichen Manager, erscheint im Menü {\b Trainer} für jeden ein eigener Eintrag. Neben Name, Alter, Vermögen und Schwierigkeitsgrad lassen sich dort auch die {\b Kompetenzpunkte} (Verhandlungsgeschick, Motivationsfähigkeit, Trainingsgestaltung, Autorität, Fremdsprachenkenntnisse, Ausstrahlung) für jedes Trainer-Level einzeln bearbeiten, das {\b Image} (z. B. Schleifer, Wissenschaftler) sowie bis zu sechs {\b Aktienpositionen} (Land, Verein, Stückzahl, Kaufpreis). Neue Positionen lassen sich dabei nicht anlegen, nur bereits vorhandene ändern.\par}\par

{\pard\qj Die Registerkarte {\b Vertrag} zeigt alle Bedingungen des aktuellen Vertrags: Grundgehalt, Prämien, {\b Saisonziel}, Restlaufzeit, Verlängerungsoption und Extras (Fahrzeug, Wohnung). Dort stehen auch Land und Verein für die {\b nächste Saison} - angeboten werden nur Vereine, die das Spiel beim Saisonwechsel annimmt (im Hauptland jede Liga, in einem Bonusland nur die 1. Liga) -, die {\b Freigabeklausel} und {\b Jobangebot anfordern}: Der gewählte Verein macht beim nächsten {\b Tagesabschluss} ein normales Jobangebot, das Sie im Spiel annehmen. Vorher unbedingt speichern.\par}\par

{\pard\qj Auf der Registerkarte {\b Länderauswahl} schalten Sie die bis zu vier {\b Bonusländer} neben dem Hauptland an und ab. Abschalten geht nicht, solange ein menschlicher Trainer dort arbeitet oder zur nächsten Saison dorthin wechselt; zuschalten nur ein Land, dessen Vereine noch im Speicher sind (beim Karrierestart gewählt). Der Grund für ein gesperrtes Häkchen steht daneben.\par}\par

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
