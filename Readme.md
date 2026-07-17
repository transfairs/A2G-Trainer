![C#](https://img.shields.io/badge/language-C%23-239120?logo=c-sharp&logoColor=white) ![.NET Framework](https://img.shields.io/badge/.NET-4.0-blueviolet)

# A2G-Trainer

A trainer for **Anstoss 2 Gold**, tested and compatible with both the original CD release and the GOG version.  
Built specifically to run even on legacy systems. Tested working from **Windows XP SP3 x86** onwards, including all Windows OS versions until **Windows 11 x64**.

## 📸 Screenshots
![Team Choice](docs/TeamChoice.png)  
![Screenshot](docs/screenshot_main.png)  
![Club View](docs/ClubView.png)  
![Stadium](docs/Stadium.png)
![Trainees](docs/Trainees.png)  

## 🛠 Features
- ✅ Live memory editing of *Anstoss 2 Gold*
- ✅ **Edit all players** and **amateurs** of **any club**
- ✅ Edit player attributes, positions and skills
- ✅ Supports multiplexed bitfields (skills etc.)
- ✅ **Inspect your youth players**  
- ✅ Runs on **Windows XP** (built against `.NET Framework 4.0`)
- ✅ **Club editing** (name, finances, stadium details)  
- ✅ **Stadium editor** with up to 2 × 32,767 seats per block  
- ✅ **Freeze condition & freshness** in the Team tab so they stay fixed while you play  
- ✅ **Automatic reconnect** — start the trainer before or after the game, it detects a game restart or a different savegame being loaded on its own  
- ✅ **Help section** integrated into the UI  
- ✅ Clean WinForms UI with tabbed views and menu navigation
- ✅ Modular MVC architecture
- ✅ Portable version

## 🧩 Dependencies
- On **Windows XP**, make sure [.NET Framework 4.0](https://www.microsoft.com/en-us/download/details.aspx?id=17718) is installed.  
- On **Windows 11**, no additional installation is necessary.  

## 🚀 Run Instructions
1. Launch `A2G-Trainer-XP.exe` and start **Anstoss 2 Gold** — either order works.  
2. Load your savegame. The tool detects the game automatically and reconnects on its own after a game restart or when you load a different savegame.  
3. Use the **Tool Strip** to switch to the club view (finances, stadium, etc.) or edit other teams.

## 💡 Tips & Hints
- To assign a **negative number of seats** to a stadium block, enter any value **greater than 32,767**.  
- After editing a stadium block, **Block A is automatically set to one week construction time** to ensure all changes are persisted.  

## 🔧 Build Instructions
1. Open the solution in **Visual Studio 2019**  
2. Ensure the target framework is **.NET Framework 4.0**  
3. Build the project (`Release|x86` recommended for XP)  
4. Optional: run the **A2G-Trainer-XP.Tests** project (xUnit) to verify core logic before building a release  

## Extras
Included in this repository is an updated **Cheat Engine table** originally created by [strajk-](https://www.anstoss-juenger.de/index.php/topic,4619.0.html) for the original version of *Anstoss 2 Gold* (not the GOG version).

## Credits
Thanks to the [anstoss-juenger.de](https://www.anstoss-juenger.de/index.php/topic,6260.0.html) community, especially **strajk-**. Some code fragments and ideas were taken from his [A2G-RTE project](https://github.com/strajk-/A2G-RTE). Many thanks to **22** for his efforts and assistance in finding any detail of the game in the memory.

## 🛡 Disclaimer
This tool directly accesses game memory. Use at your own risk.  
This project is **not affiliated** with **Ascaron** or the developers of *Anstoss 2 Gold*.
