# Pictogrammes du jeu — liste à produire

Le jeu utilise **10 pictogrammes**. Aujourd'hui ils sont dessinés par le code (`UI/IconLibrary.cs` : formes géométriques blanches, 128 px). Chacun peut être remplacé par ton fichier **sans toucher au code ni reconstruire le HUD** : tu glisses le fichier dans le champ correspondant du composant **Icon Overrides** (il est sur l'objet `LocalGameEngine` de la scène Game, ou sur un objet `Pictogrammes`) et il remplace le dessin généré **partout** d'un coup.

## Format à respecter

| Point | Valeur |
|---|---|
| Fichier | PNG, **carré**, 256 × 256 px (512 si tu veux de la marge) |
| Couleur | **Blanc pur #FFFFFF sur fond transparent**. Jamais de couleur dans le fichier : la teinte est donnée par le code (le même eclair est or dans le compteur d'Énergie, rouge dans une alerte). Un fichier déjà coloré ne pourra plus changer de teinte. |
| Alpha | Silhouette pleine, bords nets (anti-aliasing OK). Pas de dégradé ni d'ombre portée. |
| Marge | Le dessin remplit ~80 % du carré (≈ 10 % de marge de chaque côté), **centré**. Les 10 icônes doivent avoir le même poids visuel. |
| Finesse | Les icônes descendent jusqu'à **20 px** à l'écran (compteurs) : pas de trait plus fin que 1/12 de la largeur, pas de détail intérieur. Une silhouette qu'on reconnaît à 20 px. |
| Import Unity | Texture Type **Sprite (2D and UI)**, Mesh Type Full Rect, Filter Bilinear, Compression None (ou High Quality), Alpha Is Transparency coché. |

Aucune des icônes n'a de sens « gauche/droite » : l'interface est en hébreu (RTL), rien n'est retourné, mais évite une flèche qui pointe d'un côté.

## Les 10 pictogrammes

| # | Champ Icon Overrides | Élément du jeu | Forme actuelle (générée) | Teintes où il apparaît | Où on le voit, taille à l'écran |
|---|---|---|---|---|---|
| 1 | `energy` | **L'Énergie** (la ressource) | éclair | or `#FFC65C` | compteur d'Énergie en haut (≈ 24 px), prix dans le menu de case (24 px), coût sur les boutons, légende (58 px) |
| 2 | `factory` | **L'usine à Gaz** (case gas) | goutte | vert `#6BEB9E` | légende (58 px), info-bulle de case (34 px), consignes de départ |
| 3 | `crystal` | **Le Cristal** (case crystal, évolution des Tanks) | losange | cyan `#4DF0DB` | légende (58 px), info-bulle (34 px), cartes de choix du Tank « via Cristal » (44 px) |
| 4 | `tank` | **Un Tank** (unité du joueur) | chevron | bleu `#5EA8FF` | cartes de choix du Tank (44 px), popup Tank (34 px), info-bulle, légende (58 px), consignes |
| 5 | `bunker` | **Le Bunker** (case hill construite) | bouclier | or `#FFC65C` | légende (58 px), info-bulle (34 px), **marqueur de posture GARDE au-dessus des Tanks** (monde, petit), sélecteur de cible |
| 6 | `command` | **Le Centre de Commandement** (case mountain) | étoile 5 branches | orange `#FF8A3D` | légende (58 px), info-bulle (34 px) |
| 7 | `portal` | **Le Shofar** (portail ennemi) | hexagone percé (anneau) | orange `#FF6B4A` | légende (58 px), info-bulle, **marqueur de posture ASSAUT au-dessus des Tanks**, sélecteur de cible, consignes |
| 8 | `crack` | **Une fissure** (sur la bulle du Shofar) | éclat / ligne brisée | orange clair `#FFA84D` | légende (58 px), consignes de départ, libellés de fissure |
| 9 | `enemy` | **Le Yetzer Hara** (ennemi) | triangle pointe en bas | rouge `#FF4D5E` | légende (58 px), info-bulle (34 px), **marqueur de posture CHASSE au-dessus des Tanks**, sélecteur de cible, consignes |
| 10 | `baseIcon` | **La Base** (la case à défendre) | carré plein | blanc cassé `#E6ECFF` | légende (58 px) |

Les trois **postures** des Tanks (Garde / Assaut / Chasse) n'ont pas d'icône à elles : elles réutilisent bouclier / Shofar / ennemi (« ce que le Tank protège ou vise »). Si tu préfères trois icônes dédiées, dis-le et j'ajoute trois champs (`stanceGuard`, `stanceAssault`, `stanceHunt`).

## Conseils par icône

- **Énergie** : c'est la plus vue (compteur permanent, prix partout). Un éclair franc, épais, lisible à 20 px.
- **Usine / Cristal / Bunker / Centre** : les quatre bâtiments. Ils se lisent côte à côte dans la légende et l'info-bulle, garde-leur la même famille de trait.
- **Tank / Ennemi** : les deux camps. Deux silhouettes clairement opposées (le code les colore bleu et rouge, mais la forme seule doit déjà les distinguer : un daltonien doit s'y retrouver).
- **Shofar / Fissure** : les deux vont ensemble dans l'explication « 4 fissures et le bouclier tombe » ; la fissure doit pouvoir se lire **posée sur** le Shofar.
- **Base** : apparaît seulement dans la légende ; une forme massive, « ce qu'on tient ».

## Mise en place

1. Dépose les PNG où tu veux dans `Assets/` (par ex. `Assets/Art/Icons/`), règle l'import comme ci-dessus.
2. Scène Game → objet `LocalGameEngine` → composant **Icon Overrides** → glisse chaque fichier dans son champ.
3. C'est tout : le changement se voit tout de suite dans l'éditeur (OnValidate rafraîchit les images), et partout dans le jeu au lancement. Un champ laissé vide garde la forme générée — tu peux en remplacer 3 et garder les 7 autres.

## Hors liste (pas des pictogrammes)

Les plaques et boutons de l'interface (`Assets/Resources/UI/ui_plate_*.png`, `ui_button_*.png`, `ui_panel_dark.png`, `ui_hexgrid.png`, `ui_bracket.png`, `ui_glowline.png`, `ui_scanlines.png`, `ui_vignette.png`) sont des **habillages 9-slice**, pas des icônes ; les photos des éléments (`ElementPhotos`) sont prises sur les modèles 3D. Si tu veux refaire les habillages aussi, demande et je te donne leurs dimensions et bordures.
