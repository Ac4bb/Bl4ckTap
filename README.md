# 🖱️ Bl4ckTap

**Bl4ckTap est un autoclicker** qui automatise les clics de souris pour les tâches répétitives. Il permet de choisir le clic gauche, droit ou les deux, la vitesse et un mode maintien ou continu.

## 📋 Prérequis

- **Windows** avec **.NET Framework 4.8**.
- Si tu télécharges les sources, **compile le projet** (voir Lancement) pour obtenir l'exécutable. Visual Studio et Python ne sont pas nécessaires.

## 🚀 Lancement

Choisis l'une des deux options :

- **Exécutable prêt à lancer :** télécharge `Bl4ckTap.exe` dans **Releases**, puis double-clique dessus.
- **Compiler toi-même :** sur la page GitHub, clique sur **Code → Download ZIP**, puis extrais l'archive. Ouvre PowerShell dans le dossier du projet et lance :

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\bin\Bl4ckTap.exe
```

L'exécutable compilé se trouve dans `bin/`.

## ⚠️ Avertissement Windows

Bl4ckTap n'est pas signé numériquement. Au premier lancement, Windows SmartScreen peut afficher « Windows a protégé votre ordinateur ». C'est normal pour un petit programme indépendant : clique sur **Informations complémentaires**, puis sur **Exécuter quand même**.

Certains antivirus se méfient des autoclickers. Le code source est entièrement visible dans ce dépôt, et tu peux compiler le programme toi-même si tu préfères.

## 📄 Licence

MIT, voir le fichier `LICENSE`.
