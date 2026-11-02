using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using MNLTHII;
using MNLTHII.Data;
using MNLTHII.Managers;
using MNLTHII.Rules;

namespace MNLTHII.Dev
{
    /// <summary>
    /// L'AUDIT GENERAL : le jeu se joue tout seul et se surveille.
    ///
    /// Un pilote prend la main du joueur avec une STRATEGIE differente a chaque
    /// partie (conseiller, tortue, ruee, economie, chaos), enchaine les tours a
    /// l'allure Eclair, et verifie a chaque tour :
    ///   - les INVARIANTS : Energie jamais negative, aucun pion mort encore pose,
    ///     jamais deux pions sur une case, PV / bulles / fissures dans leurs bornes,
    ///     aucun panneau reste ouvert quand le joueur a la main, aucune erreur console ;
    ///   - les ROLES : un Tank en Garde reste pres de son ancrage, un Tank en Assaut
    ///     avance vers un Shofar, un Tank en Chasse vers un ennemi, un ennemi avance
    ///     vers le joueur, un Bunker tire quand un ennemi est a portee, le Cristal
    ///     soigne, les usines paient ce qu'elles doivent, les Shofars deploient ;
    ///   - la SAUVEGARDE : a mi-partie, sauvegarde, rechargement, et la partie doit
    ///     reprendre exactement ou elle etait, pion par pion.
    ///
    /// Lancement : Milchemet > Tests > Audit general. Rien dans le build : un seul
    /// PlayerPrefs, lu au chargement. Rapport dans Audit/rapport_audit.txt.
    /// </summary>
    public class GameAudit : MonoBehaviour
    {
        public const string PrefKey = "mnlth_audit";       // 0 = off, 1 = une partie, 2 = toutes
        public const string PrefTurnsKey = "mnlth_audit_turns";

        private const int DefaultMaxTurns = 40;
        private const int SaveCheckTurn = 6;

        public static GameAudit Instance { get; private set; }

        /// <summary>Les facons de jouer. Une partie n'en suit qu'une : c'est ce qui les rend differentes.</summary>
        public enum Strategy { Advisor = 0, Turtle = 1, Rush = 2, Economy = 3, Chaos = 4, Player = 5 }
        private const int StrategyCount = 6;

        // =================================================================
        //  ETAT QUI SURVIT AUX RECHARGEMENTS DE SCENE (statique)
        // =================================================================
        private sealed class Run
        {
            public int index;
            public DifficultyLevel difficulty;
            public int layout;
            public Strategy strategy;
            public string outcome = "?";
            public int turns;
            public int tanksBuilt;
            public int actions;
            public int enemiesKilled;
            public int enemiesSpawned;
            public int portalsLeft;
            public int portalsTurned;
            public int errorsBefore;
            public float worstFrameMs;
            public float sumFrameMs;
            public int frames;

            /// <summary>
            /// APPELS DE DESSIN ET LOTS, releves au pire moment de la partie (le plus
            /// souvent quand le plateau est le plus charge). C'est le chiffre qui dit si
            /// l'instanciation GPU travaille : sans elle, chaque case et chaque pion est
            /// un appel a lui seul, et sur un telephone c'est le processeur qui plie bien
            /// avant le GPU. "regroupes" compte les objets dessines ensemble (instancies
            /// ou mis en lot) : plus ce nombre monte par rapport aux appels, mieux c'est.
            /// </summary>
            public int worstDrawCalls;
            public int worstSetPass;
            public int worstGrouped;
            public readonly List<string> failures = new List<string>();
            public readonly List<string> roleNotes = new List<string>();
            public bool saveCheckDone;
            public bool saveCheckOk;
            public string composition = "";
            public int jammedEnemies;
            // Les jalons d'une partie : de quoi lire sa courbe sans la regarder.
            public int firstShieldBreakTurn;
            public int firstPortalFallTurn;
            public int maxEnemiesAlive;
            public int tanksLost;
            public int baseLowestHp = int.MaxValue;
            public readonly StringBuilder timeline = new StringBuilder(512);
        }

        private static readonly List<Run> _runs = new List<Run>();
        private static readonly StringBuilder _journal = new StringBuilder(16384);
        private static readonly List<string> _errorSamples = new List<string>();
        private static int _errorCount;
        private static bool _logHooked;
        private static bool _sceneHooked;
        private static int _mode;
        private static int _maxTurns = DefaultMaxTurns;
        private static Run _current;
        private static SaveGameFile _pendingCheck;
        private static MemoryStore _store;

        // Les reglages du joueur (allure, difficulte), rendus tels quels a la fin.
        private static bool _settingsSaved;
        private static PhaseSpeed _playerSpeed;
        private static DifficultyLevel _playerDifficulty;

        private static readonly AdvisorSuggestion[] _suggestions = new AdvisorSuggestion[AdvisorRules.MaxSuggestions];

        // =================================================================
        //  CYCLE DE VIE
        // =================================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (PlayerPrefs.GetInt(PrefKey, 0) == 0) return;

            if (!_sceneHooked)
            {
                _sceneHooked = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            Spawn();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (PlayerPrefs.GetInt(PrefKey, 0) == 0) return;
            Spawn();
        }

        private static void Spawn()
        {
            if (Instance != null) return;
            if (LocalGameEngine.Instance == null) return;   // pas la scene de jeu
            new GameObject("Audit general").AddComponent<GameAudit>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _mode = PlayerPrefs.GetInt(PrefKey, 1);
            _maxTurns = Mathf.Clamp(PlayerPrefs.GetInt(PrefTurnsKey, DefaultMaxTurns), 3, 200);

            if (!_settingsSaved)
            {
                _settingsSaved = true;
                _playerSpeed = PhasePace.Speed;
                _playerDifficulty = GameDifficulty.Current;
            }

            // LE REALISATEUR DU TRAILER, S'IL EST RESTE ALLUME, remplace le magasin de
            // sauvegarde par un trou noir (NullStore) : l'audit croirait que la
            // sauvegarde ne marche pas. Il n'a rien a faire pendant un audit.
            if (TrailerDirector.Instance != null) Destroy(TrailerDirector.Instance.gameObject);

            if (_store == null) _store = new MemoryStore();
            SaveManager.Store = _store;   // jamais dans les vraies sauvegardes du joueur

            if (LocalGameEngine.Instance != null) LocalGameEngine.Instance.playIntroTour = false;
            TeachingPanel.AutoSkip = true;
            TeachingPanel.ForceQuestion = false;
            PhasePace.Speed = PhaseSpeed.Blitz;

            // LE JOURNAL SE TAIT PENDANT L'AUDIT. Douze parties x quarante tours
            // ecrivaient des dizaines de milliers de lignes dans la console, et chaque
            // ligne coute : le temps d'image moyen passait de 6-7 ms sur les premieres
            // parties a 14 ms sur les dernieres. Ce n'etait pas le jeu qui ralentissait,
            // c'etait la console. Les erreurs continuent d'arriver (OnLog les compte) et
            // les notes de l'audit vont dans son rapport, pas dans la console.
            GameLog.Verbose = false;

            if (!_logHooked)
            {
                _logHooked = true;
                Application.logMessageReceived += OnLog;
            }

            // Premiere partie : la configuration se choisit AVANT que le plateau ne se construise.
            if (_pendingCheck == null && (_current == null || _current.outcome != "?")) BeginRun();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Apres tous les Awake de la scene : personne ne doit avoir remplace le magasin.
            SaveManager.Store = _store;
            StartCoroutine(Main());
        }

        private void Update()
        {
            // Rien a regarder : on avance au plus vite.
            if (PhasePace.Running) PhasePace.SkipPhase();

            if (_current != null)
            {
                float ms = Time.unscaledDeltaTime * 1000f;
                _current.frames++;
                _current.sumFrameMs += ms;
                if (ms > _current.worstFrameMs && _current.frames > 30) _current.worstFrameMs = ms;

                // Un releve des compteurs de rendu toutes les trente images : les lire
                // coute un appel, et on ne veut que le pire moment de la partie.
                if ((_current.frames % 30) == 0) SampleRenderStats();
            }
        }

        /// <summary>
        /// Les compteurs de rendu d'Unity, au moment ou on passe. On garde le pire de la
        /// partie : c'est celui qui decide de la fluidite sur le materiel le plus faible.
        /// </summary>
        private static void SampleRenderStats()
        {
#if UNITY_EDITOR
            if (_current == null) return;

            // Les compteurs de la fenetre Stats, lus PAR REFLEXION. UnityStats est une
            // classe interne a l'editeur dont les membres changent d'une version
            // d'Unity a l'autre (par exemple "batches" a disparu) : un acces direct
            // casse la compilation au prochain changement de version, alors qu'ici un
            // compteur absent vaut simplement zero. C'est du code de diagnostic, appele
            // une image sur trente : le cout de la reflexion n'a aucune importance.
            if (!_statsProbed)
            {
                _statsProbed = true;
                _statDraws = FindStat("drawCalls");
                _statSetPass = FindStat("setPassCalls");
                _statInstanced = FindStat("instancedBatchedDrawCalls");
                _statBatched = FindStat("batchedDrawCalls");
                if (_statBatched == null) _statBatched = FindStat("dynamicBatchedDrawCalls");
            }

            int draws = Stat(_statDraws);
            if (draws > _current.worstDrawCalls) _current.worstDrawCalls = draws;

            int setPass = Stat(_statSetPass);
            if (setPass > _current.worstSetPass) _current.worstSetPass = setPass;

            // Ce qui nous interesse vraiment : combien d'objets ont ete regroupes.
            int grouped = Stat(_statInstanced) + Stat(_statBatched);
            if (grouped > _current.worstGrouped) _current.worstGrouped = grouped;
#endif
        }

#if UNITY_EDITOR
        private static bool _statsProbed;
        private static System.Reflection.PropertyInfo _statDraws;
        private static System.Reflection.PropertyInfo _statSetPass;
        private static System.Reflection.PropertyInfo _statInstanced;
        private static System.Reflection.PropertyInfo _statBatched;

        private static System.Reflection.PropertyInfo FindStat(string name)
        {
            return typeof(UnityEditor.UnityStats).GetProperty(
                name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        }

        private static int Stat(System.Reflection.PropertyInfo prop)
        {
            if (prop == null) return 0;
            object value = prop.GetValue(null, null);
            return (value is int) ? (int)value : 0;
        }
#endif

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            _errorCount++;
            if (_errorSamples.Count < 40)
            {
                string firstLine = "";
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    int nl = stackTrace.IndexOf('\n');
                    firstLine = (nl > 0) ? stackTrace.Substring(0, nl) : stackTrace;
                }
                string where = (_current != null) ? ("[partie " + (_current.index + 1) + ", tour " + CurrentTurn() + "] ") : "";
                _errorSamples.Add(where + type + " : " + condition + "   @ " + firstLine);
            }
        }

        private static int CurrentTurn()
        {
            return (TurnManager.Instance != null) ? TurnManager.Instance.currentTurn : 0;
        }

        // =================================================================
        //  CONFIGURATION D'UNE PARTIE
        // =================================================================
        private struct Plan
        {
            public DifficultyLevel difficulty;
            public Strategy strategy;
            public Plan(DifficultyLevel d, Strategy s) { difficulty = d; strategy = s; }
        }

        private static readonly Plan[] FullPlan =
        {
            new Plan(DifficultyLevel.Easy, Strategy.Player),   new Plan(DifficultyLevel.Medium, Strategy.Player),  new Plan(DifficultyLevel.Hard, Strategy.Player),
            new Plan(DifficultyLevel.Easy, Strategy.Advisor),  new Plan(DifficultyLevel.Medium, Strategy.Advisor), new Plan(DifficultyLevel.Hard, Strategy.Advisor),
            new Plan(DifficultyLevel.Easy, Strategy.Turtle),   new Plan(DifficultyLevel.Medium, Strategy.Rush),    new Plan(DifficultyLevel.Hard, Strategy.Turtle),
            new Plan(DifficultyLevel.Easy, Strategy.Economy),  new Plan(DifficultyLevel.Medium, Strategy.Chaos),   new Plan(DifficultyLevel.Hard, Strategy.Rush),
        };

        private static int TotalRuns { get { return (_mode == 2) ? FullPlan.Length : 1; } }

        private static void BeginRun()
        {
            Run run = new Run();
            run.index = _runs.Count;
            run.errorsBefore = _errorCount;

            if (_mode == 2)
            {
                // Douze parties : chaque difficulte voit le Joueur et le Conseiller (les
                // deux facons "raisonnables" de jouer) plus deux strategies de contraste.
                int i = run.index % FullPlan.Length;
                run.difficulty = FullPlan[i].difficulty;
                run.strategy = FullPlan[i].strategy;
                run.layout = run.index % MapGenerator.LayoutCount;
                GameDifficulty.Current = run.difficulty;
                MapGenerator.CurrentLayout = run.layout;
            }
            else
            {
                run.difficulty = GameDifficulty.Current;
                run.layout = MapGenerator.CurrentLayout;
                run.strategy = (Strategy)Random.Range(0, StrategyCount);
            }

            _runs.Add(run);
            _current = run;
            Note("=== PARTIE " + (run.index + 1) + "/" + TotalRuns + " : difficulte " + run.difficulty
                 + ", plateau " + run.layout + ", strategie " + StrategyName(run.strategy) + " ===");
        }

        private static string StrategyName(Strategy s)
        {
            switch (s)
            {
                case Strategy.Advisor: return "Conseiller (suit les conseils)";
                case Strategy.Turtle: return "Tortue (Bunkers, Tanks en Garde)";
                case Strategy.Rush: return "Ruee (Tanks en Assaut des que possible)";
                case Strategy.Economy: return "Economie (usines puis Cristal, Tanks en Chasse, evolutions)";
                case Strategy.Player: return "Joueur (usine, deux gardes, Bunker, puis l'assaut par paires)";
                default: return "Chaos (actions et postures au hasard)";
            }
        }

        private static void Note(string line)
        {
            _journal.Append(line).Append('\n');
            Debug.Log("[Audit] " + line);
        }

        private static void Fail(string what)
        {
            if (_current != null) _current.failures.Add("tour " + CurrentTurn() + " : " + what);
            Debug.LogWarning("[Audit] ECHEC : " + what);
        }

        private static void Role(string what)
        {
            if (_current != null && _current.roleNotes.Count < 60) _current.roleNotes.Add("tour " + CurrentTurn() + " : " + what);
            Debug.LogWarning("[Audit] ROLE : " + what);
        }

        // =================================================================
        //  LA BOUCLE
        // =================================================================
        private IEnumerator Main()
        {
            yield return WaitReady();

            TurnManager turns = TurnManager.Instance;
            if (turns == null || BoardController.instance == null)
            {
                Fail("le plateau ne s'est pas construit (pas de TurnManager ou de BoardController)");
                yield return FinishRun("plateau absent");
                yield break;
            }

            // Retour d'un rechargement : la partie doit reprendre exactement ou elle etait.
            if (_pendingCheck != null)
            {
                VerifyReload(_pendingCheck);
                _pendingCheck = null;
            }
            else
            {
                CheckInvariants("ouverture");
                int portalsAtStart = (PortalManager.Instance != null) ? PortalManager.Instance.CountPortals() : 0;
                if (portalsAtStart != InteractionRules.PORTAL_COUNT)
                    Fail("ouverture : " + portalsAtStart + " Shofars au lieu de " + InteractionRules.PORTAL_COUNT);
                int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : -1;
                if (energy < InteractionRules.STARTING_ENERGY)
                    Fail("ouverture : " + energy + " Energie au lieu d'au moins " + InteractionRules.STARTING_ENERGY);
            }

            // Apres un rechargement, on recompte les Tanks presents.
            _tanksSeen = 0;
            if (BoardController.instance != null && BoardController.instance.PawnsInBoard != null)
                for (int i = 0; i < BoardController.instance.PawnsInBoard.Count; i++)
                    if (BoardController.instance.PawnsInBoard[i] != null && !BoardController.instance.PawnsInBoard[i].IsEnemy) _tanksSeen++;

            while (!turns.gameOver && turns.currentTurn <= _maxTurns)
            {
                if (turns.phase != TurnPhase.Spending)
                {
                    Fail("la main n'est pas au joueur au debut de la boucle (phase " + turns.phase + ")");
                    break;
                }

                yield return Spend();
                CheckInvariants("apres la depense");
                SnapshotBeforeTurn();

                if (!_current.saveCheckDone && turns.currentTurn >= SaveCheckTurn)
                {
                    _current.saveCheckDone = true;
                    if (SaveAndReload()) yield break;   // la scene se recharge : Main repart
                }

                int before = turns.currentTurn;
                yield return PlayTurn();

                if (turns.gameOver) break;

                if (turns.currentTurn == before)
                {
                    Fail("le tour " + before + " ne s'est pas termine en 180 s (phase " + turns.phase + ")");
                    break;
                }

                CheckInvariants("debut du tour");
                CheckRoles();
                _current.turns = turns.currentTurn;
            }

            string outcome;
            if (turns.gameOver)
            {
                bool baseDown = BuildingManager.Instance != null && BuildingManager.Instance.IsBaseDestroyed();
                outcome = baseDown ? "DEFAITE" : "VICTOIRE";
                CheckGameOverScreen();
            }
            else if (turns.currentTurn > _maxTurns) outcome = "limite de " + _maxTurns + " tours atteinte (partie toujours en cours)";
            else outcome = "INTERROMPUE";

            yield return FinishRun(outcome);
        }

        /// <summary>Le plateau est pret et le joueur a la main.</summary>
        private IEnumerator WaitReady()
        {
            float deadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < deadline)
            {
                TurnManager turns = TurnManager.Instance;
                BoardController board = BoardController.instance;
                if (turns != null && turns.phase == TurnPhase.Spending && board != null
                    && board.HexagonsInBoard != null && board.HexagonsInBoard.Count > 100
                    && !IntroTour.Running) yield break;
                yield return null;
            }
            Fail("le plateau n'est pas pret apres 90 s");
        }

        /// <summary>Joue un tour complet et rend la main.</summary>
        private IEnumerator PlayTurn()
        {
            TurnManager turns = TurnManager.Instance;
            int before = turns.currentTurn;
            turns.EndPlayerTurn();

            float deadline = Time.realtimeSinceStartup + 180f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (turns.gameOver) yield break;
                if (turns.phase == TurnPhase.Spending && turns.currentTurn > before) yield break;
                yield return null;
            }
        }

        // =================================================================
        //  LE PILOTE : la phase de depense, selon la strategie
        // =================================================================
        private IEnumerator Spend()
        {
            TurnManager turns = TurnManager.Instance;
            int guard = 0;

            while (turns.IsSpendingPhase && guard++ < 14)
            {
                bool acted;
                switch (_current.strategy)
                {
                    case Strategy.Advisor: acted = SpendAdvisor() || SpendAnywhere(); break;
                    case Strategy.Turtle: acted = SpendTurtle(); break;
                    case Strategy.Rush: acted = SpendRush(); break;
                    case Strategy.Economy: acted = SpendEconomy(); break;
                    case Strategy.Player: acted = SpendPlayer(); break;
                    default: acted = SpendChaos(); break;
                }

                // Plus rien a batir : un joueur qui a de l'Energie qui dort repare sa
                // Base plutot que de la laisser a 11 PV. C'est aussi la seule facon de
                // verifier que le puits de depense marche (voir BASE_REPAIR_COST).
                if (!acted) acted = SpendRepair();

                if (!acted) break;
                yield return null;
            }
        }

        /// <summary>Reparer la Base, quand il reste de l'Energie et des PV a reprendre.</summary>
        private bool SpendRepair()
        {
            int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;
            if (energy < InteractionRules.BASE_REPAIR_COST) return false;
            if (!InteractionRules.BaseNeedsRepair()) return false;

            return Record(InteractionRules.RepairBase());
        }

        private static bool IsSuccess(TriviaOutcome o)
        {
            return o == TriviaOutcome.TankCreated || o == TriviaOutcome.TankEvolved
                || o == TriviaOutcome.BuildingUpgraded || o == TriviaOutcome.StanceChanged;
        }

        private bool Record(TriviaOutcome outcome)
        {
            if (!IsSuccess(outcome)) return false;
            _current.actions++;
            if (outcome == TriviaOutcome.TankCreated) { _current.tanksBuilt++; _tanksSeen++; }
            TurnManager.Instance.NotifyAdvisorAction(outcome);
            return true;
        }

        private bool SpendAdvisor()
        {
            int n = AdvisorRules.Build(_suggestions);
            for (int i = 0; i < n; i++)
            {
                AdvisorSuggestion s = _suggestions[i];
                if (s == null) continue;
                TriviaOutcome outcome = TriviaOutcome.EnergyOnly;

                switch (s.kind)
                {
                    case AdvisorActionKind.CreateTank: outcome = InteractionRules.CreateTankWithStance(s.hex, (PawnStance)s.stance); break;
                    case AdvisorActionKind.ChangeStance: outcome = InteractionRules.ChangeTankStance(s.tank, (PawnStance)s.stance); break;
                    case AdvisorActionKind.EvolveTank: outcome = InteractionRules.UpgradeTank(s.tank); break;
                    case AdvisorActionKind.Build: outcome = InteractionRules.ApplyPlayerAction(s.hex); break;
                }

                if (Record(outcome)) return true;
            }
            return false;
        }

        /// <summary>Tortue : des Bunkers pres de la Base, puis des Tanks en Garde, puis des usines.</summary>
        private bool SpendTurtle()
        {
            Hexagon hex = Pick(HexActionKind.Build, TypeOfHex.hill, true);
            if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;

            hex = Pick(HexActionKind.TankCreate, TypeOfHex.None, true);
            if (hex != null && Record(InteractionRules.CreateTankWithStance(hex, PawnStance.Guard))) return true;

            hex = Pick(HexActionKind.Build, TypeOfHex.gas, true);
            if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;

            hex = Pick(HexActionKind.Build, TypeOfHex.mountain, true);
            if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;

            return false;
        }

        /// <summary>Ruee : un Tank en Assaut a chaque fois qu'on peut, le plus loin possible de la Base.</summary>
        private bool SpendRush()
        {
            Hexagon hex = Pick(HexActionKind.TankCreate, TypeOfHex.None, false);
            if (hex != null && Record(InteractionRules.CreateTankWithStance(hex, PawnStance.Assault))) return true;

            // Une usine seulement s'il reste de quoi payer un Tank apres.
            int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;
            if (energy >= InteractionRules.TANK_CREATION_COST + InteractionRules.GetBuildCost(TypeOfHex.gas, 1))
            {
                hex = Pick(HexActionKind.Build, TypeOfHex.gas, true);
                if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;
            }
            return false;
        }

        /// <summary>Economie : usines d'abord (les deux rangs), puis Cristal, puis Tanks en Chasse qui evoluent.</summary>
        private bool SpendEconomy()
        {
            Hexagon hex = Pick(HexActionKind.Build, TypeOfHex.gas, true);
            if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;

            hex = Pick(HexActionKind.Build, TypeOfHex.crystal, true);
            if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;

            PawnController tank = PickTank(HexActionKind.TankEvolve);
            if (tank != null && Record(InteractionRules.UpgradeTank(tank))) return true;

            hex = Pick(HexActionKind.TankCreate, TypeOfHex.None, true);
            if (hex != null && Record(InteractionRules.CreateTankWithStance(hex, PawnStance.Hunt))) return true;

            return false;
        }

        /// <summary>
        /// Joueur : ce qu'un joueur qui a compris le jeu fait. Une usine d'abord, deux
        /// Tanks en Garde devant la Base, un Bunker sur la colline la plus proche, puis
        /// l'assaut par paires vers le Shofar le plus fissure - et des qu'un bouclier
        /// tombe, les Tanks les plus proches passent en Assaut.
        /// </summary>
        private bool SpendPlayer()
        {
            BoardController board = BoardController.instance;
            if (board == null) return false;
            int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;

            int tanks = 0, guards = 0, factories = 0, bunkers = 0;
            if (board.PawnsInBoard != null)
                for (int i = 0; i < board.PawnsInBoard.Count; i++)
                {
                    PawnController t = board.PawnsInBoard[i];
                    if (t == null || t.IsEnemy) continue;
                    tanks++;
                    if (t.stance == PawnStance.Guard) guards++;
                }
            if (board.HexagonsInBoard != null)
                for (int i = 0; i < board.HexagonsInBoard.Count; i++)
                {
                    Hexagon h = board.HexagonsInBoard[i];
                    if (h == null || h.level < 1) continue;
                    if (h.type == TypeOfHex.gas) factories++;
                    if (h.type == TypeOfHex.hill) bunkers++;
                }

            Hexagon hex;

            // 1. Un bouclier vient de tomber : les deux Tanks les plus proches foncent SUR CE Shofar.
            Hexagon open = OpenPortal();
            if (open != null && energy >= InteractionRules.TANK_STANCE_COST)
            {
                PawnController t = NearestTank(open.positionInTheBoard, PawnStance.Assault);
                if (t != null && BoardController.GetHexDistance(t.hexcoord, open.positionInTheBoard) <= 8
                    && Record(InteractionRules.ChangeTankStance(t, PawnStance.Assault)))
                {
                    t.orderTargetCoord = open.positionInTheBoard;
                    return true;
                }
            }

            // 2. L'economie d'abord : une usine, puis deux Tanks en Garde, puis une deuxieme usine.
            if (factories < 1) { hex = Pick(HexActionKind.Build, TypeOfHex.gas, true); if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true; }
            if (guards < 2) { hex = Pick(HexActionKind.TankCreate, TypeOfHex.None, true); if (hex != null && Record(InteractionRules.CreateTankWithStance(hex, PawnStance.Guard))) return true; }
            if (factories < 2) { hex = Pick(HexActionKind.Build, TypeOfHex.gas, true); if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true; }

            // 3. Un Bunker sur la colline la plus proche de la Base.
            if (bunkers < 1) { hex = Pick(HexActionKind.Build, TypeOfHex.hill, true); if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true; }

            // 4. Un Tank au Cristal evolue des qu'on a les moyens : rang 2, il avance de deux cases.
            if (energy >= InteractionRules.TANK_EVOLVE_COST)
            {
                PawnController t = PickTank(HexActionKind.TankEvolve);
                if (t != null && Record(InteractionRules.UpgradeTank(t))) return true;
            }

            // 5. L'assaut : des Tanks en Assaut, chacun envoye sur un Shofar different
            //    (comme un joueur qui choisit sa cible), en commencant par le plus proche.
            if (tanks >= 2 && InteractionRules.CanAddTank())
            {
                hex = Pick(HexActionKind.TankCreate, TypeOfHex.None, false);
                if (hex != null && Record(InteractionRules.CreateTankWithStance(hex, PawnStance.Assault)))
                {
                    PawnController made = board.getPawnByCoord(hex.positionInTheBoard);
                    Hexagon target = LeastAssignedPortal();
                    if (made != null && target != null) made.orderTargetCoord = target.positionInTheBoard;
                    return true;
                }
            }
            else if (!InteractionRules.CanAddTank())
            {
                // Armee au complet : un joueur elargit son commandement - un Centre
                // vers l'avant (plafond + tete de pont), puis un Cristal pour faire
                // evoluer ce qu'il a deja.
                hex = Pick(HexActionKind.Build, TypeOfHex.mountain, false);
                if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;
                hex = Pick(HexActionKind.Build, TypeOfHex.crystal, false);
                if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;
            }

            // 6. Le reste : usines au rang 2, Cristal, Centre.
            hex = Pick(HexActionKind.Build, TypeOfHex.gas, true);
            if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;
            hex = Pick(HexActionKind.Build, TypeOfHex.crystal, true);
            if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;
            hex = Pick(HexActionKind.Build, TypeOfHex.mountain, true);
            if (hex != null && Record(InteractionRules.ApplyPlayerAction(hex))) return true;

            return false;
        }

        /// <summary>
        /// Le Shofar debout sur lequel le moins de Tanks en Assaut sont deja envoyes
        /// (a egalite, le plus proche de la Base) : on repartit l'assaut au lieu de
        /// faire la queue devant le meme.
        /// </summary>
        private static Hexagon LeastAssignedPortal()
        {
            BoardController board = BoardController.instance;
            BuildingManager buildings = BuildingManager.Instance;
            if (board == null || board.HexagonsInBoard == null) return null;

            Hexagon best = null; int bestLoad = int.MaxValue; int bestDist = int.MaxValue;
            for (int i = 0; i < board.HexagonsInBoard.Count; i++)
            {
                Hexagon h = board.HexagonsInBoard[i];
                if (h == null || h.type != TypeOfHex.portal || h.currentHP <= 0) continue;

                int load = 0;
                if (board.PawnsInBoard != null)
                    for (int k = 0; k < board.PawnsInBoard.Count; k++)
                    {
                        PawnController t = board.PawnsInBoard[k];
                        if (t == null || t.IsEnemy || t.stance != PawnStance.Assault || t.orderTargetCoord == null) continue;
                        if (t.orderTargetCoord.CompareHexCoord(h.positionInTheBoard)) load++;
                    }
                int dist = (buildings != null) ? buildings.DistanceToBase(h.positionInTheBoard) : 0;
                if (load < bestLoad || (load == bestLoad && dist < bestDist)) { bestLoad = load; bestDist = dist; best = h; }
            }
            return best;
        }

        /// <summary>Un Shofar dont le bouclier est rompu, le plus proche de la Base ; sinon null.</summary>
        private static Hexagon OpenPortal()
        {
            BoardController board = BoardController.instance;
            PortalManager portals = PortalManager.Instance;
            BuildingManager buildings = BuildingManager.Instance;
            if (board == null || portals == null || board.HexagonsInBoard == null) return null;

            Hexagon best = null; int bestDist = int.MaxValue;
            for (int i = 0; i < board.HexagonsInBoard.Count; i++)
            {
                Hexagon h = board.HexagonsInBoard[i];
                if (h == null || h.type != TypeOfHex.portal || !portals.IsShieldDown(h)) continue;
                int d = (buildings != null) ? buildings.DistanceToBase(h.positionInTheBoard) : 0;
                if (d < bestDist) { bestDist = d; best = h; }
            }
            return best;
        }

        /// <summary>Le Tank le plus proche d'un point qui n'a pas deja cette posture.</summary>
        private static PawnController NearestTank(HexCoord at, PawnStance notAlready)
        {
            BoardController board = BoardController.instance;
            if (board == null || board.PawnsInBoard == null) return null;
            PawnController best = null; int bestDist = int.MaxValue;
            for (int i = 0; i < board.PawnsInBoard.Count; i++)
            {
                PawnController t = board.PawnsInBoard[i];
                if (t == null || t.IsEnemy || t.hexcoord == null || t.stance == notAlready) continue;
                int d = BoardController.GetHexDistance(at, t.hexcoord);
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        /// <summary>Chaos : la premiere action payable trouvee au hasard, et parfois un Tank qui change de role.</summary>
        private bool SpendChaos()
        {
            BoardController board = BoardController.instance;
            if (board != null && board.PawnsInBoard != null && board.PawnsInBoard.Count > 0 && Random.value < 0.35f)
            {
                for (int k = 0; k < 6; k++)
                {
                    PawnController p = board.PawnsInBoard[Random.Range(0, board.PawnsInBoard.Count)];
                    if (p == null || p.IsEnemy) continue;
                    PawnStance s = (PawnStance)Random.Range(0, 3);
                    if (s == p.stance) continue;
                    if (Record(InteractionRules.ChangeTankStance(p, s))) return true;
                }
            }
            return SpendAnywhere();
        }

        /// <summary>Sans plan : la premiere action payable sur le plateau, posture au hasard.</summary>
        private bool SpendAnywhere()
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return false;

            List<Hexagon> hexes = board.HexagonsInBoard;
            int start = Random.Range(0, hexes.Count);

            for (int k = 0; k < hexes.Count; k++)
            {
                Hexagon hex = hexes[(start + k) % hexes.Count];
                if (hex == null) continue;

                HexAction action = HexAction.Resolve(hex);
                if (!action.affordable) continue;

                TriviaOutcome outcome;
                if (action.kind == HexActionKind.TankCreate)
                    outcome = InteractionRules.CreateTankWithStance(hex, (PawnStance)Random.Range(0, 3));
                else if (action.kind == HexActionKind.Build)
                    outcome = InteractionRules.ApplyPlayerAction(hex);
                else continue;

                if (Record(outcome)) return true;
            }
            return false;
        }

        /// <summary>
        /// La case payable qui correspond : une construction d'un type donne, ou une
        /// case libre pour un Tank. nearBase : la plus proche de la Base, sinon la plus
        /// proche d'un Shofar.
        /// </summary>
        private static Hexagon Pick(HexActionKind kind, TypeOfHex type, bool nearBase)
        {
            BoardController board = BoardController.instance;
            BuildingManager buildings = BuildingManager.Instance;
            if (board == null || board.HexagonsInBoard == null) return null;

            Hexagon best = null;
            int bestScore = int.MaxValue;
            List<Hexagon> hexes = board.HexagonsInBoard;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon hex = hexes[i];
                if (hex == null) continue;
                if (type != TypeOfHex.None && hex.type != type) continue;

                HexAction action = HexAction.Resolve(hex);
                if (action.kind != kind || !action.affordable) continue;

                int score;
                if (nearBase)
                {
                    int d = (buildings != null) ? buildings.DistanceToBase(hex.positionInTheBoard) : 0;
                    score = (d == int.MaxValue) ? 999 : d;
                }
                else
                {
                    int d = NearestPortalDistance(hex.positionInTheBoard);
                    score = (d == int.MaxValue) ? 999 : -d;
                }
                score = score * 4 + Random.Range(0, 4);   // un peu de hasard entre deux cases equivalentes

                if (score < bestScore) { bestScore = score; best = hex; }
            }
            return best;
        }

        private static PawnController PickTank(HexActionKind kind)
        {
            BoardController board = BoardController.instance;
            if (board == null || board.PawnsInBoard == null) return null;

            for (int i = 0; i < board.PawnsInBoard.Count; i++)
            {
                PawnController p = board.PawnsInBoard[i];
                if (p == null || p.IsEnemy || p.hexcoord == null) continue;
                Hexagon under = board.getHexByCoord(p.hexcoord);
                if (under == null) continue;
                HexAction action = HexAction.Resolve(under);
                if (action.kind == kind && action.affordable) return p;
            }
            return null;
        }

        // =================================================================
        //  LES ROLES : chacun fait-il ce qu'il doit ?
        // =================================================================
        private sealed class Track
        {
            public int q, r;
            public int stance;
            public int sameStanceTurns;
            public int bestPortalDistance = int.MaxValue;
            public int bestEnemyDistance = int.MaxValue;
            public int stillTurns;
            public bool flaggedIdle;
            public bool flaggedGuard;
        }

        private static readonly Dictionary<PawnController, Track> _tracks = new Dictionary<PawnController, Track>();

        // Photo prise a la fin de la depense, comparee au debut du tour suivant.
        private static int _energyAfterSpend;
        private static int _expectedIncome;
        private static readonly Dictionary<Hexagon, int> _bunkerHp = new Dictionary<Hexagon, int>();
        private static readonly HashSet<Hexagon> _bunkerHadTargets = new HashSet<Hexagon>();
        private static readonly Dictionary<PawnController, int> _healable = new Dictionary<PawnController, int>();
        private static readonly Dictionary<Hexagon, int> _payers = new Dictionary<Hexagon, int>();
        private static int _jammedEnemies;

        private void SnapshotBeforeTurn()
        {
            BoardController board = BoardController.instance;
            if (board == null) return;

            _energyAfterSpend = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;
            _expectedIncome = InteractionRules.BASE_INCOME_PER_TURN + InteractionRules.GasIncomePerTurn()
                              + InteractionRules.CountTurnedPortals() * InteractionRules.TURNED_PORTAL_INCOME;

            _bunkerHp.Clear();
            _bunkerHadTargets.Clear();
            _healable.Clear();
            _payers.Clear();

            List<Hexagon> hexes = board.HexagonsInBoard;
            List<PawnController> pawns = board.PawnsInBoard;
            if (hexes == null || pawns == null) return;

            for (int i = 0; i < hexes.Count; i++)
            {
                Hexagon h = hexes[i];
                if (h == null || h.positionInTheBoard == null) continue;

                // Ce que cette case doit verser en fin de tour - si elle y arrive entiere.
                int pays = InteractionRules.GetHexIncome(h);
                if (pays > 0) _payers[h] = pays;

                if (h.type == TypeOfHex.hill && h.level >= 1)
                {
                    _bunkerHp[h] = h.currentHP;
                    for (int p = 0; p < pawns.Count; p++)
                    {
                        PawnController e = pawns[p];
                        if (e == null || !e.IsEnemy || e.hexcoord == null) continue;
                        if (BoardController.GetHexDistance(h.positionInTheBoard, e.hexcoord) <= InteractionRules.BUNKER_RANGE)
                        {
                            _bunkerHadTargets.Add(h);
                            break;
                        }
                    }
                }

                if (h.type == TypeOfHex.crystal && h.level >= 1)
                {
                    int range = InteractionRules.GetCrystalRange(h.level);
                    for (int p = 0; p < pawns.Count; p++)
                    {
                        PawnController t = pawns[p];
                        if (t == null || t.IsEnemy || t.hexcoord == null || t.currentHP >= t.maxHP) continue;
                        if (BoardController.GetHexDistance(h.positionInTheBoard, t.hexcoord) <= range && !EnemyNear(t.hexcoord, 2))
                            _healable[t] = t.currentHP;
                    }
                }
            }
        }

        private int _tanksSeen;

        private void CheckRoles()
        {
            BoardController board = BoardController.instance;
            TurnManager turns = TurnManager.Instance;
            if (board == null || turns == null || board.PawnsInBoard == null) return;

            // --- les jalons et la courbe de la partie ---
            {
                int foes = 0, tanksNow = 0;
                for (int i = 0; i < board.PawnsInBoard.Count; i++)
                {
                    PawnController p = board.PawnsInBoard[i];
                    if (p == null) continue;
                    if (p.IsEnemy) foes++; else tanksNow++;
                }
                if (foes > _current.maxEnemiesAlive) _current.maxEnemiesAlive = foes;
                // Les Tanks perdus : ce qu'on avait, plus ce qu'on a pose, moins ce qu'on a.
                int expectedTanks = _tanksSeen;
                if (tanksNow < expectedTanks) _current.tanksLost += expectedTanks - tanksNow;
                _tanksSeen = tanksNow;

                int baseHp = (BuildingManager.Instance != null) ? BuildingManager.Instance.GetBaseHP() : 0;
                if (baseHp < _current.baseLowestHp) _current.baseLowestHp = baseHp;

                PortalManager pm = PortalManager.Instance;
                int portalsLeft = (pm != null) ? pm.CountPortals() : 0;
                bool anyDown = false;
                if (pm != null && board.HexagonsInBoard != null)
                    for (int i = 0; i < board.HexagonsInBoard.Count; i++)
                    {
                        Hexagon h = board.HexagonsInBoard[i];
                        if (h != null && h.type == TypeOfHex.portal && pm.IsShieldDown(h)) { anyDown = true; break; }
                    }
                if (anyDown && _current.firstShieldBreakTurn == 0) _current.firstShieldBreakTurn = turns.currentTurn;
                if (portalsLeft < InteractionRules.PORTAL_COUNT && _current.firstPortalFallTurn == 0) _current.firstPortalFallTurn = turns.currentTurn;

                if (turns.currentTurn % 5 == 0 || turns.currentTurn == 2)
                {
                    int en = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;
                    _current.timeline.Append("t").Append(turns.currentTurn).Append(": ")
                            .Append(tanksNow).Append(" Tanks / ").Append(foes).Append(" ennemis, Base ").Append(baseHp)
                            .Append(", ").Append(en).Append(" Energie, ").Append(portalsLeft).Append(" Shofars")
                            .Append(anyDown ? " (un bouclier rompu)" : "").Append(" | ");
                }
            }

            // --- le revenu ---
            // Une usine detruite ou retrogradee pendant la phase ennemie ne paie pas :
            // on retire sa part de ce qu'on attendait, le reste doit etre arrive.
            int lost = 0;
            foreach (KeyValuePair<Hexagon, int> kv in _payers)
            {
                Hexagon h = kv.Key;
                int now = (h == null) ? 0 : InteractionRules.GetHexIncome(h);
                if (now < kv.Value) lost += kv.Value - now;
            }
            int expected = _expectedIncome - lost;
            int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;
            int gained = energy - _energyAfterSpend;
            if (gained < expected - 5 || gained > expected + 25)
                Role("revenu : +" + gained + " Energie entre deux depenses, attendu " + expected
                     + " (plancher " + InteractionRules.BASE_INCOME_PER_TURN + " + usines + Shofars retournes"
                     + (lost > 0 ? ", " + lost + " perdu(s) avec les usines detruites ce tour" : "") + ")");

            // --- les Bunkers ont tire ---
            foreach (KeyValuePair<Hexagon, int> kv in _bunkerHp)
            {
                Hexagon b = kv.Key;
                if (b == null || b.type != TypeOfHex.hill || b.level < 1) continue;
                if (!_bunkerHadTargets.Contains(b) || b.currentHP < kv.Value) continue;
                // L'ennemi a pu mourir sous les Tanks avant que le Bunker ne joue : on ne
                // s'etonne que s'il y en a encore un a portee maintenant.
                if (NearestPawnDistance(b.positionInTheBoard, true) <= InteractionRules.BUNKER_RANGE)
                    Role("Bunker (" + b.positionInTheBoard.q + "," + b.positionInTheBoard.r + ") avec un ennemi a portee avant et apres le tour ne s'est pas use : a-t-il tire ?");
            }

            // --- le Cristal a soigne ---
            foreach (KeyValuePair<PawnController, int> kv in _healable)
            {
                PawnController t = kv.Key;
                if (t == null || t.currentHP <= 0 || t.hexcoord == null) continue;
                if (t.currentHP == kv.Value && !EnemyNear(t.hexcoord, 2))
                    Role("Tank blesse pres d'un Cristal (" + kv.Value + " PV, inchange) : pas soigne");
            }

            // --- le Yetzer Hara deploie ---
            if (PortalManager.Instance != null && turns.currentTurn >= InteractionRules.PORTAL_SPAWN_INTERVAL + 3
                && PortalManager.Instance.totalEnemiesSpawned == 0)
                Role("aucun ennemi deploye apres " + turns.currentTurn + " tours");

            // --- les pions, un par un ---
            List<PawnController> pawns = board.PawnsInBoard;
            HashSet<PawnController> alive = new HashSet<PawnController>();

            for (int i = 0; i < pawns.Count; i++)
            {
                PawnController p = pawns[i];
                if (p == null || p.hexcoord == null) continue;
                alive.Add(p);

                Track t;
                if (!_tracks.TryGetValue(p, out t))
                {
                    t = new Track();
                    _tracks[p] = t;
                    t.q = p.hexcoord.q; t.r = p.hexcoord.r;
                    t.stance = (int)p.stance;
                    t.bestPortalDistance = NearestPortalDistance(p.hexcoord);
                    t.bestEnemyDistance = NearestPawnDistance(p.hexcoord, true);
                    continue;
                }

                bool moved = (t.q != p.hexcoord.q || t.r != p.hexcoord.r);
                t.stillTurns = moved ? 0 : t.stillTurns + 1;
                t.q = p.hexcoord.q; t.r = p.hexcoord.r;

                if (p.IsEnemy)
                {
                    // Un ennemi avance vers le joueur, ou frappe ce qu'il a au contact.
                    bool atContact = PlayerTargetNear(p.hexcoord, Mathf.Max(1, p.attackRange)) || MountainBubbleNear(p.hexcoord);
                    if (t.stillTurns >= 3 && !atContact && !t.flaggedIdle)
                    {
                        t.flaggedIdle = true;
                        // Coince (aucune case libre autour : file d'ennemis, collines) ou vraiment inerte ?
                        if (HasFreeNeighbour(p.hexcoord)) 
                            Role("ennemi rang " + p.level + " en (" + p.hexcoord.q + "," + p.hexcoord.r + ") immobile depuis 3 tours sans cible au contact, avec une case libre a cote");
                        else _jammedEnemies++;
                    }
                    continue;
                }

                // --- un Tank ---
                if ((int)p.stance != t.stance)
                {
                    t.stance = (int)p.stance;
                    t.sameStanceTurns = 0;
                    t.bestPortalDistance = NearestPortalDistance(p.hexcoord);
                    t.bestEnemyDistance = NearestPawnDistance(p.hexcoord, true);
                    t.flaggedIdle = false;
                    t.flaggedGuard = false;
                }
                else t.sameStanceTurns++;

                int portalDist = NearestPortalDistance(p.hexcoord);
                int enemyDist = NearestPawnDistance(p.hexcoord, true);

                switch (p.stance)
                {
                    case PawnStance.Guard:
                    {
                        int anchor = NearestAnchorDistance(p.hexcoord);
                        int radius = (turns.guardRadius > 0) ? turns.guardRadius : 4;
                        if (anchor != int.MaxValue && anchor > radius + 1 && !t.flaggedGuard)
                        {
                            t.flaggedGuard = true;
                            Role("Tank en GARDE a " + anchor + " cases de tout ancrage (rayon de garde " + radius + ")");
                        }
                        break;
                    }

                    case PawnStance.Assault:
                    {
                        // En quatre tours de la meme posture, il doit s'etre rapproche de SON
                        // Shofar - celui qu'on lui a designe, sinon le plus proche - ou etre a portee.
                        if (p.orderTargetCoord != null)
                        {
                            Hexagon ordered = board.getHexByCoord(p.orderTargetCoord);
                            if (ordered != null && ordered.type == TypeOfHex.portal)
                                portalDist = BoardController.GetHexDistance(p.hexcoord, ordered.positionInTheBoard);
                        }
                        if (portalDist < t.bestPortalDistance) { t.bestPortalDistance = portalDist; t.sameStanceTurns = 0; }
                        bool inRange = portalDist <= Mathf.Max(1, p.attackRange) + 1;
                        if (t.sameStanceTurns >= 4 && !inRange && portalDist != int.MaxValue && !t.flaggedIdle)
                        {
                            t.flaggedIdle = true;
                            Role("Tank en ASSAUT a " + portalDist + " cases du Shofar le plus proche, sans s'en rapprocher depuis 4 tours");
                        }
                        break;
                    }

                    case PawnStance.Hunt:
                    {
                        // Un Tank envoye vers un Cristal attend son evolution : il ne chasse pas.
                        if (p.seekCrystal) { t.sameStanceTurns = 0; break; }
                        if (enemyDist < t.bestEnemyDistance) { t.bestEnemyDistance = enemyDist; t.sameStanceTurns = 0; }
                        bool inRange = enemyDist <= Mathf.Max(1, p.attackRange);
                        if (t.sameStanceTurns >= 4 && enemyDist != int.MaxValue && !inRange && !t.flaggedIdle)
                        {
                            t.flaggedIdle = true;
                            Role("Tank en CHASSE a " + enemyDist + " cases de l'ennemi le plus proche, sans s'en rapprocher depuis 4 tours");
                        }
                        if (enemyDist == int.MaxValue) t.bestEnemyDistance = int.MaxValue;
                        break;
                    }
                }
            }

            // Les morts quittent la memoire.
            List<PawnController> gone = null;
            foreach (KeyValuePair<PawnController, Track> kv in _tracks)
                if (kv.Key == null || !alive.Contains(kv.Key)) { if (gone == null) gone = new List<PawnController>(); gone.Add(kv.Key); }
            if (gone != null) for (int i = 0; i < gone.Count; i++) _tracks.Remove(gone[i]);
        }

        private static readonly int[] NeighbourDQ = { 1, 1, 0, -1, -1, 0 };
        private static readonly int[] NeighbourDR = { 0, -1, -1, 0, 1, 1 };

        /// <summary>Une case voisine ou cet ennemi pourrait avancer : praticable et vide.</summary>
        private static bool HasFreeNeighbour(HexCoord at)
        {
            BoardController board = BoardController.instance;
            if (board == null || at == null) return false;
            for (int n = 0; n < 6; n++)
            {
                int q = at.q + NeighbourDQ[n], r = at.r + NeighbourDR[n];
                HexCoord c = new HexCoord(q, r, -q - r);
                Hexagon h = board.getHexByCoord(c);
                if (h == null || !InteractionRules.IsHexWalkable(h)) continue;
                if (board.getPawnByCoord(c) != null) continue;
                if (InsideBubble(c)) continue;
                return true;
            }
            return false;
        }

        /// <summary>Dans la zone interdite d'un Centre de Commandement : un ennemi n'y entre pas.</summary>
        private static bool InsideBubble(HexCoord c)
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return false;
            for (int i = 0; i < board.HexagonsInBoard.Count; i++)
            {
                Hexagon h = board.HexagonsInBoard[i];
                if (h == null || h.positionInTheBoard == null) continue;
                int radius = InteractionRules.BubbleRadius(h);
                if (radius <= 0) continue;
                if (BoardController.GetHexDistance(c, h.positionInTheBoard) <= radius) return true;
            }
            return false;
        }

        private static bool EnemyNear(HexCoord at, int range)
        {
            return NearestPawnDistance(at, true) <= range;
        }

        private static int NearestPawnDistance(HexCoord at, bool enemies)
        {
            BoardController board = BoardController.instance;
            if (board == null || board.PawnsInBoard == null || at == null) return int.MaxValue;
            int best = int.MaxValue;
            for (int i = 0; i < board.PawnsInBoard.Count; i++)
            {
                PawnController p = board.PawnsInBoard[i];
                if (p == null || p.IsEnemy != enemies || p.hexcoord == null) continue;
                int d = BoardController.GetHexDistance(at, p.hexcoord);
                if (d > 0 && d < best) best = d;
            }
            return best;
        }

        private static int NearestPortalDistance(HexCoord at)
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null || at == null) return int.MaxValue;
            int best = int.MaxValue;
            for (int i = 0; i < board.HexagonsInBoard.Count; i++)
            {
                Hexagon h = board.HexagonsInBoard[i];
                if (h == null || h.type != TypeOfHex.portal || h.positionInTheBoard == null) continue;
                int d = BoardController.GetHexDistance(at, h.positionInTheBoard);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>La Base, ou un Centre de Commandement : ce autour de quoi un Tank en Garde tourne.</summary>
        private static int NearestAnchorDistance(HexCoord at)
        {
            BoardController board = BoardController.instance;
            int best = (BuildingManager.Instance != null) ? BuildingManager.Instance.DistanceToBase(at) : int.MaxValue;
            if (board == null || board.HexagonsInBoard == null) return best;
            for (int i = 0; i < board.HexagonsInBoard.Count; i++)
            {
                Hexagon h = board.HexagonsInBoard[i];
                if (h == null || h.positionInTheBoard == null) continue;
                if (h.type == TypeOfHex.mountain && h.level >= 1)
                {
                    int d = BoardController.GetHexDistance(at, h.positionInTheBoard);
                    if (d < best) best = d;
                }
            }
            return best;
        }

        /// <summary>Un Tank, la Base ou un batiment du joueur a portee : de quoi occuper un ennemi.</summary>
        private static bool PlayerTargetNear(HexCoord at, int range)
        {
            if (NearestPawnDistance(at, false) <= range) return true;
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return false;
            for (int i = 0; i < board.HexagonsInBoard.Count; i++)
            {
                Hexagon h = board.HexagonsInBoard[i];
                if (h == null || h.positionInTheBoard == null) continue;
                bool target = (h.type == TypeOfHex.Base)
                              || (h.level >= 1 && (h.type == TypeOfHex.hill || h.type == TypeOfHex.gas
                                                   || h.type == TypeOfHex.crystal || h.type == TypeOfHex.mountain));
                if (!target) continue;
                if (BoardController.GetHexDistance(at, h.positionInTheBoard) <= range) return true;
            }
            return false;
        }

        /// <summary>Au bord de la bulle d'un Centre : l'ennemi la frappe au lieu d'avancer.</summary>
        private static bool MountainBubbleNear(HexCoord at)
        {
            BoardController board = BoardController.instance;
            if (board == null || board.HexagonsInBoard == null) return false;
            for (int i = 0; i < board.HexagonsInBoard.Count; i++)
            {
                Hexagon h = board.HexagonsInBoard[i];
                if (h == null || h.type != TypeOfHex.mountain || h.level < 1) continue;
                if (InteractionRules.DistanceToTarget(at, h) <= 1) return true;
            }
            return false;
        }

        // =================================================================
        //  SAUVEGARDE / RECHARGEMENT
        // =================================================================
        private bool SaveAndReload()
        {
            SaveManager.AutoSave();

            List<SaveGameFile> files = SaveManager.List();
            SaveGameFile mine = null;
            for (int i = 0; i < files.Count; i++)
                if (files[i] != null && files[i].id == SaveManager.CurrentGameId) mine = files[i];

            if (mine == null)
            {
                Fail("sauvegarde : AutoSave n'a rien ecrit de relisible (" + files.Count + " fichier(s) dans le magasin)");
                return false;
            }

            Note("Sauvegarde au tour " + mine.turn + " : " + mine.energy + " Energie, Base " + mine.baseHP
                 + "/" + mine.baseMaxHP + ", " + mine.tanks + " Tank(s), " + mine.foes + " ennemi(s), "
                 + mine.portalsAlive + "/" + mine.portalsTotal + " Shofar(s). Rechargement...");

            _tracks.Clear();
            _pendingCheck = mine;
            SaveManager.PendingLoad = mine;
            Instance = null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return true;
        }

        private void VerifyReload(SaveGameFile file)
        {
            TurnManager turns = TurnManager.Instance;
            int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : -1;
            int baseHp = (BuildingManager.Instance != null) ? BuildingManager.Instance.GetBaseHP() : -1;
            int tanks = 0, foes = 0;
            BoardController board = BoardController.instance;
            if (board != null && board.PawnsInBoard != null)
            {
                for (int i = 0; i < board.PawnsInBoard.Count; i++)
                {
                    PawnController p = board.PawnsInBoard[i];
                    if (p == null) continue;
                    if (p.IsEnemy) foes++; else tanks++;
                }
            }
            int portals = (PortalManager.Instance != null) ? PortalManager.Instance.CountPortals() : -1;

            bool ok = true;
            if (turns.currentTurn != file.turn) { ok = false; Fail("rechargement : tour " + turns.currentTurn + " au lieu de " + file.turn); }
            if (energy != file.energy) { ok = false; Fail("rechargement : " + energy + " Energie au lieu de " + file.energy); }
            if (baseHp != file.baseHP) { ok = false; Fail("rechargement : Base " + baseHp + " PV au lieu de " + file.baseHP); }
            if (tanks != file.tanks) { ok = false; Fail("rechargement : " + tanks + " Tank(s) au lieu de " + file.tanks); }
            if (foes != file.foes) { ok = false; Fail("rechargement : " + foes + " ennemi(s) au lieu de " + file.foes); }
            if (portals != file.portalsAlive) { ok = false; Fail("rechargement : " + portals + " Shofar(s) au lieu de " + file.portalsAlive); }
            if (GameDifficulty.Current != (DifficultyLevel)file.difficulty) { ok = false; Fail("rechargement : difficulte " + GameDifficulty.Current + " au lieu de " + (DifficultyLevel)file.difficulty); }
            if (MapGenerator.CurrentLayout != file.map) { ok = false; Fail("rechargement : plateau " + MapGenerator.CurrentLayout + " au lieu de " + file.map); }

            // Chaque pion sauvegarde est revenu a sa place, avec ses PV et son role.
            if (board != null && file.pawns != null)
            {
                for (int i = 0; i < file.pawns.Count; i++)
                {
                    SavedPawn sp = file.pawns[i];
                    if (sp == null) continue;
                    PawnController p = board.getPawnByCoord(new HexCoord(sp.q, sp.r, -sp.q - sp.r));
                    if (p == null) { ok = false; Fail("rechargement : pion manquant en (" + sp.q + "," + sp.r + ")"); continue; }
                    if (p.IsEnemy != sp.foe || p.level != sp.lvl || p.currentHP != sp.hp || (!sp.foe && (int)p.stance != sp.st))
                    {
                        ok = false;
                        Fail("rechargement : pion en (" + sp.q + "," + sp.r + ") different (" + (p.IsEnemy ? "ennemi" : "Tank")
                             + " rang " + p.level + ", " + p.currentHP + " PV, posture " + (int)p.stance + ") du fichier ("
                             + (sp.foe ? "ennemi" : "Tank") + " rang " + sp.lvl + ", " + sp.hp + " PV, posture " + sp.st + ")");
                    }
                }
            }

            _current.saveCheckOk = ok;
            Note("Rechargement " + (ok ? "fidele" : "INFIDELE") + " (tour " + turns.currentTurn + ").");
            CheckInvariants("apres rechargement");
        }

        // =================================================================
        //  LES INVARIANTS
        // =================================================================
        private void CheckInvariants(string moment)
        {
            BoardController board = BoardController.instance;
            TurnManager turns = TurnManager.Instance;
            if (board == null || turns == null) { Fail(moment + " : plateau ou tour absent"); return; }

            int energy = (EnergyManager.Instance != null) ? EnergyManager.Instance.CurrentEnergy : 0;
            if (energy < 0) Fail(moment + " : Energie negative (" + energy + ")");

            // --- les pions ---
            Dictionary<int, PawnController> seen = new Dictionary<int, PawnController>();
            List<PawnController> pawns = board.PawnsInBoard;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    PawnController p = pawns[i];
                    if (p == null) { Fail(moment + " : pion detruit encore dans la liste du plateau (index " + i + ")"); continue; }
                    string who = (p.IsEnemy ? "ennemi" : "Tank") + " rang " + p.level;

                    if (p.currentHP <= 0) Fail(moment + " : " + who + " a " + p.currentHP + " PV toujours sur le plateau");
                    if (p.currentHP > p.maxHP) Fail(moment + " : " + who + " a " + p.currentHP + "/" + p.maxHP + " PV");
                    if (p.maxHP <= 0) Fail(moment + " : " + who + " sans PV max");
                    if (p.attackDamageMin != p.attackDamageMax) Fail(moment + " : " + who + " a des degats aleatoires");
                    if (p.level < 1 || p.level > 3) Fail(moment + " : " + who + " : rang hors bornes");

                    if (p.hexcoord == null) { Fail(moment + " : " + who + " sans coordonnee"); continue; }
                    Hexagon under = board.getHexByCoord(p.hexcoord);
                    if (under == null) { Fail(moment + " : " + who + " hors plateau (" + p.hexcoord.q + "," + p.hexcoord.r + ")"); continue; }

                    int key = p.hexcoord.q * 1000 + p.hexcoord.r;
                    PawnController other;
                    if (seen.TryGetValue(key, out other) && other != p)
                        Fail(moment + " : deux pions sur la case (" + p.hexcoord.q + "," + p.hexcoord.r + ")");
                    else seen[key] = p;

                    if (board.getPawnByCoord(p.hexcoord) != p)
                        Fail(moment + " : " + who + " en (" + p.hexcoord.q + "," + p.hexcoord.r + ") n'est pas celui que le plateau y voit");

                    if (!p.IsEnemy && ((int)p.stance < 0 || (int)p.stance > 2)) Fail(moment + " : posture hors bornes sur un Tank");
                    if (p.IsEnemy && under.type == TypeOfHex.Base) Fail(moment + " : un ennemi est SUR la Base");
                }
            }

            // --- les cases ---
            List<Hexagon> hexes = board.HexagonsInBoard;
            int portals = 0;
            int needed = InteractionRules.PORTAL_KILLS_TO_BREAK_SHIELD;
            if (hexes != null)
            {
                for (int i = 0; i < hexes.Count; i++)
                {
                    Hexagon h = hexes[i];
                    if (h == null) { Fail(moment + " : case detruite dans la liste du plateau (index " + i + ")"); continue; }
                    if (h.positionInTheBoard == null) { Fail(moment + " : case sans coordonnee"); continue; }

                    if (h.currentHP > h.maxHP) Fail(moment + " : case " + h.type + " a " + h.currentHP + "/" + h.maxHP + " PV");
                    if (h.shieldHP < 0 || h.shieldHP > h.shieldMax) Fail(moment + " : bulle hors bornes sur " + h.type + " (" + h.shieldHP + "/" + h.shieldMax + ")");
                    if (h.level < 0 || h.level > 2) Fail(moment + " : case " + h.type + " au niveau " + h.level);

                    if (h.type == TypeOfHex.portal)
                    {
                        portals++;
                        if (h.currentHP <= 0) Fail(moment + " : un Shofar a " + h.currentHP + " PV mais n'est pas tombe");
                        if (PortalManager.Instance != null)
                        {
                            int kills = PortalManager.Instance.GetInstability(h);
                            int down = PortalManager.Instance.GetShieldDownTurns(h);
                            if (kills < 0 || kills >= needed + 1) Fail(moment + " : fissures hors bornes (" + kills + "/" + needed + ")");
                            if (down < 0 || down > InteractionRules.PORTAL_SHIELD_DOWN_TURNS) Fail(moment + " : bouclier rompu depuis " + down + " tours (max " + InteractionRules.PORTAL_SHIELD_DOWN_TURNS + ")");
                            if (kills > 0 && down > 0) Fail(moment + " : un Shofar a des fissures ET un bouclier rompu");
                        }
                    }
                }
            }
            if (portals > InteractionRules.PORTAL_COUNT) Fail(moment + " : " + portals + " Shofars debout (max " + InteractionRules.PORTAL_COUNT + ")");
            if (PortalManager.Instance != null && PortalManager.Instance.CountPortals() != portals)
                Fail(moment + " : PortalManager compte " + PortalManager.Instance.CountPortals() + " Shofars, le plateau " + portals);

            // --- la Base ---
            if (BuildingManager.Instance != null)
            {
                int hp = BuildingManager.Instance.GetBaseHP();
                int max = BuildingManager.Instance.GetBaseMaxHP();
                if (hp > max) Fail(moment + " : Base " + hp + "/" + max + " PV");
                if (hp <= 0 && !turns.gameOver) Fail(moment + " : Base a 0 PV sans fin de partie");
            }

            // --- l'interface : rien ne doit rester ouvert quand le joueur a la main ---
            if (turns.phase == TurnPhase.Spending)
            {
                if (TeachingPanel.Instance != null && TeachingPanel.Instance.panelRoot != null && TeachingPanel.Instance.panelRoot.activeSelf)
                    Fail(moment + " : le panneau d'enseignement est reste ouvert pendant la depense");
                if (GameOverPanel.Instance != null && GameOverPanel.Instance.panelRoot != null && GameOverPanel.Instance.panelRoot.activeSelf && !turns.gameOver)
                    Fail(moment + " : l'ecran de fin est affiche sans fin de partie");
                if (PhasePace.Running) Fail(moment + " : la barre des phases tourne encore pendant la depense");
                if (ImmersiveCamera.BlocksBoardInput) Fail(moment + " : la vue immersive bloque encore le plateau pendant la depense");
                if (GameManager.instance != null && !GameManager.instance.canClickOrHover)
                    Fail(moment + " : le plateau est verrouille pendant la phase de depense");
            }

            HudController hud = HudController.Instance;
            if (hud != null)
            {
                if (hud.energyText != null && string.IsNullOrEmpty(hud.energyText.text)) Fail(moment + " : compteur d'Energie vide");
                if (hud.turnText != null && string.IsNullOrEmpty(hud.turnText.text)) Fail(moment + " : numero de tour vide");
            }
        }

        private void CheckGameOverScreen()
        {
            GameOverPanel panel = GameOverPanel.Instance;
            if (panel == null || panel.panelRoot == null) { Fail("fin de partie : pas d'ecran de fin dans la scene"); return; }
            if (!panel.panelRoot.activeSelf) Fail("fin de partie : l'ecran de fin ne s'est pas affiche");
            if (panel.titleText != null && string.IsNullOrEmpty(panel.titleText.text)) Fail("fin de partie : titre vide");
            if (panel.replayButton == null) Fail("fin de partie : pas de bouton Rejouer");
        }

        /// <summary>Ce que le joueur a sur le plateau : de quoi voir que deux parties ne se ressemblent pas.</summary>
        private static string Composition()
        {
            BoardController board = BoardController.instance;
            if (board == null) return "";

            int guard = 0, assault = 0, hunt = 0, t2 = 0, foes = 0;
            if (board.PawnsInBoard != null)
                for (int i = 0; i < board.PawnsInBoard.Count; i++)
                {
                    PawnController p = board.PawnsInBoard[i];
                    if (p == null) continue;
                    if (p.IsEnemy) { foes++; continue; }
                    if (p.level >= 2) t2++;
                    if (p.stance == PawnStance.Guard) guard++;
                    else if (p.stance == PawnStance.Assault) assault++;
                    else hunt++;
                }

            int bunkers = 0, gas = 0, crystal = 0, command = 0, turned = 0, fallen = 0;
            if (board.HexagonsInBoard != null)
                for (int i = 0; i < board.HexagonsInBoard.Count; i++)
                {
                    Hexagon h = board.HexagonsInBoard[i];
                    if (h == null) continue;
                    if (h.type == TypeOfHex.Destroyed) { if (InteractionRules.IsTurnedPortal(h)) turned++; else fallen++; continue; }
                    if (h.level < 1) continue;
                    switch (h.type)
                    {
                        case TypeOfHex.hill: bunkers++; break;
                        case TypeOfHex.gas: gas++; break;
                        case TypeOfHex.crystal: crystal++; break;
                        case TypeOfHex.mountain: command++; break;
                    }
                }

            return "Tanks " + (guard + assault + hunt) + " (garde " + guard + ", assaut " + assault + ", chasse " + hunt + ", rang 2 : " + t2
                   + "), ennemis " + foes + ", Bunkers " + bunkers + ", usines " + gas + ", Cristaux " + crystal + ", Centres " + command
                   + ", Shofars tombes " + fallen + ", retournes " + turned;
        }

        // =================================================================
        //  FIN D'UNE PARTIE, RAPPORT
        // =================================================================
        private IEnumerator FinishRun(string outcome)
        {
            Run run = _current;
            run.outcome = outcome;
            run.turns = CurrentTurn();
            run.enemiesKilled = (PortalManager.Instance != null) ? PortalManager.Instance.totalEnemiesKilled : 0;
            run.enemiesSpawned = (PortalManager.Instance != null) ? PortalManager.Instance.totalEnemiesSpawned : 0;
            run.portalsLeft = (PortalManager.Instance != null) ? PortalManager.Instance.CountPortals() : -1;
            run.portalsTurned = InteractionRules.CountTurnedPortals();
            run.composition = Composition();
            run.jammedEnemies = _jammedEnemies;
            _jammedEnemies = 0;

            int errors = _errorCount - run.errorsBefore;
            Note("Partie " + (run.index + 1) + " : " + outcome + " au tour " + run.turns + " - " + run.actions + " actions, "
                 + run.tanksBuilt + " Tanks, " + run.enemiesKilled + "/" + run.enemiesSpawned + " ennemis tues, " + run.portalsLeft + " Shofar(s) restant(s), "
                 + run.failures.Count + " invariant(s) viole(s), " + run.roleNotes.Count + " remarque(s) de role, " + errors + " erreur(s) console, pire image "
                 + run.worstFrameMs.ToString("0") + " ms.");
            Note("  " + run.composition);

            _tracks.Clear();
            yield return null;

            if (_runs.Count < TotalRuns)
            {
                _current = null;
                BeginRun();
                Instance = null;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);

                // Entre deux parties, on rend la memoire des assets de la partie finie.
                // Unity ne le fait pas tout seul assez tot, et une session de douze
                // parties finissait par peser sur chaque image.
                yield return Resources.UnloadUnusedAssets();
                System.GC.Collect();
                yield break;
            }

            WriteReport();
            PlayerPrefs.SetInt(PrefKey, 0);
            PlayerPrefs.Save();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void WriteReport()
        {
            StringBuilder sb = new StringBuilder(8192);
            int totalFailures = 0, totalRoles = 0;
            for (int i = 0; i < _runs.Count; i++) { totalFailures += _runs[i].failures.Count; totalRoles += _runs[i].roleNotes.Count; }

            bool green = (totalFailures == 0 && _errorCount == 0);

            sb.Append("AUDIT GENERAL - Milchemet HaYetzer - ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
            sb.Append("Resultat : ").Append(green ? "VERT (aucun invariant viole, aucune erreur console)" : "ROUGE").Append('\n');
            sb.Append(_runs.Count).Append(" partie(s), ").Append(totalFailures).Append(" invariant(s) viole(s), ")
              .Append(_errorCount).Append(" erreur(s)/exception(s) console, ").Append(totalRoles).Append(" remarque(s) sur les roles.\n\n");

            sb.Append("PARTIES\n");
            for (int i = 0; i < _runs.Count; i++)
            {
                Run r = _runs[i];
                float avg = (r.frames > 0) ? r.sumFrameMs / r.frames : 0f;
                sb.Append("  ").Append(i + 1).Append(". ").Append(r.difficulty).Append(" / plateau ").Append(r.layout)
                  .Append(" / ").Append(StrategyName(r.strategy))
                  .Append(" : ").Append(r.outcome).Append(" (tour ").Append(r.turns).Append(") - ")
                  .Append(r.actions).Append(" actions, ").Append(r.tanksBuilt).Append(" Tanks, ")
                  .Append(r.enemiesKilled).Append('/').Append(r.enemiesSpawned).Append(" ennemis tues, ")
                  .Append(r.portalsLeft).Append(" Shofar(s) restant(s), ").Append(r.portalsTurned).Append(" retourne(s), sauvegarde ")
                  .Append(r.saveCheckDone ? (r.saveCheckOk ? "fidele" : "INFIDELE") : "non testee")
                  .Append(", image moyenne ").Append(avg.ToString("0.0")).Append(" ms, pire ").Append(r.worstFrameMs.ToString("0")).Append(" ms")
                  .Append(", au pire ").Append(r.worstDrawCalls).Append(" appels de dessin dont ")
                  .Append(r.worstGrouped).Append(" regroupes, ").Append(r.worstSetPass)
                  .Append(" changements de materiau\n");
                sb.Append("      plateau final : ").Append(r.composition).Append('\n');
                sb.Append("      jalons : premier bouclier rompu ").Append(r.firstShieldBreakTurn > 0 ? "tour " + r.firstShieldBreakTurn : "jamais")
                  .Append(", premier Shofar tombe ").Append(r.firstPortalFallTurn > 0 ? "tour " + r.firstPortalFallTurn : "jamais")
                  .Append(", ennemis en vie au maximum ").Append(r.maxEnemiesAlive)
                  .Append(", Tanks perdus ").Append(r.tanksLost)
                  .Append(", Base au plus bas ").Append(r.baseLowestHp == int.MaxValue ? 0 : r.baseLowestHp).Append('\n');
                sb.Append("      courbe : ").Append(r.timeline).Append('\n');
                if (r.jammedEnemies > 0) sb.Append("      ennemis coinces (aucune case libre autour, 3 tours) : ").Append(r.jammedEnemies).Append('\n');
                for (int k = 0; k < r.failures.Count; k++) sb.Append("      - INVARIANT ").Append(r.failures[k]).Append('\n');
                for (int k = 0; k < r.roleNotes.Count; k++) sb.Append("      - role ").Append(r.roleNotes[k]).Append('\n');
            }

            if (_errorSamples.Count > 0)
            {
                sb.Append("\nERREURS CONSOLE (").Append(_errorCount).Append(", premieres ").Append(_errorSamples.Count).Append(")\n");
                for (int i = 0; i < _errorSamples.Count; i++) sb.Append("  - ").Append(_errorSamples[i]).Append('\n');
            }

            sb.Append("\nJOURNAL\n").Append(_journal);

            string dir = Path.Combine(Application.dataPath, "../Audit");
            try
            {
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "rapport_audit.txt");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                Debug.Log("[Audit] Rapport ecrit : " + Path.GetFullPath(path) + "\n" + sb.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Audit] Rapport non ecrit (" + e.Message + ") :\n" + sb.ToString());
            }

            _runs.Clear();
            _journal.Length = 0;
            _errorSamples.Clear();
            _errorCount = 0;
            _current = null;
            _tracks.Clear();

            // On rend au joueur ce qu'on lui a emprunte : son allure, sa difficulte,
            // ses vraies sauvegardes, ses enseignements.
            if (_settingsSaved)
            {
                _settingsSaved = false;
                PhasePace.Speed = _playerSpeed;
                GameDifficulty.Current = _playerDifficulty;
            }
            GameLog.Verbose = true;
            SaveManager.Store = null;
            _store = null;
            TeachingPanel.AutoSkip = false;
            if (_logHooked)
            {
                _logHooked = false;
                Application.logMessageReceived -= OnLog;
            }
        }

        // =================================================================
        //  MAGASIN DE SAUVEGARDE EN MEMOIRE : rien n'atterrit chez le joueur
        // =================================================================
        private sealed class MemoryStore : ISaveStore
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();
            private readonly List<string> _buffer = new List<string>();

            public List<string> ReadAll()
            {
                _buffer.Clear();
                foreach (KeyValuePair<string, string> kv in _files) _buffer.Add(kv.Value);
                return _buffer;
            }

            public void Write(string id, string json) { _files[id] = json; }
            public void Delete(string id) { _files.Remove(id); }
        }
    }
}
