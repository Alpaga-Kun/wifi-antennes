<p align="center">
  <img src="assets/icon.png" width="96" alt="Icône Wi-Fi Antennes">
</p>

<h1 align="center">Wi-Fi Antennes</h1>

<p align="center">
  📶 Trouve la meilleure position pour les antennes Wi-Fi de ton PC, en temps réel.
</p>

---

Tu as une carte Wi-Fi avec des antennes à l'arrière de ta tour et une connexion qui décroche ?
**Wi-Fi Antennes** scanne les bornes autour de toi toutes les ~2,5 s et te montre en direct si le signal
**s'améliore ou se dégrade** pendant que tu bouges les antennes.

## ✨ Fonctionnalités

| | |
|---|---|
| 🎯 **Jauge en direct** | Signal en dBm de la meilleure borne du réseau choisi, coloré selon le niveau (très faible → excellent) |
| 📈 **Tendance** | « +4 dB : mieux qu'au départ » / « moins bien » par rapport à une position de référence |
| 🏆 **Record** | Le meilleur signal obtenu est marqué sur la jauge, avec un **son** à chaque nouveau record (pratique quand tu es derrière le PC) |
| 🕒 **Historique** | Courbe des ~3 dernières minutes avec les seuils de qualité |
| 📡 **Bornes captées** | Bande (2,4 / 5 / 6 GHz), canal, signal de chaque borne, et celle à laquelle tu es connecté |
| 📝 **Positions notées** | Décris chaque position testée (« antennes verticales, en haut du bureau »…), la meilleure est mise en évidence |
| 💾 **Export CSV** | Exporte tes positions notées pour les comparer |
| 📌 **Premier plan** | La fenêtre reste au-dessus des autres pendant les essais |
| 🔁 **Mémoire du réseau** | Le dernier réseau choisi est retenu au prochain lancement |

## ⬇️ Installation

### Option 1 : télécharger l'exe

1. Va dans [**Releases**](../../releases/latest) et télécharge `WifiAntennes.exe`.
2. Lance-le. Aucune installation, aucune dépendance (utilise .NET Framework 4.x, inclus dans Windows 10/11).

> ⚠️ L'exe n'est pas signé : Windows SmartScreen peut afficher un avertissement.
> Clique sur **Informations complémentaires → Exécuter quand même**, ou compile-le toi-même (option 2).

### Option 2 : compiler depuis les sources

Aucun outil à installer : le script utilise le compilateur C# fourni avec Windows.

```powershell
git clone https://github.com/Alpaga-Kun/wifi-antennes.git
cd wifi-antennes
powershell -ExecutionPolicy Bypass -File .\build.ps1            # -> dist\WifiAntennes.exe
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Install   # + installe et crée les raccourcis Bureau / menu Démarrer
```

## 🧭 Utilisation

1. Choisis ton réseau en haut à droite (par défaut : le dernier choisi, sinon celui auquel tu es connecté).
2. Attends 2-3 mesures : la première valeur devient la **référence** 📍.
3. Bouge les antennes **doucement**, attends **~5 s par position** (les valeurs sont moyennées sur 2 scans).
4. Quand une position est bonne, clique sur **Noter la position**.
5. Garde la meilleure ⭐.

**Astuces**
- Des écarts de 1-2 dB sont du bruit ; à partir de **+5 dB**, c'est un vrai gain.
- Antennes **verticales**, le plus **haut** possible, sans métal (boîtier, bureau) devant.
- Coupe le partage de connexion de ton téléphone pendant les tests : il peut émettre sur le même canal et fausser les mesures.

## 📊 Niveaux de signal

| Signal | Niveau | En pratique |
|---|---|---|
| ≥ -60 dBm | 🟢 Excellent | Tout passe |
| -67 à -60 | 🟢 Bon | Visio, streaming OK |
| -72 à -67 | 🟡 Moyen | Utilisable, quelques ralentissements |
| -78 à -72 | 🟠 Faible | Coupures probables |
| < -78 | 🔴 Très faible | Connexion instable |

## ⚙️ Fonctionnement

- Lecture des bornes via l'**API Wi-Fi native de Windows** (`wlanapi.dll` : `WlanScan`, `WlanGetNetworkBssList`).
- Interface **WinForms** dessinée sur mesure (thème sombre, icônes Segoe Fluent), compatible écrans haute résolution.
- Un seul fichier source : [`src/WifiAntennes.cs`](src/WifiAntennes.cs).

> 🔒 Sur Windows 11, l'accès à la liste des bornes Wi-Fi nécessite que la **localisation** soit autorisée pour les applications de bureau
> (*Paramètres → Confidentialité et sécurité → Localisation*). L'application ne lit ni n'envoie ta position : tout reste en local.

## 🔒 Sécurité & confidentialité

| | |
|---|---|
| 🌐 **Aucune connexion réseau** | L'application ne contacte aucun serveur : pas de télémétrie, pas de mise à jour automatique, pas de publicité. |
| 👤 **Aucune donnée personnelle collectée** | Seul le nom du dernier réseau choisi est enregistré localement (`%APPDATA%\WifiAntennes\reseau.txt`). Les positions notées restent en mémoire, sauf si tu les exportes toi-même en CSV. |
| 🛡️ **Droits standard** | Pas de droits administrateur demandés, aucune modification de la configuration Wi-Fi ou du système : lecture seule. |
| 🧩 **Anti DLL hijacking** | Les DLL natives ne sont chargées que depuis `System32` (jamais depuis le dossier de l'exe, par ex. *Téléchargements*). |
| 📄 **Export CSV protégé** | Les noms de réseaux (choisis par n'importe qui à portée) sont neutralisés contre l'injection de formules Excel. |
| 🧱 **Lecture mémoire bornée** | Les données renvoyées par l'API Wi-Fi sont lues sans jamais dépasser la taille annoncée par Windows. |
| 📦 **Zéro dépendance** | Un seul fichier source, compilable avec le compilateur intégré à Windows : rien à télécharger, rien à faire confiance en plus. |

**Vérifier l'exe téléchargé** : compare son empreinte avec celle indiquée dans la release :

```powershell
Get-FileHash .\WifiAntennes.exe -Algorithm SHA256
```

Pour une confiance totale, compile-le toi-même depuis les sources (`build.ps1`).

## 📄 Licence

[MIT](LICENSE)
