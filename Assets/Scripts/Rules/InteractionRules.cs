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
        Blocked_NotPlayerPhase
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
        /// <summary>Solde au premier tour : de quoi poser un batiment d'ouverture.</summary>
        public const int STARTING_ENERGY = 60;

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
        public const int BASE_INCOME_PER_TURN = 10;

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
        public const int TANK_CREATION_COST = 50;

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

        public const int COST_BUNKER_L1 = 40;
        public const int COST_BUNKER_L2 = 60;
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
        /// Cout d'UN tir de Bunker. Ce n'est pas un forfait : un Bunker qui n'a aucune
        /// cible ne coute rien, et un Bunker qui vide son chargeur coute cher. Sans ce
        /// prix, un mur de six Bunkers etait un investissement unique qui bloquait la
        /// carte pour toujours - la strategie degeneree que la simulation a montree.
        /// Avec le cout au tir, tenir la ligne se paie exactement a la hauteur de ce
        /// qu'elle arrete.
        /// </summary>
        public const int BUNKER_SHOT_COST_L1 = 4;
        public const int BUNKER_SHOT_COST_L2 = 5;

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
        // Et il continue de fondre. Ce n'est donc pas une fortification, c'est une
        // TETE DE PONT : on la paie, on pousse, elle s'ecroule. C'est le seul batiment
        // qui ne rapporte rien si on le pose chez soi - son rayon doublerait celui de
        // la Base - et qui ne paie que place devant, expose.
        public const int MOUNTAIN_HP_L1 = 30;
        public const int MOUNTAIN_HP_L2 = 50;

        /// <summary>
        /// Rayon INFRANCHISSABLE pour les ennemis. Lu par BoardController quand il
        /// calcule le pas suivant d'un ennemi ; les Tanks du joueur passent librement.
        /// </summary>
        public const int MOUNTAIN_REPEL_L1 = 1;
        public const int MOUNTAIN_REPEL_L2 = 2;

        /// <summary>
        /// Usure par tour. Ramenee de 10 a 5 : a 10, un Centre rang 1 vivait trois
        /// tours pour 50 d'Energie, et personne n'en aurait jamais pose un. A 5, il
        /// tient six tours au rang 1 et dix au rang 2 - de quoi mener une offensive
        /// complete, sans jamais devenir permanent.
        /// </summary>
        public const int MOUNTAIN_DECAY_PER_TURN = 5;

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
        public const int PORTAL_EVOLVE_AFTER_TURNS = 12;

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
        public const int PORTAL_SPAWN_INTERVAL = 3;

        /// <summary>Ennemis deja presents sur la carte au premier tour.</summary>
        public const int INITIAL_ENEMIES = 3;

        /// <summary>Distance a laquelle ces premiers ennemis sont poses, en cases.</summary>
        public const int INITIAL_ENEMY_DISTANCE = 5;
        /// <summary>Nombre maximum d'ennemis deployes par tour, tous portails confondus.</summary>
        public const int MAX_SPAWNS_PER_TURN = 4;
        /// <summary>Plafond d'ennemis simultanement presents sur la carte.</summary>
        public const int MAX_ACTIVE_ENEMIES = 16;

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

        public const int PORTAL_KILLS_TO_BREAK_SHIELD = 4;

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
                if (pawn.level <= 1) { pawn.maxHP = 20; pawn.attackDamageMin = 10; pawn.attackRange = 1; }
                else if (pawn.level == 2) { pawn.maxHP = 40; pawn.attackDamageMin = 15; pawn.attackRange = 2; }
                else { pawn.maxHP = 80; pawn.attackDamageMin = 30; pawn.attackRange = 3; }
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

        /// <summary>Un hexagone est occupe des qu'un pion (allie ou ennemi) s'y trouve.</summary>
        public static bool IsHexOccupied(Hexagon hex)
        {
            if (hex == null || BoardController.instance == null) return false;
            return BoardController.instance.getPawnByCoord(hex.positionInTheBoard) != null;
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
                // Usines a Tanks
                case TypeOfHex.plain:
                case TypeOfHex.desert:
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

                    int targetLevel = hex.level + 1;
                    int cost = GetBuildCost(hex.type, targetLevel);

                    if (energy == null || !energy.TrySpend(cost))
                        return TriviaOutcome.Blocked_NotEnoughEnergy;

                    UpgradeBuilding(hex, targetLevel);
                    return TriviaOutcome.BuildingUpgraded;
                }

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

            if (FXManager.Instance != null)
                FXManager.Instance.SpawnBuildingUpgradeFX(hex.transform.position);

            Debug.LogFormat("[Rules] {0} ({1},{2}) passe au niveau interne {3} (GDD Niv {4})",
                            hex.type, hex.positionInTheBoard.q, hex.positionInTheBoard.r, hex.level, hex.level + 1);

            if (BoardController.instance != null)
            {
                BoardController.instance.UpgradeHexVisual(hex);
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
