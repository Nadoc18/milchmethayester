# Milchemet HaYetzer — implémentation du GDD V3

Document de référence pour le code. Il décrit ce qui a été câblé, où le modifier,
et les réglages qui ne viennent pas du GDD.

## Convention de niveau (à lire en premier)

Le champ `Hexagon.level` n'a pas la même base selon le type de case :

| Élément | Convention |
|---|---|
| **Terrains** (plaine, désert, colline, gaz, cristal, montagne) | `level 0` = « Niveau 1 » du GDD (naturel) · `level 1` = « Niveau 2 » · `level 2` = « Niveau 3 » |
| **Portails** et **Base** | `level` suit directement le GDD (1 ou 2) |
| **Pions** (`PawnController.level`) | suit directement le GDD (1, 2, 3) |

C'est la base historique des données de carte ; toutes les fonctions
`InteractionRules.Get*` prennent le niveau interne et renvoient la valeur du GDD.

## Séquence d'un tour

`TurnManager` orchestre, dans cet ordre :

1. **Phase joueur** — clic sur un hexagone → `TriviaManager` pose une question (10 s).
   La réponse clôt le tour. Sans clic, le tour s'arrête au bout de `turnDuration` (60 s).
2. **Phase Tanks** — chaque Tank vise l'ennemi le plus proche, sinon le portail le plus proche.
   À portée il attaque, sinon il avance d'une case.
3. **Phase Yetzer Hara** — `EnemyAI` fait la même chose en visant l'allié le plus proche, sinon la Base.
4. **Fin de tour** — `BuildingManager` (bunkers, gaz, cristaux, montagnes) puis
   `PortalManager` (déploiement, évolution), puis test de victoire / défaite.

## Économie

`EnergyManager` tient le solde global. Bonne réponse : **+25**. Création d'un Tank : **−50**.
L'ancien système de Command Points par hexagone n'est plus utilisé (le champ
`Hexagon.commandPoints` subsiste pour les panneaux d'UI, mais plus aucune règle ne s'en sert).

Conséquence du barème : le premier Tank arrive au 2ᵉ tour utile (25 puis 50 → dépense).

## Actions d'une bonne réponse (`InteractionRules.ApplyCorrectAnswer`)

| Case cliquée | Effet |
|---|---|
| Plaine / Désert libre | Crée un Tank Niv 1 si le solde ≥ 50 |
| Colline / Gaz / Cristal / Montagne | Monte d'un niveau (plafond : GDD Niv 3) |
| Case occupée par un Tank Niv 1 | Évolue en Niv 2 **si** un Cristal actif le couvre |
| Case occupée par un ennemi | Rien (le GDD interdit de bâtir sur une case occupée) |
| Portail / Base | Rien |

Dans tous les cas, les +25 Énergie sont crédités.

## Combat

Aucun hasard : `attackDamageMin == attackDamageMax`, `critChance = 0`, `dodgeChance = 0`.
Chaque attaque touche. Le défenseur riposte automatiquement si l'attaquant est à sa portée.
Les Portails, la Base et les bâtiments ne ripostent jamais.

`InteractionRules.ApplyStatsToPawn` est la **seule** source des statistiques :
Tank 30/10/P1 puis 60/20/P2 ; Ennemis 20/10/P1, 40/15/P2, 80/30/P3.

## Structures

| Structure | PV | Effet |
|---|---|---|
| Base | 200 (réservoir commun aux 7 hexagones) | 0 PV → défaite |
| Portail Niv 1 | 50 | Déploie des ennemis Niv 1, évolue au bout de 12 tours |
| Portail Niv 2 | 100 | Ennemis Niv 2, 10 % de chance d'un Niv 3 |
| Bunker (colline Niv 2) | 40 | Fin de tour : 4 cibles au hasard, portée 2, 10 dégâts |
| Forteresse (colline Niv 3) | 80 | 6 cibles, portée 2, 15 dégâts |
| Gaz Niv 2 / Niv 3 | — | Soigne +10 PV à 1 case / +20 PV à 2 cases |
| Cristal Niv 2 / Niv 3 | — | +20 PV Max aux alliés à 1 / 2 cases, et débloque l'évolution des Tanks |
| Montagne Niv 2 / Niv 3 | 30 / 50 | Repousse les ennemis à 1 / 2 cases, perd 10 PV par tour |

Les collines sont des obstacles infranchissables dès le niveau naturel, pour les deux camps.
Le bonus de Cristal est **recalculé** chaque tour (`SetCrystalBonus`), il ne se cumule jamais.

## Réglages hors GDD (à ajuster librement)

Dans `InteractionRules` :

- `PORTAL_SPAWN_INTERVAL = 3` — un portail donné ne déploie qu'un ennemi tous les 3 tours.
- `MAX_SPAWNS_PER_TURN = 2` — plafond global par tour, tous portails confondus.
- `MAX_ACTIVE_ENEMIES = 18` — plafond d'ennemis simultanés.

Avec 12 portails sur la carte et un Tank tous les deux tours côté joueur, ces trois valeurs
décident entièrement de la difficulté. Ce sont les premiers curseurs à toucher en playtest.

Dans `TurnManager` : `turnDuration` (60 s). Dans `TriviaManager` : `timeLimit` (10 s).

## Trivia

Les questions vivent dans `Assets/Resources/trivia_questions.json`, en UTF-8, avec les quatre
catégories du GDD (Halacha, Tanakh, Musar, Tefillah). 12 questions livrées, 3 par catégorie.
Le tirage est sans répétition jusqu'à épuisement du sac.

L'hébreu ne transite plus par un fichier `.cs` : c'est ce qui avait détruit la question
d'origine (remplacée par des caractères U+FFFD irrécupérables).

Format d'une entrée :

```json
{ "category": "Tanakh", "question": "...", "answers": ["a","b","c","d"], "correctIndex": 2 }
```

## À câbler dans la scène

- `TriviaManager` : `modalWindow`, `timerBar`, `answerButtons` (4 boutons Reach).
- `EnergyManager.energyText` (optionnel) : un TextMeshProUGUI pour afficher le solde.
- `TurnManager.turnNumberText` (optionnel).
- `EnergyManager`, `PortalManager` et `BuildingManager` sont ajoutés automatiquement
  par `LocalGameEngine` s'ils sont absents — pas besoin de les poser à la main.

## Points restés en suspens

- Le joueur n'a qu'une action par tour (une question). Les Tanks agissent ensuite seuls.
  Si le GDD veut un déplacement manuel des Tanks, c'est `TurnManager.ProcessPlayerUnitsSequential`
  qu'il faut remplacer par une sélection à la souris.
- Les prefabs n'ont pas de modèle distinct pour le « Niveau 3 » des terrains :
  `PrefabsPath` retombe sur le modèle de Niveau 2.
- `GameCanvasController.cs` est encodé en Windows-1252 et non en UTF-8. Il n'a pas été touché,
  mais il faudra le convertir avant d'y écrire du texte accentué ou hébreu.
