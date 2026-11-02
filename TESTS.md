# Tests et audit — préparer la prod

Trois niveaux, du plus rapide au plus complet.

## 1. Tests unitaires (30 secondes)

**Window → General → Test Runner → onglet EditMode → Run All**
(ou *Milchemet → Tests → Tests unitaires*). Fichiers : `Assets/Scripts/Editor/Tests/`.

| Fichier | Ce qu'il fige |
|---|---|
| `RulesTests.cs` | les chiffres des règles : PV / dégâts / portée / mobilité des Tanks et des ennemis **aux 3 difficultés**, aucun hasard, PV et coûts des bâtiments, économie (plancher, usines, Cristal, Bunker), Centre de Commandement (bulle qui absorbe puis cède), Shofars (contrecoup, plancher, bouclier), distance hexagonale, les 4 dispositions du plateau (169 cases, 6 Shofars, une Base, aucun pion posé d'avance, coordonnées valides et uniques) |
| `ContentTests.cs` | `hud_labels.json` valide, **toutes les clés lues par le code existent** (une clé manquante = un texte vide dans le jeu, sans erreur), aucune clé en double contradictoire, les formats ont leurs `{0}` ; `teachings.json` complet (titre, texte, source, questions avec 2–4 réponses et index correct) ; les 23 photos présentes et importées en Sprite ; les 10 pictogrammes ; les 3 shaders et l'habillage UI ; les scènes du build (menu en premier) ; **aucun script manquant dans les scènes** ; aucun caractère non-ASCII dans un `.cs` ; `TestRunner` absent des scènes ; `Rtl.Fix` ; aller-retour JSON d'une sauvegarde |

Un test rouge dit exactement quoi et où. Si la fenêtre ne liste aucun test, dis-le-moi (le dossier `Editor/Tests` doit être compilé avec NUnit ; sinon je passe par une assembly definition).

## 2. Audit général (5 à 20 minutes, le jeu joue seul)

**Milchemet → Tests → Audit général**, trois variantes :
- *une partie (40 tours)* — la difficulté et le plateau courants ;
- *une partie longue (80 tours)* ;
- *toutes les configurations (12 parties)* — 3 difficultés × 4 plateaux.

Le mode Play démarre tout seul, le pilote (`Assets/Scripts/Dev/GameAudit.cs`) prend la main avec **une stratégie différente à chaque partie** — Conseiller (suit les conseils), Tortue (Bunkers + Tanks en Garde), Ruée (Tanks en Assaut dès que possible), Économie (usines, Cristal, Tanks en Chasse qui évoluent), Chaos (actions et postures au hasard) — et termine les tours à l'allure Éclair. Les 12 parties du mode complet couvrent les 5 stratégies ; le rapport donne pour chacune la composition finale du plateau, pour voir que deux parties ne se ressemblent pas. Ne touche à rien pendant qu'il tourne. À la fin le mode Play s'arrête et le rapport est dans **`Audit/rapport_audit.txt`** (menu *Ouvrir le dernier rapport*). Première ligne : **VERT** ou **ROUGE**.

Ce qu'il vérifie à chaque tour :
- Énergie jamais négative ; aucun pion à 0 PV encore sur le plateau, aucun pion au-dessus de ses PV max, aucun pion hors plateau, jamais deux pions sur la même case, le plateau voit bien le pion là où il est ;
- PV des cases ≤ max, bulles dans leurs bornes, Shofars : PV > 0 tant qu'ils sont debout, fissures entre 0 et le seuil, bouclier rompu ≤ 4 tours, jamais « fissuré ET rompu », compte cohérent entre le plateau et `PortalManager` ;
- Base ≤ max, et pas de Base à 0 sans fin de partie ;
- quand le joueur a la main : aucun panneau d'enseignement resté ouvert, pas d'écran de fin fantôme, barre des phases arrêtée, vue immersive relâchée, plateau déverrouillé, compteurs du HUD non vides ;
- **les rôles** : un Tank en Garde reste à moins de 5 cases de son ancrage (Base ou Centre) ; un Tank en Assaut se rapproche d'un Shofar en 4 tours au plus ; un Tank en Chasse se rapproche d'un ennemi ; un ennemi ne reste pas 3 tours immobile sans cible au contact ; un Bunker avec un ennemi à portée s'use (donc a tiré) ; un Tank blessé près d'un Cristal, sans ennemi proche, est soigné ; le revenu entre deux dépenses vaut plancher + usines + Shofars retournés ; le Yetzer Hara a déployé avant le tour « intervalle + 3 ». Ces remarques sont listées à part (« role ») : elles ne mettent pas le rapport en rouge, elles disent ce qu'il faut aller regarder ;
- **toute erreur ou exception dans la console** est comptée et citée (les 40 premières) ;
- au tour 6 : **sauvegarde → rechargement de la scène → la partie reprend au même tour, même Énergie, même Base, mêmes Shofars, même difficulté, même plateau, et chaque pion à sa place avec ses PV et son rôle** (dans un magasin en mémoire : tes vraies sauvegardes ne sont pas touchées) ;
- à la fin : l'écran de fin s'affiche avec un titre et un bouton Rejouer ; temps d'image moyen et pire image de la partie.

Un tour qui ne se termine pas en 3 minutes est signalé (phase bloquée). *Arrêter l'audit* coupe tout.

## 3. Test à la main avant la mise en ligne (20 minutes)

Ce qu'aucun robot ne voit : lisibilité, sons, caméra, toucher.

1. **Menu** : les 3 difficultés se sélectionnent, Nouvelle partie lance la visite d'intro, Échap la saute, Reprendre liste bien une partie en cours (et seulement celles-là).
2. **Un tour complet en Normal** : lire l'ouverture du tour, l'enseignement et sa question (bonne et mauvaise réponse), poser un Tank dans chaque posture (Garde / Assaut / Chasse + choix de cible), construire chaque bâtiment, faire évoluer un Tank près d'un Cristal, utiliser Voir de près et sortir.
3. **Le menu de case** s'ouvre à côté de la case, la flèche désigne la bonne case, le survol d'un conseil pointe la bonne case.
4. **Phases automatiques** : les 3 allures, « suivant », « tout passer » ; les lasers (bleu / rouge-orange) partent du bon pion ; le coup de fissure se voit (flash, pastilles, impact) ; pastilles au-dessus du Shofar tant que la bulle tient, barre de vie une fois rompue.
5. **Jouer jusqu'à une victoire et une défaite** (une partie en Facile, une en Difficile sans rien construire) : écran de fin, rang affiché, Rejouer.
6. **Build WebGL** : jouer un tour dans Chrome ET sur un téléphone (taille des textes, cibles tactiles, pas de scroll de page) ; recharger la page et reprendre la partie (les sauvegardes passent par PlayerPrefs/IndexedDB sur le web).

## Déjà corrigé pour la prod

- **Plafond de Tanks** (`InteractionRules.TankCap`) : la Base coordonne 10 / 9 / 8 Tanks (Facile / Normal / Difficile), chaque Centre de Commandement debout en ajoute 2 (rang 2 : 4). Au plafond, le menu de case dit « l'armée est au complet · x/y », l'info-bulle explique, le Conseiller ne propose plus de Tank, et le compteur du HUD (ligne « Tanks », à reconstruire via le HudBuilder) passe à l'orange. Sans ça, une partie riche finissait à 40 ou 100 Tanks en file devant le même Shofar.
- **Sauvegarde sous un Cristal** : un Tank sauvé à 50/50 (bonus +20) revenait à 30/30 au rechargement ; le bonus est maintenant rendu avant de relire ses PV.

- **Le plateau se tire au sort** à chaque nouvelle partie (jamais le même que la précédente) ; le bouton de choix de la disposition n'est plus construit dans le menu.

- **La touche R rasait le plateau en pleine partie** (`LocalGameEngine.Update`, outil de debug) : désormais compilée seulement dans l'éditeur et les builds de développement.
- `TestRunner.cs` (vérifications au lancement) n'est dans aucune scène — le test le garantit.

## Avant de builder

- `Edit → Project Settings → Player → WebGL` : compression Brotli ou Gzip (ou *Decompression Fallback* si l'hébergeur ne règle pas les en-têtes), `Run In Background` coché.
- Retirer de la scène tout composant de debug éventuel ; l'audit et le trailer ne vivent que dans un PlayerPrefs, rien ne part dans le build.
- Lancer les tests unitaires + un audit « toutes les configurations » : les deux verts, puis le test à la main.
