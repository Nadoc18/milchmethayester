# Milchemet HaYetzer - Passe d'equilibrage

Ce document decrit ce qui a change par rapport au GDD V3 brut, pourquoi, et les
resultats de la simulation qui a servi a regler les chiffres.

---

## 1. Ce qui n'allait pas

Trois defauts structurels, pas des chiffres mal regles :

**Une seule decision par tour.** Le joueur cliquait un hexagone, repondait a une
question, et le tour s'arretait. Le reste (deplacement, ciblage, attaque) etait
automatique. Il n'y avait pas de strategie, seulement un quiz.

**Construire etait gratuit.** `ApplyCorrectAnswer` posait le batiment sans rien
facturer. Un Bunker infligeait 40 degats par tour, exactement le rythme de
deploiement des portails. Six Bunkers gratuits verrouillaient la carte pour
toujours : une seule ouverture dominait, et elle etait sans risque.

**La victoire etait hors d'atteinte.** Un Tank Niveau 1 fait 10 degats et avance
d'une case. Un portail a 7 cases avec 50 PV demandait une douzaine de tours, et il
y en a six. Avec 25 Energie par tour pour un Tank a 50, le joueur produisait 0,5
unite par tour contre 2 ennemis deployes : 4 contre 1.

---

## 2. Les cinq changements

### 2.1 Le revenu est separe de la depense

| Phase | Ce qui se passe |
|---|---|
| Revenu | Revenu passif (Base + Cristaux), puis une question de Trivia. Bonne reponse : +25 Energie. |
| Depense | Phase libre. Autant d'actions que le solde permet. **Espace** ou **Entree** termine le tour. |
| Tanks | Les Tanks agissent selon leur posture. |
| Yetzer Hara | L'IA joue les ennemis. |
| Fin de tour | Batiments, portails, annonce de vague, victoire / defaite. |

Le Trivia finance la strategie au lieu de la remplacer.

### 2.2 Tout coute

| Action | Cout | Niveau 3 |
|---|---|---|
| Tank | 50 | evolution 60 |
| Bunker (colline) | 40 | 60 |
| Usine a gaz | 30 | 45 |
| Cristal | 60 | 90 |
| Centre de Commandement (montagne) | 50 | 75 |

Revenus : solde d'ouverture **60**, Base **+12/tour**, Trivia **+25**, Cristal
**+5** (Niv2) ou **+10** (Niv3) par tour.

Un tour parfait sans Cristal rapporte 37 : moins qu'un Tank. Il faut choisir ou
economiser, jamais les deux.

**Entretien des Bunkers : 3 Energie/tour (5 au Niveau 3).** Un Bunker qu'on ne
peut pas alimenter ne tire pas ce tour-ci. C'est ce qui empeche le mur permanent.

### 2.3 Postures de Tanks

Un clic sur un Tank fait tourner sa posture (gratuit). Elle persiste et pilote le
ciblage automatique. La couleur du Tank indique sa posture.

- **Garde** (bleu) : ne depasse jamais 4 cases de la Base, ignore les Portails.
- **Assaut** (orange) : marche sur les Portails. Il se defend s'il est attaque a
  portee, mais ne se laisse pas rappeler.
- **Chasse** (vert) : l'ennemi le plus proche, ou qu'il soit.

### 2.4 Instabilite des Portails

C'est le mecanisme central : il relie la defense et l'offensive.

- Chaque ennemi tue inflige **12 PV** a son portail d'origine, bouclier ignore.
- Ce contrecoup ne descend jamais sous **20 % des PV max** : resister use la
  source, seul un Tank peut la fermer.
- Au bout de **4 morts**, le bouclier saute pendant **4 tours** : le portail cesse
  de deployer et encaisse les degats en plein (au lieu de moitie).

Concretement : tenir la ligne devant sa Base amene un portail Niveau 1 a son
plancher au moment exact ou il devient vulnerable. La fenetre d'assaut se merite.

### 2.5 Imprevisibilite annoncee

- **Vagues (surges)** : un portail est designe un tour a l'avance (log + bouffee de
  fumee sur la case) et deploie 3 ennemis au lieu de 1. L'intervalle se resserre
  avec le temps : rester sur la defensive n'est pas un abri.
- **Composition variable** : tours 1-6 uniquement Niv1 ; 7-12 melange Niv1/Niv2 ;
  13+ ajoute des Niv3.
- **Evolution echelonnee** : les portails passent au Niveau 2 aux tours 18, 21, 24,
  27, 30, 33 - plus tous en meme temps. Et l'evolution **reporte les degats deja
  encaisses** au lieu de remettre a neuf.

---

### 2.6 Vision du Yetzer Hara (apercu de menace)

Des fantomes translucides des unites ennemies rejouent **en boucle** ce que
l'adversaire s'apprete a faire. **Tab**, ou un bouton d'interface.

Chaque intention est une petite scene qui tourne :
**apparition (FX de deploiement) -> action -> pause 2 s -> on recommence.**

| Fantome | Signification |
|---|---|
| Glisse vers une case, teinte orange | cet ennemi va s'y deplacer |
| Reste en place, rouge, explosion sur une cible | cet ennemi va attaquer la |
| Apparait sur une case, violet | un Portail va deployer ici en fin de tour |
| Plusieurs fantomes en cercle, rouges | vague annoncee (autant de fantomes que d'ennemis) |

L'explosion d'attaque ne fait **pas** vibrer la camera : une secousse dirait
"ca arrive maintenant", alors que justement ce n'est pas encore arrive. C'est la
meme raison qui rend les modeles translucides.

Ce n'est pas de la triche : le combat est deterministe et l'IA choisit par score,
donc l'information existait deja, elle etait juste invisible. La transparence dit
que **c'est une projection, pas une certitude** : les Tanks jouent avant les
ennemis, et chaque action du joueur recalcule l'apercu en direct. Poser un Bunker
et voir trois fleches changer de cible, c'est exactement la decision qu'on veut
donner au joueur.

L'apercu appelle `EnemyAI.SelectEnemyTarget` et `PortalManager.PredictSpawnCount`,
les memes fonctions que la phase ennemie : il ne peut donc pas mentir sur les regles.

**Mise en place :** rien d'obligatoire, la touche Tab suffit. Pour un bouton,
poser le composant `ThreatPreviewButton` sur le GameObject du bouton - il se
branche tout seul, rien a cabler dans OnClick.

---

## 3. Verification par simulation

Un simulateur rejoue ces regles hors Unity, 600 parties par strategie.

| Strategie | Victoire | Defaite | Duree victoire | Duree defaite |
|---|---|---|---|---|
| Tortue (fortifications seules) | **0 %** | 72 % | - | 50 |
| Rush (tout en Tanks d'assaut) | 41 % | 59 % | **17** | 21 |
| Economie (Cristaux d'abord) | 38 % | 62 % | **26** | 33 |
| Equilibree (socle defensif puis offensive) | **54 %** | 46 % | **27** | 35 |
| Adaptative (mur puis assaut au tour 9) | **56 %** | 44 % | **26** | 34 |

Lecture :

- **Les victoires tombent entre 17 et 27 tours**, dans la cible visee.
- **Quatre strategies actives sont viables** (38 a 56 %), aucune n'ecrase les autres.
- **Les strategies mixtes sont les meilleures**, ce qui est le signe d'un espace de
  decision sain.
- **La tortue pure ne gagne jamais.** Elle ne peut pas : le plancher de contrecoup
  interdit de fermer un portail sans y aller. Elle finit meme par perdre (72 %),
  parce que les vagues se resserrent.

---

## 4. Ou regler quoi

| Reglage | Fichier |
|---|---|
| Couts, revenus, instabilite, entretien | `InteractionRules.cs` (toutes les `const`) |
| Poids de ciblage des Tanks, rayon de Garde | `TurnManager.cs`, inspecteur |
| Poids de ciblage ennemi, attrait des batiments | `EnemyAI.cs`, inspecteur |
| Frequence et premier tour des vagues | `PortalManager.cs`, inspecteur |
| Touche, couleurs et transparence de l'apercu | `ThreatPreview.cs`, inspecteur |
| Duree de la phase de depense (0 = illimitee) | `TurnManager.spendingDuration` |

---

## 5. A faire dans l'editeur Unity

1. `BoardController` : reassigner `m_HexagonPrefabs` indices **22, 23, 24** vers
   `enemy_1/2/3.prefab` (ils pointent encore sur les prefabs d'ennemis detruits).
2. `GameManager` : verifier que **Board Layer Mask** vaut *Everything*.
3. `TriviaManager` : assigner `questionText` (et une police contenant l'hebreu).
4. `TurnManager` : assigner `phaseText` (optionnel) pour afficher la phase et le solde.
5. **Ctrl+S** sur la scene pour conserver les assignations.
