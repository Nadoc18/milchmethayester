using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Moteur local : remplace l'ancien serveur ReactJS. Construit la carte,
    /// route les clics vers le Trivia et applique le resultat via InteractionRules.
    /// </summary>
    public class LocalGameEngine : MonoBehaviour
    {
        public static LocalGameEngine Instance { get; private set; }

        private BoardController _boardController;

        [Header("Local Data Mock")]
        [TextArea(5, 10)]
        public string initialBoardJson;

        [Header("Generation")]
        public bool useProceduralMap = true;

        [Header("Presentation")]
        [Tooltip("Au debut d'une partie NEUVE, la camera passe sur chaque type de case "
               + "et explique ce qu'on peut y construire, puis donne les grandes lignes "
               + "d'une bonne strategie. Echap saute la visite. Reprendre une partie "
               + "sauvegardee ne la rejoue jamais.")]
        public bool playIntroTour = true;

        private void Awake()
        {
            if (gameObject.GetComponent<TurnManager>() == null) gameObject.AddComponent<TurnManager>();
            if (gameObject.GetComponent<EnemyAI>() == null) gameObject.AddComponent<EnemyAI>();
            if (gameObject.GetComponent<FXManager>() == null) gameObject.AddComponent<FXManager>();
            if (gameObject.GetComponent<EnergyManager>() == null) gameObject.AddComponent<EnergyManager>();
            if (gameObject.GetComponent<PortalManager>() == null) gameObject.AddComponent<PortalManager>();
            if (gameObject.GetComponent<BuildingManager>() == null) gameObject.AddComponent<BuildingManager>();
            if (gameObject.GetComponent<ThreatPreview>() == null) gameObject.AddComponent<ThreatPreview>();

            // La terre qui refleurit quand un Shofar tombe. Posee ici comme les autres :
            // elle existe meme dans une scene ou personne n'a ajoute le composant.
            if (gameObject.GetComponent<LandBloom>() == null) gameObject.AddComponent<LandBloom>();

            // Les cordons ennemi-Shofar. Ils se posent ici comme tout le reste : ainsi
            // ils existent meme dans une scene ou personne n'a ajoute le composant a la
            // main, et ils partagent la duree de vie des gestionnaires qui les nourrissent.
            if (gameObject.GetComponent<PortalTether>() == null) gameObject.AddComponent<PortalTether>();

            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); }
        }

        private void Start()
        {
            _boardController = FindObjectOfType<BoardController>();
            Invoke(nameof(InitializeLocalGame), 1.0f);
        }

        private void InitializeLocalGame()
        {
            Debug.Log("LocalGameEngine: construction de la partie locale...");

            if (_boardController == null)
            {
                Debug.LogError("LocalGameEngine: aucun BoardController dans la scene.");
                return;
            }

            // UNE PARTIE A REPRENDRE ?
            //
            // Le menu principal a pose le fichier ici avant de charger la scene. Dans ce
            // cas le plateau ne vient pas du generateur mais de la sauvegarde : les
            // Bunkers construits, les usines montees, les Shofars entames y sont deja.
            MNLTHII.Data.SaveGameFile saved = SaveManager.PendingLoad;

            string boardJson;

            if (saved != null)
            {
                // La difficulte AVANT tout le reste : elle decide des PV et des degats
                // des ennemis, donc elle doit etre en place avant le premier pion pose.
                GameDifficulty.Current = (DifficultyLevel)saved.difficulty;
                boardJson = SaveManager.BuildBoardJson(saved);
            }
            else if (!useProceduralMap && !string.IsNullOrEmpty(initialBoardJson))
                boardJson = initialBoardJson;
            else
                boardJson = MapGenerator.GenerateIntelligentBoard();

            if (string.IsNullOrEmpty(boardJson))
            {
                Debug.LogWarning("LocalGameEngine: aucune donnee de carte disponible.");
                return;
            }

            _boardController.UpdateBoard(boardJson);

            if (saved != null) Invoke(nameof(RestoreSavedGame), 1.5f);
            else Invoke(nameof(SetupInitialState), 1.5f);
        }

        /// <summary>
        /// Remet la partie sauvegardee en place, une fois les 169 cases construites.
        /// Contrairement a SetupInitialState, rien n'est remis a zero : ni l'Energie,
        /// ni la Base, ni les Shofars - tout vient du fichier.
        /// </summary>
        private void RestoreSavedGame()
        {
            MNLTHII.Data.SaveGameFile saved = SaveManager.PendingLoad;

            // Consomme : un retour au menu puis une nouvelle partie ne doit pas
            // recharger celle-ci par surprise.
            SaveManager.PendingLoad = null;

            if (saved == null) { SetupInitialState(); return; }

            SaveManager.RestoreInto(saved, _boardController);

            if (GameManager.instance != null && GameManager.instance.zonesCoord == null)
            {
                GameManager.instance.zonesCoord = new List<List<HexCoord>>();
                for (int i = 0; i <= 10; i++) GameManager.instance.zonesCoord.Add(new List<HexCoord>());
            }

            if (GameManager.OnStateOfGameChanged != null)
                GameManager.OnStateOfGameChanged.Invoke(StateOfGame.inGame);

            if (TurnManager.Instance != null) TurnManager.Instance.ResumeSavedTurn(saved.turn);
        }

        private void SetupInitialState()
        {
            // Une partie neuve : elle recoit son propre fichier de sauvegarde, qui sera
            // reecrit a chaque tour. Sans cet appel, elle ecraserait celui de la partie
            // chargee juste avant.
            SaveManager.BeginNewGame();

            MapGenerator.SpawnInitialUnits(_boardController);

            // Solde d'ouverture : sans lui, le premier tour ne permettrait aucune action.
            if (EnergyManager.Instance != null) EnergyManager.Instance.ResetEnergy(InteractionRules.STARTING_ENERGY);
            if (BuildingManager.Instance != null) BuildingManager.Instance.InitializeBase();
            if (PortalManager.Instance != null) PortalManager.Instance.ResetPortalState();
            if (_boardController != null)
            {
                _boardController.RefreshAllAuras();
                _boardController.RefreshStructureHealthBars();
            }

            if (GameManager.instance != null && GameManager.instance.zonesCoord == null)
            {
                GameManager.instance.zonesCoord = new List<List<HexCoord>>();
                for (int i = 0; i <= 10; i++) GameManager.instance.zonesCoord.Add(new List<HexCoord>());
            }

            if (GameManager.OnStateOfGameChanged != null)
                GameManager.OnStateOfGameChanged.Invoke(StateOfGame.inGame);

            int portals = PortalManager.Instance != null ? PortalManager.Instance.CountPortals() : 0;
            Debug.Log("LocalGameEngine: partie prete. " + portals + " portails a detruire.");

            StartCoroutine(OpenNewGame());
        }

        /// <summary>
        /// L'ouverture d'une partie neuve : la visite du plateau, PUIS le premier tour.
        ///
        /// La visite passe avant, et pas apres : une fois le premier tour lance, le
        /// joueur a deja de l'energie a depenser et des decisions a prendre - lui
        /// expliquer le terrain a ce moment-la, c'est l'interrompre.
        /// </summary>
        private IEnumerator OpenNewGame()
        {
            if (playIntroTour) yield return StartCoroutine(IntroTour.Play());

            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.gameOver = false;
                TurnManager.Instance.currentTurn = 1;
                TurnManager.Instance.StartTurn();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log("LocalGameEngine: reconstruction complete du plateau...");
                if (_boardController != null)
                {
                    CancelInvoke(nameof(SetupInitialState));
                    _boardController.ClearMap();
                    _boardController.UpdateBoard(MapGenerator.GenerateIntelligentBoard());
                    Invoke(nameof(SetupInitialState), 1.5f);
                }
            }
        }

        // =================================================================
        //  CLIC JOUEUR -> PHASE DE DEPENSE -> REGLES
        // =================================================================
        /// <summary>
        /// Clic du joueur pendant sa phase de depense. Il n'ouvre plus de question :
        /// le Trivia est passe en debut de tour et a deja verse le revenu. Ici, on
        /// depense, autant de fois que le solde le permet.
        /// </summary>
        public void ProcessHexClick(HexCoord hexCoord)
        {
            TurnManager turns = TurnManager.Instance;
            if (turns == null || !turns.IsSpendingPhase) return;

            Debug.Log("LocalGameEngine: clic sur l'hexagone (" + hexCoord.q + "," + hexCoord.r + "," + hexCoord.s + ")");

            turns.HandleSpendingClick(hexCoord);
        }

        /// <summary>
        /// Applique directement une action payante sur un hexagone. Conserve pour les
        /// scripts de test et les UnityEvents de la scene.
        /// </summary>
        public TriviaOutcome ResolveTriviaAnswer(HexCoord hexCoord, bool correct)
        {
            if (!correct)
            {
                Debug.Log("LocalGameEngine: aucune action demandee.");
                return TriviaOutcome.EnergyOnly;
            }

            Hexagon hex = (_boardController != null) ? _boardController.getHexByCoord(hexCoord) : null;
            TriviaOutcome outcome = InteractionRules.ApplyPlayerAction(hex);
            Debug.Log("LocalGameEngine: resultat -> " + outcome);
            return outcome;
        }

        /// <summary>Compatibilite avec les anciens appels (UnityEvents de la scene).</summary>
        public void ApplyCommandPoints(HexCoord hexCoord, int points)
        {
            ResolveTriviaAnswer(hexCoord, points > 0);
        }
    }
}
