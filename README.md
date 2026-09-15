# AltTabPlus

Un remplaçant d'Alt+Tab pour Windows qui règle un problème précis : quand
plusieurs fenêtres sont ancrées ensemble (Snap Layouts / Snap Groups),
l'Alt+Tab natif les affiche à la fois comme groupe *et* comme fenêtres
individuelles — ça pollue le sélecteur au lieu de le simplifier, et Windows
ne propose aucun réglage officiel pour corriger ça
([discussion Microsoft Community](https://techcommunity.microsoft.com/discussions/windows11/snapped-window-groups-and-individual-windows/3842896)).

AltTabPlus remplace entièrement le switcher : un groupe ancré devient **une
seule tuile**, avec un badge indiquant combien de fenêtres il contient.

## Comment ça marche

Windows n'expose aucune API publique pour savoir "quelles fenêtres
appartiennent à quel Snap Group" — cette information vit uniquement dans le
shell (`twinui.pcshell.dll`), sans interface documentée. Le projet contourne
ça avec de la géométrie :

1. **`Hooking/KeyboardHook.cs`** — un hook clavier bas niveau (`WH_KEYBOARD_LL`)
   intercepte Alt+Tab et empêche l'appel de remonter jusqu'au switcher natif
   (`CallNextHookEx` n'est pas appelé pour cette touche). C'est la même
   technique qu'utilisent des outils comme AltTabTerminator ou GoToWindow —
   il n'existe pas de méthode "propre" pour désactiver le switcher natif.
2. **`Windows/WindowEnumerator.cs`** — énumère les fenêtres éligibles à
   l'Alt-Tab (visibles, sans propriétaire, pas des tool windows, non
   "cloaked" c-à-d pas sur un autre bureau virtuel).
3. **`Grouping/SnapGroupDetector.cs`** — la vraie logique du projet : regroupe
   les fenêtres par moniteur, relie celles dont les rectangles se touchent
   bord à bord, et ne retient un groupe que si son union couvre une bonne
   partie de la zone de travail du moniteur (signature géométrique d'un Snap
   Layout plutôt que deux fenêtres côte à côte par hasard).
4. **`UI/SwitcherOverlayForm.cs` + `UI/ThumbnailPanel.cs`** — l'overlay qui
   remplace le switcher natif, avec des miniatures live via
   `DwmRegisterThumbnail` (le même mécanisme que les aperçus de la barre des
   tâches).
5. **`Switching/SwitchTarget.cs`** — une tuile = soit une fenêtre seule, soit
   un groupe entier ; `Activate()` remet au premier plan toutes les fenêtres
   du groupe.

## Build

Le projet cible `net8.0-windows` (WinForms). Un `dotnet build` doit
s'exécuter **sur Windows** — WinForms + les appels P/Invoke user32/dwmapi ne
tournent que là :

```
dotnet build AltTabPlus.sln
```

ou ouvrir `AltTabPlus.sln` dans Visual Studio / Rider.

Pour lancer en debug, il faut lancer Visual Studio (ou `dotnet run`) **en
administrateur** si tu veux capturer Alt+Tab dans des fenêtres qui tournent
elles-mêmes en admin (limitation UIPI classique de Windows : un hook clavier
non-admin ne voit pas les touches destinées à une fenêtre admin).

## Limitations connues (v0)

- **Détection heuristique, pas garantie.** Deux fenêtres redimensionnées
  manuellement pour se toucher peuvent être détectées comme un "groupe" à
  tort ; un Snap Group dont une fenêtre a été redimensionnée après coup peut
  ne plus être détecté. Le seuil de couverture (`MinWorkAreaCoverage` dans
  `SnapGroupDetector.cs`) est ajustable.
- **Pas de persistance de préférences** (pas encore de fenêtre de réglages).
- **Pas d'installeur** — c'est un exécutable autonome pour l'instant.
- **Pas de gestion multi-bureaux virtuels avancée** au-delà du filtre cloaked.
- Aucune télémétrie, aucun accès réseau — tout tourne localement.

## Roadmap possible

- [ ] Réglages persistés (seuil de détection, raccourci alternatif, thème)
- [ ] Vraies miniatures pour chaque fenêtre d'un groupe (mini-grille dans la
      tuile plutôt qu'une seule preview)
- [ ] Détection explicite via l'API interne de Snap Layout si elle devient
      accessible un jour, en repli sur l'heuristique sinon
- [ ] Installeur (MSIX ou simple installeur signé) + démarrage automatique
- [ ] Tests unitaires sur `SnapGroupDetector` avec des géométries de fenêtres
      simulées (aucune dépendance Win32 nécessaire pour ça)

## Licence

MIT — voir [`LICENSE`](./LICENSE).
