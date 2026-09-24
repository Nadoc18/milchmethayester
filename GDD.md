# מלחמת היצר — Milchemet HaYetzer

**Game Design Document — version complete**
Etat du code au 24 septembre 2026, branche `features`.

Ce document decrit le jeu **tel qu'il est ecrit dans le code**, pas tel qu'il etait
prevu. Chaque chiffre vient d'une constante reelle ; les sources sont indiquees en
fin de section. Ce qui n'est pas encore branche est signale au chapitre 17.

---

## 1. Le jeu en une page

**Genre** — tactique au tour par tour sur plateau hexagonal, un joueur contre le
plateau. Un melange de defense de position et de conquete : on batit une economie,
on tient une ligne, puis on sort la briser.

**Le theme** — ce n'est pas une guerre contre un ennemi exterieur. Le **Yetzer Hara**
(le penchant au mal) tient six **Shofars** (שופר, portails) aux six pointes du
plateau. Il en fait sortir ses emissaires, et il a desseche tout le pourtour de la
carte. Le joueur part du centre avec une **Base**, et doit fermer les six Shofars
avant que sa Base ne tombe.

**Le but ultime, et ce qui distingue le jeu** — fermer un Shofar n'est que la
premiere moitie du travail. Le Tanya distingue **אתכפיא** (*atkafya*, soumettre le
Yetzer) et **אתהפכא** (*at'hapkha*, le retourner en bien). Sur la ruine d'un Shofar
abattu, le joueur peut **batir** : la porte du mal devient une source de production.
Ce n'est pas obligatoire, mais c'est ce qui determine son rang final.

**Plateforme** — Unity 6000.4.10f1, Built-in Render Pipeline, cible **WebGL**.
Interface integralement en hebreu, sens de lecture droite-gauche.

**Duree d'une partie** — une quinzaine a une trentaine de tours.

---

## 2. La boucle de jeu

Un tour comporte cinq etapes. Chacune est annoncee par un bandeau plein ecran
(`PhaseBanner`) pour que le joueur sache toujours qui joue.

### Etape 1 — Revenu (`TurnPhase.Income`)

Le plateau est verrouille.

1. Le **plancher de la Base** est verse : 12 Energie (moyen).
2. L'**ecran d'ouverture de tour** annonce le numero du tour et ce plancher.
3. L'**enseignement** : deux textes tires du Tanya, de la Guemara ou du Messilat
   Yesharim, et un tour sur deux une question. Bonne reponse = **une fissure de
   plus sur le Shofar le plus fissure** (voir §13).

> **Les usines ne paient pas ici.** Elles ont verse a la fin du tour precedent, une
> par une, pendant que la camera s'arretait sur chacune. C'est voulu : un compteur
> qui saute de 40 a 190 en silence n'apprend rien, et le tour ou une usine ne repond
> plus doit se voir.

### Etape 2 — Depense (`TurnPhase.Spending`)

La seule etape ou le joueur agit. **Aucune limite de temps** par defaut
(`spendingDuration = 0`). Il clique autant de cases que son solde le permet :

- poser un **Tank** sur une plaine ;
- **batir** ou faire evoluer un batiment ;
- changer la **posture** d'un Tank deja pose, ou le faire evoluer ;
- **retourner** la ruine d'un Shofar.

Un clic ouvre le **menu de case** (voir de pres / details / utiliser l'energie) ; il
ne depense jamais directement. Le joueur termine avec **Espace**, **Entree** ou le
bouton de fin de tour.

Touche **Tab** : la *vision du Yetzer Hara* — des fantomes rejouent en boucle ce que
chaque ennemi et chaque Shofar s'apprete a faire. Cet apercu appelle exactement le
meme code que la phase ennemie ; **il ne peut pas mentir**.

### Etape 3 — Tanks (`TurnPhase.Tanks`)

Les Tanks agissent seuls, un par un, dans l'ordre de la liste du plateau. Le joueur
ne pilote rien : il a donne une **intention** (la posture) et eventuellement une
**cible** ; la resolution est automatique et deterministe. La camera suit chaque
unite, une carte nomme qui agit et ce qu'il fait.

### Etape 4 — Yetzer Hara (`TurnPhase.Enemies`)

Meme grammaire a l'ecran, en rouge. Meme tempo (0,95 s de cadrage, 0,35 s de visee,
1,05 s de resolution) : un ennemi plus rapide donnerait l'impression qu'il triche.

### Etape 5 — Fin de tour (`TurnPhase.EndOfTurn`)

Dans cet ordre exact :

1. **Les usines paient.** La camera va sur chaque usine et sur chaque Shofar
   retourne ; le gain s'affiche en grand puis file vers le compteur.
2. **Les Bunkers tirent**, les **Cristaux soignent**, les **Centres de Commandement
   s'usent**.
3. **Les Shofars deploient**, se refont, evoluent ; une vague est annoncee.
4. **La terre refleurit** (§11) si un Shofar est tombe.
5. Test de victoire / defaite. Sinon, tour suivant.

*Sources : `TurnManager.cs`, `PhaseBanner.cs`, `ThreatPreview.cs`.*

---

## 3. Le plateau

**Rayon 7, 169 cases, coordonnees cubiques.** Le plateau est **fixe** : deux parties
donnent exactement le meme dessin. C'est un plateau d'echecs — ce qui change d'une
partie a l'autre, c'est ce qu'on en fait.

Il a une **symetrie d'ordre 6** : chaque site est donne par une seule case, les cinq
autres exemplaires viennent de la rotation de 60 degres `(q,r,s) -> (-r,-s,-q)`.
Aucun secteur n'est avantage.

### 3.1 Composition

| Terrain | Nombre | Role |
|---|---|---|
| Base | 7 | le centre et ses six voisines, un seul reservoir de PV |
| Shofar | 6 | les six pointes |
| Colline | 6 | support de Bunker |
| Usine de Gaz | 6 | toute l'economie |
| Cristal | 6 | soin, PV max, evolution des Tanks |
| Centre de Commandement | 6 | tete de pont |
| Plaine | 60 | **seul terrain qui porte un Tank** — tout l'interieur du plateau |
| Desert | 72 | les deux anneaux exterieurs, et **rien d'autre** |

### 3.2 Distances (ce qui decide de tout)

La distance qui compte pour la construction est celle au **bord de la Base**, pas au
centre.

| Site | Depuis le centre | Depuis la Base | Du Shofar le plus proche |
|---|---|---|---|
| Colline | 3 | **2** | 4 |
| Usine de Gaz | 4 | **3** | 5 |
| Cristal | 5 | **4** | 3 |
| Centre de Commandement | 5 | **4** | 3 |
| Shofar | 7 | **6** | — |

**Consequence : tout ce qui n'est pas du desert est a portee de la Base des le
premier tour** (portee 4). Le joueur n'a jamais a deviner pourquoi un refus.

Les collines sont posees **sur le couloir** qui va de la Base au Shofar de leur axe :
un Bunker y porte a 2 cases, donc il couvre le corridor depuis le bord de la Base
jusqu'a mi-chemin.

Les Cristaux et les Centres sont a **3 cases d'un Shofar**. Faire evoluer un Tank
demande donc de sortir de chez soi.

**Regle de voisinage** : aucun site n'en touche un autre. Chaque site est entoure
uniquement de plaine — donc de cases ou les pions circulent. Sans cette regle, un
site pouvait naitre ni defendable ni attaquable.

**Une seule frontiere de sol.** Anneaux 0 a 5 : plaine, partout. Anneaux 6 et 7 :
desert, partout. Le sol interieur etait autrefois tire d'un hachage - deux cases sur
cinq en desert, eparpillees sur les anneaux 3, 4 et 5. C'etait stable et symetrique,
mais arbitraire : aucun joueur ne devine un modulo, et le desert perdait son sens en
apparaissant au pied de la Base. Il est maintenant exactement, et uniquement, ce que
le Yetzer a desseche.

### 3.3 Le glacis

**Les deux anneaux exterieurs (anneaux 6 et 7) sont du desert nu** : 78 cases, dont
6 Shofars, soit **72 cases mortes — 43 % du plateau**. Ni usine, ni colline, ni meme
de plaine : une plaine porterait un Tank, et on pourrait donc s'installer a la porte
du Shofar.

Ce que le glacis a enleve : **6 collines avancees** (a 2 cases d'un Shofar) et
**6 usines de gaz exposees**. Elles promettaient une « economie risquee » ; en
pratique elles offraient surtout de quoi batir une forteresse chez le Yetzer — un
Bunker a deux cases d'un Shofar fauchait ses emissaires a la sortie, sans risque et
sans fin.

Le glacis a trois effets :

1. **Il plafonne l'economie** a six usines au lieu de douze.
2. **Il donne du temps** : l'ennemi marche trois tours a decouvert avant d'atteindre
   la premiere ligne.
3. **Il fait de l'assaut un vrai depart** : on quitte tout ce qu'on a construit, on
   n'emporte que des Tanks.

Ce n'est pas un decor : c'est ce que le Yetzer a desseche, et cela se rend (§11).

*Source : `MapGenerator.cs`.*

---

## 4. L'economie

### 4.1 Les revenus

| Source | Rang 1 | Rang 2 |
|---|---|---|
| Plancher de la Base | 15 / **12** / 10 par tour (facile / moyen / difficile) | — |
| Usine de Gaz | +15 / tour | +30 / tour |
| Shofar retourne | +35 / tour | — |
| Cristal | **0** | 0 |

Le Cristal ne rapporte rien, et c'est deliberement net : **le Gaz paie, le Cristal
sert l'armee**. Avant, les deux rapportaient quelque chose de vaguement utile et le
joueur ne voyait pas la difference.

**Revenu maximal avant toute conquete** : 12 + 6 x 30 = **192 par tour**.

### 4.2 Les couts

| Achat | Rang 1 | Rang 2 (cout du passage) |
|---|---|---|
| Tank | **40** | 60 (evolution, uniquement pres d'un Cristal) |
| Changement de posture | 15 (gratuit a la creation) | — |
| Bunker (colline) | **60** | 80 |
| Usine de Gaz | 30 | 45 |
| Cristal | 60 | 90 |
| Centre de Commandement | 50 | 75 |
| Retourner un Shofar tombe | **120** | — |
| Un tir de Bunker | 4 | 5 |

**Le Tank coute moins cher que le Bunker, et c'est le pivot de tout l'equilibrage
militaire.** Le Bunker frappe quatre fois plus fort, deux fois plus loin, et n'exige
aucun pilotage. Mais un Bunker ne bouge pas, et les Shofars sont a sept cases : le
Tank est le **seul** moyen d'aller en fermer un, et le seul moyen de porter la
construction au-dela de la Base. Faire payer le plus cher l'unique chemin vers la
victoire, c'etait taxer la sortie et subventionner l'attente.

Rendement tout compris, une fois le Bunker use :
- **Bunker** : 60 + ~48 de munitions pour ~120 degats -> 0,9 par degat
- **Tank** : 40 pour ~50 degats s'il tient cinq tours -> 0,8 par degat

Presque le meme prix au degat. Le Bunker paie une prime pour la salve, la portee et
le zero-pilotage ; le Tank est moins cher et demande de la patience.

### 4.3 La prosperite nourrit le Yetzer

> **Chaque 60 Energie de revenu d'usines retire un tour a l'intervalle de
> deploiement de TOUS les Shofars.** Plancher : 2 tours.

C'est la regle qui empeche la partie economique pure. Construire est toujours bon,
mais jamais gratuit : une carte entierement developpee (192 par tour) accelere le
Yetzer de **3 tours**, ce qui ramene l'intervalle de 4 a 2 en difficulte moyenne.

**Seules les usines de Gaz comptent** dans ce calcul. Un Shofar retourne verse 35
par tour **sans nourrir le Yetzer** : cette energie-la lui a ete prise, elle ne le
renforce pas. C'est la recompense mecanique de l'*at'hapkha*.

*Sources : `InteractionRules.GasIncomePerTurn()`, `SpawnAcceleration()`.*

---

## 5. Les batiments

Chaque terrain se construit en deux rangs. Le niveau interne 0 = case nue,
1 = construit, 2 = ameliore.

### 5.1 La Base — 200 PV

Sept cases, **un seul reservoir de 200 PV**. Elle verse le plancher de revenu. Elle
donne la portee de construction sur 4 cases. **Si elle tombe, la partie est perdue.**
Elle ne se repare pas.

### 5.2 Le Bunker (colline)

| | Rang 1 | Rang 2 (forteresse) |
|---|---|---|
| Cout | 60 | 80 |
| PV | 40 | 80 |
| Degats par tir | 10 | 15 |
| Tirs par tour | 4 | 6 |
| Portee | 2 | 2 |
| Cout d'un tir | 4 | 5 |
| Usure par tour | 8 | 11 |
| Usure par tir | 2 | 2 |

**Il fond.** Plein feu : environ 3 tours au rang 1, 4 au rang 2. Au calme : 5 et 8
tours. A zero PV il s'effondre, la colline redevient nue et se reconstruit.

Sans cette usure, un Bunker etait eternel : on couvrait la carte de forteresses, plus
rien n'arrivait nulle part, et la partie etait jouee.

**Tir concentre** : le Bunker vide son chargeur sur la meme cible jusqu'a ce qu'elle
tombe, puis passe a la suivante. Il vise d'abord **ce qu'il peut achever ce tir-ci**,
sinon **le plus avance vers la Base**. La regle inverse — un tir par ennemi — avait
l'air prudente et rendait l'arme inutile : dix degats sur trente points de vie ne
tuent jamais personne.

Un ennemi abattu par un Bunker ebranle son Shofar d'origine exactement comme s'il
tombait sous le feu d'un Tank. **Tenir la ligne est une facon de gagner.**

### 5.3 L'usine de Gaz

| | Rang 1 | Rang 2 |
|---|---|---|
| Cout | 30 | 45 |
| PV | 40 | 70 |
| Revenu | +15 / tour | +30 / tour |

Toute l'Energie vient de la. Une usine detruite ne produit plus rien. C'est **la
premiere cible du Yetzer Hara** (priorite 980, au-dessus de la Base elle-meme).

### 5.4 Le Cristal

| | Rang 1 | Rang 2 |
|---|---|---|
| Cout | 60 | 90 |
| PV | 35 | 60 |
| Soin par tour | 10 | 20 |
| Portee | 1 | 2 |
| Bonus PV max | +20 (sans cumul) | +20 |

**Ne rapporte aucune Energie.** Il soigne les Tanks a portee, leur donne +20 PV max
tant qu'ils restent dessous, et surtout : **c'est le seul endroit ou un Tank peut
passer au rang 2**. Place a 4 cases de la Base et 3 d'un Shofar, il oblige a avancer.

### 5.5 Le Centre de Commandement (montagne)

| | Rang 1 | Rang 2 |
|---|---|---|
| Cout | 50 | 75 |
| PV | 30 | 50 |
| Repousse les ennemis a | 1 case | 2 cases |
| Rayon de commandement | 2 | 3 |
| Usure | -5 PV / tour | -5 PV / tour |

Trois effets, tous importants :

1. **Il barre le passage** : les cases dans son rayon de repulsion sont
   infranchissables pour les ennemis, et seulement pour eux.
2. **Il commande** : tout Tank dans son rayon avance **une case de plus** par tour,
   et un Tank en Garde defend autour de lui comme autour de la Base.
3. **Il porte la construction** : on peut batir dans son rayon de commandement, meme
   loin de la Base. C'est ce qui permet de prendre le terrain rendu par un Shofar
   tombe.

Il s'use de 5 PV par tour, quoi qu'il arrive : **c'est une tete de pont, pas une
fortification permanente**. Au rang 1 il tient 6 tours, au rang 2 dix.

*Sources : `InteractionRules.cs`, `BuildingManager.cs`.*

---

## 6. Les Tanks

**Stats**

| | Rang 1 | Rang 2 |
|---|---|---|
| Cout | 40 | +60, uniquement au contact d'un Cristal |
| PV | 30 | 60 |
| Degats | 10 | 20 |
| Portee | 1 | 2 |
| Mobilite | 1 case | 2 cases |

Un Tank ne se pose que sur une **plaine**, et seulement dans la **portee de
construction** (§7). Le combat est **sans hasard** : pas de critique, pas d'esquive.
Le defenseur **riposte automatiquement** s'il survit et que l'attaquant est a sa
portee.

### 6.1 Les trois postures

Le joueur ne pilote pas chaque deplacement : il donne une **intention**.

| Posture | Ce qu'elle fait | Shofars | Ennemis | Poids de la distance |
|---|---|---|---|---|
| **Garde** (שמירה) | tient un perimetre de 4 cases autour de la Base ou d'un Centre | ignores | x1,2 | x1,0 |
| **Assaut** (הסתערות) | marche sur les Shofars, ignore ce qui ne le gene pas | x1,4 | x0,35 | x0,7 |
| **Chasse** (ציד) | fonce sur l'ennemi le plus proche, ou qu'il soit | ignores | x1,4 | x1,8 |

Changer de posture coute **15 Energie** sur un Tank deja pose ; c'est gratuit a la
creation. Ce prix n'est pas la pour freiner : il est la pour que la posture soit un
**engagement**. Gratuite, elle ne coutait rien a reconsiderer, et « ce Tank-la tient
la ligne quoi qu'il arrive » n'existait pas.

**Legitime defense** : un Tank en Assaut qui campe devant un Shofar abat d'abord ce
qui est deja a portee (bonus +1100), sauf s'il peut fermer le Shofar ce tour-ci.
Sans cette regle, il tapait la structure pendant qu'on le criblait dans le dos et
l'offensive etait mathematiquement intenable.

### 6.2 Designer une cible

Apres avoir choisi le role, le plateau passe en **mode selection** (`TargetPicker`) :
les cibles possibles sont marquees, un clic decide.

- **Garde** -> la Base ou un Centre de Commandement : ce sera son **point d'ancrage**.
- **Assaut** -> un Shofar : il ira a celui-la et pas a un autre.
- **Chasse** -> un ennemi precis : il le traque jusqu'a sa mort.

S'il n'y a qu'une cible possible, elle est prise d'office sans deranger le joueur.
Zero cible : rien ne s'ouvre. La consigne tombe d'elle-meme quand elle est accomplie
(le Shofar ferme, la proie morte).

### 6.3 L'ordre « rejoindre un Cristal »

Un Tank loin de tout Cristal ne peut pas evoluer. Au lieu d'une carte grisee sans
explication, l'ecran propose alors l'ordre d'**aller au Cristal le plus proche**. Le
Tank marche vers lui en ignorant sa posture, s'arrete au contact, continue de tirer
sur ce qui passe, et attend qu'on le fasse evoluer.

*Sources : `TurnManager.SelectTankTarget()`, `TargetPicker.cs`, `PawnController.cs`.*

---

## 7. La portee de construction

On ne batit pas n'importe ou. Une case est constructible si **l'une** de ces trois
conditions est vraie :

1. elle est a **4 cases ou moins du bord de la Base** ;
2. elle est dans le **rayon de commandement** d'un Centre debout (2 ou 3 cases) ;
3. un **Tank allie vivant** est a **1 case** d'elle.

A 4, tout ce qui n'est pas du desert est a portee des le premier tour. La regle ne
sert donc plus qu'a un seul endroit, et c'est le bon : **la terre qu'un Shofar tombe
vient de rendre** est au bord du plateau, a 6 cases de la Base. Pour y batir il faut
y envoyer un Tank ou y planter un Centre de Commandement.

> Gagner ouvre du terrain, mais ne te l'offre pas.

Le message de refus le dit : *« קרקע שנפתחה מעבר לטווח הבסיס »* — une terre qui s'est
ouverte au-dela de la portee de la Base.

**Le desert** est un cas a part : il n'a **aucune action et aucune evolution**,
jamais, nulle part. Le menu de case le dit explicitement (titre **מדבר**, phrase
*« אין כאן שום פעולה ואין שום התפתחות · לא בונים, לא מציבים טנק · רק עוברים »*), et le
bouton affiche **מדבר** au lieu d'un prix. Trois refus, trois mots differents, et
chacun se repare autrement : *מדבר* ne se repare pas, *hors de portee* se repare en y
allant.

`PORTAL_SHADOW_RADIUS` vaut **0** : l'ancienne regle d'ombre des Shofars est
desactivee, le glacis l'a rendue redondante. Le code est conserve.

*Source : `InteractionRules.CanBuildAt()`.*

---

## 8. Le Yetzer Hara

### 8.1 Les emissaires

| Rang | PV | Degats | Mobilite | Portee |
|---|---|---|---|---|
| 1 | 20 | 10 | 1 case | **1** |
| 2 | 40 | 15 | 2 cases | **1** |
| 3 | 80 | 30 | 3 cases | **1** |

> **Il tue toujours au contact.** Les rangs 2 et 3 tiraient autrefois a deux et trois
> cases : ils pilonnaient une usine sans jamais s'exposer, et rien ne pouvait les
> arreter avant qu'ils aient fait leur travail. Ce qui monte avec le rang, c'est la
> **vitesse**. Le joueur a donc toujours un tour pour agir, et **la distance
> redevient une defense**.

En facile et moyen, les PV et les degats sont reduits a 70 % et 85 %.

### 8.2 Ce qu'il vise

| Cible | Attrait de base | Remarque |
|---|---|---|
| **Usine de Gaz** | **980** | au-dessus de la Base : casser le revenu paie plus que frapper un mur |
| Base | 900 | objectif de victoire |
| Tank allie | 650 | engage en chemin |
| Cristal | 620 | |
| Bunker / Centre | 430 | seulement a 3 cases ou moins |

Modificateurs : **-35 par case** de distance, **+200** si deja a portee, **+250** si
le coup suffit a detruire, **+180** pour une usine de rang 2, et surtout
**-160 par defenseur** (chaque Tank a 2 cases, chaque Bunker a portee).

C'est ce dernier calcul qui fait choisir l'usine **mal couverte** plutot que la mieux
defendue. Une usine rang 2 sans defense passe avant la Base ; la meme derriere trois
Tanks ne vaut pas le detour.

*Source : `EnemyAI.SelectEnemyTarget()`.*

---

## 9. Les Shofars

Six, aux six pointes. **PV 50 au rang 1, 100 au rang 2.**

### 9.1 Le deploiement

Chaque Shofar deploie un ennemi tous les `PORTAL_SPAWN_INTERVAL` tours :

```
intervalle = (5 facile / 4 moyen / 3 difficile) - (revenu des usines / 60)
             borne a 2 minimum
```

Les six Shofars sont **decales** dans le cycle (decalage = leur index modulo
l'intervalle). Avec six Shofars et un intervalle de six, il en sort exactement un par
tour au lieu de trois d'un coup tous les trois tours. Le rythme moyen est identique ;
c'est la sensation qui change.

**Plafonds** : au plus 2 / **3** / 4 deploiements par tour toutes sources confondues,
et au plus 8 / **12** / 16 ennemis vivants simultanement.

**Niveau de l'ennemi deploye** :

| Tour | Rang 1 | Rang 2 | Rang 3 |
|---|---|---|---|
| < 7 | 100 % | — | — |
| 7 a 12 | 70 % | 30 % | — |
| >= 13 | 45 % | 40 % | 15 % |

Un Shofar passe au **rang 2** monte tout d'un cran, et a **10 %** de chance de monter
d'un cran de plus (mini-boss).

Le niveau est **tire a l'avance** et memorise, pour que l'apercu de menace montre
exactement ce qui sortira. Une prevision qui se trompe est pire que pas de prevision.

### 9.2 L'evolution

Un Shofar passe au rang 2 apres `20 / 16 / 12` tours de vie, **plus 3 tours par
rang** : ils montent donc echelonnes, pas tous le meme tour.

> L'evolution **augmente le reservoir, elle ne soigne pas**. Les degats deja encaisses
> sont reportes. Un Shofar presque abattu qui revenait a pleine vie effacait tout le
> travail du joueur.

### 9.3 Le bouclier et l'instabilite

**Tant que le bouclier tient, tous les degats structurels sont divises par 2**
(minimum 1). On ne ferme pas un Shofar en tapant dessus.

Chaque **ennemi issu de ce Shofar qui meurt** produit deux effets :

- **+1 fissure** sur son compteur d'instabilite ;
- **-12 PV de contrecoup**, qui **ignore le bouclier** (c'est une blessure interne)
  mais **ne peut pas le fermer** : il s'arrete a un plancher de 20 % des PV max.

A **3** (facile) ou **4** (moyen et difficile) fissures, **le bouclier saute pour
4 tours** :

- le Shofar **encaisse les degats en plein** ;
- il **ne deploie plus rien** ;
- le compteur de fissures repart a zero.

**C'est la fenetre d'assaut, et elle se merite en defendant.** Tenir la ligne devant
sa Base est ce qui ouvre la porte pour aller abattre la source.

### 9.4 Il se refait

> **Trois tours sans qu'un Tank le frappe, et il commence a se reconstruire :**
> **+8 PV par tour, et -1 fissure tous les 2 tours.**

Les PV reviennent d'abord, l'instabilite s'efface plus lentement : le joueur voit la
barre remonter avant de perdre ses fissures — il est prevenu avant d'etre puni.

Seul un **coup porte** remet ce compteur a zero. Le contrecoup des morts ne compte
pas. C'est ce qui empeche d'accumuler indefiniment de l'avance en defendant sans
jamais aller conclure.

### 9.5 Les vagues annoncees (surge)

A partir du **tour 6**, un Shofar tire au sort prepare une vague de **3 ennemis**
(1 + 2). Elle est **toujours annoncee un tour a l'avance** : log, bouffee de fumee
sur la case, encadre dans le panneau. Le joueur ne subit jamais une surprise qu'il ne
pouvait pas voir venir — il subit un **choix difficile**.

L'intervalle entre deux vagues :

```
5 + (4 facile / 2 moyen / 0 difficile) - (tour / 10), minimum 2
```

**Les vagues se rapprochent avec le temps.** Laisser le Yetzer Hara tranquille n'est
pas un abri, c'est un sursis : c'est ce qui empeche une partie purement defensive de
durer indefiniment derriere ses Bunkers.

Un Shofar dont le bouclier est tombe n'est jamais choisi : il ne deploie rien,
l'annoncer serait mensonger.

*Source : `PortalManager.cs`.*

---

## 10. אתכפיא et אתהפכא — les deux etapes du Tanya

C'est le coeur thematique du jeu, et sa seule mecanique qui ne soit pas empruntee au
genre.

### אתכפיא — soumettre

Resister au Yetzer, arreter ses emissaires, et faire tomber ses portes. C'est tout le
jeu decrit jusqu'ici. Un Shofar abattu laisse une **ruine**, definitivement en place.
La ruine se traverse.

### אתהפכא — retourner

**Sur la ruine d'un Shofar, on peut batir : 120 Energie.** La porte du mal devient
une source de production qui verse **35 par tour** — plus qu'une usine de gaz au rang
2 (30).

Quatre proprietes, chacune voulue :

1. **Production seule.** Un Shofar retourne ne tire pas, ne soigne pas, ne repousse
   rien. Il produit, un peu plus qu'un gaz rang 2, et c'est tout.
2. **Son energie ne nourrit pas le Yetzer.** L'acceleration du deploiement ne compte
   que les usines de Gaz. Cette energie-la lui a ete prise : elle ne le renforce pas.
3. **Il est indestructible**, et ce n'est pas un oubli : **ce qui a ete retourne ne se
   retourne pas en sens inverse**. On ne peut pas non plus marcher dessus — c'est un
   batiment.
4. **Ce n'est pas obligatoire.** 120 Energie, c'est le prix d'une offensive. Les
   depenser a retourner une ruine, c'est choisir de ne pas les depenser a fermer le
   Shofar suivant.

La ruine est a 6 cases de la Base, donc **hors de portee de construction** : il faut y
avoir un Tank ou un Centre de Commandement. Retourner un Shofar demande d'y etre
reste.

*Sources : `InteractionRules.TurnPortal()`, `IsFallenPortal()`, `IsTurnedPortal()`.*

---

## 11. La terre refleurit

> **Quand un Shofar tombe, sa part du plateau — le sixieme qui lui fait face —
> redevient ce qu'elle etait avant lui.**

C'est la seule recompense du jeu qui ne soit pas un chiffre. Fermer un Shofar ne fait
pas monter un compteur : **ca rend une terre**.

Chacun des six secteurs du glacis compte **11 a 13 cases**. Quand son Shofar tombe,
elles redeviennent :

| Ce qui repousse | Par secteur | Sur les six |
|---|---|---|
| Plaine (on peut y poser des Tanks) | 9 a 11 | 60 |
| **Colline avancee** (a 2 cases du Shofar voisin) | **1** | 6 |
| **Usine de Gaz** | **1** | 6 |

Les sites reviennent **vierges** : niveau 0, a construire, a 6 cases de la Base donc
hors de portee.

**Le premier Shofar abattu rend le deuxieme plus facile** : une usine de plus, une
colline en position de siege face au Shofar voisin, et des plaines pour poser les
Tanks de la suite. La fin de partie **s'accelere** au lieu de s'etaler.

Techniquement, la floraison est **sans etat** : rien n'est sauvegarde, tout se
recalcule depuis le plateau, et repasser l'operation ne defait rien. Elle ne touche
que les cases encore vierges (desert, niveau 0, sans pion).

*Sources : `LandBloom.cs`, `MapGenerator.GlacisSiteAt()`.*

---

## 12. Victoire, defaite, et les trois rangs

**Victoire** : plus aucun Shofar debout.
**Defaite** : la Base a 0 PV.

L'ecran de fin donne trois chiffres — tours tenus, Shofars fermes, ennemis abattus —
parce qu'une fin sans bilan n'apprend rien. « J'ai perdu » devient « j'ai perdu au
tour 14 avec deux Shofars encore ouverts », et c'est cette phrase-la qui donne envie
de rejouer.

**Mais gagner ne dit pas comment on a gagne.** Le rang final se lit sur le nombre de
Shofars **retournes** par rapport au nombre **tombes** :

| Retournes | Rang | Sens |
|---|---|---|
| tous (>= tombes) | **צדיק וטוב לו** | il ne reste plus rien a soumettre |
| au moins un | **צדיק ורע לו** | une part est devenue bien, une part reste du mal tenu |
| aucun | **בינוני** | *atkafya* pure : le Yetzer est soumis, il n'est pas transforme |

**Aucun des trois n'est une defaite.** Le Tanya fait du beinoni le niveau de tout
homme — ce n'est pas un echec, c'est la mesure.

*Source : `GameOverPanel.Show()`.*

---

## 13. Les enseignements

Chaque debut de tour presente **deux enseignements** tires de `teachings.json` :
12 textes, sources citees (Souka 52, Kidouchin 30, Messilat Yesharim, Berakhot 5,
Avot 2 et 4, Tanya chapitres 1, 12 et 26).

**Un tour sur deux**, si un des deux textes en porte une, une **question** est posee.

> **Bonne reponse = une fissure de plus** sur le Shofar le plus fissure parmi ceux
> dont le bouclier n'est pas deja tombe. **Pas d'Energie.**

C'est voulu : **comprendre le Yetzer Hara l'ebranle exactement comme abattre ses
emissaires**, et ca remplit la meme jauge. Il n'y a pas deux facons de briser un
bouclier. Une mauvaise reponse ne punit pas.

Cela remplace l'ancienne question de Trivia, qui etait un **peage** : il fallait payer
pour avoir le droit de jouer, chaque tour, et l'Energie versee ne se trouvait nulle
part sur la carte.

**Consignes de jeu** : les deux premiers tours, toutes ; des tours 3 a 8, une seule
qui tourne ; au-dela, plus rien.

*Sources : `TeachingManager.cs`, `TeachingPanel.cs`, `Resources/teachings.json`.*

---

## 14. Difficulte

Trois niveaux, choisis au menu principal, memorises dans `PlayerPrefs`. Par defaut
**moyen**. **Difficile = l'equilibrage d'origine** (tous les pourcentages a 100).

| Levier | Facile | Moyen | Difficile |
|---|---|---|---|
| PV des ennemis | 70 % | 85 % | 100 % |
| Degats des ennemis | 70 % | 85 % | 100 % |
| Energie de depart | 110 | 90 | 70 |
| Plancher de la Base par tour | 15 | 12 | 10 |
| Evolution des Shofars au tour | 20 | 16 | 12 |
| Intervalle de deploiement | 5 | 4 | 3 |
| Deploiements par tour (max) | 2 | 3 | 4 |
| Ennemis simultanes (max) | 8 | 12 | 16 |
| Fissures pour rompre un bouclier | 3 | 4 | 4 |
| Espacement des vagues | +4 | +2 | +0 |

Rien d'autre ne depend de la difficulte.

*Source : `GameDifficulty.cs`.*

---

## 15. L'interface

Tout l'hebreu vient de `Assets/Resources/hud_labels.json`. **Aucun fichier `.cs` ne
contient un seul octet non-ASCII.**

### Permanent

- **Bandeau du haut** : Energie (qui monte chiffre par chiffre), numero de tour, rail
  des 5 phases, 6 losanges pour les Shofars, jauge de PV de la Base.
- **Legende** en bas a gauche : les 10 pictogrammes du jeu, dessines par le code.

### A la demande

- **Menu de case** (clic) : voir de pres / details / utiliser l'energie. Le prix est
  sur le bouton ; s'il est refuse, le bouton dit **pourquoi** (trop cher, hors de
  portee, desert).
- **Panneau de details** : photo de l'element, les trois evolutions en vignettes,
  jusqu'a trois lignes chiffrees (tirs, portee, soin, revenu...).
- **Ecran de choix d'un Tank** : les trois postures cote a cote avec leur role et leur
  prix, plus une quatrieme carte (evolution, ou « rejoindre un Cristal »).
- **Conseiller** : jusqu'a 3 cartes en bas a droite pendant la depense — quoi faire,
  pourquoi maintenant, combien. Au survol, un Tank fantome joue l'action sur le
  plateau et la camera va la cadrer. Neuf raisons possibles, de « le bouclier est
  tombe » a « ton revenu stagne ».
- **Vision du Yetzer Hara** (Tab) : les fantomes de ce qui va arriver.
- **Vue depuis le tank** : camera a la troisieme personne derriere une unite.
- **Panneau d'un Shofar** (survol) : PV, 4 segments d'instabilite, etat du bouclier,
  tours avant evolution.

### Sur le plateau

- **Pastille de posture** au-dessus de chaque Tank — un pictogramme, pas un mot
  (illisible en vue orthographique) : bouclier = Garde, hexagone perce = Assaut,
  triangle inverse = Chasse.
- **Bulle de bouclier** orange au-dessus de chaque Shofar intact, qui faiblit a
  mesure des fissures ; 4 pastilles au-dessus pour les compter ; pastilles rouges
  clignotantes quand le bouclier est rompu, dont le nombre = les tours restants.
- **Cordon electrique** reliant chaque ennemi au Shofar qui l'a envoye. A sa mort,
  une decharge remonte le cordon et le Shofar encaisse **visiblement**.
- **Chiffres de degats** qui s'envolent, **traits de tir** des Bunkers, **ombre de
  contact** et **anneau de socle** pour detacher les pions du decor.

### Presentation d'ouverture

A chaque **nouvelle partie** (jamais a un chargement), une visite de **12 etapes** :
la camera se pose sur chaque type de case et explique ce qu'on peut y batir et ce que
ca donne, avec les **vrais chiffres** des regles injectes dans le texte. Puis un
treizieme ecran donne **huit lignes de strategie**. Echap saute la visite mais pas
l'ecran de strategie.

*Sources : `HudController.cs`, `HexActionMenu.cs`, `HexTooltipController.cs`,
`AdvisorPanel.cs`, `IntroTour.cs`, `PortalStatusVisual.cs`, `PortalTether.cs`.*

---

## 16. Sauvegarde

**Automatique, deux fois par tour** : a l'ouverture de la phase de depense (rien ne
bouge, le revenu est verse, la main est au joueur) et au debut de la phase des Tanks
(tout ce qui vient d'etre achete est sur le plateau).

- **6 emplacements** maximum, tries du plus recent au plus ancien.
- Un fichier JSON par partie dans `Application.persistentDataPath/parties/`.
- **Repli PlayerPrefs** si le disque refuse.
- **La partie achevee disparait** : le menu ne propose que des parties en cours.

Le fichier contient : le tour, la difficulte, l'Energie, les PV de la Base, les 169
cases (type, niveau, PV, tours vecus), les pions (position, niveau, PV, posture,
ordre en cours, proie, Shofar d'origine), l'etat des six Shofars (fissures, tours de
bouclier, tours de calme), les compteurs, et la vague annoncee.

**Reprendre ne rejoue pas le debut du tour** : ni le revenu, ni l'enseignement. On
rentre directement dans la phase de depense, qui est exactement l'instant sauvegarde.
Sinon il aurait suffi de recharger pour s'enrichir.

Le stockage passe par une interface **`ISaveStore`** a trois methodes
(`ReadAll` / `Write` / `Delete`). **C'est le point unique de bascule** pour brancher
le site qnigame en WebGL : une classe a ecrire, une ligne a changer.

*Sources : `SaveManager.cs`, `SaveGameData.cs`, `LoadGamePanel.cs`.*

---

## 17. Etat du code

### Architecture

| Espace de noms | Contenu |
|---|---|
| global | `PawnController`, `Hexagon`, `HexCoord`, `TypeOfHex`, `TypeOfPawn`, `HexagonData`, `PrefabsPath`, `EnemyAI`, `TriviaManager` |
| `MNLTHII` | `BoardController`, `GameManager` |
| `MNLTHII.Rules` | `InteractionRules`, `AdvisorRules`, `GameDifficulty` |
| `MNLTHII.Managers` | tous les gestionnaires **et tous les panneaux d'interface** |
| `MNLTHII.UI` | `IconLibrary`, `HudLabelsRuntime`, `TextFeatures`, `StanceStyle`, `DamagePopup`, `StanceMarker`, `ElementPhotos` |
| `MNLTHII.Data` | `SaveGameData`, `HexCoord`, `TurnData` |
| `MNLTHII.Factories`, `MNLTHII.EditorTools` | fabriques et outils d'editeur |

**102 fichiers, environ 34 000 lignes.**

### Regles de code non negociables

1. **Zero octet non-ASCII dans un `.cs`.** Tout l'hebreu vit dans
   `Resources/hud_labels.json` et `teachings.json`. Une chaine hebraique dans un
   fichier source casse la compilation sur certaines configurations d'encodage.
2. **Aucune allocation dans `Update`.** Pas de LINQ, pas de concatenation de chaines,
   pas de `new WaitForSeconds` dans une coroutine repetee, pas de closure. Les
   composants sont resolus une fois dans `Awake`, les tampons sont des champs
   reutilises, les `TextMeshPro` sont ecrits avec `SetText("{0}", v)`.
3. **Cible WebGL** : pas de post-effet plein ecran, pas de shader introuvable dans le
   build, textures et objets poolés.
4. **Une seule source de verite par regle.** `InteractionRules` porte tous les
   chiffres ; `IsHexWalkable` est la seule definition de la praticabilite ; le calcul
   de cible de l'apercu de menace est litteralement celui de la phase ennemie.
5. **Interface construite en code quand elle doit pouvoir changer** sans reconstruire
   le HUD : bandeaux de phase, selection de cible, pastilles de posture, panneau de
   chargement, visite d'ouverture, chiffres volants.

### Ce qui n'est pas branche

- **Le systeme de Trivia** (`TriviaManager`, `SubjectChoicePanel`,
  `TriviaPanelController`, 48 questions reparties sur 4 sujets et 4 paliers) est
  **fonctionnel mais dormant** : plus aucun appelant depuis la boucle de jeu. Les
  enseignements l'ont remplace. Le code et les recompenses (+25 / 35 / 50 / 85) sont
  conserves.
- **`QniGameSaveStore`** n'existe pas encore. Seul `LocalSaveStore` implemente
  `ISaveStore`.
- **`LaserFX`** est remplace par `ShotTracer` (son shader n'etait pas inclus dans le
  build, l'effet etait invisible).
- **Rien n'a ete teste dans Unity.** Tout le code de cette version a ete ecrit et
  verifie hors editeur — compilation contre des stubs, modeles Python pour la
  geometrie et l'equilibrage. Les erreurs de console restent a remonter.

---

## 18. Annexe — bareme complet

Valeurs en difficulte **moyenne** sauf mention.

### Economie

```
Energie de depart                  90
Plancher de la Base / tour         12
Usine de Gaz rang 1        cout 30   revenu +15
Usine de Gaz rang 2        cout 45   revenu +30
Shofar retourne            cout 120  revenu +35
Acceleration du Yetzer     1 tour de moins par 60 de revenu d'usines
Plancher d'intervalle      2 tours
```

### Couts militaires

```
Tank                       40
Posture (Tank pose)        15
Evolution d'un Tank        60   (uniquement au contact d'un Cristal)
Bunker rang 1 / 2          60 / 80
Cristal rang 1 / 2         60 / 90
Centre de Com. rang 1 / 2  50 / 75
Tir de Bunker rang 1 / 2   4 / 5
```

### Points de vie

```
Base                       200   (reservoir unique pour 7 cases)
Bunker rang 1 / 2          40 / 80
Usine de Gaz rang 1 / 2    40 / 70
Cristal rang 1 / 2         35 / 60
Centre de Com. rang 1 / 2  30 / 50
Shofar rang 1 / 2          50 / 100
Tank rang 1 / 2            30 / 60
Ennemi rang 1 / 2 / 3      20 / 40 / 80
```

### Combat

```
Tank rang 1                10 degats, portee 1, mobilite 1
Tank rang 2                20 degats, portee 2, mobilite 2
Ennemi rang 1 / 2 / 3      10 / 15 / 30 degats, portee 1, mobilite 1 / 2 / 3
Bunker rang 1              10 degats x 4 tirs, portee 2
Bunker rang 2              15 degats x 6 tirs, portee 2
Contre-attaque             automatique si le defenseur survit et l'attaquant
                           est a sa portee
Hasard                     aucun (ni critique, ni esquive)
```

### Usure

```
Bunker rang 1 / 2          -8 / -11 PV par tour, plus 2 par tir
Centre de Commandement     -5 PV par tour
```

### Soutien

```
Cristal rang 1 / 2         soin 10 / 20, portee 1 / 2, +20 PV max (sans cumul)
Centre rang 1 / 2          repulsion 1 / 2, commandement 2 / 3, +1 case de mouvement
```

### Shofars

```
Nombre                     6
Bouclier                   degats divises par 2 (minimum 1)
Fissures pour le rompre    4    (3 en facile)
Duree du bouclier rompu    4 tours   (ni degats reduits, ni deploiement)
Contrecoup par mort        -12 PV, plancher a 20 % des PV max
Calme avant reconstruction 3 tours sans coup porte
Reconstruction             +8 PV / tour, -1 fissure tous les 2 tours
Evolution                  tour 16, plus 3 tours par rang
Vague annoncee             +2 ennemis, a partir du tour 6
Intervalle entre vagues    5 + 2 - (tour / 10), minimum 2
Chance de mini-boss        10 % (Shofar rang 2 uniquement)
```

### Portees

```
Construction depuis la Base    4 cases
Construction depuis un Tank    1 case
Construction sous commandement 2 ou 3 cases
Bunker                         2 cases
Perimetre d'un Tank en Garde   4 cases
Ombre d'un Shofar              0 (desactivee)
```

### Plateau

```
Rayon                      7      (169 cases)
Base                       7 cases au centre
Premier anneau du glacis   6      (72 cases de desert, 43 % du plateau)
Distance Base -> Shofar    6 cases
```

---

*Document genere depuis le code le 24 septembre 2026. Les chiffres de ce document ont
ete extraits automatiquement des constantes et verifies un a un.*
