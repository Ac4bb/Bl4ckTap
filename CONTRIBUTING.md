# Contribuer à Bl4ckTap

Les corrections et améliorations sont les bienvenues. Pour signaler un problème,
précise ta version de Windows, le mode utilisé, les boutons sélectionnés, les CPS,
le raccourci et les étapes permettant de reproduire le problème.

## Vérifier une modification

Depuis le dossier du projet, sur Windows avec .NET Framework 4.8, compile le projet
pour vérifier que tout fonctionne :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Lance ensuite `bin\Bl4ckTap.exe` et teste ta modification. Vérifie aussi le rendu
de l'interface si ta modification concerne les contrôles ou leur disposition.

Une pull request doit expliquer le problème, la correction et les vérifications
effectuées. Ne joins pas tes réglages personnels ni les exécutables générés.
Garde les modifications de présentation séparées des changements du moteur.

Les contributions sont proposées sous la licence MIT du projet.
