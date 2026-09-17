# Milchemet HaYetzer - Deroule d'une partie

Document de reference pour attaquer l'UI. Tous les chiffres viennent du code reel
(`InteractionRules.cs`), pas d'une intention. Chaque section dit **ce que le joueur
fait**, **ce qui se passe**, et **ce qu'il doit voir a l'ecran** - c'est cette
derniere colonne qui donnera la liste des ecrans a construire.

---

## 1. En une phrase

Six Portails aux six pointes du plateau deversent le Yetzer Hara vers ta Base.
Tu reponds a une question par tour pour financer ta defense, et tu dois fermer
les six Portails avant que ta Base (200 PV) ne tombe. Une partie dure **20 a 30
tours**.

**Victoire** : les 6 Portails detruits.
**Defaite** : la Base a 0 PV.

---

## 2. L'anatomie d'un tour

Un tour, c'est cinq moments. Trois t'appartiennent, deux se jouent devant toi.

### Phase 1 - REVENU (automatique, puis une question)

Ce qui se passe :

- La Base verse **+12 Energie**.
- Chaque Cristal actif verse **+5** (Niv2) ou **+10** (Niv3).
- Une question s'ouvre : Halacha, Tanakh, Musar ou Tefillah. Bonne reponse
  **+25 Energie**, mauvaise reponse ou temps ecoule : rien.

Le plateau est verrouille pendant la question.

**Ce que le joueur doit voir** : la question, sa categorie, le chrono, et - au
moment ou elle se referme - **combien il vient de gagner et quel est son nouveau
solde**. Aujourd'hui il ne voit ni l'un ni l'autre.

### Phase 2 - DEPENSE (a toi, sans limite de temps)

Tu cliques les hexagones. Chaque clic est une action facturee immediatement.
Tu en fais autant que ton solde le permet. Rien ne se passe tant que tu n'as pas
termine.

| Ce que tu cliques | Effet | Cout |
|---|---|---|
| Plaine ou desert libre | cree un Tank | **50** |
| Colline | Bunker, puis Forteresse | **40** puis **60** |
| Gaz | soigne les allies proches | **30** puis **45** |
| Cristal | +20 PV max aux allies proches, et revenu | **60** puis **90** |
| Montagne | repousse les ennemis | **50** puis **75** |
| Un de tes Tanks, avec un Cristal a portee | le fait evoluer Niv2 | **60** |
| Un de tes Tanks, sans Cristal ou deja Niv2 | change sa posture | **gratuit** |

Tu termines avec **Espace**, **Entree**, ou un bouton.

**Ce que le joueur doit voir** : son solde en permanence, **le prix de ce qu'il
survole avant de cliquer**, et ce qu'il n'a pas les moyens de payer. Aujourd'hui
il n'a aucune de ces trois informations - il clique a l'aveugle.

### Phase 3 - TANKS (automatique, tu regardes)

Chaque Tank agit seul, selon la posture que tu lui as donnee.

- **Garde** (bleu) : ne depasse jamais 4 cases de la Base, ignore les Portails.
  S'il n'a rien a faire, il rentre se poster.
- **Assaut** (orange) : marche sur les Portails. Il se defend si on l'attaque a
  portee, mais ne se laisse pas rappeler par ce qui se passe derriere lui.
- **Chasse** (vert) : l'ennemi le plus proche, ou qu'il soit.

Un Tank Niv1 avance d'une case et frappe a une case. Un Tank Niv2 avance de
**deux** cases et frappe a **deux** cases.

**Ce que le joueur doit voir** : quelle unite joue, quelle cible elle a choisie,
et pourquoi. Le projecteur qui suit l'unite existe deja ; le "pourquoi" manque.

### Phase 4 - YETZER HARA (automatique)

Chaque ennemi vise, dans l'ordre de son score : la **Base** (son objectif), un
**Tank** rencontre en chemin, ou un **batiment** qui le gene a 3 cases ou moins.

### Phase 5 - FIN DE TOUR (automatique)

Dans cet ordre :

1. **Bunkers** : chacun paie son entretien (**3** Energie, **5** au Niv3), puis
   tire sur jusqu'a 4 cibles a portee 2, 10 degats chacune.
2. **Gaz** : soigne les Tanks a portee.
3. **Cristaux** : recalculent le bonus de PV max.
4. **Montagnes** : perdent 10 PV ; a 0 elles s'effondrent.
5. **Portails** : deploiement, instabilite, annonce de la prochaine vague.
6. Verification victoire / defaite.

**Ce que le joueur doit voir** : un recap de fin de tour. Combien de tirs, combien
de morts, combien d'ennemis sont sortis et ou. Aujourd'hui tout cela n'existe que
dans la console.

---

## 3. Le coeur du jeu : l'instabilite des Portails

C'est le mecanisme dont tout depend, et c'est **le seul qui soit aujourd'hui
totalement invisible a l'ecran**. Si on ne montre qu'une chose, c'est celle-la.

Chaque ennemi tue inflige **12 PV a son Portail d'origine**, ou qu'il meure sur la
carte. Mais ce contrecoup ne descend jamais sous **20 % des PV max** : il use la
source, il ne la ferme jamais.

Pour un Portail Niveau 1 (50 PV, plancher 10) :

| Morts | PV du Portail | Etat |
|---|---|---|
| 0 | 50 / 50 | blinde - un Tank Niv1 ne place que 5 degats |
| 1 | 38 | blinde |
| 2 | 26 | blinde |
| 3 | 14 | blinde |
| 4 | **10** (plancher) | **BOUCLIER ROMPU, 4 tours** - il cesse de deployer |

Bouclier rompu = degats en plein. Un seul Tank Niv1 adjacent (10 degats) **ferme
le Portail d'un coup**.

Ce que cela veut dire concretement : **tenir la ligne devant sa Base prepare
l'assaut, mais ne le remplace pas.** Il faut avoir envoye un Tank la-bas a
l'avance, parce qu'un Portail est a 7 cases et qu'un Tank Niv1 avance d'une case
par tour. Six tours de marche. Tu dois decider au tour 4 ce que tu veux pouvoir
faire au tour 10.

**L'horloge** : a partir du **tour 18**, les Portails passent au Niveau 2, l'un
apres l'autre (18, 21, 24, 27, 30, 33). Un Portail Niv2 a 100 PV, un plancher de
20, et deploie des ennemis d'un cran superieur. Quatre morts ne l'amenent plus
qu'a 64 PV : il faut alors sept coups de Tank Niv1 au lieu d'un seul.

**Tout se joue donc avant le tour 18.** C'est la tension centrale de la partie, et
elle n'est aujourd'hui lisible nulle part.

---

## 4. Le deroule reel, tour par tour

### Acte I - L'installation (tours 1 a 6)

**Tour 1.** Tu demarres avec **60 Energie**. Revenu : +12, +25 si tu reponds bien.
Solde : **97**. Trois Tanks Niv1 sont deja poses autour de ta Base, en posture
Garde. La carte est vide d'ennemis.

Tes vrais choix, ce tour-ci :

- un **Bunker** (40) plus une **usine a gaz** (30) : le socle defensif, 27 restants ;
- un **Cristal** (60) : rien pour l'instant, mais +5 par tour et la condition pour
  faire evoluer tes Tanks plus tard ;
- un **Tank** (50) et tu l'envoies en Assaut tout de suite : il arrivera au
  Portail vers le tour 7.

Un tour de revenu parfait rapporte 37, soit moins qu'un Tank. Tu ne peux donc
jamais tout faire : c'est voulu.

**Tours 2 et 3.** Toujours aucun ennemi. Tu accumules, tu poses, tu decides quelles
postures donner. **A la fin du tour 3**, trois Portails sur six deploient chacun un
ennemi Niveau 1 - le plafond est de 3 deploiements par tour, et les six Portails
sont synchronises sur le meme rythme d'un tour sur trois.

**Tours 4 a 6.** Les trois ennemis marchent vers ta Base, une case par tour. Tes
Tanks en Garde ne bougent pas tant que l'ennemi n'est pas a 4 cases de la Base.
Fin du tour 6 : trois nouveaux ennemis, et **la premiere vague annoncee devient
possible** - un Portail est designe, et il sortira 3 ennemis au lieu d'un.

### Acte II - Le contact (tours 7 a 15)

**Tours 7 a 9.** Premiers combats. Un Tank Niv1 fait 10 degats, un ennemi Niv1 a
20 PV : deux tours par kill. Un Bunker, lui, fait 4 tirs de 10 degats par tour -
il tue deux ennemis par tour a lui seul. C'est pour ca qu'il coute 40 **et** 3 par
tour.

Chaque mort envoie son contrecoup sur le Portail qui l'a envoyee. Les compteurs
montent, mais eparpilles sur six Portails.

**Tour 10 environ.** Ton Tank parti en Assaut au tour 1 arrive enfin a son Portail.
Bouclier intact : il ne place que 5 degats par tour. C'est lent, et c'est normal -
il est la pour etre en position quand le bouclier tombera.

**Tours 10 a 15.** Le rythme s'installe : environ un ennemi par tour en moyenne,
des vagues annoncees de trois toutes les cinq tours, et une composition qui se
durcit - a partir du tour 7 des ennemis Niv2 apparaissent (40 PV, 15 degats,
portee 2), a partir du tour 13 des Niv3 (80 PV, 30 degats, portee 3).

Un Niv3 detruit un Tank Niv1 **d'un seul coup**. C'est le moment ou tes Tanks Niv1
cessent de suffire et ou l'evolution (60 Energie, Cristal a portee) devient le bon
investissement.

**Le premier Portail ferme** tombe quelque part ici : quatre morts tracees sur lui,
bouclier rompu, ton Tank d'Assaut en position. Un coup, et il n'en reste que cinq.

### Acte III - La course (tours 16 a 30)

**Tour 18 : le premier Portail passe Niveau 2.** Puis un autre tous les trois
tours. Chaque Portail qui durcit avant que tu ne l'atteignes te coute plusieurs
tours de plus.

A partir de la, deux courses en parallele :

- la tienne : fermer les Portails restants avant qu'ils ne durcissent ;
- la sienne : user tes 200 PV de Base, avec des vagues qui se resserrent (les
  annonces passent de toutes les 5 turns a toutes les 2 avec le temps).

**Une victoire tombe entre le tour 17 et le tour 29** selon la ligne jouee. Une
defaite arrive plutot vers le tour 21 (rush trop nu) ou le tour 35 (economie trop
lente).

### Les quatre lignes viables

Mesurees sur 600 parties simulees par strategie :

| Ligne | Ce que tu fais | Victoire | Duree |
|---|---|---|---|
| **Rush** | tout en Tanks d'Assaut, zero defense | 45 % | 17 tours |
| **Economie** | Cristaux d'abord, armee ensuite | 42 % | 28 tours |
| **Equilibree** | 3 Bunkers, 1 Cristal, puis tout en Assaut | 59 % | 29 tours |
| **Adaptative** | mur jusqu'au tour 8, puis bascule en Assaut | 59 % | 27 tours |
| **Tortue pure** | que des fortifications | **0 %** | perd vers 50 |

La tortue ne peut pas gagner : le plancher de contrecoup interdit de fermer un
Portail sans y aller. C'est voulu, et c'est ce qui force tout le monde a attaquer
un jour.

---

## 5. Ce que l'UI doit montrer

Derive directement de ce qui precede. Classe par ce qui manque le plus.

### Priorite 1 - Sans ca, le jeu est injouable

| Element | Pourquoi | Etat |
|---|---|---|
| **Solde d'Energie**, permanent | c'est la ressource de toute la partie | champ existe, **non assigne** |
| **Prix au survol** d'un hexagone | il clique a l'aveugle, il ne connait aucun cout | **inexistant** |
| **Phase en cours** + "Espace pour finir" | rien ne dit que la main est a lui | champ existe, **non assigne** |
| **Portails restants (x/6)** | c'est la condition de victoire | **inexistant** |
| **PV de la Base**, une jauge unique | c'est la condition de defaite ; aujourd'hui c'est 7 petites barres | a refaire |

### Priorite 2 - Sans ca, la strategie est invisible

| Element | Pourquoi |
|---|---|
| **Etat d'un Portail** : PV, morts tracees (x/4), bouclier intact ou rompu, tours restants | c'est LE mecanisme central, et il est totalement invisible |
| **Posture d'un Tank** : un badge lisible, pas seulement une teinte | le seul levier tactique du joueur |
| **Vague annoncee** : un marqueur franc sur le Portail concerne | l'imprevu doit etre lisible, sinon c'est de l'arbitraire |
| **Gain de revenu** en debut de tour : "+37" qui monte a l'ecran | il ne sait pas ce que sa bonne reponse lui a rapporte |

### Priorite 3 - Le confort et la beaute

| Element | Pourquoi |
|---|---|
| **Recap de fin de tour** : tirs, morts, deploiements | tout est dans la console aujourd'hui |
| **Portee d'un batiment** au survol | les auras existent mais ne disent pas "portee 2" |
| **Bouton de fin de tour** visible | Espace marche, mais rien ne l'annonce |
| **Panneau d'unite** au survol : PV, degats, portee, posture | les panneaux existent, a rebrancher |
| **Ecran de victoire / defaite** | il y a un StateOfGame, pas d'ecran |

---

## 6. Deux points a trancher

**1. L'entretien des Bunkers se paie meme sans cible.**
Aujourd'hui un Bunker preleve ses 3 Energie en fin de tour meme si aucun ennemi
n'est a portee - donc des les tours 1 a 3, ou la carte est vide. Est-ce un cout de
poste permanent (et alors c'est coherent : le mur coute cher a tenir), ou un cout
de tir (et alors il ne devrait rien prelever quand il ne tire pas) ? Le second est
plus doux et plus intuitif ; le premier est ce qui empeche le mur permanent.
Je n'ai rien change sans ton avis.

**2. Le rythme de deploiement est plus lent qu'il n'y parait.**
Les six Portails sont synchronises et le plafond est de 3 par tour : dans les
faits il sort **3 ennemis tous les 3 tours**, soit 1 par tour en moyenne, et non 2.
Si tu trouves le debut de partie mou, c'est le levier a bouger - mais la
simulation dit que l'equilibre actuel tient, donc a ne toucher qu'apres t'etre fait
un avis manette en main.

---

## 7. La suite

Envoie les captures. Avec elles on pourra decider :

- ou vit le HUD permanent (solde, tour, Portails, Base) ;
- a quoi ressemble l'infobulle de cout au survol ;
- comment on affiche l'etat d'un Portail sans encombrer le plateau ;
- quel style visuel unifie la modale de Trivia, deja en Reach UI, avec le reste.
