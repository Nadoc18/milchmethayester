using System.Collections.Generic;
using UnityEngine;
using MNLTHII.Managers;

namespace MNLTHII.Rules
{
    /// <summary>Ce qu'un conseil propose de faire.</summary>
    public enum AdvisorActionKind
    {
        None,

        /// <summary>Poser un Tank neuf, dans une posture precise.</summary>
        CreateTank,

        /// <summary>Changer la posture d'un Tank deja pose.</summary>
        ChangeStance,

        /// <summary>Faire passer un Tank au Niveau 2.</summary>
        EvolveTank,

        /// <summary>Construire ou monter d'un niveau un terrain.</summary>
        Build
    }

    /// <summary>
    /// Un conseil : quoi faire, ou, combien ca coute, et POURQUOI.
    ///
    /// C'est une classe et non une structure : les conseils vivent dans un tableau
    /// reutilise d'un tour a l'autre, donc ils sont remplis sur place. Des structures
    /// obligeraient a recopier l'objet entier a chaque tri.
    /// </summary>
    public class AdvisorSuggestion
    {
        public AdvisorActionKind kind;

        /// <summary>Ou l'action se produit : la case a batir, ou celle du Tank.</summary>
        public Hexagon hex;

        /// <summary>Le Tank concerne, pour un changement de posture ou une evolution.</summary>
        public PawnController tank;

        /// <summary>Posture visee, dans l'ordre de PawnStance.</summary>
        public int stance;

        public int cost;
        public int score;

        /// <summary>Index dans la table de raisons, cote interface.</summary>
        public int reason;

        /// <summary>
        /// Ce que l'action VISE : le Shofar a fermer, l'ennemi a arreter. Sert a
        /// orienter le fantome et a cadrer la camera. Peut etre nul.
        /// </summary>
        public Hexagon focus;

        public void Clear()
        {
            kind = AdvisorActionKind.None;
            hex = null;
            tank = null;
            stance = 0;
            cost = 0;
            score = 0;
            reason = 0;
            focus = null;
        }
    }

    /// <summary>
    /// LE CONSEILLER.
    ///
    /// Pourquoi il existe : la boucle de ce jeu n'est pas devinable. Tuer des ennemis
    /// use le Shofar qui les a pondus, quatre morts brisent son bouclier, et c'est
    /// PENDANT ces quatre tours-la qu'il faut envoyer un Tank en Hesteharut. Un joueur
    /// qui ne sait pas ca joue une partie ingagnable sans jamais comprendre pourquoi -
    /// il defend correctement, et rien n'avance.
    ///
    /// Le conseiller ne joue pas a la place du joueur : il propose au plus trois
    /// actions, dit ce qu'elles coutent et surtout POURQUOI elles comptent maintenant.
    /// Le joueur reste libre de faire autre chose.
    ///
    /// CE QUI EST PROPOSE, PAR ORDRE D'URGENCE
    ///
    ///   1. Un bouclier vient de tomber - la fenetre offensive est ouverte, elle dure
    ///      quatre tours et ne reviendra qu'apres quatre nouvelles morts.
    ///   2. La Base est menacee - il faut de quoi tenir la ligne.
    ///   3. Un Shofar est au plancher de ses PV - un seul coup le fermerait.
    ///   4. Un Tank est a portee de Cristal - l'evolution double ses degats.
    ///   5. Le revenu est trop faible - sans Energie, aucune de ces actions n'existe.
    ///   6. Aucun Tank ne marche vers un Shofar - la partie ne peut pas etre gagnee.
    ///
    /// Note d'optimisation : le tableau de conseils et les listes de travail sont
    /// statiques et reutilises. Le conseiller n'est appele qu'aux changements d'etat
    /// (ouverture de la phase, action jouee), jamais dans Update - donc rien n'est
    /// alloue image par image.
    /// </summary>
    public static class AdvisorRules
    {
        /// <summary>Au-dela de trois, ce n'est plus un conseil mais une liste de courses.</summary>
        public const int MaxSuggestions = 3;

        // --- identifiants de raison, partages avec l'interface ---
        public const int REASON_SHIELD_DOWN = 0;
        public const int REASON_PORTAL_LOW = 1;
        public const int REASON_BASE_THREAT = 2;
        public const int REASON_SURGE = 3;
        public const int REASON_CRYSTAL = 4;
        public const int REASON_INCOME = 5;
        public const int REASON_NO_ASSAULT = 6;
        public const int REASON_PORTAL_REGEN = 7;
        public const int REASON_BRIDGEHEAD = 8;
        public const int REASON_COUNT = 9;

        private const int MaxCandidates = 24;

        private static readonly AdvisorSuggestion[] _pool = BuildPool();
        private static int _used;

        private static readonly List<Hexagon> _portals = new List<Hexagon>(8);
        private static readonly List<PawnController> _tanks = new List<PawnController>(16);

        private static AdvisorSuggestion[] BuildPool()
        {
            AdvisorSuggestion[] pool = new AdvisorSuggestion[MaxCandidates];
            for (int i = 0; i < MaxCandidates; i++) pool[i] = new AdvisorSuggestion();
            return pool;
        }

        // =================================================================
        //  POINT D'ENTREE
        // =================================================================
        /// <summary>
        /// Remplit output avec au plus MaxSuggestions conseils, du plus urgent au
        /// moins urgent, et retourne combien il y en a.
        ///
        /// Seules les actions REALISABLES sont proposees. Conseiller une action qu'on
        /// ne peut pas payer serait pire qu'un silence : le joueur cliquerait, rien ne
        /// se passerait, et il cesserait de lire le conseiller.
        /// </summary>
        public static int Build(AdvisorSuggestion[] output)
        {
            _used = 0;
            for (int i = 0; i < _pool.Length; i++) _pool[i].Clear();

            BoardController board = BoardController.instance;
            if (board == null || output == null) return 0;

            EnergyManager wallet = EnergyManager.Instance;
            int energy = (wallet != null) ? wallet.CurrentEnergy : 0;

            CollectPortals(board);
            CollectTanks(board);

            ProposeOffensive(energy);
            ProposeBridgehead(board, energy);
            ProposeDefensive(board, energy);
            ProposeEvolution(energy);
            ProposeIncome(board, energy);

            return Rank(output);
        }

        // =================================================================
        //  COLLECTE
        // =================================================================
        private static void CollectPortals(BoardController board)
        {
            _portals.Clear();

            List<Hexagon> hexes = board.HexagonsInBoard;
            if (hexes == null) return;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex != null && hex.type == TypeOfHex.portal && hex.currentHP > 0) _portals.Add(hex);
            }
        }

        private static void CollectTanks(BoardController board)
        {
            _tanks.Clear();

            List<PawnController> pawns = board.PawnsInBoard;
            if (pawns == null) return;

            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController pawn = pawns[i];
                if (pawn == null || pawn.IsEnemy) continue;
                if (pawn.typeOfPawn != TypeOfPawn.unit) continue;
                if (pawn.currentHP <= 0) continue;
                _tanks.Add(pawn);
            }
        }

        // =================================================================
        //  1 ET 3 : L'OFFENSIVE
        // =================================================================
        /// <summary>
        /// La fenetre offensive. Un bouclier rompu vaut au joueur des degats pleins ET
        /// un Shofar muet pendant quatre tours ; c'est le seul moment ou fermer un
        /// portail coute peu. Le manquer, c'est recommencer quatre morts plus loin.
        /// </summary>
        private static void ProposeOffensive(int energy)
        {
            PortalManager portals = PortalManager.Instance;
            if (portals == null) return;

            bool anyAssault = false;
            for (int i = 0; i < _tanks.Count; i++)
                if (_tanks[i].stance == PawnStance.Assault) { anyAssault = true; break; }

            for (int p = 0; p < _portals.Count; p++)
            {
                Hexagon portal = _portals[p];

                bool shieldDown = portals.IsShieldDown(portal);

                // "Au plancher" : le contrecoup des morts l'a amene aussi bas qu'il
                // peut aller. Un seul coup de Tank suffit alors a le fermer.
                int floor = (portal.maxHP * InteractionRules.PORTAL_BACKLASH_FLOOR_PERCENT) / 100;
                bool lowHP = portal.currentHP <= floor + 1;

                if (!shieldDown && !lowHP) continue;

                PawnController best = NearestTank(portal, false);
                if (best == null) continue;

                int distance = BoardController.GetHexDistance(best.hexcoord, portal.positionInTheBoard);

                // Un Shofar qu'on laisse tranquille se refait : les PV arraches
                // reviennent et l'instabilite s'efface. C'est le moment ou le joueur
                // PERD ce qu'il a gagne en defendant, et il ne le voit pas tout seul.
                bool regenerating = portals.IsRegenerating(portal);

                int reason = shieldDown ? REASON_SHIELD_DOWN
                           : (regenerating ? REASON_PORTAL_REGEN : REASON_PORTAL_LOW);

                int urgency = shieldDown ? 1000 : (regenerating ? 760 : 700);

                // Plus le Tank est loin, moins le conseil est realiste : il lui faudra
                // des tours pour arriver, et la fenetre ne dure pas.
                int score = urgency - distance * 20;

                if (shieldDown)
                {
                    int turnsLeft = portals.GetShieldDownTurns(portal);
                    if (turnsLeft > 0 && distance > turnsLeft * 2) score -= 250;
                }

                if (best.stance == PawnStance.Assault)
                {
                    // Deja en route : rien a payer, on se contente de le cadrer pour
                    // que le joueur voie que l'affaire est lancee.
                    continue;
                }

                if (energy < InteractionRules.TANK_STANCE_COST) continue;

                AdvisorSuggestion s = Take();
                if (s == null) return;

                s.kind = AdvisorActionKind.ChangeStance;
                s.tank = best;
                s.hex = HexOf(best);
                s.stance = (int)PawnStance.Assault;
                s.cost = InteractionRules.TANK_STANCE_COST;
                s.reason = reason;
                s.focus = portal;
                s.score = score;
            }

            // Personne ne marche vers un Shofar : la partie ne peut pas etre gagnee,
            // quoi que le joueur fasse par ailleurs. C'est le conseil le plus important
            // du jeu, et c'est celui qui n'existait pas.
            if (anyAssault || _portals.Count == 0) return;
            if (energy < InteractionRules.TANK_STANCE_COST) return;

            Hexagon target = _portals[0];
            PawnController runner = NearestTank(target, false);

            for (int p = 1; p < _portals.Count; p++)
            {
                PawnController candidate = NearestTank(_portals[p], false);
                if (candidate == null) continue;

                if (runner == null)
                {
                    runner = candidate;
                    target = _portals[p];
                    continue;
                }

                int here = BoardController.GetHexDistance(candidate.hexcoord, _portals[p].positionInTheBoard);
                int there = BoardController.GetHexDistance(runner.hexcoord, target.positionInTheBoard);
                if (here < there) { runner = candidate; target = _portals[p]; }
            }

            if (runner == null) return;

            AdvisorSuggestion push = Take();
            if (push == null) return;

            push.kind = AdvisorActionKind.ChangeStance;
            push.tank = runner;
            push.hex = HexOf(runner);
            push.stance = (int)PawnStance.Assault;
            push.cost = InteractionRules.TANK_STANCE_COST;
            push.reason = REASON_NO_ASSAULT;
            push.focus = target;
            push.score = 620;
        }

        // =================================================================
        //  1 bis : LA TETE DE PONT
        // =================================================================
        /// <summary>
        /// Une fenetre est ouverte, mais le Tank le plus proche est trop loin pour en
        /// profiter : c'est le moment du Centre de Commandement.
        ///
        /// Sans lui, le joueur voit "le bouclier est tombe, attaque maintenant" et
        /// constate que son Tank met six tours a arriver. Le conseil serait juste et
        /// inapplicable - la pire espece de conseil. Le Centre change precisement ca :
        /// ses gardes tiennent le terrain devant, et ses Tanks avancent d'une case de
        /// plus. C'est aussi la seule facon d'apprendre au joueur a quoi sert ce
        /// batiment, puisqu'il ne sert a rien pose chez soi.
        /// </summary>
        private static void ProposeBridgehead(BoardController board, int energy)
        {
            PortalManager portals = PortalManager.Instance;
            BuildingManager buildings = BuildingManager.Instance;
            if (portals == null || buildings == null) return;

            int cost = InteractionRules.GetBuildCost(TypeOfHex.mountain, 1);
            if (energy < cost) return;

            for (int p = 0; p < _portals.Count; p++)
            {
                Hexagon portal = _portals[p];
                if (!portals.IsShieldDown(portal)) continue;

                PawnController runner = NearestTank(portal, false);
                if (runner == null) continue;

                // Assez pres pour arriver dans la fenetre : pas besoin de tete de pont.
                int distance = BoardController.GetHexDistance(runner.hexcoord, portal.positionInTheBoard);
                if (distance <= 3) continue;

                Hexagon spot = BridgeheadSpot(board, buildings, portal, cost, energy);
                if (spot == null) continue;

                AdvisorSuggestion s = Take();
                if (s == null) return;

                s.kind = AdvisorActionKind.Build;
                s.hex = spot;
                s.cost = InteractionRules.GetBuildCost(spot.type, spot.level + 1);
                s.reason = REASON_BRIDGEHEAD;
                s.focus = portal;
                s.score = 900;
                return;
            }
        }

        /// <summary>
        /// La Montagne libre la plus proche du Shofar vise, a condition qu'elle soit
        /// devant - plus pres de lui que la Base ne l'est. Une Montagne posee derriere
        /// ne fait que doubler le perimetre de la Base et fondre pour rien.
        /// </summary>
        private static Hexagon BridgeheadSpot(BoardController board, BuildingManager buildings,
                                              Hexagon portal, int cost, int energy)
        {
            List<Hexagon> hexes = board.HexagonsInBoard;
            if (hexes == null) return null;

            int baseDistance = buildings.DistanceToBase(portal.positionInTheBoard);

            Hexagon best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.mountain) continue;
                if (hex.level >= InteractionRules.MAX_TERRAIN_LEVEL) continue;

                int price = InteractionRules.GetBuildCost(TypeOfHex.mountain, hex.level + 1);
                if (price > energy) continue;

                int toPortal = BoardController.GetHexDistance(hex.positionInTheBoard, portal.positionInTheBoard);
                if (toPortal >= baseDistance) continue;      // elle est derriere : inutile
                if (toPortal >= bestDistance) continue;

                best = hex;
                bestDistance = toPortal;
            }

            return best;
        }

        // =================================================================
        //  2 : LA DEFENSE
        // =================================================================
        /// <summary>
        /// Tenir la ligne n'est pas une posture d'attente : chaque mort use le Shofar
        /// d'origine et rapproche la rupture de bouclier. La defense EST l'offensive,
        /// et c'est pour ca qu'elle merite un conseil a part entiere.
        /// </summary>
        private static void ProposeDefensive(BoardController board, int energy)
        {
            BuildingManager buildings = BuildingManager.Instance;
            if (buildings == null) return;

            int threats = CountEnemiesNearBase(board, buildings, 4);
            bool surge = AnySurgeAnnounced();

            if (threats == 0 && !surge) return;

            // Combien de Tanks tiennent deja la ligne ? Un conseil qui repete
            // "construis un Tank" alors que six sont deja postes est un bruit.
            int guards = 0;
            for (int i = 0; i < _tanks.Count; i++)
            {
                if (_tanks[i].stance != PawnStance.Guard) continue;
                if (buildings.DistanceToBase(_tanks[i].hexcoord) <= 4) guards++;
            }

            if (guards >= threats + 1) return;

            // a) poser un Tank de Garde sur la case libre la plus proche de la Base
            if (energy >= InteractionRules.TANK_CREATION_COST)
            {
                Hexagon spot = NearestBuildSpot(board, buildings);
                if (spot != null)
                {
                    AdvisorSuggestion s = Take();
                    if (s == null) return;

                    s.kind = AdvisorActionKind.CreateTank;
                    s.hex = spot;
                    s.stance = (int)PawnStance.Guard;
                    s.cost = InteractionRules.TANK_CREATION_COST;
                    s.reason = surge ? REASON_SURGE : REASON_BASE_THREAT;
                    s.focus = NearestEnemyHex(board, buildings);
                    s.score = (surge ? 900 : 820) + threats * 15;
                }
            }

            // b) une colline libre pres de la Base devient un Bunker : il tire quatre
            //    fois par tour sans jamais bouger, donc il tue sans risquer un Tank.
            int bunkerCost = InteractionRules.GetBuildCost(TypeOfHex.hill, 1);
            if (energy < bunkerCost) return;

            Hexagon hill = NearestHill(board, buildings);
            if (hill == null) return;

            AdvisorSuggestion b = Take();
            if (b == null) return;

            b.kind = AdvisorActionKind.Build;
            b.hex = hill;
            b.cost = bunkerCost;
            b.reason = surge ? REASON_SURGE : REASON_BASE_THREAT;
            b.focus = NearestEnemyHex(board, buildings);
            b.score = (surge ? 860 : 780);
        }

        // =================================================================
        //  4 : L'EVOLUTION
        // =================================================================
        private static void ProposeEvolution(int energy)
        {
            if (energy < InteractionRules.TANK_EVOLVE_COST) return;

            for (int i = 0; i < _tanks.Count; i++)
            {
                PawnController tank = _tanks[i];
                if (tank.level >= InteractionRules.MAX_TANK_LEVEL) continue;
                if (!InteractionRules.HasCrystalSupport(tank.hexcoord)) continue;

                AdvisorSuggestion s = Take();
                if (s == null) return;

                s.kind = AdvisorActionKind.EvolveTank;
                s.tank = tank;
                s.hex = HexOf(tank);
                s.cost = InteractionRules.TANK_EVOLVE_COST;
                s.reason = REASON_CRYSTAL;
                s.focus = null;

                // Un Tank en Hesteharut qui evolue gagne une case de mobilite : il
                // arrivera au Shofar un tour plus tot. C'est le meilleur des 60.
                s.score = (tank.stance == PawnStance.Assault) ? 560 : 500;
                return;   // un seul conseil d'evolution suffit
            }
        }

        // =================================================================
        //  5 : LE REVENU
        // =================================================================
        /// <summary>
        /// Sans Energie, aucun des autres conseils n'existe. On ne le propose que
        /// quand le revenu est reellement faible : monter un Cristal quand on encaisse
        /// deja largement, c'est retarder l'offensive pour rien.
        /// </summary>
        private static void ProposeIncome(BoardController board, int energy)
        {
            int income = InteractionRules.BASE_INCOME_PER_TURN;

            List<Hexagon> hexes = board.HexagonsInBoard;
            if (hexes == null) return;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex != null && hex.type == TypeOfHex.gas && hex.level >= 1 && hex.currentHP > 0)
                    income += InteractionRules.GetGasIncome(hex.level);
            }

            if (income >= 30) return;

            Hexagon best = null;
            int bestCost = int.MaxValue;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.gas) continue;
                if (hex.level >= InteractionRules.MAX_TERRAIN_LEVEL) continue;

                int cost = InteractionRules.GetBuildCost(TypeOfHex.gas, hex.level + 1);
                if (cost > energy || cost >= bestCost) continue;

                best = hex;
                bestCost = cost;
            }

            if (best == null) return;

            AdvisorSuggestion s = Take();
            if (s == null) return;

            s.kind = AdvisorActionKind.Build;
            s.hex = best;
            s.cost = bestCost;
            s.reason = REASON_INCOME;
            s.focus = null;
            s.score = 480 - income * 4;
        }

        // =================================================================
        //  TRI
        // =================================================================
        /// <summary>
        /// Tri par insertion sur au plus trois places. Pas de List.Sort, pas de
        /// comparateur : un comparateur alloue, et trier vingt-quatre elements pour
        /// n'en garder que trois serait du travail perdu.
        /// </summary>
        private static int Rank(AdvisorSuggestion[] output)
        {
            int limit = (output.Length < MaxSuggestions) ? output.Length : MaxSuggestions;
            int kept = 0;

            for (int i = 0; i < _used; i++)
            {
                AdvisorSuggestion candidate = _pool[i];
                if (candidate.kind == AdvisorActionKind.None) continue;

                // Deux conseils sur la MEME case seraient deux fois le meme clic.
                bool duplicate = false;
                for (int k = 0; k < kept; k++)
                {
                    if (output[k].hex == candidate.hex && output[k].kind == candidate.kind)
                    { duplicate = true; break; }
                }
                if (duplicate) continue;

                int slot = kept;
                while (slot > 0 && output[slot - 1].score < candidate.score) slot--;

                if (slot >= limit) continue;

                for (int m = (kept < limit ? kept : limit - 1); m > slot; m--)
                    output[m] = output[m - 1];

                output[slot] = candidate;
                if (kept < limit) kept++;
            }

            return kept;
        }

        // =================================================================
        //  OUTILS
        // =================================================================
        private static AdvisorSuggestion Take()
        {
            if (_used >= _pool.Length) return null;
            return _pool[_used++];
        }

        private static Hexagon HexOf(PawnController pawn)
        {
            if (pawn == null || BoardController.instance == null) return null;
            return BoardController.instance.getHexByCoord(pawn.hexcoord);
        }

        /// <summary>Le Tank le plus proche d'une cible. requireAssault filtre la posture.</summary>
        private static PawnController NearestTank(Hexagon target, bool requireAssault)
        {
            if (target == null) return null;

            PawnController best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < _tanks.Count; i++)
            {
                PawnController tank = _tanks[i];
                if (requireAssault && tank.stance != PawnStance.Assault) continue;

                int distance = BoardController.GetHexDistance(tank.hexcoord, target.positionInTheBoard);
                if (distance >= bestDistance) continue;

                best = tank;
                bestDistance = distance;
            }

            return best;
        }

        private static int CountEnemiesNearBase(BoardController board, BuildingManager buildings, int radius)
        {
            List<PawnController> pawns = board.PawnsInBoard;
            if (pawns == null) return 0;

            int count = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController pawn = pawns[i];
                if (pawn == null || !pawn.IsEnemy || pawn.currentHP <= 0) continue;
                if (buildings.DistanceToBase(pawn.hexcoord) <= radius) count++;
            }
            return count;
        }

        private static Hexagon NearestEnemyHex(BoardController board, BuildingManager buildings)
        {
            List<PawnController> pawns = board.PawnsInBoard;
            if (pawns == null) return null;

            Hexagon best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController pawn = pawns[i];
                if (pawn == null || !pawn.IsEnemy || pawn.currentHP <= 0) continue;

                int distance = buildings.DistanceToBase(pawn.hexcoord);
                if (distance >= bestDistance) continue;

                Hexagon hex = board.getHexByCoord(pawn.hexcoord);
                if (hex == null) continue;

                best = hex;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>Case libre ou l'on peut poser un Tank, la plus proche de la Base.</summary>
        private static Hexagon NearestBuildSpot(BoardController board, BuildingManager buildings)
        {
            List<Hexagon> hexes = board.HexagonsInBoard;
            if (hexes == null) return null;

            Hexagon best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null) continue;
                // Le conseiller ne propose plus le desert : on ne peut plus y poser
                // de Tank.
                if (hex.type != TypeOfHex.plain) continue;
                if (board.getPawnByCoord(hex.positionInTheBoard) != null) continue;

                int distance = buildings.DistanceToBase(hex.positionInTheBoard);

                // Colle a la Base, un Tank bouche le passage sans rien couvrir ;
                // au-dela de quatre cases il ne defend plus rien.
                if (distance < 1 || distance > 4) continue;
                if (distance >= bestDistance) continue;

                best = hex;
                bestDistance = distance;
            }

            return best;
        }

        private static Hexagon NearestHill(BoardController board, BuildingManager buildings)
        {
            List<Hexagon> hexes = board.HexagonsInBoard;
            if (hexes == null) return null;

            Hexagon best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.hill) continue;
                if (hex.level >= InteractionRules.MAX_TERRAIN_LEVEL) continue;

                int distance = buildings.DistanceToBase(hex.positionInTheBoard);
                if (distance > 4 || distance >= bestDistance) continue;

                best = hex;
                bestDistance = distance;
            }

            return best;
        }

        private static bool AnySurgeAnnounced()
        {
            PortalManager portals = PortalManager.Instance;
            if (portals == null) return false;

            for (int i = 0; i < _portals.Count; i++)
                if (portals.IsSurgeAnnounced(_portals[i])) return true;

            return false;
        }
    }
}
