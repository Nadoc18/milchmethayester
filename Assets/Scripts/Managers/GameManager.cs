using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MNLTHII
{
    /// <summary>
    /// Entree du joueur et etat global. Update() est le seul code execute a chaque frame
    /// du jeu : il est maintenu a zero allocation (voir la note en bas de fichier).
    /// </summary>
    [ExecuteInEditMode]
    public class GameManager : MonoBehaviour
    {
        #region Singleton
        public static GameManager instance;
        #endregion

        #region Events
        public static HexagonEvent OnClickHexagonEvent;
        public static StateOfGameEvent OnStateOfGameChanged;
        #endregion

        #region Public Fields
        public int turn = 0;
        public Hexagon hexClicked;
        public int[] zonesPrompt;
        public Material[] materials;

        public List<Hexagon> Patterns;
        public List<HexCoord> PatternsCoord = new List<HexCoord>();
        public List<List<HexCoord>> zonesCoord;

        public bool canClickOrHover = false;
        public StateOfGame m_stateOfGame = StateOfGame.none;
        public string jsonPrompt;
        public float timeInSeconds;
        #endregion

        #region Serialize Fields
        [SerializeField] private Button promptBtn;
        [SerializeField] private Button timerBtn;
        [SerializeField] private Button resynchronize;

        [Header("Raycast")]
        [Tooltip("Limiter le raycast aux couches du plateau evite de tester toute la scene.")]
        [SerializeField] private LayerMask boardLayerMask = ~0;
        [SerializeField] private float rayMaxDistance = 500f;
        #endregion

        #region Private Fields
        private GameCanvasController _gameCanvasController;
        private float _timeRemaining = 10;
        private bool _timerIsRunning = false;

        // --- Caches chauds : resolus une fois, jamais dans Update ---
        private Camera _mainCamera;

        // Tampon de raycast reutilise : RaycastNonAlloc n'alloue rien, contrairement
        // a Physics.Raycast(out hit) qui boxe le RaycastHit sur certains backends,
        // et surtout contrairement a Physics.RaycastAll qui alloue un tableau par appel.
        private readonly RaycastHit[] _rayHits = new RaycastHit[4];

        // Delegue mis en cache : evite d'allouer une closure a chaque abonnement.
        private UnityAction _onResynchronizeCached;
        #endregion

        #region Unity Callbacks
        private void Awake()
        {
            // Le doublon part AVANT qu'on touche aux evenements : les recreer ici
            // effacerait les abonnes que le vrai GameManager a deja distribues.
            if (instance != null && instance != this) { Destroy(this.gameObject); return; }
            instance = this;

            // Ces deux evenements sont STATIQUES. On les repart a neuf a chaque
            // demarrage de partie : sans ca, quand le rechargement de domaine est
            // desactive, ils gardent les abonnes de la partie precedente - des
            // objets detruits dont les references d'inspecteur sont nulles, ce qui
            // se traduit par une NullReferenceException au premier clic.
            OnClickHexagonEvent = new HexagonEvent();
            OnStateOfGameChanged = new StateOfGameEvent();

            CacheCamera();
        }

        private void Start()
        {
            _onResynchronizeCached = Resynchronize;
            if (resynchronize != null) resynchronize.onClick.AddListener(_onResynchronizeCached);

            _gameCanvasController = GameCanvasController.instance;
            CacheCamera();
        }

        private void OnDestroy()
        {
            if (resynchronize != null && _onResynchronizeCached != null)
                resynchronize.onClick.RemoveListener(_onResynchronizeCached);

            // Un static qui garde un objet detruit fait croire a la partie suivante
            // qu'elle a deja un GameManager valide.
            if (instance == this)
            {
                instance = null;
                OnClickHexagonEvent = null;
                OnStateOfGameChanged = null;
            }
        }

        private void CacheCamera()
        {
            // Camera.main fait un FindGameObjectsWithTag en interne : jamais dans une boucle.
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            // [ExecuteInEditMode] ferait tourner ce code dans l'editeur hors Play.
            if (!Application.isPlaying) return;

            if (!canClickOrHover) return;
            if (!Input.GetMouseButtonDown(0)) return;

            if (_mainCamera == null)
            {
                CacheCamera();
                if (_mainCamera == null) return;
            }

            HandleClick();
        }
        #endregion

        /// <summary>
        /// Resolution du clic. Aucune allocation : Ray et RaycastHit sont des structs,
        /// le tampon de hits est preallouee et CompareTag ne cree pas de string.
        /// </summary>
        private void HandleClick()
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

            int hitCount = Physics.RaycastNonAlloc(ray, _rayHits, rayMaxDistance, boardLayerMask);
            if (hitCount <= 0) return;

            // On garde le hit le plus proche : RaycastNonAlloc ne trie pas.
            int nearestIndex = 0;
            float nearestDistance = _rayHits[0].distance;
            for (int i = 1; i < hitCount; i++)
            {
                float d = _rayHits[i].distance;
                if (d < nearestDistance) { nearestDistance = d; nearestIndex = i; }
            }

            Collider collider = _rayHits[nearestIndex].collider;
            if (collider == null) return;

            Transform parent = collider.transform.parent;
            if (parent == null) return;
            if (!parent.CompareTag("Hexagon")) return;

            Hexagon clicked = parent.GetComponent<Hexagon>();
            if (clicked == null) return;

            // Eteint le halo de la selection precedente.
            if (hexClicked != null && hexClicked.hoverLockedFX != null)
                hexClicked.hoverLockedFX.SetActive(false);

            if (clicked.hoverLockedFX != null) clicked.hoverLockedFX.SetActive(true);
            hexClicked = clicked;

            Managers.LocalGameEngine engine = Managers.LocalGameEngine.Instance;
            if (engine != null) engine.ProcessHexClick(clicked.positionInTheBoard);

            if (OnClickHexagonEvent != null) OnClickHexagonEvent.Invoke(clicked);
        }

        #region Public Functions

        public void UpdateTurn(string jsonData)
        {
            TurnData turnData = JsonConvert.DeserializeObject<TurnData>(jsonData);
            if (turnData == null) return;

            GameCanvasController canvas = GameCanvasController.instance;
            if (canvas != null) canvas.RefreshTurnInfo(turnData);

            turn = turnData.num;
            if (turnData.prompt != null) zonesPrompt = turnData.prompt.zone;
        }

        // React
        public void GetTimer(float seconds)
        {
            if (_gameCanvasController != null)
            {
                _gameCanvasController._timerMinuteUnit.color = Color.white;
                _gameCanvasController._timerMinuteTen.color = Color.white;
                _gameCanvasController._timerPoints.color = Color.white;
                _gameCanvasController._timerSecondesUnit.color = Color.white;
                _gameCanvasController._timerSecondesTen.color = Color.white;
            }

            canClickOrHover = true;
            _timeRemaining = seconds;
            _timerIsRunning = true;
        }

        /// <summary>Recherche a plat, sans lambda : List.Find(x => ...) alloue une closure a chaque appel.</summary>
        public Hexagon GetHexByCoord(Hexagon hexagon)
        {
            if (hexagon == null || Patterns == null) return null;

            HexCoord target = hexagon.positionInTheBoard;
            for (int i = 0; i < Patterns.Count; i++)
            {
                Hexagon candidate = Patterns[i];
                if (candidate != null && target.CompareHexCoord(candidate.positionInTheBoard))
                    return candidate;
            }
            return null;
        }

        // React
        public void GetPatterns(string jsonData)
        {
            PatternsCoord = JsonConvert.DeserializeObject<List<HexCoord>>(jsonData);
        }

        // React
        public void GetZones(string jsonData)
        {
            zonesCoord = JsonConvert.DeserializeObject<List<List<HexCoord>>>(jsonData);
        }

        public void GetStateOfGame(int stateOfGame)
        {
            switch (stateOfGame)
            {
                case 0: OnStateOfGameChanged.Invoke(StateOfGame.defeat); break;
                case 1: OnStateOfGameChanged.Invoke(StateOfGame.victory); break;
            }
        }

        public void ClearHexClicked()
        {
            if (hexClicked == null) return;

            // hoverLockedFX est deja la reference directe : pas de transform.Find(string).
            if (hexClicked.hoverLockedFX != null) hexClicked.hoverLockedFX.SetActive(false);
            hexClicked = null;
        }

        public void Resynchronize()
        {
            SceneManager.LoadScene("Game");
        }
        #endregion
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. Camera.main effectue un FindGameObjectsWithTag a chaque acces. Il etait appele
//    a chaque clic ; il est desormais resolu dans Awake et memorise (_mainCamera).
// 2. Physics.Raycast(out hit) est remplace par Physics.RaycastNonAlloc avec un tampon
//    membre de 4 elements, plus un masque de couches et une distance maximale : le
//    cout du test physique est borne et aucune memoire n'est allouee par clic.
// 3. GetComponent<Hexagon>() n'est plus appele deux fois sur le meme transform.
// 4. List.Find(x => ...) dans GetHexByCoord allouait une closure a chaque appel ;
//    remplace par une boucle plate indexee, sans delegue.
// 5. Le listener du bouton Resynchronize etait une lambda () => Resynchronize(),
//    donc une allocation ; il est mis en cache dans un UnityAction et retire dans
//    OnDestroy pour ne pas fuir.
// 6. Update sort immediatement hors Play Mode ([ExecuteInEditMode] le faisait tourner
//    dans l'editeur) et avant tout travail si le plateau est verrouille.
// ---------------------------------------------------------------------------
