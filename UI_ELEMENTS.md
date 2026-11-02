# Les elements de l'interface — ou les changer

Reference pour retoucher tailles, polices et positions. Tout ce qui est cuit dans la scene
par **Milchemet > Construire le HUD** se regle dans `Assets/Scripts/Editor/HudBuilder.cs`
puis se reconstruit ; les panneaux "auto" (barre de phase, bandeau, intro, sauvegardes,
chiffres flottants, barres de vie) se construisent en code a chaque lancement : il suffit
de modifier leur script, sans rebuild.

Les textes hebreux ne sont jamais dans le code : `Assets/Resources/hud_labels.json`
(section indiquee pour chaque bloc) et `Assets/Resources/teachings.json`.

## 1. Les leviers globaux (HudBuilder.cs, en tete de fichier)

| Constante | Valeur | Effet |
|---|---|---|
| `RefWidth` / `RefHeight` | 1920 x 1080 | resolution de reference du CanvasScaler (l.49-50) ; toutes les tailles ci-dessous sont en pixels de cette grille |
| `TextScale` | 1.35 | multiplie TOUTES les polices passees a `NewText` (l.83, applique l.3158) |
| `MinFontSize` | 19 | plancher : aucune police ne descend dessous apres le multiplicateur (l.93) |
| `TooltipPhotoHeight` / `TooltipEvolutionHeight` | 230 / 164 | hauteurs internes de l'info-bulle (l.64, l.67) |
| `UiSpriteBorder` | 30 | bordure 9-slice des plaques du theme (l.2966) |

Attention : `TextScale` ne grossit que les mots, pas les plaques ni les marges. Pour qu'un
bloc entier grossisse proprement (telephone), le plus sur est de mettre une echelle sur
l'objet racine du bloc (`block.localScale`) : texte, plaque, icones et marges suivent ensemble.
J'ai une version de HudBuilder qui fait exactement ca (une constante d'echelle par bloc :
coins 1.35, conseiller 1.30, menu de case 1.30, info-bulle 1.25, Shofar 1.40, carte d'unite 1.35,
aide 1.15, choix du Tank 1.20, fin 1.30) — dis-le-moi si tu la veux, elle n'est pas deposee.

Le theme electronique (plaques chanfreinees, trame hexagonale, boutons) est applique
apres la construction par `ApplyElectronicTheme` ; les sprites sont dans
`Assets/Resources/UI/` (`ui_plate_cyan/gold/red`, `ui_button_cyan/gold`, `ui_panel_dark`,
`ui_hexgrid`, `ui_scanlines`, `ui_vignette`, `ui_glowline`, `ui_bracket`) — 9-sliced, bord 30 px.

## 2. Les blocs cuits dans la scene (HudBuilder.cs)

Chaque ligne donne le nom de l'objet dans la hierarchie (`HUD Canvas/HUD/...`), la ligne
du fichier, et pour les textes la taille **avant** `TextScale`. Les ancres : `offset(x, y)`
par rapport au coin indique, `taille(largeur, hauteur)`.

### Energie (haut droite)
`HudBuilder.BuildEnergy` (l.318) — libelles : `racine : energy, energyDeltaFormat`

- objet `Energie`  (l.320)
    - ancre TopRight de `block` : offset(-56f, -40f) taille(300f, 150f)  (l.321)
- texte `Valeur` sous `block` : **56 px**  (l.328)
    - ancre TopRight de `value.rectTransform` : offset(0f, -34f) taille(300f, 64f)  (l.331)
- texte `Gain` sous `block` : **22 px**  (l.333)
    - ancre TopRight de `delta.rectTransform` : offset(0f, -102f) taille(300f, 30f)  (l.335)

### Tour et phases (haut centre)
`HudBuilder.BuildCenter` (l.342) — libelles : `phases, turn`

- objet `TourEtPhases`  (l.344)
    - ancre TopCenter de `block` : offset(0f, -36f) taille(900f, 120f)  (l.345)
- texte `Tour` sous `block` : **20 px**  (l.348)
- objet `Rail`  (l.352)
    - ancre TopCenter de `rail` : offset(0f, -40f) taille(880f, 62f)  (l.353)
- objet `Phase`  (l.374)
- texte `Nom` sous `step` : **19 px**  (l.391)

### Objectifs : Shofars + Base (haut gauche)
`HudBuilder.BuildObjectives` (l.407) — libelles : `portals, base, portalCountFormat`

- objet `Objectifs`  (l.409)
    - ancre TopLeft de `block` : offset(56f, -40f) taille(480f, 120f)  (l.410)
- objet `Shofars`  (l.414)
- texte `Compte` sous `portalRow` : **24 px**  (l.417)
- objet `Pastilles`  (l.422)
    - ancre TopLeft de `dots` : offset(80f, -6f) taille(160f, 20f)  (l.423)
    - taille `rectTransform` = (13f, 13f)  (l.435)
    - ancre TopLeft de `portalLabel.rectTransform` : offset(262f, 0f) taille(200f, 32f)  (l.442)
- objet `Base`  (l.445)
    - ancre TopLeft de `baseRow` : offset(0f, -46f) taille(480f, 34f)  (l.446)
- texte `PV` sous `baseRow` : **24 px**  (l.448)
    - ancre TopLeft de `track.rectTransform` : offset(80f, -12f) taille(224f, 9f)  (l.454)
    - ancre TopLeft de `baseLabel.rectTransform` : offset(322f, 0f) taille(150f, 32f)  (l.466)

### Bouton Fin de tour (bas centre)
`HudBuilder.BuildEndTurn` (l.474) — libelles : `endTurn, keySpace`

- objet `FinDeTour`  (l.476)
    - ancre BottomCenter de `block` : offset(0f, 54f) taille(360f, 72f)  (l.477)

### Bouton Menace (bas, a gauche du Fin de tour)
`HudBuilder.BuildThreatButton` (l.504) — libelles : `banners.threatShow / threatHide`

- objet `Menace`  (l.506)
    - ancre BottomCenter de `block` : offset(-360f, 54f) taille(320f, 56f)  (l.507)

### Bouton Aide (bas gauche)
`HudBuilder.BuildHelpButton` (l.559) — libelles : `immersive`

- objet `BoutonAide`  (l.561)
    - taille `block` = (280f, 66f)  (l.566)
    - ancre CenterRight de `icon.rectTransform` : offset(-16f, 0f) taille(32f, 32f)  (l.585)
- texte `Libelle` sous `block` : **22 px**  (l.589)
    - ancre CenterRight de `label.rectTransform` : offset(-58f, 0f) taille(200f, 40f)  (l.591)

### Page Comment jouer
`HudBuilder.BuildHelpPage` (l.600) — libelles : `intro, tooltip (legende)`

    - constante `PageW` = 1120  (l.602)
    - constante `PageH` = 880  (l.603)
    - constante `Pad` = 44  (l.604)
- objet `CommentJouer`  (l.606)
- objet `Page`  (l.617)
- texte `Titre` sous `page` : **34 px**  (l.631)
    - ancre TopRight de `header.rectTransform` : offset(-Pad, -28f) taille(760f, 50f)  (l.634)
- texte `SousTitre` sous `page` : **18 px**  (l.636)
    - ancre TopRight de `tagline.rectTransform` : offset(-Pad, -78f) taille(760f, 30f)  (l.638)
    - ancre TopCenter de `rule.rectTransform` : offset(0f, -116f) taille(PageW - Pad * 2f, 1f)  (l.641)
    - constante `StepH` = 98  (l.649)
    - constante `IconW` = 58  (l.650)
- objet `Etape`  (l.655)
- texte `Titre` sous `row` : **25 px**  (l.666)
- texte `Texte` sous `row` : **19 px**  (l.671)
- objet `Fermer`  (l.687)
    - ancre BottomCenter de `close` : offset(0f, 26f) taille(300f, 62f)  (l.688)
- texte `Libelle` sous `close` : **23 px**  (l.700)
    - constante `CellH` = 52  (l.729)
    - ancre TopCenter de `rule.rectTransform` : offset(0f, -640f) taille(pageW - pad * 2f, 1f)  (l.736)
- texte `Legende_Titre` sous `page` : **18 px**  (l.738)
    - ancre TopRight de `header.rectTransform` : offset(-pad, -664f) taille(600f, 28f)  (l.740)
- objet `Entree`  (l.779)
    - ancre TopCenter de `cell` : offset(x, y) taille(cellW - 12f, CellH - 6f)  (l.780)
    - ancre CenterRight de `icon.rectTransform` : offset(-8f, 0f) taille(36f, 36f)  (l.783)
- texte `Nom` sous `cell` : **20 px**  (l.787)

### Info-bulle d une case
`HudBuilder.BuildTooltip` (l.853) — libelles : `tooltip`

- objet `InfoBulle`  (l.863)
    - ancre TopLeft de `panel` : offset(400f, -400f) taille(470f, 440f)  (l.866)
- objet `Photo`  (l.880)
    - taille `photoFrame` = (-2f, TooltipPhotoHeight)  (l.885)
    - taille `rectTransform` = (0f, 1f)  (l.906)
- objet `Evolutions`  (l.912)
    - taille `evo` = (0f, TooltipEvolutionHeight)  (l.917)
    - constante `Thumb` = 104  (l.919)
    - constante `SlotGap` = 140  (l.920)
- objet `Niveau`  (l.930)
    - ancre TopCenter de `slot` : offset(SlotGap - i * SlotGap, -14f) taille(Thumb, Thumb + 32f)  (l.931)
- texte `Libelle` sous `slot` : **15 px**  (l.947)
- objet `Contenu`  (l.971)
    - taille `content` = (0f, 440f)  (l.976)
    - ancre TopRight de `kindIcon.rectTransform` : offset(-26f, -24f) taille(34f, 34f)  (l.979)
- texte `Titre` sous `content` : **26 px**  (l.983)
    - ancre TopRight de `title.rectTransform` : offset(-70f, -22f) taille(280f, 46f)  (l.984)
- texte `Cout` sous `content` : **30 px**  (l.987)
    - ancre TopLeft de `cost.rectTransform` : offset(26f, -22f) taille(120f, 50f)  (l.988)
- texte `SousTitre` sous `content` : **19 px**  (l.991)
    - ancre TopRight de `subtitle.rectTransform` : offset(-26f, -72f) taille(360f, 36f)  (l.992)
    - ancre TopRight de `label.rectTransform` : offset(-26f, y) taille(270f, 36f)  (l.1004)
    - ancre TopLeft de `value.rectTransform` : offset(26f, y) taille(150f, 36f)  (l.1008)
- texte `SoldeApres` sous `content` : **19 px**  (l.1014)
    - ancre TopLeft de `after.rectTransform` : offset(26f, -256f) taille(180f, 36f)  (l.1016)
    - ancre TopRight de `afterLabel.rectTransform` : offset(-26f, -256f) taille(270f, 36f)  (l.1020)
    - ancre TopRight de `rule.rectTransform` : offset(-26f, -294f) taille(418f, 1f)  (l.1023)
- texte `Effet` sous `content` : **17 px**  (l.1028)
    - ancre TopRight de `effect.rectTransform` : offset(-26f, -308f) taille(418f, 112f)  (l.1032)

### Panneau du Shofar
`HudBuilder.BuildPortalPanel` (l.1109) — libelles : `portal`

    - constante `W` = 420  (l.1113)
    - constante `Pad` = 26  (l.1114)
- objet `PanneauShofar`  (l.1117)
    - ancre TopRight de `panel` : offset(-56f, -236f) taille(W, 392f)  (l.1118)
- texte `Nom` sous `panel` : **20 px**  (l.1133)
    - ancre TopRight de `title.rectTransform` : offset(-Pad, -22f) taille(250f, 38f)  (l.1136)
- texte `Niveau` sous `panel` : **15 px**  (l.1138)
    - ancre TopLeft de `level.rectTransform` : offset(Pad, -26f) taille(120f, 32f)  (l.1140)
- texte `PV` sous `panel` : **15 px**  (l.1143)
    - ancre TopRight de `hp.rectTransform` : offset(-Pad, -74f) taille(100f, 30f)  (l.1146)
    - ancre TopRight de `note.rectTransform` : offset(-Pad, -100f) taille(Inner, 56f)  (l.1172)
    - ancre TopRight de `rule.rectTransform` : offset(-Pad, -166f) taille(Inner, 1f)  (l.1177)
    - ancre TopRight de `instLabel.rectTransform` : offset(-Pad, -184f) taille(230f, 32f)  (l.1182)
- texte `Instabilite_Valeur` sous `panel` : **15 px**  (l.1184)
    - ancre TopLeft de `instValue.rectTransform` : offset(Pad, -184f) taille(110f, 32f)  (l.1187)
- objet `Instabilite_Segments`  (l.1189)
    - ancre TopRight de `segRow` : offset(-Pad, -222f) taille(Inner, 7f)  (l.1190)
- objet `Bouclier`  (l.1207)
    - ancre TopRight de `box` : offset(-Pad, -248f) taille(Inner, 92f)  (l.1208)
- texte `Titre` sous `box` : **16 px**  (l.1218)
    - ancre TopRight de `shieldTitle.rectTransform` : offset(-18f, -14f) taille(300f, 32f)  (l.1221)
- texte `Corps` sous `box` : **13 px**  (l.1223)
    - ancre TopRight de `shieldBody.rectTransform` : offset(-18f, -50f) taille(330f, 32f)  (l.1226)
- objet `Evolution`  (l.1229)
    - ancre TopRight de `evolveRow` : offset(-Pad, -352f) taille(Inner, 32f)  (l.1230)
- texte `Valeur` sous `evolveRow` : **14 px**  (l.1236)

### Carte d unite + flash Impact
`HudBuilder.BuildUnitCard` (l.1287) — libelles : `unit`

    - constante `W` = 680  (l.1291)
    - constante `Pad` = 26  (l.1292)
- objet `CarteUnite`  (l.1295)
    - ancre BottomCenter de `card` : offset(0f, 150f) taille(W, 268f)  (l.1297)
- texte `Nom` sous `card` : **22 px**  (l.1311)
    - ancre TopRight de `title.rectTransform` : offset(-Pad, -18f) taille(300f, 40f)  (l.1314)
- texte `SousTitre` sous `card` : **16 px**  (l.1316)
    - ancre TopLeft de `subtitle.rectTransform` : offset(Pad, -22f) taille(280f, 34f)  (l.1318)
- texte `PV` sous `card` : **15 px**  (l.1320)
    - ancre TopRight de `hp.rectTransform` : offset(-Pad, -64f) taille(100f, 30f)  (l.1323)
    - ancre TopRight de `label.rectTransform` : offset(-Pad, y) taille(240f, 32f)  (l.1349)
    - ancre TopLeft de `value.rectTransform` : offset(Pad + 168f, y) taille(180f, 32f)  (l.1353)
    - ancre TopLeft de `unit.rectTransform` : offset(Pad, y) taille(160f, 32f)  (l.1357)
- objet `Impact`  (l.1367)
    - ancre BottomCenter de `flash` : offset(0f, 452f) taille(420f, 120f)  (l.1368)
- texte `Valeur` sous `flash` : **54 px**  (l.1370)
- texte `Legende` sous `flash` : **15 px**  (l.1375)
    - ancre TopCenter de `flashCaption.rectTransform` : offset(0f, -84f) taille(420f, 34f)  (l.1377)

### Panneau Question (trivia, dormant)
`HudBuilder.BuildTrivia` (l.1434) — libelles : `trivia`

    - constante `ColWidth` = 940  (l.1438)
- objet `PanneauQuestion`  (l.1440)
- texte `Categorie` sous `panel` : **44 px**  (l.1448)
- texte `Tarif_Valeur` sous `panel` : **36 px**  (l.1460)
- texte `Question` sous `panel` : **46 px**  (l.1467)
    - ancre Center de `question.rectTransform` : offset(0f, 150f) taille(ColWidth, 200f)  (l.1469)
- objet `Reponse`  (l.1484)
    - ancre Center de `row` : offset(0f, y) taille(ColWidth, 92f)  (l.1485)
- texte `Texte` sous `row` : **26 px**  (l.1505)
    - ancre TopRight de `text.rectTransform` : offset(-34f, -24f) taille(860f, 46f)  (l.1507)
- texte `Chrono_Valeur` sous `panel` : **22 px**  (l.1520)
    - ancre Center de `timerTrack.rectTransform` : offset(0f, -472f) taille(ColWidth, 5f)  (l.1527)

### Choix du sujet (dormant)
`HudBuilder.BuildSubjectChoice` (l.1609) — libelles : `subjectChoice`

    - constante `CardW` = 340  (l.1614)
    - constante `CardH` = 340  (l.1615)
    - constante `Gap` = 26  (l.1616)
- objet `ChoixDuSujet`  (l.1618)
    - ancre Center de `title.rectTransform` : offset(0f, 292f) taille(1000f, 56f)  (l.1628)
    - ancre Center de `subtitle.rectTransform` : offset(0f, 238f) taille(1000f, 32f)  (l.1632)
- objet `Sujet`  (l.1654)
    - ancre Center de `card` : offset(x, -30f) taille(CardW, CardH)  (l.1655)
- texte `Nom` sous `card` : **32 px**  (l.1677)
    - ancre TopCenter de `name.rectTransform` : offset(0f, -32f) taille(CardW - 24f, 58f)  (l.1680)
- objet `Paliers`  (l.1685)
    - ancre TopCenter de `dotRow` : offset(0f, -104f) taille(140f, 11f)  (l.1686)
- texte `Difficulte` sous `card` : **16 px**  (l.1700)
    - ancre TopCenter de `tier.rectTransform` : offset(0f, -132f) taille(CardW - 24f, 34f)  (l.1702)
- texte `Gain` sous `card` : **58 px**  (l.1704)
    - ancre TopCenter de `reward.rectTransform` : offset(0f, -196f) taille(CardW - 24f, 100f)  (l.1707)

### Choix du Tank (posture)
`HudBuilder.BuildTankChoice` (l.1779) — libelles : `tankChoice`

    - constante `CardW` = 330  (l.1784)
    - constante `CardH` = 420  (l.1785)
    - constante `Gap` = 24  (l.1786)
- objet `ChoixDuTank`  (l.1788)
    - ancre Center de `title.rectTransform` : offset(0f, 300f) taille(1000f, 56f)  (l.1800)
    - ancre Center de `subtitle.rectTransform` : offset(0f, 250f) taille(1100f, 32f)  (l.1804)
- objet `Cartes`  (l.1810)
    - ancre Center de `row` : offset(0f, -10f) taille(1600f, CardH)  (l.1811)
- objet `Option`  (l.1834)
    - taille `card` = (CardW, CardH)  (l.1835)
    - ancre TopCenter de `cardIcon.rectTransform` : offset(0f, -22f) taille(44f, 44f)  (l.1867)
- texte `Nom` sous `card` : **30 px**  (l.1871)
    - ancre TopCenter de `name.rectTransform` : offset(0f, -74f) taille(CardW - 24f, 54f)  (l.1874)
    - ancre TopCenter de `rule.rectTransform` : offset(0f, -136f) taille(CardW - 72f, 1f)  (l.1877)
- texte `Explication` sous `card` : **19 px**  (l.1882)
    - ancre TopCenter de `body.rectTransform` : offset(0f, -152f) taille(CardW - 40f, 160f)  (l.1886)
- texte `Cout` sous `card` : **40 px**  (l.1890)
    - ancre BottomCenter de `cost.rectTransform` : offset(0f, 26f) taille(CardW - 24f, 58f)  (l.1893)
- objet `Annuler`  (l.1905)
    - ancre Center de `cancel` : offset(0f, -268f) taille(280f, 62f)  (l.1906)
- objet `VueTank`  (l.1928)
    - ancre Center de `view` : offset(0f, -346f) taille(340f, 62f)  (l.1929)

### Conseiller
`HudBuilder.BuildAdvisor` (l.2001) — libelles : `advisor`

    - constante `PanelW` = 470  (l.2006)
    - constante `CardW` = 438  (l.2007)
    - constante `CardH` = 104  (l.2008)
- objet `Conseiller`  (l.2010)
    - taille `panel` = (PanelW, 400f)  (l.2015)
    - taille `rectTransform` = (CardW, 30f)  (l.2045)
- objet `Conseil`  (l.2065)
    - taille `card` = (CardW, CardH)  (l.2066)
- texte `Titre` sous `card` : **21 px**  (l.2092)
    - ancre TopRight de `title.rectTransform` : offset(-14f, -12f) taille(280f, 34f)  (l.2095)
- texte `Cout` sous `card` : **22 px**  (l.2099)
    - ancre TopLeft de `cost.rectTransform` : offset(14f, -12f) taille(110f, 34f)  (l.2102)
- texte `Raison` sous `card` : **15 px**  (l.2106)
    - ancre TopRight de `reason.rectTransform` : offset(-14f, -50f) taille(CardW - 28f, 46f)  (l.2110)

### Enseignement (texte, question, resultat)
`HudBuilder.BuildTeaching` (l.2160) — libelles : `teachings.json (ui)`

- objet `Enseignement`  (l.2162)
- texte `Titre` sous `panel` : **34 px**  (l.2177)
    - ancre Center de `header.rectTransform` : offset(0f, 372f) taille(1000f, 50f)  (l.2180)
- texte `Accroche` sous `panel` : **20 px**  (l.2184)
    - ancre Center de `tagline.rectTransform` : offset(0f, 326f) taille(1100f, 34f)  (l.2186)
    - ancre Center de `ruleTop.rectTransform` : offset(0f, 292f) taille(880f, 1f)  (l.2189)
- objet `Texte`  (l.2194)
    - ancre Center de `teaching` : offset(0f, -20f) taille(1040f, 560f)  (l.2195)
- texte `Source` sous `teaching` : **19 px**  (l.2199)
    - ancre TopCenter de `source.rectTransform` : offset(0f, -12f) taille(1000f, 34f)  (l.2201)
- texte `Sujet` sous `teaching` : **40 px**  (l.2203)
    - ancre TopCenter de `title.rectTransform` : offset(0f, -56f) taille(1000f, 62f)  (l.2206)
- texte `Corps` sous `teaching` : **25 px**  (l.2208)
    - ancre TopCenter de `body.rectTransform` : offset(0f, -140f) taille(940f, 300f)  (l.2212)
- texte `Compteur` sous `teaching` : **18 px**  (l.2215)
    - ancre BottomCenter de `counter.rectTransform` : offset(0f, 96f) taille(200f, 32f)  (l.2217)
- objet `Suivant`  (l.2219)
    - ancre BottomCenter de `next` : offset(0f, 20f) taille(300f, 66f)  (l.2220)
- texte `Libelle` sous `next` : **24 px**  (l.2232)
- objet `Question`  (l.2240)
    - ancre Center de `question` : offset(0f, -20f) taille(1040f, 560f)  (l.2241)
- texte `Intitule` sous `question` : **24 px**  (l.2243)
    - ancre TopCenter de `questionTitle.rectTransform` : offset(0f, -8f) taille(900f, 40f)  (l.2246)
- texte `Situation` sous `question` : **26 px**  (l.2248)
    - ancre TopCenter de `situation.rectTransform` : offset(0f, -62f) taille(920f, 170f)  (l.2252)
    - constante `AnswerW` = 880  (l.2258)
    - constante `AnswerH` = 86  (l.2259)
- objet `Reponse`  (l.2263)
- texte `Texte` sous `card` : **24 px**  (l.2284)
- objet `Resultat`  (l.2297)
    - ancre Center de `result` : offset(0f, -20f) taille(1040f, 400f)  (l.2298)
- texte `Verdict` sous `result` : **46 px**  (l.2300)
    - ancre TopCenter de `resultTitle.rectTransform` : offset(0f, -40f) taille(900f, 70f)  (l.2303)
- texte `Explication` sous `result` : **24 px**  (l.2305)
    - ancre TopCenter de `resultBody.rectTransform` : offset(0f, -128f) taille(900f, 180f)  (l.2309)

### Menu d une case (Voir / Details / Utiliser)
`HudBuilder.BuildHexMenu` (l.2373) — libelles : `hexMenu`

    - constante `W` = 380  (l.2377)
    - constante `BtnW` = 344  (l.2378)
    - constante `BtnH` = 58  (l.2379)
- objet `MenuCase`  (l.2381)
    - taille `panel` = (W, 3f * BtnH + 72f)  (l.2385)
- objet `VoirDePres`  (l.2397)
    - ancre TopCenter de `view` : offset(0f, -18f) taille(BtnW, BtnH)  (l.2398)
- objet `Details`  (l.2416)
- objet `UtiliserEnergie`  (l.2435)
    - taille `rectTransform` = (24f, 24f)  (l.2462)
- texte `Cout` sous `use` : **24 px**  (l.2466)
    - taille `rectTransform` = (70f, 0f)  (l.2473)

### Barre de la vue immersive
`HudBuilder.BuildImmersive` (l.2531) — libelles : `immersive`

    - ancre BottomCenter de `bar` : offset(0f, 40f) taille(820f, 92f)  (l.2544)
    - taille `rectTransform` = (480f, 60f)  (l.2561)
- objet `Retour`  (l.2565)
    - taille `exit` = (270f, 60f)  (l.2570)

### Ouverture du tour
`HudBuilder.BuildTurnAnnounce` (l.2637) — libelles : `turnAnnounce`

- objet `OuvertureDuTour`  (l.2641)
    - ancre Center de `ruleTop.rectTransform` : offset(0f, 104f) taille(520f, 1f)  (l.2656)
    - ancre Center de `turnLabel.rectTransform` : offset(0f, 74f) taille(700f, 44f)  (l.2660)
- texte `Numero` sous `panel` : **120 px**  (l.2662)
    - ancre Center de `turnNumber.rectTransform` : offset(0f, -26f) taille(700f, 190f)  (l.2665)
    - ancre Center de `ruleBottom.rectTransform` : offset(0f, -112f) taille(520f, 1f)  (l.2668)
    - ancre Center de `incomeLabel.rectTransform` : offset(88f, -176f) taille(240f, 40f)  (l.2674)
- texte `Revenu_Valeur` sous `panel` : **28 px**  (l.2676)
    - ancre Center de `incomeValue.rectTransform` : offset(-92f, -178f) taille(190f, 46f)  (l.2679)

### Ecran de fin
`HudBuilder.BuildGameOver` (l.2807) — libelles : `gameOver`

    - constante `PanelW` = 820  (l.2811)
    - constante `PanelH` = 560  (l.2812)
- objet `FinDePartie`  (l.2814)
- objet `Carte`  (l.2825)
- texte `Titre` sous `panel` : **64 px**  (l.2839)
    - ancre TopCenter de `title.rectTransform` : offset(0f, -56f) taille(PanelW - 60f, 106f)  (l.2842)
- texte `SousTitre` sous `panel` : **21 px**  (l.2844)
    - ancre TopCenter de `subtitle.rectTransform` : offset(0f, -176f) taille(PanelW - 100f, 44f)  (l.2846)
    - ancre TopCenter de `rule.rectTransform` : offset(0f, -240f) taille(PanelW - 120f, 1f)  (l.2849)
    - constante `ColumnW` = 240  (l.2855)
- objet `Bilan`  (l.2862)
    - ancre TopCenter de `column` : offset(x, -278f) taille(ColumnW, 140f)  (l.2863)
- texte `Valeur` sous `column` : **46 px**  (l.2865)
- texte `Libelle` sous `column` : **16 px**  (l.2870)
    - ancre TopCenter de `label.rectTransform` : offset(0f, -82f) taille(ColumnW, 34f)  (l.2872)
- objet `Rejouer`  (l.2879)
    - ancre BottomCenter de `replay` : offset(0f, 44f) taille(340f, 78f)  (l.2880)
- texte `Texte` sous `replay` : **22 px**  (l.2892)
    - constante `UiSpriteBorder` = 30  (l.2966)
- objet `Plaque`  (l.3008)
- objet `Trame`  (l.3114)

## 3. Les panneaux construits en code au lancement (pas de rebuild)

| Element | Script | Tailles a changer |
|---|---|---|
| Barre de phase (Tanks / ennemis / batiments, vitesse) | `Scripts/UI/PhaseSkipBar.cs` | `ButtonWidth` 250, `ButtonHeight` 66, `Spacing` 14, `MarginX/Y` 40 (l.40-44) ; libelle des boutons 30 px (l.294) ; raccourcis 22 px (l.249) |
| Bandeau d'etape (titre de phase plein ecran) | `Scripts/UI/PhaseBanner.cs` | bande 250 px de haut (l.116) ; titre 78 px (l.136), phrase 32 px (l.144) ; durees `fadeIn` .25, `hold` 2.5, `fadeOut` .35 (l.37-39) |
| Ouverture du tour (gros numero) | `Scripts/UI/TurnAnnouncePanel.cs` + HudBuilder.BuildTurnAnnounce | numero 120 px, revenu 28 px (HudBuilder l.2717, l.2731) ; durees l.68-70 |
| Visite guidee (cartes de l'intro) | `Scripts/Managers/IntroTour.cs` | carte 1580 x 340 (l.418), photo 262 (l.440), titre 54 px (l.446), texte 31 px (l.455), compteur 24, indice 26 ; panneau strategie 1500 x 760 (l.495), titre 60, texte 32, indice 34 |
| Charger une partie | `Scripts/UI/LoadGamePanel.cs` | panneau 1180 x 860 (l.230) ; titre 56, indice 28, lignes 38 / 26 / 26, X 30 (l.236-342) |
| Chiffres flottants (-12, fissure 3/4...) | `Scripts/UI/DamagePopup.cs` | `FontSize` 40, `PixelWidth/Height` 120 x 48, `WorldWidth` 1.0, `Lifetime` .95, `RiseDistance` 1.1 (l.60-69) ; les libelles (surge, bouclier...) a 80 % (l.277) |
| Barres de vie | `Scripts/Controllers/FloatingHealthBar.cs` | `BarWorldWidth` .70, `BarPixelWidth/Height` 100 x 18, `FillPixelWidth/Height` 94 x 13, `ClearanceAboveModel` .18, `VisibleDuration` 2.5 (l.24-32, l.126) |
| Marque de posture (icone au-dessus des Tanks) | `Scripts/UI/StanceMarker.cs` | `HeightOffset` 1.55, `WorldWidth` .42, 64 x 64 px (l.26-29) |
| Menu principal (hexagones) | `Scripts/Editor/MainMenuBuilder.cs` (rebuild : Milchemet > Construire le menu) | `HexSize` 190, `HexGap` 1.06 (l.63-67) ; titre 104 px (l.188), sous-titre 38 (l.193), description 32 (l.300), libelle des hexagones = `fontSize` passe a l.368 |
| Menu principal : logo, voile de nuit | `Scripts/UI/MainMenuController.cs` | `titleWidth` 1150, `titleY` 452 (l.45-47), bande libre `topLimit` 528 / `bottomLimit` 236 (l.336), `nightVeil` (inspecteur) ; diorama : `nightMode`, `nightFog`, `nightAmbient` dans `MainMenuDiorama.cs` |
| Aura / zones de portee sur le plateau | `Scripts/Controllers/BoardController.cs` | `AuraMountain`, `AuraHill`, `AuraCrystal` (couleurs + alpha) |
| Teinte des terrains | `Scripts/Managers/BoardReadability.cs` | `plainTint`, `plainDesaturation` .08, `plainBrightness` 1.7, `TerrainTuning[]` (l.86) |

## 4. Apres modification

- `HudBuilder.cs` ou `hud_labels.json` : **Milchemet > Construire le HUD**, puis enregistrer la scene Game.
- `MainMenuBuilder.cs` : **Milchemet > Construire le menu**, puis enregistrer la scene MainMenu.
- Les scripts de la section 3 : rien a faire, c'est pris au prochain Play.
- Jamais d'hebreu dans un `.cs` : les textes passent par `HudLabelsRuntime.Get(cle)` / `hud_labels.json`.
