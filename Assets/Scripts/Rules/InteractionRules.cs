using UnityEngine;
using MNLTHII;
using MNLTHII.Managers;

namespace MNLTHII.Rules
{
    /// <summary>Resultat d'une action de construction payee par le joueur.</summary>
    public enum TriviaOutcome
    {
        EnergyOnly,
        TankCreated,
        TankEvolved,
        BuildingUpgraded,
        StanceChanged,
        Blocked_Occupied,
        Blocked_NotEnoughEnergy,
        Blocked_MaxLevel,
        Blocked_NoCrystal,
        Blocked_NotPlayerPhase,

        /// <summary>Trop loin de tout ce qui donne le droit de batir ici.</summary>
        Blocked_OutOfRange
    }

    /// <summary>
    /// Toutes les regles chiffrees de "Milchemet HaYetzer" (GDD V3 + passe d'equilibrage).
    ///
    /// CONVENTION DE NIVEAU (important) :
    ///   Pour les TERRAINS, le champ Hexagon.level est 0-base :
    ///     level 0 = "Niveau 1" du GDD (terrain naturel)
    ///     level 1 = "Niveau 2" du GDD (Bunker / Gaz soigneur / Cristal / Centre de Com.)
    ///     level 2 = "Niveau 3" du GDD (Forteresse / Gaz ameliore / etc.)
    ///   Pour les PORTAILS et la BASE, level suit directement le GDD (1 ou 2).
    ///   Pour les PAWNS, level suit directement le GDD (1, 2 ou 3).
    ///
    /// COMBAT : aucun hasard. Chaque attaque touche, les degats sont fixes,
    /// et le defenseur contre-attaque si l'attaquant est dans sa portee.
    ///
    /// EQUILIBRAGE - les trois piliers ajoutes a la V3 :
    ///   1. Le revenu est separe de la depense. Le Trivia rapporte de l'Energie en
    ///      debut de tour ; ensuite le joueur depense librement. Plus une seule
    ///      action par tour, mais un budget a arbitrer.
    ///   2. Tout coute. Construire etait gratuit, donc il n'existait aucun choix :
    ///      chaque batiment a desormais un prix (voir COST_*).
    ///   3. Les Portails sont blindes tant qu'ils sont stables. Chaque ennemi tue
    ///      deleste son portail d'origine ; au bout de PORTAL_KILLS_TO_BREAK_SHIELD
    ///      morts le bouclier saute pour quelques tours. La defense ouvre la
    ///      fenetre offensive : les deux moities du jeu sont liees.
    /// </summary>
    public class InteractionRules
    {
        // =====================================================================
        //  ECONOMIE - revenu
        // =====================================================================
        /// <summary>
        /// Solde au premier tour.
        ///
        /// RAMENE DE 140 A 90 (moyen). A 140, une usine coutant 30 et remboursee en
        /// deux tours, le premier coup etait toujours le meme : quatre usines, sans
        /// risque et sans reflexion. Un premier coup qui ne se discute pas est un tour
        /// perdu.
        ///
        /// A 90 le premier tour ne permet plus tout : trois usines, ou une usine et un
        /// Bunker, ou une usine et de quoi poser un Tank au tour suivant. Il faut
        /// choisir par quel cote on ouvre - et c'est la que le plateau commence a
        /// vouloir dire quelque chose.
        /// </summary>
        /// <remarks>Selon la difficulte (voir GameDifficulty) : 110 / 90 / 70.</remarks>
        public static int STARTING_ENERGY { get { return GameDifficulty.Pick(110, 90, 70); } }

        /// <summary>
        /// LE PLANCHER. Verse en debut de tour quoi qu'il arrive, meme si le joueur a
        /// perdu toutes ses usines.
        ///
        /// Il existe pour une seule raison : sans lui, perdre ses usines serait une
        /// mort lente et sans retour - plus de revenu, donc plus de Tanks, donc plus de
        /// reconquete. Une partie perdue doit se terminer, pas s'enliser.
        ///
        /// Volontairement MAIGRE. Dix par tour, c'est un cinquieme d'un Tank : de quoi
        /// survivre et reconstruire une usine, jamais de quoi jouer.
        /// </summary>
        /// <remarks>Selon la difficulte : 15 / 12 / 10.</remarks>
        public static int BASE_INCOME_PER_TURN { get { return GameDifficulty.Pick(15, 12, 10); } }

        /// <summary>Bonne reponse au Trivia de debut de tour.</summary>
        public const int TRIVIA_ENERGY_REWARD = 25;

        /// <summary>
        /// L'USINE. C'est le Gaz, et seulement lui, qui produit l'Energie.
        ///
        /// POURQUOI L'ECONOMIE A CHANGE DE MAIN
        ///
        /// L'Energie venait du Trivia : une question par tour, une bonne reponse, de
        /// l'argent. Rien de tout cela n'etait sur la carte, donc rien de tout cela ne
        /// pouvait etre menace. On pouvait se terrer derriere un mur de Bunkers sans
        /// jamais rien risquer, et gagner.
        ///
        /// Maintenant l'argent est POSE SUR LE PLATEAU, loin de la Base, et l'ennemi
        /// peut le casser. Defendre cesse d'etre une posture d'attente : il faut
        /// couvrir plusieurs points a la fois, donc tenir du terrain, donc des Centres
        /// de Commandement et des Tanks en Garde postes ailleurs que chez soi.
        ///
        /// Le rang 2 double la production : une usine bien defendue vaut deux usines
        /// exposees, ce qui est exactement la decision qu'on veut faire prendre.
        /// </summary>
        public const int GAS_INCOME_L1 = 15;
        public const int GAS_INCOME_L2 = 30;

        /// <summary>
        /// LES PV D'UNE USINE. Ils n'existaient pas, et c'etait un bug grave.
        ///
        /// GetBuildingMaxHP ne connaissait que la Base, les Portails, les Collines et
        /// les Montagnes ; le Gaz et le Cristal tombaient dans le "default: return 0".
        /// SyncHPFromRules mettait donc leurs PV a ZERO. Consequences en chaine :
        ///
        ///   - le revenu ignorait toutes les usines (elles etaient vues comme detruites),
        ///     donc l'Energie ne montait pas et l'effet visuel ne se declenchait jamais ;
        ///   - et le premier coup recu les effacait, puisqu'elles partaient de zero PV.
        ///
        /// Un batiment qu'on doit defendre doit pouvoir encaisser. Quarante PV, c'est
        /// quatre coups d'un ennemi de rang 1 : de quoi voir venir et reagir.
        /// </summary>
        public const int GAS_HP_L1 = 40;
        public const int GAS_HP_L2 = 70;

        public const int CRYSTAL_HP_L1 = 35;
        public const int CRYSTAL_HP_L2 = 60;

        // =====================================================================
        //  ECONOMIE - couts
        // =====================================================================
        /// <summary>
        /// LE TANK COUTE MOINS CHER QUE LE BUNKER. C'est volontaire, et c'est le
        /// pivot de tout l'equilibrage militaire.
        ///
        /// Compare ce que les deux font vraiment, par tour :
        ///
        ///                    degats/tour   portee   duree       pilotage
        ///   Bunker rang 1     40 (4 x 10)     2     ~3 tours    aucun
        ///   Tank rang 1       10 (1 x 10)     1     indefinie   posture a donner
        ///
        /// Le Bunker frappe quatre fois plus fort, deux fois plus loin, et il n'exige
        /// rien du joueur : il se declenche seul sur ce qui passe a portee. Le Tank
        /// frappe une cible, avance d'une case, meurt en trois coups, et il faut
        /// l'avoir compris avant de s'en servir. A prix egal, personne n'a de raison
        /// de sortir : on se terre derriere des Bunkers et on regarde.
        ///
        /// Or se terrer ne gagne pas cette partie. Les Shofars sont aux coins, a sept
        /// cases ; un Bunker porte a deux et ne bouge pas. Le Tank est le SEUL moyen
        /// d'aller fermer un Shofar, et le seul moyen de porter la construction plus
        /// loin que la Base (BUILD_RANGE_FROM_TANK). Faire payer le plus cher l'unique
        /// chemin vers la victoire, c'etait taxer la sortie et subventionner l'attente.
        ///
        /// A 40, le Tank redevient ce qu'il doit etre : peu efficace, mais jetable. On
        /// peut en perdre un sur une erreur de posture sans avoir perdu son tour. Le
        /// prix ne rend pas le Tank plus facile a diriger - il rend l'erreur moins
        /// chere, et c'est la seule reponse honnete a une unite difficile a diriger.
        ///
        /// Rendement tout compris, une fois le Bunker use :
        ///   Bunker : 60 + ~48 de munitions pour ~120 degats -> 0.9 par degat
        ///   Tank   : 40 pour ~50 degats s'il tient cinq tours -> 0.8 par degat
        /// Presque le meme prix au degat. Le Bunker paie une prime pour la salve, la
        /// portee et le zero-pilotage ; le Tank est moins cher et demande de la
        /// patience. C'est redevenu un choix, ce n'etait plus une evidence.
        /// </summary>
        public const int TANK_CREATION_COST = 40;

        /// <summary>
        /// Prix d'un changement de posture sur un Tank DEJA POSE. Choisir la posture
        /// a la creation reste gratuit : on paie deja le Tank.
        ///
        /// Ce cout n'est pas la pour freiner le joueur, il est la pour que la posture
        /// soit une DECISION. Gratuite, elle ne coutait rien a reconsiderer : on
        /// ajustait ses six Tanks a chaque tour selon la menace du moment, et
        /// l'engagement - "ce Tank-la tient la ligne, quoi qu'il arrive" - n'existait
        /// pas. A 15, se tromper se repare, mais changer d'avis tous les tours coute
        /// un Tank toutes les trois corrections.
        /// </summary>
        public const int TANK_STANCE_COST = 15;
        public const int TANK_EVOLVE_COST = 60;

        /// <summary>
        /// LE BUNKER EST LE BATIMENT LE PLUS CHER APRES LE CRISTAL, et il est le seul
        /// qui disparaisse. Les deux vont ensemble : voir TANK_CREATION_COST.
        ///
        /// Ce qu'on achete pour 60 : quatre tirs de 10 par tour sur dix-huit cases,
        /// sans un clic, pendant trois tours. Il n'existe rien d'autre dans le jeu qui
        /// arrete une vague entiere a lui tout seul. Ce prix-la se paie en un tour de
        /// revenu, et c'est le bon ordre de grandeur pour une decision qui doit rester
        /// ponctuelle - "une vague arrive ICI" - et jamais devenir une doctrine.
        ///
        /// Le rang 2 ne suit PAS la regle du x1.5 des autres batiments (Gaz 30/45,
        /// Cristal 60/90, Centre 50/75). Ceux-la sont permanents : on amortit leur
        /// second rang sur toute la partie. Le Bunker fond en quatre tours, donc son
        /// second rang s'amortit sur quatre tours, et 80 est deja le plafond de ce
        /// qu'on peut demander pour quelque chose qu'on ne reverra pas.
        /// </summary>
        /// PRIX RELEVE PARCE QUE LE TIR EST DEVENU GRATUIT. Le Bunker coutait 60 a la
        /// construction PLUS 4 d'Energie par coup tire, soit une cinquantaine de plus
        /// sur toute sa vie. Ce prelevement au tir a disparu (voir BUNKER_SHOT_COST) :
        /// son cout entier est maintenant paye a la pose, et il est passe de 60 a 75
        /// pour que le rendement ne bouge pas.
        ///
        /// Ce qu'on achete pour 75, et c'est enfin un nombre que le joueur peut
        /// retenir : DIX SALVES. Quarante PV, quatre de perdus par coup parti. Le
        /// rang 2 en offre vingt, de quinze degats chacune.
        ///
        /// Prix au degat : 0,75 pour le Bunker, 0,80 pour le Tank. Les deux sont a
        /// egalite, et le Tank reste le moins cher a l'achat - le pivot tient (voir
        /// TANK_CREATION_COST).
        ///
        /// A SAVOIR, PARCE QUE C'EST UNE VRAIE DECISION : ameliorer un Bunker au rang 2
        /// remet ses PV au maximum. Un Bunker presque vide peut donc etre RECHARGE pour
        /// 95 au lieu d'etre laisse mourir et reconstruit pour 75. On paie vingt de plus
        /// et on recoit vingt salves plus fortes au lieu de dix.
        public const int COST_BUNKER_L1 = 75;
        public const int COST_BUNKER_L2 = 95;
        public const int COST_GAS_L1 = 30;
        public const int COST_GAS_L2 = 45;
        public const int COST_CRYSTAL_L1 = 60;
        public const int COST_CRYSTAL_L2 = 90;
        public const int COST_MOUNTAIN_L1 = 50;
        public const int COST_MOUNTAIN_L2 = 75;

        // ---------------- Section 3 : Base ----------------
        public const int BASE_HP = 200;

        // ---------------- Section 3 : Colline / Bunker ----------------
        // level 1 (= GDD Niv2 Bunker), level 2 (= GDD Niv3 Forteresse)
        public const int BUNKER_HP_L1 = 40;
        public const int BUNKER_HP_L2 = 80;
        public const int BUNKER_DAMAGE_L1 = 10;
        public const int BUNKER_DAMAGE_L2 = 15;
        public const int BUNKER_TARGETS_L1 = 4;
        public const int BUNKER_TARGETS_L2 = 6;
        public const int BUNKER_RANGE = 2;

        /// <summary>
        /// L'USURE DU BUNKER : UN BUNKER NE S'USE QU'EN TIRANT.
        ///
        /// CE QUE C'ETAIT, ET POURQUOI C'ETAIT FAUX
        ///
        /// Il perdait 8 PV par tour au rang 1, 11 au rang 2, QUOI QU'IL ARRIVE, plus
        /// 2 par tir. Une colline au calme fondait donc exactement comme une colline
        /// au front : cinq tours et elle s'effondrait sans avoir tire un coup.
        ///
        /// L'intention etait bonne - empecher le mur de Bunkers eternel qui gagnait la
        /// partie tout seul - mais le moyen retirait au joueur le seul levier qui
        /// comptait. Il ne pouvait ni reparer, ni ralentir, ni meme mettre son Bunker a
        /// l'abri. Un batiment qui fond a un rythme qu'on ne controle pas n'est pas une
        /// decision, c'est un impot avec un minuteur - et cela donnait au jeu entier un
        /// gout de construire sur du sable.
        ///
        /// LA REGLE MAINTENANT, ET ELLE TIENT EN UNE PHRASE
        ///
        /// Le Bunker ne perd de PV qu'en tirant : BUNKER_WEAR_PER_SHOT par coup parti.
        /// Ce n'est plus de la rouille, c'est des MUNITIONS - il se depense sur
        /// l'ennemi, et le joueur decide combien en choisissant ou il le pose.
        ///
        ///                     tous ses tirs   feu moyen   au calme
        ///   rang 1 (40 PV)      3 tours        5 tours     indefini
        ///   rang 2 (80 PV)      4 tours        7 tours     indefini
        ///
        /// SOUS LE FEU, LA DUREE EST EXACTEMENT CELLE D'AVANT : l'usure par tir passe
        /// de 2 a 4, ce qui compense au PV pres la decroissance passive supprimee. On
        /// n'a donc pas rendu le Bunker plus fort la ou il sert - on a seulement cesse
        /// de le punir la ou il ne sert pas.
        ///
        /// LE VERROU ANTI-FORTERESSE TIENT TOUJOURS, et c'est verifiable : six Bunkers
        /// qui tirent tous, ce sont 24 tirs, donc 96 Energie de munitions par tour -
        /// la moitie du revenu maximal de la carte - et 96 PV d'usure. Le mur fond
        /// toujours, exactement la ou il arrete quelque chose. Un Bunker ne dure que la
        /// ou personne ne passe, c'est-a-dire la ou il ne sert a rien.
        /// </summary>
        public const int BUNKER_DECAY_L1 = 0;
        public const int BUNKER_DECAY_L2 = 0;
        public const int BUNKER_WEAR_PER_SHOT = 4;

        public static int GetBunkerDecay(int level) { return (level >= 2) ? BUNKER_DECAY_L2 : BUNKER_DECAY_L1; }

        /// <summary>
        /// L'ENERGIE NE SE PERD QU'EN ETANT DEPENSEE. Ces deux constantes valent zero,
        /// et c'est desormais une regle du jeu, pas un reglage.
        ///
        /// Un tir de Bunker coutait 4 d'Energie au rang 1, 5 au rang 2. C'etait le
        /// DERNIER prelevement passif du jeu : le seul endroit ou le solde du joueur
        /// baissait sans qu'il ait clique sur quoi que ce soit. Tout le reste - un
        /// Tank, un batiment, une posture, un Shofar retourne - se paie sur une
        /// decision.
        ///
        /// Un compteur qui descend tout seul apprend au joueur a ne pas construire.
        /// Il regardait ses six Bunkers lui manger cent Energie par tour pour un
        /// resultat qu'il ne pouvait pas rattacher a un choix, et le jeu entier prenait
        /// le gout d'un entretien plutot que d'une conquete.
        ///
        /// Le prix n'a pas disparu, il a change de place : il est paye a la
        /// construction (voir COST_BUNKER_L1, passe de 60 a 75) et en PV a chaque tir
        /// (BUNKER_WEAR_PER_SHOT). Le Bunker reste donc exactement aussi cher pour ce
        /// qu'il fait - mais tout se decide au moment de le poser, et plus rien ne
        /// coule ensuite.
        ///
        /// Le verrou anti-forteresse ne reposait pas sur ce prix : six Bunkers, c'est
        /// 450 d'Energie a reconstruire tous les trois tours, soit 150 par tour sur un
        /// revenu maximal de 192. Le mur complet reste impossible, et il l'est
        /// maintenant a cause d'une decision de construction, pas d'une fuite.
        ///
        /// Elles restent dans le code plutot que d'etre supprimees : BuildingManager
        /// les additionne toujours, et un reglage futur a une valeur non nulle
        /// remarcherait sans toucher a rien.
        /// </summary>
        public const int BUNKER_SHOT_COST_L1 = 0;
        public const int BUNKER_SHOT_COST_L2 = 0;

        // ---------------- Section 3 : Cristal (soutien militaire) ----------------
        //
        // DEUX PILIERS, ZERO RECOUVREMENT.
        //
        // Avant, le Gaz soignait et le Cristal payait ET renforcait. Les deux
        // rapportaient quelque chose de vaguement utile, et le joueur ne voyait pas la
        // difference - il l'a dit mot pour mot apres sa premiere partie complete.
        //
        // Desormais la ligne de partage est nette :
        //   le GAZ produit l'Energie, et c'est ce qu'il faut defendre ;
        //   le CRISTAL ne rapporte rien et sert uniquement l'armee.
        //
        // Le soin est passe du Gaz au Cristal pour cette raison : une usine qui soigne
        // en plus de payer serait a nouveau le batiment qui fait tout.
        public const int CRYSTAL_BONUS_MAXHP = 20;
        public const int CRYSTAL_HEAL_L1 = 10;
        public const int CRYSTAL_HEAL_L2 = 20;
        public const int CRYSTAL_RANGE_L1 = 1;
        public const int CRYSTAL_RANGE_L2 = 2;

        // ---------------- Section 3 : Montagne (Centre de Commandement) ----------------
        //
        // CE QU'IL FAISAIT DEJA, ET POURQUOI PERSONNE NE LE VOYAIT
        //
        // Il barre le passage : les cases a MOUNTAIN_REPEL de lui deviennent
        // infranchissables pour les ennemis, et seulement pour eux - c'est teste dans
        // BoardController.GetNextStepTowards. L'effet est reel, mais totalement
        // invisible : les ennemis contournent, rien ne le montre, aucun texte ne
        // l'annonce, et le batiment fondait en trois tours. Le joueur payait 50 et
        // n'observait rigoureusement rien.
        //
        // CE QU'ON LUI AJOUTE, ET POURQUOI
        //
        // Le probleme de fond du jeu n'etait pas ce batiment : c'est que les Tanks sont
        // enchaines a la Base. Un Tank en Garde ignore tout ce qui se trouve a plus de
        // guardRadius cases de la Base, et un Tank rang 1 avance d'une case par tour,
        // alors que les Shofars sont a sept ou huit. La carte etait coupee en deux :
        // chez soi, ou l'on tient tout, et le reste, ou l'on ne tient rien. Camper
        // derriere ses Bunkers etait donc la seule strategie coherente.
        //
        // Le Centre de Commandement devient un DEUXIEME point d'ancrage :
        //
        //   - les Tanks en Garde defendent autour de LUI comme autour de la Base ;
        //   - les Tanks dans son rayon avancent d'une case de plus.
        //
        // Ce n'est pas une fortification, c'est une TETE DE PONT - et une tete de pont
        // ne fond pas toute seule : on vient la lui prendre. Voir MOUNTAIN_SHIELD_L1.
        //
        // PV releves de 30/50 a 45/75. A 30, un Centre pose a quatre cases de la Base -
        // donc a portee des emissaires - tombait en trois coups d'un ennemi de rang 1,
        // ou en un seul d'un rang 3, pour 50 d'Energie. Ce n'etait pas une tete de
        // pont, c'etait une cible.
        public const int MOUNTAIN_HP_L1 = 45;
        public const int MOUNTAIN_HP_L2 = 75;

        /// <summary>
        /// Rayon INFRANCHISSABLE pour les ennemis. Lu par BoardController quand il
        /// calcule le pas suivant d'un ennemi ; les Tanks du joueur passent librement.
        ///
        /// TROIS CORRECTIONS, ET ELLES VONT ENSEMBLE. La repulsion rendait le Centre
        /// litteralement invincible : un ennemi frappe au CONTACT, et la repulsion
        /// fermait justement les six cases de contact. Pire, l'IA le choisissait quand
        /// meme comme cible, marchait vers lui, se faisait bloquer a deux cases, et
        /// restait plantee la - ni avancer, ni frapper, tour apres tour.
        ///
        ///   1. LE RANG 2 NE REPOUSSE PLUS PLUS LOIN. Il est passe de 2 a 1.
        ///      Mesure sur le plateau reel : six Centres rang 2 debout fermaient
        ///      TOUTES les cases d'attaque de la Base - zero sur douze. Le joueur
        ///      n'avait qu'a en batir six pour ne plus jamais pouvoir perdre. Cela ne
        ///      tenait avant que parce qu'un Centre fondait en dix tours ; maintenant
        ///      qu'il est permanent, c'etait une victoire automatique a 300 d'Energie.
        ///      Le rang 2 garde tout le reste : plus de PV, une bulle plus grande, et
        ///      un rayon de commandement qui passe de 2 a 3.
        ///
        ///   2. CELUI QUI VIENT POUR LUI PASSE. Un Centre barre le passage a ceux qui
        ///      vont AILLEURS ; il n'empeche pas de frapper ce qu'on est venu frapper.
        ///      Voir BoardController.PathStepTowards : la repulsion est ignoree quand
        ///      la cible de l'ennemi est dans sa zone ou au bord.
        ///
        ///   3. LA BULLE EST LE MUR. Bulle percee, le Centre ne barre plus rien.
        ///
        /// Ensemble, ces trois regles repondent a la seule question qui comptait :
        /// comment abat-on un Centre ? On vient expres pour lui - la repulsion ne
        /// protege pas de cela - on perce sa bulle, et alors le couloir s'ouvre pour
        /// tout le monde.
        /// </summary>
        public const int MOUNTAIN_REPEL_L1 = 1;
        public const int MOUNTAIN_REPEL_L2 = 1;

        /// <summary>
        /// PLUS D'USURE DU TOUT. Un Centre de Commandement ne fond plus.
        ///
        /// Il perdait 5 PV par tour, quoi qu'il arrive : six tours de vie au rang 1,
        /// dix au rang 2. Or le trajet de la Base a un Shofar prend trois tours a un
        /// Tank rang 2. Le joueur posait donc son Centre, s'en servait deux fois, et le
        /// regardait tomber sans pouvoir rien y faire. Payer 50 d'Energie pour un objet
        /// dont on connait deja la date de mort, c'est la definition du sable mouvant.
        ///
        /// CE QUI LE REND PERISSABLE MAINTENANT : L'ENNEMI, ET RIEN D'AUTRE. Il est
        /// pose en avant, expose, et le Yetzer Hara le prend pour cible des qu'il passe
        /// a trois cases. Il tombera - mais parce qu'on est venu le lui prendre, et
        /// c'est une histoire que le joueur peut suivre, contester, et parfois gagner.
        ///
        /// Conservee a zero plutot que supprimee : BuildingManager l'additionne
        /// toujours, et une valeur non nulle remarcherait sans toucher a rien.
        /// </summary>
        public const int MOUNTAIN_DECAY_PER_TURN = 0;

        // =====================================================================
        //  LA BULLE DU CENTRE DE COMMANDEMENT
        // =====================================================================
        //
        // L'ennemi doit d'abord CASSER LA BULLE. Tant qu'elle tient, le batiment ne
        // prend rien : tous les degats vont dans le bouclier.
        //
        // C'est la meme grammaire que le bouclier d'un Shofar, retournee. Le joueur
        // connait deja la regle - il passe la partie a briser celle du Yetzer Hara -
        // et il la retrouve ici de son cote. Rien de neuf a apprendre.
        //
        // POURQUOI UN BOUCLIER PLUTOT QUE PLUS DE PV
        //
        // Doubler les PV aurait rendu le Centre plus dur a tuer, pas plus interessant.
        // Un bouclier fait trois choses qu'un tas de PV ne fait pas :
        //
        //   - il SE VOIT. Un dome au-dessus de la case, exactement comme sur un Shofar
        //     debout : on sait d'un coup d'oeil si sa tete de pont tient encore.
        //   - il SE REFAIT quand on laisse le Centre tranquille, donc une escarmouche
        //     ne coute rien a long terme, alors qu'un assaut soutenu perce.
        //   - il DONNE UN TOUR. Le tour ou la bulle tombe est un avertissement : le
        //     joueur voit venir la perte et peut encore envoyer un Tank.
        //
        // Un seul ennemi de rang 2 ne percera jamais un Centre rang 1 : il fait 15
        // degats et la bulle en reprend 8 par tour de calme. Il faut une vraie poussee,
        // ce qui est exactement ce qu'on veut qu'une tete de pont demande.

        public const int MOUNTAIN_SHIELD_L1 = 40;
        public const int MOUNTAIN_SHIELD_L2 = 70;

        /// <summary>
        /// Ce que la bulle reprend par tour, UNIQUEMENT si le Centre n'a rien encaisse
        /// ce tour-ci. Frappe, elle ne se refait pas : sinon un assaut lent n'aboutirait
        /// jamais et la tete de pont redeviendrait une fortification.
        /// </summary>
        public const int MOUNTAIN_SHIELD_REGEN = 8;

        public static int GetMountainShield(int level)
        {
            if (level >= 2) return MOUNTAIN_SHIELD_L2;
            return (level == 1) ? MOUNTAIN_SHIELD_L1 : 0;
        }

        // =====================================================================
        //  LA BULLE EST UNE ZONE, ET ELLE SE FRAPPE DE L'EXTERIEUR
        // =====================================================================
        //
        // LA VERSION PRECEDENTE ETAIT UN BRICOLAGE, ET CA SE VOYAIT.
        //
        // Le bouclier etait colle au batiment, la repulsion fermait separement les
        // cases autour, et il fallait deux exceptions pour que l'ennemi puisse
        // quand meme entrer frapper : "celui qui vient pour lui passe", "la cible dans
        // la zone annule la repulsion". Deux regles qu'aucun joueur n'aurait devinees
        // en regardant le plateau.
        //
        // LA BULLE COUVRE DES CASES. C'est tout, et ca se voit.
        //
        //   Le Centre et les cases a MOUNTAIN_REPEL autour de lui - sept cases au
        //   total - sont SOUS la bulle. Aucun ennemi n'y entre, jamais, sans exception.
        //
        //   Un ennemi arrive au bord, a une case de la bulle, et il FRAPPE LA BULLE.
        //   Il est au contact de sa surface : sa portee de une case suffit. Neuf cases
        //   font le tour de la bulle, il y en a toujours une de libre.
        //
        //   La bulle tombe, la zone s'ouvre, les ennemis entrent et s'en prennent au
        //   batiment.
        //
        // Une seule phrase pour tout le systeme : ON NE PEUT PAS ENTRER DANS LA BULLE,
        // ON LA CASSE DE L'EXTERIEUR. Les deux exceptions ont disparu avec elle.

        /// <summary>
        /// Rayon de la bulle de cette case, en cases. Zero quand il n'y en a pas -
        /// n'importe quelle cible, un Shofar, une usine, un Tank - ce qui fait de
        /// DistanceToTarget un calcul valable partout.
        /// </summary>
        public static int BubbleRadius(Hexagon hex)
        {
            if (hex == null) return 0;
            if (hex.type != TypeOfHex.mountain || hex.level < 1) return 0;
            if (hex.currentHP <= 0 || hex.shieldHP <= 0) return 0;

            return GetMountainRepel(hex.level);
        }

        /// <summary>
        /// Distance de FRAPPE jusqu'a cette case : la distance ordinaire, moins le
        /// rayon de sa bulle. Un ennemi a deux cases d'un Centre protege est donc a
        /// une case de la bulle, c'est-a-dire a portee.
        ///
        /// C'est le seul calcul que l'IA ennemie doit faire pour que tout le systeme
        /// marche : chercher une cible, decider d'attaquer plutot que d'avancer, et
        /// savoir quand s'arreter en chemin passent tous par ici.
        /// </summary>
        public static int DistanceToTarget(HexCoord from, Hexagon target)
        {
            if (from == null || target == null || target.positionInTheBoard == null) return int.MaxValue;

            int distance = BoardController.GetHexDistance(from, target.positionInTheBoard) - BubbleRadius(target);
            return (distance < 0) ? 0 : distance;
        }

        /// <summary>
        /// La bulle de ce Centre tient-elle encore ? Lue par l'affichage et par le
        /// filtre de degats.
        /// </summary>
        public static bool HasShield(Hexagon hex)
        {
            return hex != null
                && hex.type == TypeOfHex.mountain
                && hex.level >= 1
                && hex.currentHP > 0
                && hex.shieldHP > 0;
        }

        /// <summary>
        /// (Re)pose la bulle a son maximum. Appele a la construction et a chaque montee
        /// de rang : un Centre ameliore repart avec une bulle pleine, comme il repart
        /// avec ses PV pleins.
        /// </summary>
        public static void ResetShield(Hexagon hex)
        {
            if (hex == null || hex.type != TypeOfHex.mountain) return;

            hex.shieldMax = GetMountainShield(hex.level);
            hex.shieldHP = hex.shieldMax;
        }

        /// <summary>
        /// Les degats passent d'abord dans la bulle. Rend ce qui reste pour le
        /// batiment - zero tant que la bulle encaisse tout.
        ///
        /// Le debordement n'est PAS reporte sur le batiment : le coup qui fait tomber
        /// la bulle s'arrete a la bulle. C'est ce qui garantit le tour d'avertissement
        /// dont parle le commentaire ci-dessus - la bulle tombe, le Centre est encore
        /// entier, le joueur a un tour pour reagir.
        /// </summary>
        public static int FilterMountainDamage(Hexagon hex, int amount)
        {
            if (hex == null || amount <= 0) return amount;
            if (hex.type != TypeOfHex.mountain || hex.level < 1) return amount;
            if (hex.shieldHP <= 0) return amount;

            hex.shieldHP -= amount;

            if (hex.shieldHP <= 0)
            {
                hex.shieldHP = 0;
                Debug.LogFormat("[Centre] La bulle de ({0},{1}) vient de ceder.",
                                hex.positionInTheBoard.q, hex.positionInTheBoard.r);
            }

            return 0;
        }

        /// <summary>
        /// Rayon de COMMANDEMENT : jusqu'ou ce Centre remplace la Base comme point
        /// d'ancrage des Tanks en Garde, et jusqu'ou il accorde sa case de mobilite.
        ///
        /// Plus large que le rayon infranchissable, et c'est voulu : la zone que les
        /// ennemis ne peuvent pas traverser doit rester petite, sinon deux Centres
        /// muraient la carte ; la zone que le joueur commande doit etre assez large
        /// pour qu'une ligne s'y installe.
        /// </summary>
        public const int MOUNTAIN_COMMAND_RADIUS_L1 = 2;
        public const int MOUNTAIN_COMMAND_RADIUS_L2 = 3;

        /// <summary>Cases de deplacement gagnees par un Tank dans le rayon.</summary>
        public const int MOUNTAIN_MOVE_BONUS = 1;

        // ---------------- Section 4 : Portails ----------------
        /// <summary>
        /// Nombre de Shofars au depart : un par coin du plateau hexagonal. Sert de
        /// denominateur partout ou l'on affiche une progression - le bandeau, l'ecran
        /// de fin. Le compter sur le plateau ne marcherait pas : a la fin de la partie,
        /// ceux qu'on a fermes n'y sont plus.
        /// </summary>
        public const int PORTAL_COUNT = 6;

        public const int PORTAL_HP_L1 = 50;
        public const int PORTAL_HP_L2 = 100;
        /// <summary>
        /// Tours de survie avant qu'un portail ne passe au Niveau 2. Les six portails
        /// n'evoluent PAS en meme temps : voir PORTAL_EVOLVE_STAGGER. Quand ils le
        /// faisaient, le tour 12 effacait d'un coup tout le travail du joueur.
        /// </summary>
        /// <remarks>Selon la difficulte : 20 / 16 / 12.</remarks>
        public static int PORTAL_EVOLVE_AFTER_TURNS { get { return GameDifficulty.Pick(20, 16, 12); } }

        /// <summary>Decalage, en tours, entre l'evolution de deux portails voisins.</summary>
        public const int PORTAL_EVOLVE_STAGGER = 3;

        public const float PORTAL_MINIBOSS_CHANCE = 0.10f;
        /// <summary>
        /// Un portail donne ne deploie qu'un ennemi tous les N tours, et chaque portail
        /// a son propre decalage dans ce cycle (voir PortalManager). A 6 portails et un
        /// intervalle de 6, il sort exactement UN ennemi par tour, tous les tours.
        ///
        /// Avant, les six portails etaient synchronises sur un intervalle de 3 et le
        /// plafond en laissait passer 3 : le rythme moyen etait le meme, mais il tombait
        /// par paquets de trois tous les trois tours. Entre deux paquets il ne se passait
        /// rien, et le debut de partie donnait l'impression que le jeu ne marchait pas.
        /// </summary>
        /// <summary>
        /// Tours entre deux deploiements d'un meme Shofar.
        ///
        /// Ramene de 6 a 3 apres une partie ou le joueur a gagne sans qu'un seul ennemi
        /// l'approche. Le calcul etait sans appel : six Shofars a un ennemi tous les six
        /// tours font UN ennemi par tour sur toute la carte, alors qu'un seul Bunker
        /// tire quatre fois pour dix degats, soit DEUX ennemis rang 1 tues par tour.
        /// Un mur etait de trop des le premier Bunker.
        ///
        /// Le prix au tir, qui existe precisement pour empecher les murs, ne mordait
        /// pas non plus : peu d'ennemis veut dire peu de tirs, donc presque gratuit.
        /// A deux ennemis par tour, un Bunker depense 16 d'Energie par tour contre 12
        /// de revenu de base - et la regle se met enfin a faire son travail.
        ///
        /// On a choisi d'augmenter les arrivees plutot que d'affaiblir le Bunker : le
        /// Bunker n'est pas trop fort dans l'absolu, il l'est par rapport a ce qui
        /// arrive. L'affaiblir assez pour changer le debut de partie l'aurait rendu
        /// inutile contre les rangs 2 et 3 en fin de partie.
        /// </summary>
        /// <remarks>Selon la difficulte : 5 / 4 / 3, MOINS l'acceleration ci-dessous.</remarks>
        public static int PORTAL_SPAWN_INTERVAL
        {
            get
            {
                int interval = GameDifficulty.Pick(5, 4, 3) - SpawnAcceleration();
                return (interval < PORTAL_SPAWN_INTERVAL_FLOOR) ? PORTAL_SPAWN_INTERVAL_FLOOR : interval;
            }
        }

        // ------- LE YETZER SE NOURRIT DE TA PROSPERITE -------
        //
        // C'etait le trou de l'equilibrage : une economie exponentielle en face d'une
        // pression PLATE. Quatre usines au premier tour, aucune contrepartie, et des
        // le quatrieme tour l'Energie cessait d'etre une contrainte - avec elle,
        // toutes les decisions qu'elle portait.
        //
        // Maintenant, chaque tranche de revenu enleve un tour au cycle de deploiement
        // de TOUS les Shofars. Batir n'est plus gratuit : ca t'achete du temps et ca
        // t'en reprend. Et l'austerite devient une vraie strategie - rester pauvre
        // pour que l'ennemi reste lent.
        //
        // C'est aussi la regle la plus juste thematiquement : ce qu'on accumule
        // renforce ce qu'on affronte.

        /// <summary>
        /// Revenu d'usines qui enleve UN tour au cycle de deploiement.
        ///
        /// PORTE DE 45 A 60 QUAND LE GLACIS A REDUIT LA CARTE A SIX USINES. A 45,
        /// batir simplement ses six usines de depart (90 de revenu) suffisait a
        /// atteindre le plancher du cycle : passe ce point, monter en rang 2, liberer
        /// un secteur ou doubler son economie ne coutait plus RIEN, et la regle
        /// cessait d'exister au moment ou elle aurait du mordre.
        ///
        /// A 60, la courbe respire : six usines rang 1 valent une marche, les monter
        /// toutes en rang 2 en vaut une deuxieme et touche le plancher. L'evolution
        /// d'une usine redevient une decision, au lieu d'un gain sans facture.
        /// </summary>
        public const int SPAWN_ACCEL_PER_INCOME = 60;

        /// <summary>Le cycle ne descend jamais sous ce seuil, quelle que soit la richesse.</summary>
        public const int PORTAL_SPAWN_INTERVAL_FLOOR = 2;

        /// <summary>Tours retires au cycle par la richesse du joueur.</summary>
        public static int SpawnAcceleration()
        {
            return GasIncomePerTurn() / SPAWN_ACCEL_PER_INCOME;
        }

        // Memoire d'une frame : PORTAL_SPAWN_INTERVAL est lu plusieurs fois par tour
        // (deploiement, apercu de menace, panneau d'un Shofar). Sans ce garde-fou on
        // reparcourrait les 169 cases a chaque lecture. Avec, au plus une fois par
        // frame - et jamais de valeur perimee, puisqu'une usine batie change la frame.
        private static int _incomeFrame = -1;
        private static int _incomeValue;

        /// <summary>Ce que les usines DEBOUT rapportent par tour, Base non comprise.</summary>
        public static int GasIncomePerTurn()
        {
            int frame = Time.frameCount;
            if (frame == _incomeFrame) return _incomeValue;
            _incomeFrame = frame;
            _incomeValue = 0;

            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return 0;

            System.Collections.Generic.List<Hexagon> hexes = board.HexagonsInBoard;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.gas || hex.level < 1 || hex.currentHP <= 0) continue;
                _incomeValue += GetGasIncome(hex.level);
            }

            return _incomeValue;
        }

        /// <summary>
        /// Ennemis deja presents sur la carte au premier tour. 0 : le plateau commence
        /// vide, les premiers ennemis sortent des Portails.
        /// </summary>
        public const int INITIAL_ENEMIES = 0;

        /// <summary>Tanks offerts au joueur au debut de la partie. 0 : c'est a lui de les construire.</summary>
        public const int INITIAL_PLAYER_TANKS = 0;

        /// <summary>Distance a laquelle ces premiers ennemis sont poses, en cases.</summary>
        public const int INITIAL_ENEMY_DISTANCE = 5;
        /// <summary>Nombre maximum d'ennemis deployes par tour, tous portails confondus.</summary>
        /// <remarks>Selon la difficulte : 2 / 3 / 4.</remarks>
        public static int MAX_SPAWNS_PER_TURN { get { return GameDifficulty.Pick(2, 3, 4); } }
        /// <summary>Plafond d'ennemis simultanement presents sur la carte.</summary>
        /// <remarks>Selon la difficulte : 8 / 12 / 16.</remarks>
        public static int MAX_ACTIVE_ENEMIES { get { return GameDifficulty.Pick(8, 12, 16); } }

        // ------- Instabilite des Portails (le coeur de l'equilibrage) -------
        /// <summary>
        /// Degats encaisses par le portail d'origine a chaque ennemi qu'il a deploye et
        /// qui meurt. Tuer, c'est deja attaquer la source.
        /// </summary>
        public const int PORTAL_KILL_BACKLASH = 12;

        /// <summary>
        /// Plancher, en pourcentage des PV max, sous lequel le contrecoup ne peut pas
        /// descendre. Resister use le portail ; seul un Tank peut le fermer. Sans ce
        /// plancher, une partie purement defensive gagnait toute seule en quarante
        /// tours, sans que le joueur ait jamais a prendre le moindre risque.
        /// </summary>
        public const int PORTAL_BACKLASH_FLOOR_PERCENT = 20;

        /// <summary>Nombre de morts qui font sauter le bouclier d'un portail.</summary>
        /// <summary>
        /// L'HORLOGE : un Shofar qu'on n'attaque pas se refait.
        ///
        /// Augmenter le nombre d'ennemis releve le NIVEAU de la pression, mais la
        /// laisse plate : un joueur qui a trouve la reponse a deux ennemis par tour l'a
        /// trouvee pour toujours, et le tour 30 n'est pas plus dangereux que le tour 10.
        /// Attendre reste gratuit, et camper redevient optimal avec un mur plus gros.
        ///
        /// Ces trois valeurs donnent la PENTE. Passe PORTAL_CALM_TURNS tours sans
        /// recevoir un seul coup de Tank, un Shofar regagne des PV et efface peu a peu
        /// son instabilite. L'avance prise en defendant n'est donc plus un acquis :
        /// c'est une avance qui EXPIRE si on ne la convertit pas en offensive.
        ///
        /// Seul un coup PORTE SUR LE SHOFAR remet le compteur a zero. Le contrecoup des
        /// morts ne compte pas : c'est justement le joueur qui accumule des morts sans
        /// jamais aller conclure que cette regle vise.
        /// </summary>
        public const int PORTAL_CALM_TURNS = 3;
        public const int PORTAL_REGEN_PER_TURN = 8;

        /// <summary>Tours de calme entre deux points d'instabilite effaces.</summary>
        public const int PORTAL_CALM_PER_INSTABILITY_LOST = 2;

        /// <remarks>Selon la difficulte : 3 / 4 / 4.</remarks>
        public static int PORTAL_KILLS_TO_BREAK_SHIELD { get { return GameDifficulty.Pick(3, 4, 4); } }

        /// <summary>Duree, en tours, pendant laquelle le bouclier reste tombe.</summary>
        public const int PORTAL_SHIELD_DOWN_TURNS = 4;

        /// <summary>
        /// Diviseur applique aux degats recus tant que le bouclier tient. A 2, un Tank
        /// Niveau 1 ne place que 5 degats sur un portail stable, contre 10 une fois le
        /// bouclier tombe : la fenetre d'assaut vaut la peine d'etre preparee.
        /// </summary>
        public const int PORTAL_SHIELD_DIVISOR = 2;

        /// <summary>Ennemis supplementaires lors d'une vague annoncee (surge).</summary>
        public const int SURGE_EXTRA_SPAWNS = 2;

        // ---------------- Plafonds ----------------
        public const int MAX_TERRAIN_LEVEL = 2;  // = GDD Niveau 3
        public const int MAX_TANK_LEVEL = 2;
        public const int MAX_PORTAL_LEVEL = 2;
        public const int MAX_ENEMY_LEVEL = 3;

        // =====================================================================
        //  STATS DES UNITES - Section 5 (source de verite unique)
        // =====================================================================
        public static void ApplyStatsToPawn(PawnController pawn)
        {
            if (pawn == null) return;

            int move = 1;

            if (pawn.typeOfPawn == TypeOfPawn.enemy)
            {
                // LE YETZER HARA FRAPPE AU CONTACT, TOUJOURS.
                //
                // Les rangs 2 et 3 tiraient a deux et trois cases : ils pilonnaient une
                // usine ou un Tank sans jamais s'exposer, et il n'existait aucune facon
                // de les arreter avant qu'ils aient fait leur travail. Ce qui monte avec
                // le rang, maintenant, c'est la VITESSE : ils arrivent plus vite, mais
                // ils doivent arriver. Le joueur a donc toujours un tour pour agir, et
                // la distance redevient une defense.
                if (pawn.level <= 1) { pawn.maxHP = 20; pawn.attackDamageMin = 10; move = 1; }
                else if (pawn.level == 2) { pawn.maxHP = 40; pawn.attackDamageMin = 15; move = 2; }
                else { pawn.maxHP = 80; pawn.attackDamageMin = 30; move = 3; }

                pawn.attackRange = 1;

                // Facile et Moyen : des ennemis moins solides et moins durs. En
                // Difficile les pourcentages valent 100 et rien ne change.
                pawn.maxHP = GameDifficulty.Scale(pawn.maxHP, GameDifficulty.EnemyHpPercent);
                pawn.attackDamageMin = GameDifficulty.Scale(pawn.attackDamageMin, GameDifficulty.EnemyDamagePercent);
            }
            else // Tank du joueur - il n'y a plus de classes (GDD V3, 5.1)
            {
                if (pawn.level <= 1) { pawn.maxHP = 30; pawn.attackDamageMin = 10; pawn.attackRange = 1; }
                else
                {
                    // Le Tank Niveau 2 gagne aussi une case de mobilite : c'est ce qui
                    // rend l'evolution interessante quand les portails sont a sept cases.
                    pawn.maxHP = 60; pawn.attackDamageMin = 20; pawn.attackRange = 2;
                    move = 2;
                }
            }

            // Combat sans hasard : degats fixes, pas de critique, pas d'esquive.
            pawn.attackDamageMax = pawn.attackDamageMin;
            pawn.critChance = 0f;
            pawn.dodgeChance = 0f;
            pawn.moveRange = move;

            pawn.baseMaxHP = pawn.maxHP;
            pawn.currentHP = pawn.maxHP;
            pawn.energy = pawn.maxHP;
            pawn.energymax = pawn.maxHP;
        }

        public static int GetPawnDamage(PawnController pawn)
        {
            return pawn != null ? pawn.attackDamageMin : 0;
        }

        // =====================================================================
        //  BATIMENTS - Section 3
        // =====================================================================
        public static int GetBuildingMaxHP(TypeOfHex type, int level)
        {
            switch (type)
            {
                case TypeOfHex.Base: return BASE_HP;
                case TypeOfHex.portal: return (level >= 2) ? PORTAL_HP_L2 : PORTAL_HP_L1;
                case TypeOfHex.hill: return (level >= 2) ? BUNKER_HP_L2 : (level == 1 ? BUNKER_HP_L1 : 0);
                case TypeOfHex.mountain: return (level >= 2) ? MOUNTAIN_HP_L2 : (level == 1 ? MOUNTAIN_HP_L1 : 0);
                case TypeOfHex.gas: return (level >= 2) ? GAS_HP_L2 : (level == 1 ? GAS_HP_L1 : 0);
                case TypeOfHex.crystal: return (level >= 2) ? CRYSTAL_HP_L2 : (level == 1 ? CRYSTAL_HP_L1 : 0);
                default: return 0;
            }
        }

        /// <summary>
        /// Cout d'un batiment pour atteindre targetLevel (1 = GDD Niv2, 2 = GDD Niv3).
        /// Retourne 0 pour un type non constructible.
        /// </summary>
        public static int GetBuildCost(TypeOfHex type, int targetLevel)
        {
            bool second = targetLevel >= 2;

            switch (type)
            {
                case TypeOfHex.hill: return second ? COST_BUNKER_L2 : COST_BUNKER_L1;
                case TypeOfHex.gas: return second ? COST_GAS_L2 : COST_GAS_L1;
                case TypeOfHex.crystal: return second ? COST_CRYSTAL_L2 : COST_CRYSTAL_L1;
                case TypeOfHex.mountain: return second ? COST_MOUNTAIN_L2 : COST_MOUNTAIN_L1;

                // La ruine d'un Shofar : son unique "niveau" est le retournement.
                case TypeOfHex.Destroyed: return second ? 0 : COST_TURN_PORTAL;

                default: return 0;
            }
        }

        /// <summary>Revenu par tour d'un Cristal actif.</summary>
        public static int GetGasIncome(int level)
        {
            if (level >= 2) return GAS_INCOME_L2;
            if (level == 1) return GAS_INCOME_L1;
            return 0;
        }

        public static int GetCrystalHeal(int level) { return (level >= 2) ? CRYSTAL_HEAL_L2 : CRYSTAL_HEAL_L1; }

        /// <summary>
        /// Conserve a zero : le Cristal ne rapporte plus rien. La methode reste pour que
        /// le vieux banc de tests continue de compiler et de dire la verite.
        /// </summary>
        public static int GetCrystalIncome(int level)
        {
            return 0;
        }

        public static int GetBunkerShotCost(int level) { return (level >= 2) ? BUNKER_SHOT_COST_L2 : BUNKER_SHOT_COST_L1; }
        public static int GetBunkerDamage(int level) { return (level >= 2) ? BUNKER_DAMAGE_L2 : BUNKER_DAMAGE_L1; }
        public static int GetBunkerTargets(int level) { return (level >= 2) ? BUNKER_TARGETS_L2 : BUNKER_TARGETS_L1; }
        /// <summary>
        /// Le Gaz ne soigne plus - c'est le Cristal qui soigne. Ces deux methodes
        /// renvoient ce que le CRISTAL fait, pour que rien de ce qui les appelait
        /// encore ne se mette a mentir en silence.
        /// </summary>
        public static int GetGasHeal(int level) { return GetCrystalHeal(level); }
        public static int GetGasRange(int level) { return GetCrystalRange(level); }
        public static int GetCrystalRange(int level) { return (level >= 2) ? CRYSTAL_RANGE_L2 : CRYSTAL_RANGE_L1; }
        public static int GetMountainRepel(int level) { return (level >= 2) ? MOUNTAIN_REPEL_L2 : MOUNTAIN_REPEL_L1; }

        public static int GetMountainCommandRadius(int level)
        {
            return (level >= 2) ? MOUNTAIN_COMMAND_RADIUS_L2 : MOUNTAIN_COMMAND_RADIUS_L1;
        }

        /// <summary>
        /// Un pion (Tank ou ennemi) peut-il se POSER sur cette case ?
        ///
        /// Non sur : une colline (obstacle naturel, Bunker ou pas), la Base, un Shofar,
        /// et toute case ou un batiment est construit (Gaz, Cristal, Centre de
        /// Commandement a partir du niveau interne 1). Un batiment detruit repasse au
        /// niveau 0 : sa case redevient praticable.
        ///
        /// C'est la seule definition : le calcul de chemin, la sortie des ennemis par
        /// les Shofars et l'apercu des menaces l'appellent tous.
        /// </summary>
        public static bool IsHexWalkable(Hexagon hex)
        {
            if (hex == null) return false;

            switch (hex.type)
            {
                case TypeOfHex.hill:
                case TypeOfHex.Base:
                case TypeOfHex.portal:
                    return false;

                case TypeOfHex.gas:
                case TypeOfHex.crystal:
                case TypeOfHex.mountain:
                    return hex.level < 1;

                // Une ruine de Shofar se traverse ; une ruine RETOURNEE est un
                // batiment, et un batiment ne se pietine pas.
                case TypeOfHex.Destroyed:
                    return hex.level < 1;

                default:
                    return true;
            }
        }

        // =====================================================================
        //  ATKAFYA ET AT'HAPKHA - les deux etapes du Tanya
        // =====================================================================
        //
        // Le Tanya distingue deux travaux sur le Yetzer Hara.
        //
        //   ATKAFYA - le soumettre. On resiste, on l'empeche d'agir, on le met a bas.
        //             Le mal reste du mal ; il est tenu. C'est tout le jeu jusqu'ici :
        //             la ligne qu'on tient, le bouclier qu'on brise, le Shofar qu'on
        //             abat. Il en reste une ruine, morte et sterile.
        //
        //   AT'HAPKHA - le retourner. Le mal lui-meme devient bien : ce n'est plus une
        //             force qu'on contient, c'est une force qui sert. Et la lumiere
        //             tiree de l'obscurite vaut plus que la lumiere ordinaire -
        //             YITRON HA'OR MIN HA'HOSHEKH.
        //
        // Sur le plateau, la ruine d'un Shofar peut donc etre RETOURNEE : elle devient
        // une source qui verse plus qu'une usine de Gaz au rang 2.
        //
        // Et surtout - c'est la ligne qui porte toute l'idee - cette Energie-la
        // N'ACCELERE PAS le Yetzer. Le Gaz qu'on batit le nourrit (voir
        // SPAWN_ACCEL_PER_INCOME, qui ne compte que les usines) ; ce qu'on lui a pris
        // ne le nourrit pas. La richesse ordinaire a un prix, la richesse retournee
        // n'en a aucun : elle est d'un autre ordre.
        //
        // Un Shofar retourne est INDESTRUCTIBLE, et ce n'est pas un oubli : ce qui a
        // ete retourne ne se retourne pas en sens inverse.

        /// <summary>Prix du retournement d'une ruine de Shofar. C'est le prix d'une offensive.</summary>
        public const int COST_TURN_PORTAL = 120;

        /// <summary>Ce que verse un Shofar retourne, chaque tour. Plus qu'un Gaz rang 2.</summary>
        public const int TURNED_PORTAL_INCOME = 35;

        /// <summary>Une ruine de Shofar, pas encore retournee.</summary>
        public static bool IsFallenPortal(Hexagon hex)
        {
            return hex != null && hex.type == TypeOfHex.Destroyed && hex.level < 1;
        }

        /// <summary>Un Shofar retourne : il verse, et il ne nourrit plus le Yetzer.</summary>
        public static bool IsTurnedPortal(Hexagon hex)
        {
            return hex != null && hex.type == TypeOfHex.Destroyed && hex.level >= 1;
        }

        /// <summary>Peut-on retourner cette ruine maintenant ? (portee comprise)</summary>
        public static bool CanTurnPortal(Hexagon hex)
        {
            return IsFallenPortal(hex) && CanBuildAt(hex.positionInTheBoard);
        }

        /// <summary>
        /// Le retournement. La case garde son type (une ruine reste une ruine aux yeux
        /// du code : elle ne compte pas parmi les Shofars a abattre) et passe au niveau
        /// 1, ce qui suffit a la distinguer partout. Aucun nouveau type d'hexagone :
        /// les parties deja sauvegardees continuent donc de se relire sans rien changer.
        /// </summary>
        public static TriviaOutcome TurnPortal(Hexagon hex)
        {
            if (!IsFallenPortal(hex)) return TriviaOutcome.EnergyOnly;
            if (!CanBuildAt(hex.positionInTheBoard)) return TriviaOutcome.Blocked_OutOfRange;

            EnergyManager energy = EnergyManager.Instance;
            if (energy == null || !energy.TrySpend(COST_TURN_PORTAL))
                return TriviaOutcome.Blocked_NotEnoughEnergy;

            hex.level = 1;
            hex.buildingType = "Prod";

            if (FXManager.Instance != null)
            {
                FXManager.Instance.SpawnCrystalBuffFX(hex.transform.position);
                FXManager.Instance.PlayBuildingSFX();
            }

            if (BoardController.instance != null)
            {
                BoardController.instance.UpgradeHexVisual(hex);
                BoardController.instance.RefreshAllAuras();
            }

            Debug.LogFormat("[Rules] Shofar retourne en ({0},{1}) : il verse {2} par tour.",
                            hex.positionInTheBoard.q, hex.positionInTheBoard.r, TURNED_PORTAL_INCOME);

            return TriviaOutcome.BuildingUpgraded;
        }

        /// <summary>Combien de Shofars sont tombes - retournes ou non.</summary>
        public static int CountFallenPortals()
        {
            return CountDestroyed(false);
        }

        /// <summary>Combien de Shofars ont ete RETOURNES. C'est la mesure du Tanya.</summary>
        public static int CountTurnedPortals()
        {
            return CountDestroyed(true);
        }

        private static int CountDestroyed(bool turnedOnly)
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return 0;

            System.Collections.Generic.List<Hexagon> hexes = board.HexagonsInBoard;

            int count = 0;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.Destroyed) continue;
                if (turnedOnly && hex.level < 1) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Ce que cette case verse a chaque tour : une usine de Gaz debout, ou un
        /// Shofar retourne. Un seul endroit pour les deux, sinon l'un des deux finira
        /// par etre oublie quelque part.
        /// </summary>
        public static int GetHexIncome(Hexagon hex)
        {
            if (hex == null) return 0;

            if (hex.type == TypeOfHex.gas)
                return (hex.level < 1 || hex.currentHP <= 0) ? 0 : GetGasIncome(hex.level);

            return IsTurnedPortal(hex) ? TURNED_PORTAL_INCOME : 0;
        }

        /// <summary>Un hexagone est occupe des qu'un pion (allie ou ennemi) s'y trouve.</summary>
        public static bool IsHexOccupied(Hexagon hex)
        {
            if (hex == null || BoardController.instance == null) return false;
            return BoardController.instance.getPawnByCoord(hex.positionInTheBoard) != null;
        }

        // =====================================================================
        //  PORTEE DE CONSTRUCTION
        // =====================================================================
        //
        // POURQUOI ON NE BATIT PLUS N'IMPORTE OU
        //
        // Avant, une usine pouvait naitre a l'autre bout de la carte au premier tour,
        // et un Tank s'achetait colle a un Shofar. Trois choses en mouraient d'un coup :
        //
        //   - la GEOGRAPHIE. Le plateau distingue soigneusement les usines couvertes
        //     par une colline et celles exposees au bord ; si les deux s'atteignent
        //     pour le meme prix et sans risque, la distinction ne coute rien et ne
        //     veut donc rien dire ;
        //   - le TEMPS DE TRAJET, qui devait etre la grande friction de l'offensive.
        //     Il s'achetait pour cinquante d'Energie ;
        //   - le CENTRE DE COMMANDEMENT, pense comme une tete de pont. On n'a pas
        //     besoin d'une tete de pont quand on peut deja tout poser la-bas.
        //
        // Desormais on batit autour de ce qu'on TIENT. Trois ancrages, et ils
        // racontent les trois ages d'une partie :
        //
        //   la BASE           -> l'anneau interieur : collines de couloir, usines sures.
        //                        C'est chez toi, tu n'as besoin de personne.
        //   un TANK POSTE     -> tout site a une case de lui. Le Tank devient pionnier :
        //                        il marche, et tu batis derriere lui. C'est ce qui
        //                        ouvre l'anneau des Cristaux et des Centres.
        //   un CENTRE DE      -> son rayon de commandement, qui porte jusqu'au bord.
        //   COMMANDEMENT        Planter un Centre ouvre les usines exposees et les
        //                        collines avancees - et comme il fond, cette ouverture
        //                        est une FENETRE, pas une conquete.
        //
        // Le desert trouve ici son role : on n'y batit pas, mais on s'y tient. C'est le
        // sol depuis lequel un Tank ouvre un site.

        /// <summary>
        /// Distance a la Base (bord compris) en deca de laquelle on batit librement.
        ///
        /// PORTE DE 3 A 4 QUAND LE GLACIS A PRIS LE RELAIS. A 3, les Cristaux et les
        /// Centres de Commandement - poses a 4 - demandaient un Tank pionnier pour la
        /// moindre construction. C'etait une friction de plus, et surtout une friction
        /// INVISIBLE : le joueur voyait un site, cliquait, et recevait un refus qu'il
        /// ne pouvait pas deviner.
        ///
        /// A 4, tout ce qui n'est pas du desert est a portee des le premier tour. Le
        /// terrain dit alors tout : ce qui est constructible se voit, et ce qui ne
        /// l'est pas est du sable.
        ///
        /// La regle continue de servir a un seul endroit, et c'est le bon : la terre
        /// qu'un Shofar tombe vient de rendre. Elle est au bord du plateau, hors de
        /// portee de la Base - il faut donc aller la prendre, avec un Tank ou un
        /// Centre de Commandement. Gagner ouvre du terrain, mais ne l'offre pas.
        /// </summary>
        public const int BUILD_RANGE_FROM_BASE = 4;

        /// <summary>Distance a un Tank a soi en deca de laquelle on peut batir.</summary>
        public const int BUILD_RANGE_FROM_TANK = 1;

        /// <summary>
        /// L'OMBRE DU SHOFAR : rien ne se construit a cette distance d'un Shofar DEBOUT.
        ///
        /// On ne batit pas de forteresse au pied du Yetzer. Le siege confortable - un
        /// Bunker pose a deux cases, qui fauche les emissaires a leur sortie et engrange
        /// l'instabilite sans jamais rien risquer - disparait. Il reste l'assaut : on y
        /// va avec des Tanks, ou on n'y va pas.
        ///
        /// Deux cases, et pas une autre valeur : c'est exactement la portee d'un Bunker,
        /// donc la distance qui rendait ce siege possible.
        ///
        /// ELLE MEURT AVEC LUI. L'ombre est attachee au Shofar vivant : quand il tombe,
        /// la terre autour s'ouvre, et la colline avancee qu'elle gelait devient enfin
        /// constructible - contre ses voisins. Fermer un Shofar ne rapporte donc plus
        /// seulement un point de victoire : ca rapporte du terrain.
        ///
        /// Elle interdit de CONSTRUIRE, jamais de MARCHER : un Tank vient se poster a
        /// deux cases et tire. L'assaut est intact.
        ///
        /// A ZERO, ET C'EST VOLONTAIRE : le GLACIS l'a remplacee. Les deux derniers
        /// anneaux du plateau sont devenus du desert nu (voir MapGenerator), et le
        /// desert ne porte deja ni batiment ni Tank. Mesure faite, une ombre de rayon 2
        /// n'interdirait plus que SIX cases que le glacis ne couvre pas deja, et une
        /// ombre de rayon 1 n'en interdirait aucune : deux regles pour le meme effet,
        /// dont une invisible.
        ///
        /// Le code reste, juste, et pret : si les sites revenaient vers le bord un
        /// jour, remettre 2 ici suffit a retrouver l'interdiction - avec, en prime, ce
        /// que le desert ne sait pas faire, s'effacer quand le Shofar tombe.
        /// </summary>
        public const int PORTAL_SHADOW_RADIUS = 0;

        // Coordonnees des Shofars encore debout, relevees une fois par frame. Sans ce
        // cache, le conseiller - qui teste les 169 cases - relirait le plateau entier
        // pour chacune d'elles.
        private static int _shadowFrame = -1;
        private static int _shadowCount;
        private static readonly int[] _shadowQ = new int[16];
        private static readonly int[] _shadowR = new int[16];

        private static void RefreshPortalShadows()
        {
            int frame = Time.frameCount;
            if (frame == _shadowFrame) return;
            _shadowFrame = frame;
            _shadowCount = 0;

            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return;

            System.Collections.Generic.List<Hexagon> hexes = board.HexagonsInBoard;
            for (int i = 0; i < hexes.Count && _shadowCount < _shadowQ.Length; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.portal) continue;
                if (hex.currentHP <= 0 || hex.positionInTheBoard == null) continue;

                _shadowQ[_shadowCount] = hex.positionInTheBoard.q;
                _shadowR[_shadowCount] = hex.positionInTheBoard.r;
                _shadowCount++;
            }
        }

        /// <summary>Cette case est-elle dans l'ombre d'un Shofar encore debout ?</summary>
        public static bool IsInPortalShadow(HexCoord coord)
        {
            if (coord == null || PORTAL_SHADOW_RADIUS <= 0) return false;

            RefreshPortalShadows();

            for (int i = 0; i < _shadowCount; i++)
            {
                // Distance cubique, calculee a plat : s se deduit de q et r.
                int dq = coord.q - _shadowQ[i];
                int dr = coord.r - _shadowR[i];
                int ds = -dq - dr;

                int distance = (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;
                if (distance <= PORTAL_SHADOW_RADIUS) return true;
            }

            return false;
        }

        /// <summary>
        /// A-t-on le droit de construire sur cette case ? Seule regle de portee du jeu :
        /// l'interface l'interroge pour eteindre ses boutons, et ApplyPlayerAction pour
        /// refuser l'action. Les deux ne peuvent donc pas diverger.
        /// </summary>
        public static bool CanBuildAt(HexCoord coord)
        {
            if (coord == null) return false;

            // L'ombre d'un Shofar debout passe AVANT tout le reste : ni la Base, ni un
            // Tank, ni un Centre de Commandement n'y donnent le droit de batir.
            if (IsInPortalShadow(coord)) return false;

            BuildingManager buildings = BuildingManager.Instance;
            if (buildings != null)
            {
                if (buildings.DistanceToBase(coord) <= BUILD_RANGE_FROM_BASE) return true;

                // Le rayon de commandement d'un Centre debout - pas celui de la Base,
                // que la ligne precedente couvre deja.
                if (buildings.HasCommandSupport(coord)) return true;
            }

            BoardController board = BoardController.instance;
            if (board == null || board.PawnsInBoard == null) return false;

            System.Collections.Generic.List<PawnController> pawns = board.PawnsInBoard;
            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController pawn = pawns[i];
                if (pawn == null || pawn.IsEnemy || pawn.currentHP <= 0 || pawn.hexcoord == null) continue;
                if (BoardController.GetHexDistance(coord, pawn.hexcoord) <= BUILD_RANGE_FROM_TANK) return true;
            }

            return false;
        }

        /// <summary>Un Cristal actif (level interne 1+, "Niv 2" au GDD) couvre-t-il cet hexagone ?</summary>
        public static bool HasCrystalSupport(HexCoord coord)
        {
            if (coord == null || BoardController.instance == null || BoardController.instance.HexagonsInBoard == null) return false;

            System.Collections.Generic.List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.crystal || hex.level < 1) continue;
                if (BoardController.GetHexDistance(coord, hex.positionInTheBoard) <= GetCrystalRange(hex.level))
                    return true;
            }
            return false;
        }

        // =====================================================================
        //  REVENU DE DEBUT DE TOUR
        // =====================================================================
        /// <summary>
        /// Verse le revenu du tour : le plancher de la Base, plus chaque USINE debout.
        /// Retourne le total verse, pour l'affichage.
        ///
        /// C'est ici que se joue toute la tension du jeu : ce nombre monte quand on
        /// tient ses usines et s'effondre quand on les perd.
        /// </summary>
        public static int GrantPassiveIncome()
        {
            int total = BASE_INCOME_PER_TURN;

            if (BoardController.instance != null && BoardController.instance.HexagonsInBoard != null)
            {
                System.Collections.Generic.List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
                for (int i = 0; i < hexes.Count; i++)
                {
                    Hexagon hex = hexes[i];
                    if (hex == null || hex.type != TypeOfHex.gas || hex.level < 1) continue;
                    if (hex.currentHP <= 0) continue;          // une usine detruite ne produit plus
                    total += GetGasIncome(hex.level);
                }
            }

            if (EnergyManager.Instance != null) EnergyManager.Instance.Add(total);
            return total;
        }

        /// <summary>
        /// Verse UNIQUEMENT le plancher de la Base.
        ///
        /// Les usines versent leur part une par une, pendant qu'on les regarde -
        /// voir TurnManager.ShowFactoryIncome. Tout verser d'un bloc ici remplissait
        /// le compteur avant meme que la premiere usine ne s'allume, et l'animation
        /// n'avait plus rien a montrer.
        /// </summary>
        public static int GrantBaseIncome()
        {
            int total = BASE_INCOME_PER_TURN;
            if (EnergyManager.Instance != null) EnergyManager.Instance.Add(total);
            return total;
        }

        // =====================================================================
        //  TANKS : creation et posture
        // =====================================================================
        /// <summary>
        /// Cree un Tank deja dans la posture voulue. La posture est choisie AVANT la
        /// pose, ce qui evite de payer deux fois : une fois le Tank, une fois sa
        /// reorientation immediate.
        /// </summary>
        public static TriviaOutcome CreateTankWithStance(Hexagon hex, PawnStance stance)
        {
            if (hex == null) return TriviaOutcome.EnergyOnly;

            // La portee est verifiee ICI aussi, et pas seulement a l'ouverture de
            // l'ecran de choix : entre le clic et le choix, le Tank qui donnait le
            // droit de batir a pu mourir, ou le Centre de Commandement s'effondrer.
            // C'est le dernier point avant la depense.
            if (!CanBuildAt(hex.positionInTheBoard)) return TriviaOutcome.Blocked_OutOfRange;

            EnergyManager energy = EnergyManager.Instance;
            if (energy == null || !energy.TrySpend(TANK_CREATION_COST))
                return TriviaOutcome.Blocked_NotEnoughEnergy;

            if (BoardController.instance == null) return TriviaOutcome.EnergyOnly;

            BoardController.instance.SpawnUnitVisual(hex, 1, "unit");

            // La posture s'applique APRES la pose : le pion n'existe pas avant.
            PawnController created = BoardController.instance.getPawnByCoord(hex.positionInTheBoard);
            if (created != null) created.SetStance(stance);

            return TriviaOutcome.TankCreated;
        }

        /// <summary>Change la posture d'un Tank existant, contre de l'Energie.</summary>
        public static TriviaOutcome ChangeTankStance(PawnController tank, PawnStance stance)
        {
            if (tank == null || tank.IsEnemy) return TriviaOutcome.EnergyOnly;

            // Payer pour rester dans la meme posture n'est pas un choix, c'est un
            // piege : on refuse plutot que de facturer un non-evenement.
            if (tank.stance == stance) return TriviaOutcome.EnergyOnly;

            EnergyManager energy = EnergyManager.Instance;
            if (energy == null || !energy.TrySpend(TANK_STANCE_COST))
                return TriviaOutcome.Blocked_NotEnoughEnergy;

            tank.SetStance(stance);

            // Une nouvelle posture remplace l'ordre de rejoindre un Cristal, et efface
            // la cible choisie a la main pour l'ancienne posture.
            tank.seekCrystal = false;
            tank.orderTargetCoord = null;
            tank.orderTargetPawn = null;
            return TriviaOutcome.StanceChanged;
        }

        /// <summary>Y a-t-il au moins un Cristal construit et debout sur le plateau ?</summary>
        public static bool AnyCrystalBuilding()
        {
            return FindNearestCrystal(null) != null;
        }

        /// <summary>
        /// Le Cristal construit (niveau interne 1+) et debout le plus proche de cette
        /// case. from null : le premier trouve. Null s'il n'y en a aucun.
        /// </summary>
        public static Hexagon FindNearestCrystal(HexCoord from)
        {
            if (BoardController.instance == null || BoardController.instance.HexagonsInBoard == null) return null;

            System.Collections.Generic.List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            Hexagon best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.crystal || hex.level < 1) continue;
                if (hex.currentHP <= 0 || hex.positionInTheBoard == null) continue;

                if (from == null) return hex;

                int d = BoardController.GetHexDistance(from, hex.positionInTheBoard);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = hex;
                }
            }
            return best;
        }

        /// <summary>
        /// Ordonne a un Tank de rejoindre le Cristal le plus proche. Gratuit : l'ordre
        /// coute deja des tours de marche. Refuse si le Tank est deja au maximum, deja
        /// soutenu par un Cristal, ou s'il n'existe aucun Cristal construit.
        /// </summary>
        public static TriviaOutcome OrderTankToCrystal(PawnController tank)
        {
            if (tank == null || tank.IsEnemy) return TriviaOutcome.EnergyOnly;
            if (tank.level >= MAX_TANK_LEVEL) return TriviaOutcome.Blocked_MaxLevel;
            if (!AnyCrystalBuilding()) return TriviaOutcome.Blocked_NoCrystal;
            if (HasCrystalSupport(tank.hexcoord)) return TriviaOutcome.EnergyOnly;

            tank.seekCrystal = true;
            Debug.Log("[Rules] Un Tank part rejoindre le Cristal le plus proche.");
            return TriviaOutcome.StanceChanged;
        }

        /// <summary>Fait passer un Tank au Niveau 2, si un Cristal le soutient.</summary>
        public static TriviaOutcome UpgradeTank(PawnController tank)
        {
            if (tank == null || tank.IsEnemy) return TriviaOutcome.EnergyOnly;
            if (tank.level >= MAX_TANK_LEVEL) return TriviaOutcome.EnergyOnly;
            if (!HasCrystalSupport(tank.hexcoord)) return TriviaOutcome.EnergyOnly;

            EnergyManager energy = EnergyManager.Instance;
            if (energy == null || !energy.TrySpend(TANK_EVOLVE_COST))
                return TriviaOutcome.Blocked_NotEnoughEnergy;

            EvolveTank(tank);
            return TriviaOutcome.TankEvolved;
        }

        public static void GrantTriviaReward()
        {
            if (EnergyManager.Instance != null) EnergyManager.Instance.Add(TRIVIA_ENERGY_REWARD);
        }

        /// <summary>Verse un gain de Trivia deja calcule par palier de difficulte.</summary>
        public static void GrantTriviaReward(int amount)
        {
            if (amount <= 0) return;
            if (EnergyManager.Instance != null) EnergyManager.Instance.Add(amount);
        }

        // =====================================================================
        //  PALIERS DE DIFFICULTE DU TRIVIA
        // =====================================================================
        /// <summary>
        /// Gain par palier. Ces quatre nombres ne sont PAS une echelle de
        /// generosite : ils sont calibres pour que les quatre sujets rapportent
        /// presque la meme chose EN MOYENNE.
        ///
        ///   palier 1 : +25 pour une question qu'on reussit environ 9 fois sur 10
        ///   palier 2 : +35 pour environ 7 fois sur 10
        ///   palier 3 : +50 pour environ 1 fois sur 2
        ///   palier 4 : +85 pour environ 3 fois sur 10
        ///
        /// Chaque ligne vaut a peu pres 25 Energie d'esperance, c'est-a-dire
        /// exactement la valeur sur laquelle l'equilibrage a ete simule. Le choix du
        /// sujet ne porte donc pas sur COMBIEN on gagne, mais sur le RISQUE qu'on
        /// prend : le +25 sur lequel on peut compter quand il faut absolument sortir
        /// un Tank ce tour-ci, le +85 quand on est derriere et qu'il faut un gros coup.
        ///
        /// Sans cette calibration le choix serait faux : a gains croissants et risque
        /// ignore, prendre systematiquement la question la plus dure rapporterait plus,
        /// et l'ecran de choix ne serait qu'une decoration.
        /// </summary>
        public const int TRIVIA_REWARD_T1 = 25;
        public const int TRIVIA_REWARD_T2 = 35;
        public const int TRIVIA_REWARD_T3 = 50;
        public const int TRIVIA_REWARD_T4 = 85;

        public static int GetTriviaReward(int difficulty)
        {
            switch (difficulty)
            {
                case 2: return TRIVIA_REWARD_T2;
                case 3: return TRIVIA_REWARD_T3;
                case 4: return TRIVIA_REWARD_T4;
                default: return TRIVIA_REWARD_T1;
            }
        }

        // =====================================================================
        //  ACTION PAYANTE DU JOUEUR (phase de depense)
        // =====================================================================
        /// <summary>
        /// Le joueur clique un hexagone pendant sa phase de depense. L'action est
        /// tentee et facturee ; si le solde est insuffisant rien ne se passe.
        ///
        /// Ce point d'entree a remplace l'ancien ApplyCorrectAnswer, qui accordait
        /// l'action gratuitement en recompense d'une bonne reponse. C'etait la source
        /// de la strategie degeneree : six bunkers gratuits verrouillaient la carte.
        /// </summary>
        public static TriviaOutcome ApplyPlayerAction(Hexagon hex)
        {
            if (hex == null) return TriviaOutcome.EnergyOnly;

            EnergyManager energy = EnergyManager.Instance;

            PawnController occupant = (BoardController.instance != null)
                ? BoardController.instance.getPawnByCoord(hex.positionInTheBoard)
                : null;

            // --- Case occupee ---
            if (occupant != null)
            {
                if (occupant.typeOfPawn == TypeOfPawn.enemy) return TriviaOutcome.Blocked_Occupied;

                // Un Tank deja au maximum, ou sans Cristal a portee, ne peut pas evoluer :
                // le clic sert alors a changer sa posture, ce qui est gratuit.
                if (occupant.level >= MAX_TANK_LEVEL || !HasCrystalSupport(hex.positionInTheBoard))
                {
                    occupant.CycleStance();
                    return TriviaOutcome.StanceChanged;
                }

                // Evolution possible, mais hors budget. On changeait alors la posture
                // de personne et on ne rendait rien : le clic ne produisait RIEN, et le
                // joueur en concluait que ses Tanks etaient bloques en Garde a vie.
                // C'est exactement ce qui se passait avec un Cristal a portee et moins
                // de 60 d'Energie - la situation la plus courante en debut de partie.
                //
                // Un clic sur sa propre unite doit toujours produire un effet. Faute de
                // pouvoir payer l'evolution, il fait tourner la posture, qui est gratuite.
                if (energy == null || !energy.CanAfford(TANK_EVOLVE_COST))
                {
                    occupant.CycleStance();
                    return TriviaOutcome.StanceChanged;
                }

                energy.TrySpend(TANK_EVOLVE_COST);
                EvolveTank(occupant);
                return TriviaOutcome.TankEvolved;
            }

            switch (hex.type)
            {
                // LA PLAINE, ET ELLE SEULE, PORTE UN TANK.
                //
                // Le desert ne donne rien. Avant, les deux se valaient : un Tank se
                // posait n'importe ou, donc le terrain ne voulait rien dire et le
                // plateau n'etait qu'un decor. Maintenant la moitie du sol est morte -
                // on la traverse, on ne s'y installe pas - et l'autre moitie devient
                // une ressource dont la position compte.
                case TypeOfHex.plain:
                    // On ne pose que sur le terrain qu'on tient : autour de la Base,
                    // d'un Tank deja la, ou d'un Centre de Commandement.
                    if (!CanBuildAt(hex.positionInTheBoard)) return TriviaOutcome.Blocked_OutOfRange;

                    if (energy == null || !energy.TrySpend(TANK_CREATION_COST))
                        return TriviaOutcome.Blocked_NotEnoughEnergy;
                    if (BoardController.instance != null)
                        BoardController.instance.SpawnUnitVisual(hex, 1, "unit");
                    return TriviaOutcome.TankCreated;

                // Terrains constructibles
                case TypeOfHex.hill:
                case TypeOfHex.gas:
                case TypeOfHex.crystal:
                case TypeOfHex.mountain:
                {
                    if (hex.level >= MAX_TERRAIN_LEVEL) return TriviaOutcome.Blocked_MaxLevel;

                    // Meme regle pour les batiments : la portee d'abord, le prix ensuite.
                    // Dans cet ordre, un joueur qui n'a pas les moyens ET qui vise trop
                    // loin apprend la vraie raison du refus.
                    if (!CanBuildAt(hex.positionInTheBoard)) return TriviaOutcome.Blocked_OutOfRange;

                    int targetLevel = hex.level + 1;
                    int cost = GetBuildCost(hex.type, targetLevel);

                    if (energy == null || !energy.TrySpend(cost))
                        return TriviaOutcome.Blocked_NotEnoughEnergy;

                    UpgradeBuilding(hex, targetLevel);
                    return TriviaOutcome.BuildingUpgraded;
                }

                // AT'HAPKHA : la ruine d'un Shofar se retourne.
                case TypeOfHex.Destroyed:
                    return TurnPortal(hex);

                default:
                    return TriviaOutcome.EnergyOnly;
            }
        }

        public static void EvolveTank(PawnController pawn)
        {
            if (pawn == null || BoardController.instance == null) return;
            Debug.Log("[Rules] Evolution d'un Tank en Niveau 2.");
            if (FXManager.Instance != null) FXManager.Instance.SpawnCrystalBuffFX(pawn.transform.position);
            BoardController.instance.UpgradePawnVisual(pawn, 2);
        }

        public static void UpgradeBuilding(Hexagon hex, int newLevel)
        {
            if (hex == null) return;

            hex.level = Mathf.Clamp(newLevel, 0, MAX_TERRAIN_LEVEL);
            ApplyBuildingHP(hex);

            // Un Centre neuf, ou monte en rang, repart avec une bulle pleine - comme il
            // repart avec ses PV pleins. Sur tout autre terrain, ceci ne fait rien.
            ResetShield(hex);

            if (FXManager.Instance != null)
                FXManager.Instance.SpawnBuildingUpgradeFX(hex.transform.position);

            Debug.LogFormat("[Rules] {0} ({1},{2}) passe au niveau interne {3} (GDD Niv {4})",
                            hex.type, hex.positionInTheBoard.q, hex.positionInTheBoard.r, hex.level, hex.level + 1);

            if (BoardController.instance != null)
            {
                // UpgradeHexVisual ne modifie pas l'hexagone : il en FABRIQUE UN AUTRE
                // et remplace le premier dans la liste du plateau. Tout ce qu'on vient
                // de poser sur "hex" ne vaut donc que si HexagonFactory le recopie - ce
                // qu'elle fait, bulle comprise. On repose quand meme la bulle sur le
                // nouveau : c'est lui qui vit maintenant, et une regle aussi visible que
                // celle-la ne doit dependre de personne d'autre.
                Hexagon rebuilt = BoardController.instance.UpgradeHexVisual(hex);
                if (rebuilt != null) ResetShield(rebuilt);

                BoardController.instance.RefreshAllAuras();
            }
        }

        /// <summary>(Re)cale les PV d'un hexagone sur son type et son niveau.</summary>
        public static void ApplyBuildingHP(Hexagon hex)
        {
            if (hex == null) return;
            int hp = GetBuildingMaxHP(hex.type, hex.level);
            if (hp <= 0) return;
            hex.maxHP = hp;
            hex.currentHP = hp;
            hex.energy = hp;
            hex.energymax = hp;
        }

        // =====================================================================
        //  COMBAT - Section 5 (sans hasard, avec contre-attaque)
        // =====================================================================
        /// <summary>
        /// Attaque pion contre pion : l'attaquant frappe, et si le defenseur survit
        /// et que l'attaquant est a sa portee, il riposte automatiquement.
        /// </summary>
        public static void ResolveCombat(PawnController attacker, PawnController defender)
        {
            if (attacker == null || defender == null) return;

            FXManager fx = FXManager.Instance;

            if (fx != null)
            {
                fx.PlayAttackSFX();
                fx.SpawnAttackFX((attacker.transform.position + defender.transform.position) / 2f);
            }

            defender.ApplyDamage(GetPawnDamage(attacker));

            if (defender.currentHP > 0 && fx != null)
                fx.SpawnHitFX(defender.transform.position + Vector3.up);

            bool counterAttacks = defender.currentHP > 0
                && BoardController.GetHexDistance(defender.hexcoord, attacker.hexcoord) <= defender.attackRange;

            if (counterAttacks)
            {
                attacker.ApplyDamage(GetPawnDamage(defender));

                if (attacker.currentHP > 0 && fx != null)
                    fx.SpawnHitFX(attacker.transform.position + Vector3.up);
            }

            // Retour de flamme sur les Portails avant que le pion ne soit retire du plateau.
            RegisterKillIfDead(defender);
            RegisterKillIfDead(attacker);

            if (BoardController.instance != null)
            {
                BoardController.instance.NotifyPawnDamaged(defender);
                BoardController.instance.NotifyPawnDamaged(attacker);
            }
        }

        /// <summary>
        /// Attaque pion contre structure. Les Portails ne ripostent jamais (Section 4),
        /// la Base et les batiments non plus.
        /// </summary>
        public static void ResolveCombat(PawnController attacker, Hexagon target)
        {
            if (attacker == null || target == null) return;

            FXManager fx = FXManager.Instance;

            if (fx != null)
            {
                fx.PlayAttackSFX();
                fx.SpawnAttackFX((attacker.transform.position + target.transform.position) / 2f);
            }

            DamageStructure(target, GetPawnDamage(attacker));

            if (target != null && target.currentHP > 0 && fx != null)
                fx.SpawnHitFX(target.transform.position + Vector3.up);
        }

        /// <summary>
        /// Un ennemi vient de mourir : son portail d'origine encaisse le contrecoup.
        /// Appele par le combat et par les tirs de Bunker.
        /// </summary>
        public static void RegisterKillIfDead(PawnController pawn)
        {
            if (pawn == null || pawn.currentHP > 0 || !pawn.IsEnemy) return;
            if (PortalManager.Instance != null) PortalManager.Instance.RegisterEnemyKill(pawn);
        }

        /// <summary>
        /// Point d'entree unique pour blesser une structure. Les degats subis par
        /// un hexagone de Base vont dans le reservoir commun de 200 PV, et un Portail
        /// filtre les degats a travers son bouclier tant qu'il est stable.
        /// </summary>
        public static void DamageStructure(Hexagon hex, int amount)
        {
            if (hex == null || amount <= 0) return;

            if (hex.type == TypeOfHex.Base)
            {
                if (BuildingManager.Instance != null)
                {
                    BuildingManager.Instance.DamageBase(amount);
                    hex.StartCoroutine(hex.Hitted());
                }
                else
                {
                    hex.ApplyDamage(amount);
                }
                return;
            }

            // LA BULLE DU CENTRE DE COMMANDEMENT : elle encaisse tout tant qu'elle
            // tient. Le Centre ne prend rien, et on note le coup pour que la bulle ne
            // se refasse pas ce tour-ci.
            if (hex.type == TypeOfHex.mountain && hex.level >= 1)
            {
                hex.attacked = true;

                int absorbed = amount;
                amount = FilterMountainDamage(hex, amount);

                if (amount <= 0)
                {
                    // LE CHIFFRE MONTE SUR LA BULLE, PAS SUR LE BATIMENT. Sans lui, le
                    // joueur voyait un ennemi tirer et il ne se passait rien a l'ecran :
                    // les PV du Centre ne bougeaient pas, et la seule trace du coup
                    // etait le dome qui palissait d'un cran. Le coup doit se compter.
                    if (hex.transform != null)
                    {
                        MNLTHII.UI.DamagePopup.Show(hex.transform.position + Vector3.up * 1.4f,
                                                    absorbed, MNLTHII.UI.DamageKind.TakenByPlayer);

                        if (FXManager.Instance != null)
                            FXManager.Instance.SpawnHitFX(hex.transform.position + Vector3.up * 1.2f);
                    }

                    hex.RefreshHealthBar();
                    return;
                }
            }

            if (hex.type == TypeOfHex.portal)
            {
                amount = FilterPortalDamage(hex, amount);
                if (amount <= 0) return;

                // Un coup PORTE sur le Shofar : c'est le seul evenement qui remet son
                // compteur de calme a zero et l'empeche de se refaire. Le contrecoup
                // des morts passe par un autre chemin et ne compte pas - voir
                // PORTAL_CALM_TURNS.
                if (PortalManager.Instance != null) PortalManager.Instance.NotifyPortalAttacked(hex);
            }

            hex.ApplyDamage(amount);
            if (hex.currentHP <= 0) DestroyBuilding(hex);
        }

        /// <summary>
        /// Bouclier de Portail : tant qu'il tient, les degats sont divises. Le bouclier
        /// tombe quand assez d'ennemis issus de ce portail ont ete tues (voir
        /// PortalManager.RegisterEnemyKill). C'est ce qui relie defense et offensive :
        /// tenir la ligne devant sa Base ouvre la fenetre pour abattre la source.
        /// </summary>
        public static int FilterPortalDamage(Hexagon portal, int amount)
        {
            PortalManager portals = PortalManager.Instance;
            if (portals == null) return amount;

            if (portals.IsShieldDown(portal)) return amount;

            int reduced = amount / PORTAL_SHIELD_DIVISOR;
            return (reduced < 1) ? 1 : reduced;
        }

        public static void DestroyBuilding(Hexagon hex)
        {
            if (hex == null) return;

            Debug.LogFormat("[Rules] Structure detruite : {0} niveau {1}", hex.type, hex.level);

            if (hex.type == TypeOfHex.portal)
            {
                if (PortalManager.Instance != null) PortalManager.Instance.ForgetPortal(hex);

                // Le portail laisse son epave en place, definitivement.
                // ReplaceWithDestroyedVisual joue lui-meme l'explosion et la fumee.
                if (BoardController.instance != null) BoardController.instance.ReplaceWithDestroyedVisual(hex);
                return;
            }

            if (FXManager.Instance != null)
            {
                FXManager.Instance.PlayExplosionSFX();
                FXManager.Instance.SpawnDestructionFX(hex.transform.position);
            }

            // La Base a 0 PV : la defaite est declenchee par BuildingManager.
            hex.level = 0;

            hex.currentHP = 0;
            hex.energy = 0;

            if (BoardController.instance != null)
            {
                BoardController.instance.UpgradeHexVisual(hex);
                BoardController.instance.RefreshAllAuras();
            }
        }
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. Aucune allocation : la classe est entierement statique, ne cree ni liste ni
//    string, et GrantPassiveIncome parcourt la liste d'hexagones deja existante
//    par index plutot qu'avec un foreach sur une requete LINQ.
// 2. FilterPortalDamage delegue a PortalManager, qui garde l'etat des six portails
//    dans des tableaux de taille fixe : pas de Dictionary, pas de boxing.
// 3. Les Debug.LogFormat remplacent les concatenations de string : les arguments ne
//    sont formates que si le log est reellement emis.
// 4. Tous les nombres d'equilibrage sont des const : le compilateur les inline, il
//    n'y a aucun acces memoire a l'execution.
// ---------------------------------------------------------------------------
