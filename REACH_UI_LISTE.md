# Refaire l'interface avec Reach UI — la liste complète

Tout ce qui est à l'écran dans Milchemet HaYetzer, écran par écran, avec pour chacun :
ce qu'il montre, ses états, le script qui le pilote et **les champs à rebrancher** dans
l'inspecteur une fois le visuel refait. Le détail actuel (tailles, ancres, lignes de
`HudBuilder.cs`) est dans `UI_ELEMENTS.md` ; ici c'est la vue « que faut-il refaire ».

Compte : **2 scènes, 7 blocs de HUD permanents, 15 panneaux, 9 éléments flottants.**

---

## 0. Ce qu'il faut savoir avant de commencer

**Les scripts ne dessinent pas, ils branchent.** Chaque panneau a un contrôleur
(`Assets/Scripts/UI/*.cs`) avec des champs publics : `TMPro.TextMeshProUGUI`, `Image`,
`Button` (UnityEngine.UI), `GameObject`, `CanvasGroup`. Tu peux refaire le visuel
entièrement en Reach UI **tant que ces champs reçoivent un composant du bon type**.
Deux points d'attention avec Reach UI :

- Les boutons Reach UI (`ButtonManager`) ne sont pas des `UnityEngine.UI.Button`. Deux
  solutions : (a) tu gardes un `Button` Unity (image transparente) par-dessus ou dedans
  le bouton Reach, c'est lui que tu glisses dans le champ ; (b) tu me dis et je convertis
  les contrôleurs pour accepter `ButtonManager` (le Trivia le fait déjà :
  `TriviaManager.answerButtons` est un `ButtonManager[]` du Modern UI Pack).
- Les barres de progression Reach UI (`ProgressBar`) ne sont pas des `Image` en mode
  Filled. Les contrôleurs remplissent des `Image.fillAmount` (`baseFill`, `hpFill`,
  barres de phase). Même choix : garder une Image Filled dans le widget, ou je convertis.

**Hébreu, droite-à-gauche.** Tous les textes sont en hébreu, TextMeshPro en mode
*Right to Left*, police hébraïque (le builder cherche n'importe quel `TMP_FontAsset`
hébraïque du projet : `HudBuilder.FindHebrewFont`). Les chiffres et mots latins dans une
phrase hébraïque passent par `Rtl.cs` (sinon « 40 » devient « 04 »), le crénage est
coupé au lancement par `TextFeatures.cs`. Les widgets Reach UI qui ont leur propre
TMP doivent donc être réglés en RTL + police hébraïque, et les textes alignés **à
droite** ; la première colonne d'une grille est à droite (légende, choix du sujet).

**Aucun texte dans le code.** Tout vient de `Assets/Resources/hud_labels.json`
(section indiquée à chaque bloc) via `HudLabelsRuntime.Get(clé)`, et des enseignements
de `Assets/Resources/teachings.json`. Les libellés « cuits » dans la scène sont posés
par le builder ; ceux lus au lancement (`Awake`) se mettent à jour seuls.

**Deux familles d'éléments :**
- **Cuits dans la scène** par *Milchemet → Construire le HUD* (`Editor/HudBuilder.cs`) :
  c'est ceux-là que tu refais en Reach UI. Une fois refaits à la main, **ne relance plus
  le builder** (il reconstruit tout depuis le code et écraserait ton travail) — ou dis-moi
  et je le désactive bloc par bloc.
- **Construits en code à chaque lancement** (§ 4) : ils ne sont pas dans la scène, ils se
  dessinent seuls. Pour les passer en Reach UI il faut réécrire leur script : je peux le
  faire, prévois-le à part.

**Résolution de référence** : CanvasScaler 1920 × 1080, échelle par largeur ; le jeu est
WebGL et doit se jouer **au téléphone** — garde les cibles tactiles ≥ 64 px et les
polices ≥ 20 px.

**Thème actuel** (à remplacer par celui de Reach UI) : plaques 9-slice
`Assets/Resources/UI/ui_plate_cyan/gold/red`, `ui_button_cyan/gold`, `ui_panel_dark`,
`ui_hexgrid`, `ui_scanlines`, `ui_vignette`, `ui_glowline`, `ui_bracket`, appliquées par
`HudBuilder.ApplyElectronicTheme`. Couleurs de référence : texte `#E6ECFF`, or
`#FFC65C` (Énergie, allié), cyan `#3DE0D0` (Base, Cristal), orange `#FF6B4A` (menace,
Shofar), rouge `#FF4D5E` (ennemi, danger), fond `#0E1228`.

---

## 1. Scène MainMenu — le menu principal

Contrôleur : `UI/MainMenuController.cs`. Libellés : section `mainMenu`. Fond : scène 3D
animée (`MainMenuDiorama.cs` — caméra qui respire, hexagones qui pulsent) et hexagones-
boutons (`MainMenuHexButton.cs`, grossissent au survol).

| Élément | Rôle | Champ à brancher |
|---|---|---|
| Titre (image) | logo `Resources/Photos/titre_milchemet*.png` | `titleImage` (Image), `titleText` / `subtitleText` (TMP, si texte au lieu de l'image) |
| 3 cartes de difficulté | choix קל / בינוני / קשה, une sélectionnée à la fois (fond + cadre accentués) | `difficultyButtons[3]` (Button), `difficultyBackgrounds[3]` (Image), `difficultyFrames[]` (Image), `accentColors[]` |
| Texte de la difficulté choisie | description sous les cartes | `selectedBodyText` (TMP), `bodyLabels[3]` |
| Bouton Nouvelle partie | lance la scène Game (avec visite d'intro) | `newGameButton`, `newGameText` |
| Bouton Reprendre | ouvre la liste des sauvegardes | `loadGameButton`, `loadGameText` |
| **Liste des parties** (§ 4) | construite en code : `LoadGamePanel.cs` — titre, aide, ligne par partie (tour, difficulté, reste), bouton fermer ; libellés `saves` | à réécrire si tu la veux en Reach UI |

---

## 2. Scène Game — le HUD permanent

Tout est sous `HUD Canvas/HUD/`. Contrôleur : `UI/HudController.cs`. Libellés : racine du
JSON (`energy`, `endTurn`, `phases`, `turn`, `portalCountFormat`…).

| Bloc | Position | Contenu et états | Champs |
|---|---|---|---|
| **Énergie** | haut droite | valeur (grosse), gain du tour en dessous (`+15`, apparaît puis s'efface) ; un vol d'Énergie (§ 4) vient s'y poser | `energyText`, `energyDeltaText` |
| **Tour et phases** | haut centre | numéro de tour + rail de 5 étapes (dépense / Tanks / Yetzer Hara / bâtiments / fin), chaque étape = barre qui se remplit + nom, l'étape active en couleur | `turnText`, `phaseSteps[5]` (`bar` Image Filled + `label` TMP), `phaseLabels[5]` |
| **Objectifs** | haut gauche | 6 pastilles de Shofars (allumée = debout, éteinte = retourné) + compteur `x/6` ; jauge de la Base (barre + PV) | `portalDots[6]`, `portalCountText`, `baseFill` (Image Filled), `baseHpText` |
| **Fin de tour** | bas centre | gros bouton, actif seulement en phase de dépense ; raccourci Espace | `endTurnButton`, `endTurnRoot` |
| **Menace** | bas, à gauche de Fin de tour | bouton bascule « voir / cacher la menace » (fantômes des ennemis, `ThreatPreview.cs`) ; le libellé change avec l'état ; raccourci Tab | `ThreatPreviewButton.label` ; libellés `banners.threatShow/threatHide` |
| **Aide** | bas gauche | bouton `?` qui ouvre *Comment jouer* (§ 3.11) | `HowToPlayPanel.openButton` |
| **Légende** | (dans la page d'aide et/ou coin) | en-tête + 10 entrées icône + mot, première colonne à droite | `HudLegend.headerText`, `entryRoots[10]`, `entryIcons[10]`, `iconColors[10]` |
| **Barre des phases automatiques** (§ 4) | bas | 3 boutons pendant les phases où l'on regarde : allure (normal / rapide / éclair), étape suivante, tout passer ; libellés `pace` | construite en code : `PhaseSkipBar.cs` |
| **Bandeau de phase** (§ 4) | centre | « phase des Tanks », « le Yetzer Hara joue »… apparaît 1 s puis s'efface ; libellés `banners` | construit en code : `PhaseBanner.cs` |

**Pictogrammes** : partout où un `Image` porte un `IconBinder`, le dessin vient de
`IconLibrary` / `IconOverrides` (voir `ICONS.md`) — garde le composant `IconBinder`
sur tes nouvelles images, il pose le bon sprite au lancement.

---

## 3. Scène Game — les panneaux

### 3.1 Menu d'une case
S'ouvre au clic sur une case, **à droite ou à gauche de la case** (suit la caméra).
Contrôleur `UI/HexActionMenu.cs`, libellés `hexMenu`.

| Élément | Champ |
|---|---|
| panneau (pivot bas-centre) + voile cliquable derrière | `panel` (RectTransform), `dim` (GameObject) |
| bouton **Voir de près** | `viewButton`, `viewText` |
| bouton **Détails** | `detailsButton`, `detailsText` |
| bouton **Utiliser l'Énergie** (fond doré si on a les moyens, gris sinon ; mention « hors de portée » / « ombre du Shofar » / « désert ») + prix avec icône Énergie | `useButton`, `useBackground`, `useText`, `costText` |

### 3.2 Info-bulle / Détails d'une case
Panneau de détails (prix, statistiques, effet, évolutions). Contrôleur
`UI/HexTooltipController.cs` (1 500 lignes de contenu — ne pas toucher au code, juste
rebrancher), libellés `tooltip`.

| Élément | Champ |
|---|---|
| voile + racine | `detailsDim`, `panelRoot` |
| titre, sous-titre, prix, « il te restera X » | `titleText`, `subtitleText`, `costText`, `afterBalanceText`, `costRoot` |
| icône du type (teintée) | `kindIcon` |
| **photo** de l'élément (`Resources/Photos`, hauteur variable) | `photoRoot`, `photoImage` |
| zone de contenu (lignes de stats) | `contentRoot` |
| rangée des **3 évolutions** (vignette + cadre, l'actuelle et la suivante accentuées) | `evolutionRoot`, `evolutionSlots[3]`, `evolutionFrames[3]`, `evolutionThumbs[3]` |
| texte d'effet | `effectText` |

### 3.3 Panneau du Shofar
Clic sur un Shofar. Contrôleur `UI/PortalPanelController.cs`, libellés `portal`.

| Élément | Champ |
|---|---|
| racine + barre d'accent | `panelRoot`, `accentBar` |
| nom (« le Shofar du nord-est »), niveau | `titleText`, `levelText` |
| PV : texte + barre | `hpText`, `hpFill` |
| instabilité : texte + **4 segments** (fissures) | `instabilityText`, `instabilitySegments[4]` |
| encart bouclier (titre + corps, couleur selon l'état) | `shieldBox`, `shieldTitleText`, `shieldBodyText` |
| ligne « évolue dans N tours » | `evolveRow`, `evolveText` |

### 3.4 Carte d'unité + flash Impact
Pendant les phases automatiques : qui agit, sur quoi. Contrôleur `UI/UnitActionCard.cs`,
libellés `unit`.

| Élément | Champ |
|---|---|
| racine + accent (bleu Tank / rouge ennemi) | `panelRoot`, `accentBar` |
| nom, sous-titre (rôle, cible), PV texte + barre | `titleText`, `subtitleText`, `hpText`, `hpFill` |
| **flash Impact** : gros chiffre + légende (dégâts, bouclier, Base touchée, déplacement) | `flashRoot`, `flashValueText`, `flashCaptionText` |

### 3.5 Choix du Tank (posture)
Au clic « poser un Tank » / changer de rôle. Contrôleur `UI/TankChoicePanel.cs`,
libellés `tankChoice`.

| Élément | Champ |
|---|---|
| racine, titre, sous-titre | `panelRoot`, `titleText`, `subtitleText` |
| cartes **Garde / Assaut / Chasse** (+ cartes « via Cristal ») : fond, accent, icône, titre, corps | `cardButtons[]`, `cardBackgrounds[]`, `cardAccents[]`, `cardIcons[]` |
| bouton Annuler, bouton **Vue depuis le Tank** | `cancelButton`, `cancelText`, `viewButton`, `viewText` |

### 3.6 Choix de la cible
Après le rôle : marqueurs **dans le monde** sur les cibles possibles (icône bouclier /
Shofar / ennemi). `UI/TargetPicker.cs` — construit en code (§ 4).

### 3.7 Conseiller
Panneau latéral de 1 à N cartes « fais ceci, parce que » ; survol = aperçu (caméra,
fantôme, flèche sur la case) ; clic = exécute. Contrôleur `UI/AdvisorPanel.cs`,
libellés `advisor`.

| Élément | Champ |
|---|---|
| racine, en-tête | `panelRoot`, `headerText` |
| cartes : bouton, racine, fond (survol), barre d'accent, titre, raison | `cardButtons[]`, `cardRoots[]`, `cardBackgrounds[]`, `cardAccents[]` (+ textes trouvés par nom dans la carte) |
| chaque carte porte un `AdvisorCardHover` (survol) | à garder |

### 3.8 Enseignement (texte → question → résultat)
Après un événement : un enseignement du Tanya, puis une question à 4 réponses, puis le
résultat. Contrôleur `UI/TeachingPanel.cs`, contenu `teachings.json`.

| Élément | Champ |
|---|---|
| racine + groupe (fondu) | `panelRoot`, `group` |
| en-tête, accroche | `headerText`, `taglineText` |
| **page texte** : source, titre, corps, compteur `1/3`, bouton Suivant | `teachingRoot`, `sourceText`, `titleText`, `bodyText`, `counterText`, `nextButton`, `nextText` |
| **page question** : titre, situation, **4 réponses** (bouton + fond) | `questionRoot`, `questionTitleText`, `situationText`, `answerButtons[4]`, `answerBackgrounds[4]` |
| **page résultat** : titre, corps | `resultRoot`, `resultTitleText`, `resultBodyText` |
| chaque réponse porte un `TeachingAnswerHover` | à garder |

### 3.9 Trivia (dormant)
Question chronométrée, 4 réponses, récompense. `Managers/TriviaManager.cs` (utilise
déjà `ButtonManager[]` Michsky), libellés `trivia`. Pas branché dans la boucle de jeu
aujourd'hui — à refaire seulement si tu le réactives.

### 3.10 Choix du sujet (dormant)
Cartes de sujets (Halakha, Tanakh, Moussar, Tefila) avec 4 points de difficulté.
`UI/SubjectChoicePanel.cs`, libellés `subjectChoice`. Dormant, même remarque.

### 3.11 Comment jouer
Page d'aide plein écran : en-tête, accroche, **5 étapes** avec icône (usine, Tank,
ennemi, fissure, Shofar) + texte, légende des pictogrammes, bouton Fermer.
Contrôleur `UI/HowToPlayPanel.cs`, libellés `banners` / racine.

| Élément | Champ |
|---|---|
| racine + groupe | `panelRoot`, `group` |
| en-tête, accroche | `headerText`, `taglineText` |
| étapes | `stepRoots[5]`, `stepIcons[5]` |
| boutons ouvrir (dans le HUD) / fermer | `openButton`, `closeButton`, `closeText` |

### 3.12 Barre de la vue immersive
Quand la caméra descend au niveau du Tank : le HUD s'efface (CanvasGroup) et une barre
« Échap pour sortir » + bouton apparaît. `Managers/ImmersiveCamera.cs`, libellés
`immersive` : `overlayRoot`, `exitButton`, `hudGroup`.

### 3.13 Ouverture du tour
Carte plein écran au début de chaque tour : « tour » + numéro en grand, « revenu » +
montant, deux filets. `UI/TurnAnnouncePanel.cs`, libellés `turnAnnounce` : `panelRoot`,
`group`, `turnLabelText`, `turnNumberText`, `incomeLabelText`, `incomeValueText`,
`ruleTop`, `ruleBottom`.

### 3.14 Écran de fin
Victoire (3 rangs : בינוני / צדיק ורע לו / צדיק וטוב לו) ou défaite : voile, barre
d'accent, titre, sous-titre, bouton Rejouer. `UI/GameOverPanel.cs`, libellés `gameOver` :
`panelRoot`, `group`, `dimmer`, `accentBar`, `titleText`, `subtitleText`, `replayButton`,
`replayText`.

### 3.15 Visite d'intro
Au début d'une partie neuve : la caméra s'arrête sur chaque élément, une bulle photo +
titre + texte, bouton Passer. `Managers/IntroTour.cs`, libellés `intro`, photos
`Resources/Photos`. Construite en code (§ 4).

---

## 4. Les éléments construits en code (pas dans la scène)

Ils se dessinent eux-mêmes au lancement avec le style actuel. Si tu les veux dans le
thème Reach UI, il faut réécrire leur script (je m'en charge, dis-moi lesquels) ; sinon ils
resteront tels quels au milieu du nouveau thème.

| Élément | Script | Ce que c'est |
|---|---|---|
| Barre des phases automatiques | `UI/PhaseSkipBar.cs` | 3 boutons allure / suivant / tout passer |
| Bandeau de phase | `UI/PhaseBanner.cs` | annonce « phase des Tanks »… |
| Liste des sauvegardes | `UI/LoadGamePanel.cs` | menu principal, une ligne par partie |
| Visite d'intro | `Managers/IntroTour.cs` | bulles photo + texte, bouton Passer |
| Chiffres de dégâts / libellés flottants | `UI/DamagePopup.cs` | `-12`, « fissure 3/4 », « la Base » … au-dessus des cases |
| Barres de vie | `Controllers/FloatingHealthBar.cs` | sur les Tanks, ennemis, bâtiments, Shofar (bulle rompue) |
| Marqueur de posture | `UI/StanceMarker.cs` | pictogramme Garde / Assaut / Chasse au-dessus d'un Tank |
| Marqueurs de cible | `UI/TargetPicker.cs` | icônes sur les cibles possibles |
| Vol d'Énergie | `UI/EnergyGainFlight.cs` | le `+15` qui file vers le compteur |
| Flèche de case (monde, pas UI) | `Managers/HexPointer.cs` | la flèche qui désigne la case cliquée / conseillée |
| État du Shofar (monde) | `Managers/PortalStatusVisual.cs` | bulle, pastilles de fissure |
| Fantômes de menace (monde) | `Managers/ThreatPreview.cs` | ce que l'ennemi va faire |

---

## 5. Ordre conseillé

1. **Le thème** : choisir le style Reach UI (couleurs ci-dessus), régler une fois la police
   hébraïque + RTL dans les presets de Reach UI.
2. **Le HUD permanent** (§ 2) — c'est ce qu'on voit 100 % du temps.
3. **Menu de case + Détails + Choix du Tank** (§ 3.1, 3.2, 3.5) — la boucle de jeu.
4. **Conseiller, Shofar, Carte d'unité** (§ 3.7, 3.3, 3.4).
5. **Enseignement, Fin, Ouverture du tour, Aide** (§ 3.8, 3.14, 3.13, 3.11).
6. **Menu principal** (§ 1).
7. Les éléments en code (§ 4) que tu veux harmoniser — je les réécris.

À chaque panneau refait : garde les composants `IconBinder`, `AdvisorCardHover`,
`TeachingAnswerHover`, `TankCardHover`, `SubjectCardHover`, `ThreatPreviewButton`,
`PhaseSkipHover` sur les objets correspondants, rebranche les champs du tableau, et
vérifie en Play que la console ne dit pas « référence manquante » (chaque contrôleur
imprime ce qui lui manque au démarrage).
