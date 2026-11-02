# Build léger et multi-plateforme — ce qu'il faut corriger

Relevé fait sur le projet le 30 octobre 2026 (Unity 6000.4.10f1, pipeline Built-in, 2 scènes au build : MainMenu puis Game).

## Où on en est (30 octobre)

**Fait, et vérifié.** Le dossier `Resources` est passé de **28,2 Mo à 7,46 Mo** : `Robo/`, `Sprites/`, `Fonts/` et les 46 prefabs en sont sortis (déplacés, pas supprimés — leurs GUID n'ont pas changé, les scènes les retrouvent). Il ne reste que ce que le code charge par son nom : les quatre JSON, les trois shaders, `Photos/` et `UI/`.

Côté rendu, l'instanciation GPU est branchée : les trois shaders maison (`MNLTH_BoardCalm`, `MNLTH_PawnRim`, `MNLTH_GroundDecal`) ont leur variante instanciée et leurs couleurs par instance, et `BoardReadability` ne désactive plus l'instanciation sur les matériaux calmes du plateau. L'audit mesure maintenant les appels de dessin, pour arrêter de supposer.

Côté code, les logs de déroulement passent par `GameLog` : effacés à la compilation dans un build de production, chaîne de caractères comprise.

**À faire la semaine prochaine, dans cet ordre.**

1. Les quatre variantes du titre, 3,6 Mo à elles seules sur les 7,46 restants : garder celle que `MainMenuController.titleSpriteResource` désigne dans la scène MainMenu, sortir les trois autres, et passer celle qui reste en *Max Size* 1024.
2. Les 22 photos d'éléments : *Max Size* 512, *Generate Mip Maps* décoché, compression Normal Quality (ASTC 6x6 en WebGL). Objectif : un `Resources` final à 1,5-2 Mo.
3. Les cinq restes de l'ancienne série au trait, que plus aucun code ne charge : `BaseBuild.png`, `Bunker1.png`, `Bunker2.png`, `Hill.png`, `Crystal_0.png` (217 Ko). Attention, `Bunker1.png` avec une majuscule est l'ancien ; `bunker_1.png` en minuscules sert. Et `Portal2.png` sert dans la visite d'intro.
4. Les réglages du lecteur : `Decompression Fallback` coché, `Maximum Memory Size` à 512, qualité WebGL en Medium (détails plus bas).
5. Les modules du moteur et les paquets inutiles, `Active Input Handling` sur Old.
6. Les atlas de police en statique 1024.
7. Le ménage dans les paquets d'assets non utilisés, `_Recovery`, et les 35 Mo de vieux dumps à la racine du projet (`extract.txt`, `tool_calls*.txt`, les `patch*.ps1`...).

Et après chaque étape : Run All, un audit, les deux scènes lancées à la main.

## Ta question d'abord : oui, les prefabs dans Resources, c'est le piège

`Assets/Resources` n'est pas un dossier comme les autres. Trois choses s'y passent, et les trois sont mauvaises pour le web :

1. **Tout ce qui est dedans part dans le build, utilisé ou non.** Unity ne peut pas savoir qui appelle `Resources.Load`, donc il n'ose rien retirer. Le reste du projet, lui, est nettoyé : un prefab posé dans `Assets/Art` et référencé par personne ne pèse rien dans le build final.
2. **Tout est chargé d'un bloc au démarrage.** `resources.assets` fait partie du fichier de données que le navigateur télécharge avant d'afficher quoi que ce soit, et Unity en construit la table de correspondance au lancement. C'est du temps avant la première image, et de la mémoire prise pour toute la partie.
3. **Rien n'est déchargeable proprement.** Un asset de Resources reste en mémoire jusqu'à `UnloadUnusedAssets`.

État actuel : **28,2 Mo de sources dans `Assets/Resources`** (244 fichiers hors `.meta`), dont 150 PNG pour 17,3 Mo.

La bonne nouvelle, et je l'ai vérifiée ligne par ligne : **aucun code du jeu ne charge un prefab par son nom.** Les 46 prefabs (plateau, pions, ruines) sont référencés depuis la scène, par le tableau `m_HexagonPrefabs` de `BoardController` et par les champs de `FXManager`. Le seul fichier qui faisait un `Resources.Load<GameObject>` est `Models/BoardLine.cs`, entièrement commenté depuis longtemps. Ils n'ont donc rien à faire dans Resources et peuvent en sortir sans qu'une seule référence ne casse.

Ce que le code charge réellement par nom, et qui doit rester :

| Ce qui reste dans Resources | Poids | Qui le charge |
|---|---|---|
| `hud_labels.json`, `teachings.json`, `trivia_questions.json`, `initial_board.json` | 0,12 Mo | HudLabelsRuntime, TeachingManager, TriviaManager, MapGenerator |
| `Shaders/MNLTH_GroundDecal`, `MNLTH_PawnRim`, `MNLTH_BoardCalm` | 0,01 Mo | BoardReadability, HexPointer, ShotTracer, PortalStatusVisual |
| `Photos/*` (les 22 éléments + les titres) | 7,20 Mo | ElementPhotos, IntroTour, MainMenuController |
| `UI/ui_vignette`, `UI/ui_scanlines` | 0,13 Mo | PhaseSkipBar, habillage |

**Environ 20,7 Mo peuvent sortir de Resources**, et c'est le plus gros gain du projet, pour un risque proche de zéro.

### Comment les sortir sans rien casser

Toujours **dans la fenêtre Project d'Unity**, par glisser-déposer — jamais dans l'Explorateur Windows. Unity déplace le `.meta` avec le fichier, le GUID ne change pas, et toutes les références de scène suivent. Un déplacement fait à la main dans Windows sans le `.meta` casse au contraire toutes les références.

Dans l'ordre, du plus gros gain au plus petit :

1. **`Resources/Robo/` → `Assets/Art/Robo/`** (6,0 Mo). À lui seul, `base_low_pedestal_MaskMap.png` pèse 3,08 Mo, plus 1,66 Mo de Normal et 1,08 Mo de BaseMap : trois textures de socle de logo, en pleine résolution, embarquées dans chaque partie jouée. Si ça ne servait qu'aux rendus du logo, ça sort du build entièrement.
2. **`Resources/Sprites/` → `Assets/Art/Sprites/`** (6,64 Mo, 122 fichiers). **Avant de déplacer**, vérifie les champs texte de l'inspecteur qui contiennent un chemin : `MainMenuController.titleSpriteResource` (par défaut `Photos/titre_milchemet`), les habillages de `PhaseSkipBar`, les arrêts de `IntroTour`. S'ils pointent tous vers `Photos/...` ou `UI/...` (c'est le cas dans le code), `Sprites/` ne sert à personne à l'exécution. Dedans, deux vidéos de démo de Reach UI (`VideoIdle.mp4`, `Videotalk.mp4`, 2,29 Mo à elles deux) : celles-là, supprime-les.
3. **`Resources/Fonts/` → `Assets/Fonts/`** (4,81 Mo). Trois atlas TMP : Rubik Regular et Rubik Bold à 2,08 Mo chacun, Kusanagi à 0,55 Mo. Vois aussi le point « polices » plus bas, parce que 4,8 Mo d'atlas pour deux graisses, c'est beaucoup trop.
4. **Les 46 prefabs et les 2 FBX de la racine → `Assets/Art/Board/`** (3,3 Mo). Dont `explosion_stylized_large_originalFire.prefab` à 1,89 Mo et `Rocket_01.prefab` à 0,48 Mo : vérifie dans l'inspecteur de `FXManager` s'ils sont encore branchés ; sinon ils sortent du build.
5. **Le reste de la racine** : `DOTweenSettings.asset`, les `.anim`, les `.controller`, les `.mat`, les `.rendertexture`.

Après ça, Resources doit contenir uniquement les quatre JSON, les trois shaders, `Photos/` et `UI/`.

### Et les photos, qui restent

Les quatre variantes du titre pèsent 3,6 Mo à elles seules (`titre_milchemet_robot_full` 1,26 Mo, `titre_milchemet` 1,24 Mo, `titre_milchemet_gold` 0,55 Mo, `titre_milchemet-1` 0,54 Mo). Une seule sert au menu. Garde celle-là, sors les autres, et mets sa taille maximale d'import à 1024 : un titre affiché sur 900 px de large n'a pas besoin de plus. Gain attendu : environ 3 Mo.

Pour les 22 photos d'éléments, dans l'inspecteur d'import : **Max Size 512** (elles sont affichées petites), **Compression : Normal Quality**, format **ASTC 6x6** pour WebGL/mobile ou **DXT5** à défaut, **Generate Mip Maps décoché** (ce sont des images d'interface, jamais vues en perspective). Décocher les mipmaps retire un tiers du poids de chaque texture et supprime le flou en interface.

## Réglages du lecteur : ce qui est déjà bon, ce qui ne l'est pas

Déjà correct, n'y touche pas : `Strip Engine Code` activé, `Strip Unused Mesh Components` activé, compression **Brotli**, `Data Caching` activé, `Run In Background` activé, cible **Wasm**, threads désactivés (les activer casse la compatibilité navigateur).

À corriger :

**1. La qualité par défaut en WebGL est « Very High ».** `QualitySettings` donne le niveau 4 au WebGL : ombres complètes, deux lumières par pixel, anisotropie. C'est le réglage le plus coûteux du projet et il s'applique à un téléphone comme à un PC. Mets **Medium** par défaut pour le web, et garde Very High pour un build PC. C'est probablement le plus gros gain de fluidité mobile pour un clic.

**2. `Decompression Fallback` est désactivé avec Brotli.** Ça veut dire que le serveur doit envoyer l'en-tête `Content-Encoding: br` lui-même. Si l'hébergeur ne le fait pas (itch.io le fait, un simple dossier sur un hébergement mutualisé souvent pas), **le jeu ne se lance pas du tout**, et l'erreur dans la console du navigateur ne dit pas pourquoi. Deux solutions : configurer les en-têtes côté serveur (le mieux, le fichier reste petit), ou cocher `Decompression Fallback` (ça marche partout, mais le téléchargement grossit d'environ 10 % et le décodage se fait en JavaScript). Décide selon l'hébergeur, mais ne laisse pas ce point au hasard : c'est la panne la plus fréquente d'un build WebGL.

**3. La mémoire maximale est à 2048 Mo.** Safari sur iPhone refuse d'allouer autant et la page meurt sans message. Mets `Maximum Memory Size` à **512 Mo** — le jeu est un plateau d'hexagones, il n'en demandera jamais autant — et `Initial Memory Size` à **64 Mo** pour éviter deux ou trois réallocations pendant le chargement.

**4. `Managed Stripping Level` est à « Low ».** Passer à **Medium** retire du code managé en plus. **Attention** : le projet utilise Newtonsoft.Json, qui travaille par réflexion ; un stripping trop agressif supprime des constructeurs qu'il appelle, et les sauvegardes se mettent à échouer **dans le build seulement**, pas dans l'éditeur. Si tu passes à Medium, il faut un `Assets/link.xml` qui préserve les classes sérialisées (`SaveGameFile`, `SavedPawn`, `SavedPortal`, `SavedHex`…), puis **retester une sauvegarde et un rechargement dans le build**. Si tu veux zéro risque avant la mise en ligne, laisse Low : le gain est de l'ordre de quelques centaines de kilo-octets, ce n'est pas là que ça se joue.

**5. `Exception Support` est sur « Explicitly Thrown Exceptions Only »**, ce qui est le bon compromis. Le mettre sur « None » gagne encore un peu de taille et de vitesse, mais la moindre erreur ne fait plus qu'un plantage muet. À garder tel quel tant que le jeu n'est pas rodé.

## Les modules et paquets : le vrai poids du code

Le `.wasm` ne dépend presque pas de tes scripts, il dépend des **modules du moteur** embarqués. Le projet en embarque beaucoup qu'il n'utilise pas. Dans `Window → Package Manager`, section *Built-in*, décoche ceux-là :

Cloth, Terrain, Terrain Physics, Vehicles, Wind, VR, XR, Android JNI, Adaptive Performance, Unity Analytics, Accessibility, Tilemap, Physics 2D, Umbra (Occlusion Culling), Vector Graphics, Screen Capture, Asset Bundle, Director/Timeline, Video (s'il ne reste aucune vidéo après le nettoyage de Resources).

Et dans la liste des paquets installés, retire : **com.unity.recorder** (sert à enregistrer le trailer dans l'éditeur, inutile au jeu), **com.unity.multiplayer.center**, **com.unity.ai.navigation** (le jeu a son propre chemin hexagonal, il n'y a aucun NavMesh), **com.unity.timeline**, **com.unity.collab-proxy**, et deux des trois intégrations d'IDE (garde celle que tu utilises).

Cas particulier, **com.unity.inputsystem** : le réglage `Active Input Handling` est sur **Both**, donc les deux systèmes d'entrée partent dans le build alors que tout le code utilise `Input.GetKey` (l'ancien). Mets-le sur **Input Manager (Old)** et retire le paquet. Attention, ce changement demande un redémarrage de l'éditeur, et si un composant de Reach UI dépend du nouveau système, il faudra le remplacer — vérifie une fois le projet relancé.

Ordre de grandeur réaliste pour un projet de cette taille, modules nettoyés et Resources dégraissé : on passe d'un téléchargement de plusieurs dizaines de méga-octets à **8 à 12 Mo compressés**, et le temps avant la première image se compte en secondes plutôt qu'en dizaines de secondes.

## Les polices

4,81 Mo d'atlas TMP, c'est le symptôme d'atlas dynamiques qui ont grossi à l'usage, ou d'un jeu de caractères trop large. Pour de l'hébreu, tu n'as besoin que des 27 lettres (avec les finales), des chiffres, de la ponctuation, et éventuellement des niqqud si tu les affiches.

Ouvre chaque *Font Asset*, et en `Generation Settings` : **Atlas Resolution 1024×1024** (pas 4096), **Character Set : Unicode Range** avec `0x20-0x7E,0x5B0-0x5F4,0x200E-0x200F` plutôt que *Dynamic*, puis **Clear Dynamic Data** et sauvegarde. Un atlas statique bien cadré tient en 0,5 Mo au lieu de 2 Mo, et il ne grossit plus en cours de partie. Vérifie ensuite qu'aucun caractère ne manque dans le menu, les enseignements et le HUD — c'est exactement ce que le test manuel doit regarder.

## Ménage dans les dossiers d'assets

Ces paquets sont dans le projet et, hors Resources, ils ne partent dans le build que s'ils sont référencés. Ils coûtent surtout du temps d'import, de la place sur le disque et de la confusion — mais leurs shaders et matériaux peuvent se faire embarquer sans qu'on le voie.

Trois jeux de skybox cohabitent : `Allsky`, `SpaceSkies Free`, `Starfield Skybox`. Vérifie dans `Window → Rendering → Lighting` quel skybox chaque scène utilise, garde celui-là, supprime les deux autres. Même logique pour `Robo's turret (free)` et `Robo's turrets` (deux fois le même paquet), `TowerDefence_Vsquad`, `PolygonParticleFX`, `Hovl Studio`, `EffectCore`, `EasyOutlines`, `Le Tai's Asset`, `EZ-Camera-Shake`, `Hexlands_LowPolyStyle` : pour chacun, clic droit sur le dossier → *Select Dependencies* à l'envers, ou plus simplement teste en le sortant du projet et en relançant les deux scènes. Et **`Assets/_Recovery`** : c'est un dossier de récupération d'Unity, il n'a rien à faire dans un projet livré.

Enfin, les deux scripts à la racine d'`Assets` (`SoundManager.cs`, `VFXManager.cs`, `LookAtCameraCanvas.cs`) sont des vestiges hors namespace : vérifie qu'aucune scène ne les utilise, puis supprime-les.

## Le son

Pour chaque clip, dans l'inspecteur d'import : **Load Type** `Decompress On Load` pour les sons courts (clic, tir), `Compressed In Memory` pour les boucles, `Streaming` pour la musique ; **Compression Format** `Vorbis` partout en WebGL (ADPCM et PCM gonflent le fichier) ; **Quality** autour de 70 % ; **Force To Mono** coché pour tout ce qui est un effet. Le WebGL ne supporte qu'un décodage limité : une musique en `Decompress On Load` bloque le chargement de la page pendant plusieurs secondes.

## Mobile : ce que le code impose déjà, et ce qui reste à vérifier à la main

Le jeu se joue à la souris et au clavier. En l'état : le clic gauche sélectionne (ça marche au toucher), **Échap** saute l'intro, **Espace** termine le tour, **Tab** montre la menace. Sur un téléphone, il n'y a ni Échap, ni Espace, ni Tab — donc chacun de ces raccourcis doit avoir son bouton à l'écran. Le bouton de fin de tour existe, la barre des phases existe, le bouton de menace existe ; **l'intro n'a qu'un bouton « passer » à vérifier**, et c'est le premier écran qu'un joueur voit.

À tester sur un vrai téléphone, pas dans le simulateur : taille des textes (rien sous 19 px, c'est déjà garanti par le `MinFontSize` du HudBuilder), taille des cibles tactiles (44 px minimum), pas de scroll de la page, et surtout le rechargement de page en cours de partie — les sauvegardes passent par PlayerPrefs, donc par IndexedDB sur le web, et c'est ce qui casse en premier en navigation privée.

## L'ordre dans lequel je ferais tout ça

1. Sortir les 20,7 Mo de Resources (gain immédiat, risque nul si c'est fait dans Unity).
2. Qualité WebGL en Medium, mémoire max à 512 Mo (deux clics, grosse différence sur téléphone).
3. Trancher la question du `Decompression Fallback` selon l'hébergeur.
4. Décocher les modules du moteur inutiles, retirer les paquets inutiles, passer `Active Input Handling` sur Old.
5. Refaire les atlas de police en statique 1024.
6. Réimporter les textures restantes (512, pas de mipmaps) et les sons en Vorbis.
7. Supprimer les paquets d'assets non utilisés et `_Recovery`.
8. Puis seulement : `Edit → Project Settings → Player → Publishing Settings → Build Report`, ou `webGLAnalyzeBuildSize`, pour voir ce qui pèse encore — et mesurer au lieu de supposer.

Après chaque étape : les tests unitaires (Run All), un audit général, et un lancement des deux scènes. C'est ce qui garantit qu'un dégraissage n'a rien emporté au passage.
