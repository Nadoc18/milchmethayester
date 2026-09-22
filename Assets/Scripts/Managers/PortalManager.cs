using System.Collections.Generic;
using UnityEngine;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// GDD V3 - Section 4 : les Portails et le deploiement ennemi (ticking clock).
    ///
    /// Passe d'equilibrage - trois ajouts :
    ///
    ///   INSTABILITE. Chaque portail retient les ennemis qu'il a deployes. Quand l'un
    ///   d'eux meurt, le portail encaisse PORTAL_KILL_BACKLASH degats et son compteur
    ///   d'instabilite monte. A PORTAL_KILLS_TO_BREAK_SHIELD, le bouclier saute pour
    ///   PORTAL_SHIELD_DOWN_TURNS tours : le portail cesse de deployer et encaisse les
    ///   degats en plein. C'est la fenetre d'assaut, et elle se merite en defendant.
    ///
    ///   VAGUES ANNONCEES. Un portail tire au sort declenche une surge, mais toujours
    ///   annoncee un tour a l'avance (log + FX sur la case). Le joueur ne subit jamais
    ///   une surprise qu'il ne pouvait pas voir venir : il subit un choix difficile.
    ///
    ///   COMPOSITION VARIABLE. Le niveau des ennemis deployes est tire dans une table
    ///   qui se durcit avec le numero de tour, au lieu d'etre fixe.
    ///
    /// L'etat des portails vit ici, indexe par coordonnee, et non sur Hexagon : quand
    /// un portail est detruit, BoardController remplace l'objet Hexagon par son epave.
    /// Un etat porte par la coordonnee survit a ce remplacement.
    /// </summary>
    public class PortalManager : MonoBehaviour
    {
        public static PortalManager Instance { get; private set; }

        [Header("Etat")]
        public int totalEnemiesSpawned = 0;
        public int totalEnemiesKilled = 0;

        [Header("Surge (vague annoncee)")]
        [Tooltip("Une surge est possible a partir de ce tour.")]
        public int firstSurgeTurn = 6;
        [Tooltip("Nombre de tours entre deux annonces de surge.")]
        public int surgeInterval = 5;

        /// <summary>Coordonnee du portail qui surgira au prochain tour, ou null.</summary>
        public HexCoord AnnouncedSurge { get { return (_surgeIndex >= 0) ? _announcedCoord : null; } }

        // ---------------------------------------------------------------
        //  Etat des portails : tableaux plats de taille fixe.
        //  Six portails au depart ; la marge a 16 couvre toute variante de carte
        //  sans jamais reallouer, et evite un Dictionary (hash + boxing de cle).
        // ---------------------------------------------------------------
        private const int MaxTrackedPortals = 16;

        private readonly int[] _coordQ = new int[MaxTrackedPortals];
        private readonly int[] _coordR = new int[MaxTrackedPortals];
        private readonly int[] _kills = new int[MaxTrackedPortals];
        private readonly int[] _shieldDown = new int[MaxTrackedPortals];
        private readonly bool[] _used = new bool[MaxTrackedPortals];

        // ----------------------------------------------------------------
        //  LE PLAN DE DEPLOIEMENT
        // ----------------------------------------------------------------
        //
        // Le niveau des ennemis a deployer est tire A L'AVANCE et memorise, exactement
        // comme la question de Trivia est tiree avant qu'on affiche son gain.
        //
        // Sans ca, l'apercu de menace ne pouvait que DEVINER : il montrait un ennemi de
        // Niveau 1 ou 2 selon le niveau du portail, et le tirage reel au moment du
        // deploiement sortait parfois tout autre chose. Une prevision qui se trompe est
        // pire que pas de prevision : le joueur prepare sa ligne contre trois Niveau 1
        // et recoit un Niveau 3.
        //
        // Le plan est refait a chaque tour, et il est partage : l'apercu le lit, le
        // deploiement le consomme. Les deux racontent donc forcement la meme chose.
        private const int MaxPlannedPerPortal = 1 + InteractionRules.SURGE_EXTRA_SPAWNS;

        private readonly int[] _plannedLevels = new int[MaxTrackedPortals * MaxPlannedPerPortal];
        private readonly int[] _planTurn = new int[MaxTrackedPortals];
        private readonly int[] _planCount = new int[MaxTrackedPortals];

        /// <summary>
        /// Tours consecutifs sans qu'un Tank ait frappe ce Shofar. Passe le seuil, il
        /// se refait - voir InteractionRules.PORTAL_CALM_TURNS.
        /// </summary>
        private readonly int[] _calmTurns = new int[MaxTrackedPortals];

        private int _surgeIndex = -1;          // portail annonce pour le tour suivant
        private HexCoord _announcedCoord;      // instance unique reutilisee, jamais reallouee
        private int _lastSurgeTurn = 0;

        // Tampon reutilise d'un tour a l'autre.
        private readonly List<Hexagon> _portalBuffer = new List<Hexagon>(16);

        // Six voisins reutilises : HexCoord est une classe, donc "new HexCoord[6]"
        // suivi de six "new HexCoord(...)" representait sept allocations par portail
        // et par tour. Les instances sont creees une fois et leurs champs reecrits.
        private readonly HexCoord[] _neighbourScratch = new HexCoord[6];

        private static readonly int[] NeighbourDQ = { 0, 1, 1, 0, -1, -1 };
        private static readonly int[] NeighbourDR = { -1, -1, 0, 1, 1, 0 };
        private static readonly int[] NeighbourDS = { 1, 0, -1, -1, 0, 1 };

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }

            for (int i = 0; i < _neighbourScratch.Length; i++) _neighbourScratch[i] = new HexCoord(0, 0, 0);
            _announcedCoord = new HexCoord(0, 0, 0);
        }

        /// <summary>Remise a zero complete : appelee quand une nouvelle partie demarre.</summary>
        public void ResetPortalState()
        {
            for (int i = 0; i < MaxTrackedPortals; i++)
            {
                _used[i] = false;
                _kills[i] = 0;
                _shieldDown[i] = 0;

                // -1 : aucun tour ne porte ce numero, donc le premier plan sera tire.
                _planTurn[i] = -1;
                _planCount[i] = 0;
                _calmTurns[i] = 0;
            }
            _surgeIndex = -1;
            _lastSurgeTurn = 0;
            totalEnemiesSpawned = 0;
            totalEnemiesKilled = 0;
        }

        // =================================================================
        //  INDEXATION DES PORTAILS
        // =================================================================
        /// <summary>Index de l'etat associe a une coordonnee, en le creant au besoin.</summary>
        private int GetSlot(HexCoord coord, bool createIfMissing)
        {
            if (coord == null) return -1;

            int free = -1;
            for (int i = 0; i < MaxTrackedPortals; i++)
            {
                if (!_used[i]) { if (free < 0) free = i; continue; }
                if (_coordQ[i] == coord.q && _coordR[i] == coord.r) return i;
            }

            if (!createIfMissing || free < 0) return -1;

            _used[free] = true;
            _coordQ[free] = coord.q;
            _coordR[free] = coord.r;
            _kills[free] = 0;
            _shieldDown[free] = 0;
            return free;
        }

        /// <summary>Le portail est detruit : sa case redevient anonyme.</summary>
        public void ForgetPortal(Hexagon portal)
        {
            if (portal == null) return;
            int slot = GetSlot(portal.positionInTheBoard, false);
            if (slot >= 0) _used[slot] = false;
            if (slot >= 0 && slot == _surgeIndex) _surgeIndex = -1;
        }

        /// <summary>Le bouclier de ce portail est-il tombe ?</summary>
        /// <summary>
        /// Ajoute des fissures a un Shofar sans passer par une mort.
        ///
        /// C'est la recompense des enseignements : comprendre le Yetzer Hara l'ebranle
        /// exactement comme abattre ses emissaires, et ca remplit la MEME jauge. Si le
        /// seuil est atteint, le bouclier se rompt ici aussi - il n'y a pas deux facons
        /// de le briser.
        ///
        /// Le compteur de calme est remis a zero : une fissure est une attaque.
        /// </summary>
        public void AddInstability(Hexagon portal, int amount)
        {
            if (portal == null || amount <= 0) return;

            int slot = GetSlot(portal.positionInTheBoard, true);
            if (slot < 0) return;

            _kills[slot] += amount;
            _calmTurns[slot] = 0;

            Debug.LogFormat("[Portal] ({0},{1}) se fissure : {2}/{3}.",
                            portal.positionInTheBoard.q, portal.positionInTheBoard.r,
                            _kills[slot], InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD);

            if (_kills[slot] >= InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD) BreakShield(portal, slot);

            portal.RefreshHealthBar();
        }

        /// <summary>
        /// Un Tank vient de frapper ce Shofar. Appele par InteractionRules au seul
        /// endroit ou un degat STRUCTUREL est applique a un portail.
        /// </summary>
        public void NotifyPortalAttacked(Hexagon portal)
        {
            if (portal == null) return;

            int slot = GetSlot(portal.positionInTheBoard, true);
            if (slot < 0) return;

            _calmTurns[slot] = 0;
        }

        /// <summary>
        /// Depuis combien de tours ce Shofar n'a pas ete frappe. L'interface s'en sert
        /// pour prevenir que l'avance accumulee est en train de s'effacer.
        /// </summary>
        public int GetCalmTurns(Hexagon portal)
        {
            if (portal == null) return 0;
            int slot = GetSlot(portal.positionInTheBoard, false);
            return (slot >= 0) ? _calmTurns[slot] : 0;
        }

        /// <summary>Vrai quand ce Shofar est en train de se refaire.</summary>
        public bool IsRegenerating(Hexagon portal)
        {
            if (portal == null || portal.currentHP <= 0) return false;
            return GetCalmTurns(portal) >= InteractionRules.PORTAL_CALM_TURNS;
        }

        public bool IsShieldDown(Hexagon portal)
        {
            if (portal == null) return false;
            int slot = GetSlot(portal.positionInTheBoard, false);
            return slot >= 0 && _shieldDown[slot] > 0;
        }

        /// <summary>Nombre de morts deja portees au compte de ce portail (0 a 5).</summary>
        public int GetInstability(Hexagon portal)
        {
            if (portal == null) return 0;
            int slot = GetSlot(portal.positionInTheBoard, false);
            return slot >= 0 ? _kills[slot] : 0;
        }

        /// <summary>
        /// Combien de tours le bouclier reste-t-il rompu ? 0 = il tient.
        /// Lecture seule, pour l'affichage : l'UI ne doit jamais toucher a l'etat.
        /// </summary>
        public int GetShieldDownTurns(Hexagon portal)
        {
            if (portal == null) return 0;
            int slot = GetSlot(portal.positionInTheBoard, false);
            return slot >= 0 ? _shieldDown[slot] : 0;
        }

        /// <summary>
        /// Tours restants avant que ce portail ne passe au Niveau 2.
        /// Renvoie -1 s'il est deja au maximum. Purement informatif.
        /// </summary>
        public int GetTurnsUntilEvolve(Hexagon portal)
        {
            if (portal == null) return -1;
            if (portal.level >= InteractionRules.MAX_PORTAL_LEVEL) return -1;

            int slot = GetSlot(portal.positionInTheBoard, false);
            int remaining = GetEvolveTurn(slot) - portal.turnsAlive;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>Ce portail declenchera-t-il une surge au prochain tour ?</summary>
        public bool IsSurgeAnnounced(Hexagon portal)
        {
            if (portal == null || _surgeIndex < 0) return false;
            int slot = GetSlot(portal.positionInTheBoard, false);
            return slot == _surgeIndex;
        }

        // =================================================================
        //  INSTABILITE : un ennemi meurt, son portail encaisse
        // =================================================================
        /// <summary>
        /// Appele par InteractionRules quand un ennemi tombe. Le portail qui l'a
        /// deploye perd des PV et se rapproche de la rupture de bouclier.
        /// </summary>
        public void RegisterEnemyKill(PawnController enemy)
        {
            if (enemy == null || !enemy.IsEnemy) return;

            totalEnemiesKilled++;

            if (!enemy.HasOriginPortal) return;
            if (BoardController.instance == null) return;

            // Retrouve l'hexagone du portail d'origine par ses coordonnees memorisees.
            Hexagon portal = FindPortalAt(enemy.originPortalQ, enemy.originPortalR);
            if (portal == null) return;

            int slot = GetSlot(portal.positionInTheBoard, true);
            if (slot < 0) return;

            _kills[slot]++;

            // Le contrecoup ignore le bouclier (c'est une blessure interne) mais il
            // ne peut pas fermer le portail : il s'arrete a un plancher de PV. Tenir
            // la ligne use la source ; il faut toujours aller la fermer soi-meme.
            int floor = (portal.maxHP * InteractionRules.PORTAL_BACKLASH_FLOOR_PERCENT) / 100;
            if (floor < 1) floor = 1;

            int applied = portal.currentHP - floor;
            if (applied > InteractionRules.PORTAL_KILL_BACKLASH) applied = InteractionRules.PORTAL_KILL_BACKLASH;

            if (applied > 0) portal.ApplyDamage(applied);

            // La decharge part MAINTENANT, au meme instant que les PV retires : le
            // chiffre au-dessus du Shofar et l'eclair qui l'atteint racontent alors le
            // meme evenement. C'est tout l'interet - sans cela, le joueur voit une
            // barre baisser a l'autre bout du plateau sans savoir pourquoi.
            if (PortalTether.Instance != null) PortalTether.Instance.Discharge(enemy, portal, applied);

            Debug.LogFormat("[Portal] Contrecoup sur ({0},{1}) : -{2} PV ({3}/{4} PV), instabilite {5}/{6}.",
                            portal.positionInTheBoard.q, portal.positionInTheBoard.r,
                            applied, portal.currentHP, portal.maxHP,
                            _kills[slot], InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD);

            if (_kills[slot] >= InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD)
            {
                BreakShield(portal, slot);
            }

            portal.RefreshHealthBar();
        }

        private void BreakShield(Hexagon portal, int slot)
        {
            _kills[slot] = 0;
            _shieldDown[slot] = InteractionRules.PORTAL_SHIELD_DOWN_TURNS;

            Debug.LogFormat("[Portal] BOUCLIER ROMPU en ({0},{1}) : vulnerable et muet pendant {2} tours.",
                            portal.positionInTheBoard.q, portal.positionInTheBoard.r,
                            InteractionRules.PORTAL_SHIELD_DOWN_TURNS);

            FXManager fx = FXManager.Instance;
            if (fx != null)
            {
                fx.SpawnDestructionFX(portal.transform.position);
                fx.SpawnHitFX(portal.transform.position + Vector3.up);
            }
        }

        private Hexagon FindPortalAt(int q, int r)
        {
            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            if (hexes == null) return null;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null || hex.type != TypeOfHex.portal || hex.positionInTheBoard == null) continue;
                if (hex.positionInTheBoard.q == q && hex.positionInTheBoard.r == r) return hex;
            }
            return null;
        }

        // =================================================================
        //  TOUR DES PORTAILS
        // =================================================================
        /// <summary>Appele une fois par tour, en fin de tour.</summary>
        public void ProcessPortals()
        {
            if (BoardController.instance == null || BoardController.instance.HexagonsInBoard == null) return;

            int turn = (TurnManager.Instance != null) ? TurnManager.Instance.currentTurn : 1;

            // Copie : SpawnUnitVisual modifie les listes du plateau.
            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            List<Hexagon> portals = _portalBuffer;
            portals.Clear();

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex != null && hex.type == TypeOfHex.portal) portals.Add(hex);
            }

            // Ordre aleatoire : le plafond par tour ne doit pas toujours profiter aux memes.
            for (int i = 0; i < portals.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, portals.Count);
                Hexagon tmp = portals[i];
                portals[i] = portals[j];
                portals[j] = tmp;
            }

            int spawnBudget = InteractionRules.MAX_SPAWNS_PER_TURN;
            int activeEnemies = CountActiveEnemies();
            int spawnedThisTurn = 0;

            for (int p = 0; p < portals.Count; p++)
            {
                Hexagon portal = portals[p];
                if (portal == null || portal.type != TypeOfHex.portal) continue;

                int slot = GetSlot(portal.positionInTheBoard, true);
                portal.turnsAlive++;

                // L'horloge, avant tout le reste : un Shofar laisse tranquille se
                // refait, bouclier rompu ou non. C'est ce qui empeche d'accumuler de
                // l'avance en defendant sans jamais aller conclure.
                Regenerate(portal, slot);

                // Le bouclier se reforme tour apres tour.
                if (slot >= 0 && _shieldDown[slot] > 0)
                {
                    _shieldDown[slot]--;
                    if (_shieldDown[slot] == 0)
                        Debug.LogFormat("[Portal] Le bouclier de ({0},{1}) s'est reforme.",
                                        portal.positionInTheBoard.q, portal.positionInTheBoard.r);

                    // Un portail sonne ne deploie rien : c'est la contrepartie du bouclier rompu.
                    continue;
                }

                // Evolution au Niveau 2, echelonnee portail par portail : sans le
                // decalage, les six montaient le meme tour et effacaient d'un coup
                // tout le travail deja accompli.
                if (portal.level < InteractionRules.MAX_PORTAL_LEVEL
                    && portal.turnsAlive >= GetEvolveTurn(slot))
                {
                    EvolvePortal(portal);
                }

                // Combien ce portail deploie-t-il ce tour-ci ?
                bool isSurging = (slot >= 0 && slot == _surgeIndex);
                int wanted = 0;

                if (isSurging) wanted = 1 + InteractionRules.SURGE_EXTRA_SPAWNS;
                else if (IsDeployTurn(portal.turnsAlive, slot)) wanted = 1;

                // Le plan est fige ici, avant la boucle : les niveaux tires sont
                // exactement ceux que l'apercu a montres au joueur pendant son tour.
                EnsurePlan(portal, slot, turn, wanted);

                for (int k = 0; k < wanted; k++)
                {
                    if (spawnedThisTurn >= spawnBudget && !isSurging) break;
                    if (activeEnemies >= InteractionRules.MAX_ACTIVE_ENEMIES) break;

                    if (SpawnEnemyFromPortal(portal, turn, PlannedLevelAt(slot, k)) == null) break;

                    spawnedThisTurn++;
                    activeEnemies++;
                }
            }

            // La surge annoncee vient d'etre consommee ; on en annonce une nouvelle.
            _surgeIndex = -1;
            AnnounceNextSurge(portals, turn);
        }

        /// <summary>
        /// Choisit le portail qui surgira au tour suivant et le fait savoir : log
        /// explicite et bouffee de fumee sur la case. L'imprevu doit etre lisible,
        /// sinon ce n'est pas de la tension, c'est de l'arbitraire.
        /// </summary>
        private void AnnounceNextSurge(List<Hexagon> portals, int turn)
        {
            if (portals.Count == 0) return;
            if (turn + 1 < firstSurgeTurn) return;

            // Les vagues se rapprochent avec le temps : laisser le Yetzer Hara
            // tranquille n'est pas un abri, c'est un sursis. C'est ce qui empeche une
            // partie purement defensive de durer indefiniment derriere ses Bunkers.
            // Facile / Moyen : les vagues arrivent plus espacees (voir GameDifficulty).
            int interval = surgeInterval + GameDifficulty.Pick(4, 2, 0) - (turn / 10);
            if (interval < 2) interval = 2;

            if (turn - _lastSurgeTurn < interval) return;

            // On ne previent que sur un portail dont le bouclier tient : un portail
            // sonne ne deploie rien, annoncer une surge dessus serait mensonger.
            int candidates = 0;
            for (int i = 0; i < portals.Count; i++)
            {
                int slot = GetSlot(portals[i].positionInTheBoard, true);
                if (slot >= 0 && _shieldDown[slot] == 0) candidates++;
            }
            if (candidates == 0) return;

            int pick = UnityEngine.Random.Range(0, candidates);

            for (int i = 0; i < portals.Count; i++)
            {
                Hexagon portal = portals[i];
                int slot = GetSlot(portal.positionInTheBoard, true);
                if (slot < 0 || _shieldDown[slot] != 0) continue;

                if (pick > 0) { pick--; continue; }

                _surgeIndex = slot;
                _lastSurgeTurn = turn;

                _announcedCoord.q = portal.positionInTheBoard.q;
                _announcedCoord.r = portal.positionInTheBoard.r;
                _announcedCoord.s = portal.positionInTheBoard.s;

                Debug.LogFormat("[Portal] ALERTE : le portail ({0},{1}) prepare une vague de {2} ennemis pour le tour {3}.",
                                _announcedCoord.q, _announcedCoord.r,
                                1 + InteractionRules.SURGE_EXTRA_SPAWNS, turn + 1);

                if (FXManager.Instance != null)
                    FXManager.Instance.SpawnEnemyFX(portal.transform.position + Vector3.up * 1.5f);

                return;
            }
        }

        public int CountActiveEnemies()
        {
            if (BoardController.instance == null || BoardController.instance.PawnsInBoard == null) return 0;
            List<PawnController> pawns = BoardController.instance.PawnsInBoard;
            int count = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController pawn = pawns[i];
                if (pawn != null && pawn.IsEnemy && pawn.currentHP > 0) count++;
            }
            return count;
        }

        /// <summary>
        /// Ce portail deploie-t-il a ce compteur de tours ? Chaque portail a son propre
        /// decalage dans le cycle, donne par son emplacement d'etat. Avec six portails
        /// et un intervalle de six, il en sort exactement UN par tour, tous les tours,
        /// au lieu de trois d'un coup tous les trois tours.
        ///
        /// Le rythme moyen est identique ; c'est la sensation qui change. Les deux
        /// premiers tours ou il ne se passait strictement rien donnaient l'impression
        /// que le jeu etait casse.
        /// </summary>
        private static bool IsDeployTurn(int turnsAlive, int slot)
        {
            int interval = InteractionRules.PORTAL_SPAWN_INTERVAL;
            if (interval <= 1) return true;

            int offset = (slot < 0) ? 0 : (slot % interval);
            return ((turnsAlive + offset) % interval) == 0;
        }

        /// <summary>Tour d'evolution propre a ce portail : de base + 3 tours par rang.</summary>
        private int GetEvolveTurn(int slot)
        {
            int rank = (slot < 0) ? 0 : slot;
            return InteractionRules.PORTAL_EVOLVE_AFTER_TURNS + rank * InteractionRules.PORTAL_EVOLVE_STAGGER;
        }

        private void EvolvePortal(Hexagon portal)
        {
            // L'evolution AUGMENTE le reservoir, elle ne soigne pas : les degats deja
            // encaisses sont reportes. Un portail presque abattu qui evoluait revenait
            // a pleine vie, et tout le travail du joueur etait perdu.
            int damageTaken = portal.maxHP - portal.currentHP;

            portal.level = InteractionRules.MAX_PORTAL_LEVEL;
            portal.maxHP = InteractionRules.PORTAL_HP_L2;
            portal.energymax = InteractionRules.PORTAL_HP_L2;

            int newHP = InteractionRules.PORTAL_HP_L2 - damageTaken;
            if (newHP < 1) newHP = 1;

            portal.currentHP = newHP;
            portal.energy = newHP;

            Debug.LogFormat("[Portal] Un portail evolue au Niveau 2 ({0}/{1} PV, degats reportes).",
                            newHP, InteractionRules.PORTAL_HP_L2);

            if (FXManager.Instance != null) FXManager.Instance.SpawnEnemyFX(portal.transform.position);
            if (BoardController.instance != null) BoardController.instance.UpgradeHexVisual(portal);
        }

        /// <summary>
        /// Niveau de l'ennemi deploye. La table se durcit avec le tour : les premieres
        /// vagues sont homogenes, les suivantes melangent les tiers. Un portail Niveau 2
        /// monte tout d'un cran.
        /// </summary>
        private int DrawEnemyLevel(Hexagon portal, int turn)
        {
            float roll = UnityEngine.Random.value;
            int level;

            if (turn < 7) level = 1;
            else if (turn < 13) level = (roll < 0.70f) ? 1 : 2;
            else level = (roll < 0.45f) ? 1 : ((roll < 0.85f) ? 2 : 3);

            if (portal.level >= 2)
            {
                level++;
                if (UnityEngine.Random.value < InteractionRules.PORTAL_MINIBOSS_CHANCE) level++;
            }

            if (level > InteractionRules.MAX_ENEMY_LEVEL) level = InteractionRules.MAX_ENEMY_LEVEL;
            if (level < 1) level = 1;
            return level;
        }

        /// <summary>Deploie un ennemi sur une case libre adjacente au portail.</summary>
        public PawnController SpawnEnemyFromPortal(Hexagon portal, int turn)
        {
            return SpawnEnemyFromPortal(portal, turn, 0);
        }

        /// <summary>
        /// plannedLevel : le niveau deja tire et DEJA MONTRE au joueur par l'apercu.
        /// Zero signifie "personne n'a rien annonce", et on tire alors sur place - c'est
        /// le cas d'un deploiement declenche hors du tour normal.
        /// </summary>
        public PawnController SpawnEnemyFromPortal(Hexagon portal, int turn, int plannedLevel)
        {
            if (portal == null || BoardController.instance == null) return null;

            Hexagon target = FindFreeNeighbour(portal);
            if (target == null)
            {
                Debug.Log("[Portal] Aucune case libre autour du portail, deploiement reporte.");
                return null;
            }

            int enemyLevel = (plannedLevel >= 1) ? plannedLevel : DrawEnemyLevel(portal, turn);

            PawnController enemy = BoardController.instance.SpawnUnitVisual(target, enemyLevel, "enemy");
            if (enemy != null)
            {
                // Marquage de l'origine : c'est lui qui permet le contrecoup a la mort.
                enemy.SetOriginPortal(portal.positionInTheBoard);

                totalEnemiesSpawned++;
                Debug.LogFormat("[Portal] Deploiement d'un ennemi Niveau {0} en ({1},{2}).",
                                enemyLevel, target.positionInTheBoard.q, target.positionInTheBoard.r);
                if (FXManager.Instance != null) FXManager.Instance.SpawnEnemyFX(enemy.transform.position);

                // Le cordon s'ouvre des la naissance : c'est le lien entre cet ennemi
                // et le Shofar qui l'a envoye, et c'est ce lien que le joueur doit voir
                // avant meme de tuer quoi que ce soit.
                if (PortalTether.Instance != null) PortalTether.Instance.Attach(enemy, portal);
            }
            return enemy;
        }

        /// <summary>Compatibilite : ancienne signature sans numero de tour.</summary>
        public PawnController SpawnEnemyFromPortal(Hexagon portal)
        {
            int turn = (TurnManager.Instance != null) ? TurnManager.Instance.currentTurn : 1;
            return SpawnEnemyFromPortal(portal, turn);
        }

        // =================================================================
        //  PREDICTION (utilisee par ThreatPreview)
        // =================================================================
        /// <summary>
        /// Combien d'ennemis ce portail va deployer a la fin du tour en cours.
        /// Reproduit exactement la decision prise dans ProcessPortals, sans rien
        /// modifier : c'est ce qui permet a l'apercu d'etre fiable.
        /// </summary>
        /// <summary>
        /// Un tour de calme de plus, et ce qu'il coute au joueur.
        ///
        /// Les PV reviennent d'abord ; l'instabilite s'efface plus lentement, un point
        /// tous les PORTAL_CALM_PER_INSTABILITY_LOST tours. Dans cet ordre, le joueur
        /// voit la barre remonter avant de perdre ses points d'instabilite - il est prevenu
        /// avant d'etre puni.
        /// </summary>
        private void Regenerate(Hexagon portal, int slot)
        {
            if (portal == null || slot < 0 || portal.currentHP <= 0) return;

            _calmTurns[slot]++;

            int calm = _calmTurns[slot] - InteractionRules.PORTAL_CALM_TURNS;
            if (calm < 0) return;

            bool changed = false;

            if (portal.currentHP < portal.maxHP)
            {
                portal.currentHP += InteractionRules.PORTAL_REGEN_PER_TURN;
                if (portal.currentHP > portal.maxHP) portal.currentHP = portal.maxHP;
                portal.energy = portal.currentHP;
                changed = true;
            }

            int step = InteractionRules.PORTAL_CALM_PER_INSTABILITY_LOST;
            if (step < 1) step = 1;

            if (_kills[slot] > 0 && (calm % step) == 0)
            {
                _kills[slot]--;
                changed = true;
            }

            if (!changed) return;

            portal.RefreshHealthBar();

            Debug.LogFormat("[Portal] ({0},{1}) se refait : {2}/{3} PV, instabilite {4}/{5}. "
                          + "Personne ne l'a frappe depuis {6} tours.",
                            portal.positionInTheBoard.q, portal.positionInTheBoard.r,
                            portal.currentHP, portal.maxHP,
                            _kills[slot], InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD,
                            _calmTurns[slot]);
        }

        /// <summary>
        /// Tire - et retient - ce que ce portail deploiera a la fin du tour courant.
        /// Rejoue sans rien changer tant que le tour et le nombre n'ont pas bouge :
        /// l'apercu peut donc etre ouvert et referme dix fois sans que la menace change
        /// sous les yeux du joueur.
        /// </summary>
        private void EnsurePlan(Hexagon portal, int slot, int turn, int count)
        {
            if (portal == null || slot < 0 || count <= 0) return;
            if (_planTurn[slot] == turn && _planCount[slot] == count) return;

            _planTurn[slot] = turn;
            _planCount[slot] = count;

            int baseIndex = slot * MaxPlannedPerPortal;
            int limit = (count < MaxPlannedPerPortal) ? count : MaxPlannedPerPortal;

            for (int i = 0; i < limit; i++)
                _plannedLevels[baseIndex + i] = DrawEnemyLevel(portal, turn);
        }

        private int PlannedLevelAt(int slot, int index)
        {
            if (slot < 0 || index < 0 || index >= MaxPlannedPerPortal) return 0;

            int level = _plannedLevels[slot * MaxPlannedPerPortal + index];
            return (level < 1) ? 0 : level;
        }

        /// <summary>
        /// Niveau de l'ennemi de rang "index" que ce portail va deployer. Zero quand il
        /// ne deploie rien. C'est CE niveau que l'apercu doit montrer, et c'est celui-la
        /// qui sortira reellement.
        /// </summary>
        public int PredictSpawnLevel(Hexagon portal, int index)
        {
            if (portal == null || portal.type != TypeOfHex.portal) return 0;

            int count = PredictSpawnCount(portal);
            if (count <= 0) return 0;

            int slot = GetSlot(portal.positionInTheBoard, true);
            if (slot < 0) return 0;

            int turn = (TurnManager.Instance != null) ? TurnManager.Instance.currentTurn : 1;
            EnsurePlan(portal, slot, turn, count);

            int level = PlannedLevelAt(slot, index);
            return (level < 1) ? 1 : level;
        }

        /// <summary>Le plus haut niveau annonce par ce portail : ce qu'il faut craindre.</summary>
        public int PredictHighestLevel(Hexagon portal)
        {
            int count = PredictSpawnCount(portal);
            if (count <= 0) return 0;

            int highest = 0;
            for (int i = 0; i < count && i < MaxPlannedPerPortal; i++)
            {
                int level = PredictSpawnLevel(portal, i);
                if (level > highest) highest = level;
            }
            return highest;
        }

        public int PredictSpawnCount(Hexagon portal)
        {
            if (portal == null || portal.type != TypeOfHex.portal) return 0;

            // createIfMissing : l'emplacement porte le decalage de deploiement du
            // portail. Sans lui, l'apercu du premier tour annoncerait le mauvais rythme.
            int slot = GetSlot(portal.positionInTheBoard, true);

            // Un portail sonne ne deploie rien : il se contente de refermer son bouclier.
            if (slot >= 0 && _shieldDown[slot] > 0) return 0;

            if (slot >= 0 && slot == _surgeIndex) return 1 + InteractionRules.SURGE_EXTRA_SPAWNS;

            // turnsAlive sera incremente au moment du traitement : on teste la valeur suivante.
            return IsDeployTurn(portal.turnsAlive + 1, slot) ? 1 : 0;
        }

        /// <summary>Case de sortie qu'utiliserait ce portail s'il deployait maintenant.</summary>
        public Hexagon PredictSpawnHex(Hexagon portal)
        {
            if (portal == null || BoardController.instance == null) return null;
            return FindFreeNeighbour(portal);
        }

        private Hexagon FindFreeNeighbour(Hexagon portal)
        {
            HexCoord c = portal.positionInTheBoard;
            BoardController board = BoardController.instance;

            for (int i = 0; i < 6; i++)
            {
                HexCoord n = _neighbourScratch[i];
                n.q = c.q + NeighbourDQ[i];
                n.r = c.r + NeighbourDR[i];
                n.s = c.s + NeighbourDS[i];

                Hexagon hex = board.getHexByCoord(n);
                if (hex == null) continue;
                // Colline, Base, Shofar, batiment construit : pas de sortie ici.
                if (!InteractionRules.IsHexWalkable(hex)) continue;
                if (board.getPawnByCoord(n) != null) continue;
                return hex;
            }
            return null;
        }

        public int CountPortals()
        {
            if (BoardController.instance == null || BoardController.instance.HexagonsInBoard == null) return 0;
            List<Hexagon> hexes = BoardController.instance.HexagonsInBoard;
            int count = 0;
            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex != null && hex.type == TypeOfHex.portal) count++;
            }
            return count;
        }

        /// <summary>But du jeu : plus aucun portail sur la carte.</summary>
        public bool AllPortalsDestroyed()
        {
            return CountPortals() == 0;
        }
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. L'etat des portails tient dans cinq tableaux de 16 entrees alloues une fois
//    dans les champs. Un Dictionary<HexCoord,...> aurait impose un hash par acces
//    et le boxing de la cle ; GetSlot est une boucle plate sur 16 int, plus rapide
//    a cette taille et sans aucune allocation.
// 2. _announcedCoord et _neighbourScratch sont des instances uniques dont on
//    reecrit les champs : HexCoord etant une classe, chaque "new" serait un dechet.
// 3. Aucun appel par frame : tout ce fichier ne tourne qu'une fois par tour.
// 4. Les logs passent par LogFormat, jamais par concatenation de string.
// 5. Le tri aleatoire des portails est un Fisher-Yates en place sur le tampon
//    membre, sans OrderBy(lambda) ni liste temporaire.
// ---------------------------------------------------------------------------
