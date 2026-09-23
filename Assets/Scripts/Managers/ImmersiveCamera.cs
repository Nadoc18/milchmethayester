using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LA CAMERA IMMERSIVE : deux facons de descendre au niveau du sol.
    ///
    /// A. VUE TANK (a la demande)
    ///    Pendant la phase de depense, clique sur un de tes Tanks : l'ecran de choix a
    ///    un bouton "vue depuis le tank". La camera se place derriere et au-dessus de
    ///    lui, a la troisieme personne : le Tank en bas de l'image, le terrain devant.
    ///      - glisser la souris (ou A / D, fleches) : tourner autour du Tank
    ///      - glisser vers le haut / le bas (ou W / S) : monter / descendre la camera
    ///      - molette : se rapprocher / s'eloigner
    ///      - Echap ou le bouton "retour a la carte" : revenir
    ///    Le plateau ne reagit a aucun clic pendant ce temps : on regarde, on ne joue pas.
    ///    Finir le tour (Espace) ramene aussi la camera.
    ///
    /// B. CAMERA D'ACTION (automatique, rare)
    ///    Pendant la phase des Tanks, sur un coup decisif seulement : un Tank qui abat
    ///    un ennemi de rang 2 ou plus, qui acheve un Shofar, ou le tout premier ennemi
    ///    abattu de la partie. Plan par-dessus l'epaule, ralenti, puis retour.
    ///    Une fois par tour au maximum : rare, donc marquant. Un clic ou Echap l'abrege.
    ///
    /// La camera elle-meme n'est jamais touchee : tout passe par le PIVOT de
    /// CameraDirector (la camera porte un Animator qui ecraserait tout). Pendant ces
    /// plans, CameraDirector ignore les autres demandes de cadrage, puis reprend la
    /// main en ramenant la camera a sa pose de repos, rotation comprise.
    ///
    /// Note d'optimisation : le composant se desactive quand il ne filme pas - aucun
    /// LateUpdate au repos. Aucune allocation pendant un plan (tampon de raycast
    /// membre, WaitForSecondsRealtime crees une fois).
    /// </summary>
    public class ImmersiveCamera : MonoBehaviour
    {
        public static ImmersiveCamera Instance;

        private enum Mode { None, Tank, Shot }

        // =================================================================
        //  REFERENCES (posees par le HudBuilder)
        // =================================================================
        [Header("Interface")]
        [Tooltip("Le bandeau affiche pendant la vue tank (bouton retour + aide).")]
        public GameObject overlayRoot;
        public Button exitButton;

        [Tooltip("Le HUD principal, masque pendant les plans pour laisser toute la place a l'image.")]
        public CanvasGroup hudGroup;
        public bool hideHud = true;

        // =================================================================
        //  REGLAGES - VUE TANK
        // =================================================================
        // VUE A LA TROISIEME PERSONNE : la camera suit le Tank de derriere et
        // au-dessus, et c'est LUI qu'elle regarde - il occupe le bas du centre de
        // l'image, le terrain devant lui s'etend au-dessus. Comme dans un jeu d'action
        // vu de dos.
        //
        // Les reglages portent des noms neufs (prefixe tp) : Unity garde dans la scene
        // les valeurs des anciens champs, et les nouvelles valeurs par defaut ne se
        // seraient jamais appliquees.
        [Header("A. Vue tank (troisieme personne)")]
        [Tooltip("Distance derriere le Tank, en largeurs de case.")]
        public float tpDistance = 2.2f;
        public float tpMinDistance = 1.0f;
        public float tpMaxDistance = 4.5f;

        [Tooltip("Hauteur de depart au-dessus du haut du Tank, en largeurs de case.")]
        public float tpHeight = 1.1f;

        [Tooltip("Limites de hauteur quand on glisse la souris verticalement.")]
        public float tpMinHeight = 0.2f;
        public float tpMaxHeight = 3f;

        [Tooltip("Largeurs de case par pixel de glissement vertical.")]
        public float heightDragSpeed = 0.006f;

        [Tooltip("Le point vise est un peu DEVANT le Tank (en cases) : le Tank reste dans " +
                 "le bas de l'image et on voit ou il va.")]
        public float tpLookAhead = 0.8f;

        [Tooltip("Le point vise est un peu AU-DESSUS du Tank (en cases) : plus haut, le " +
                 "Tank descend dans l'image.")]
        public float tpLookHeight = 0.25f;

        [Tooltip("Degres par pixel de glissement de souris.")]
        public float dragSpeed = 0.25f;

        [Tooltip("Degres par seconde avec A / D ou les fleches.")]
        public float keyYawSpeed = 90f;

        [Tooltip("Pas de zoom par cran de molette, en largeurs de case.")]
        public float zoomStep = 0.2f;

        public float enterDuration = 0.7f;
        public float exitDuration = 0.6f;

        [Tooltip("Garde-fou : distance minimale entre la camera et le relief sous elle.")]
        public float groundClearance = 0.25f;

        // =================================================================
        //  REGLAGES - CAMERA D'ACTION
        // =================================================================
        [Header("B. Camera d'action")]
        public bool actionShots = true;

        [Tooltip("Rang minimum d'un ennemi pour que son elimination soit filmee.")]
        public int minEnemyLevel = 2;

        [Tooltip("Filmer aussi le tout premier ennemi abattu de la partie, quel que soit son rang.")]
        public bool firstKillAlways = true;

        [Tooltip("Filmer le coup qui acheve un Shofar.")]
        public bool portalFinisher = true;

        [Tooltip("Vitesse du jeu pendant le plan (1 = normale).")]
        [Range(0.1f, 1f)] public float slowMotion = 0.35f;

        [Tooltip("Distance derriere le Tank, en largeurs de case.")]
        public float shotDistance = 1.2f;
        [Tooltip("Decalage sur le cote (plan par-dessus l'epaule), en largeurs de case.")]
        public float shotSide = 0.45f;
        // Renomme (ex-shotHeight) pour la meme raison.
        [Tooltip("Hauteur au-dessus du haut du Tank, en largeurs de case.")]
        public float shotCameraHeight = 0.75f;

        public float shotEnterDuration = 0.35f;
        [Tooltip("Temps garde sur l'impact apres la resolution, en secondes reelles.")]
        public float shotHold = 0.35f;

        [Tooltip("Garde-fou : un plan qui dure plus que ca (secondes reelles) est coupe.")]
        public float shotMaxDuration = 6f;

        [Header("Sol")]
        [Tooltip("Couches testees pour ne jamais passer sous le relief.")]
        public LayerMask groundMask = ~0;

        // =================================================================
        //  PROJECTION : ORTHOGRAPHIQUE -> PERSPECTIVE
        // =================================================================
        // La camera du plateau est ORTHOGRAPHIQUE : pas de profondeur, les objets ont
        // la meme taille pres ou loin. C'est ideal pour lire un plateau, et c'est ce
        // qui rendait la vue tank plate et fausse - en orthographique, s'approcher ne
        // grossit rien. Pendant les plans immersifs on passe donc en PERSPECTIVE, avec
        // un fondu entre les deux matrices de projection pour que le changement ne
        // saute pas a l'oeil, puis on revient exactement aux reglages d'origine.
        [Header("Projection")]
        [Tooltip("Passer en perspective pendant la vue tank et la camera d'action.")]
        public bool switchToPerspective = true;

        [Tooltip("Champ de vision en perspective, en degres.")]
        [Range(20f, 90f)] public float perspectiveFov = 50f;

        [Tooltip("Plan proche en perspective, en largeurs de case.")]
        public float perspectiveNear = 0.03f;

        // =================================================================
        //  ETAT
        // =================================================================
        private Mode _mode = Mode.None;

        // =================================================================
        //  SECOUSSE (le tremblement de l'impact)
        // =================================================================
        /// <summary>
        /// LE TREMBLEMENT DE CAMERA, PENDANT UN PLAN.
        ///
        /// Le jeu en a un (EZCameraShake, sur la Main Camera) : il decale la camera par
        /// rapport a son pivot. Pendant un plan d'action, c'est ce script qui POSE la
        /// camera a chaque LateUpdate, et la pose repasse par-dessus la secousse. Le
        /// coup partait donc sans que rien ne tremble - c'est justement le moment ou
        /// ca compte le plus.
        ///
        /// Celle-ci est appliquee APRES le calcul de la pose, donc rien ne peut
        /// l'ecraser. Elle est coupee net a la fin du plan.
        /// </summary>
        [Header("Secousse de l'impact")]
        [Tooltip("Amplitude du tremblement, en unites monde.")]
        public float shakeMagnitude = 0.16f;
        [Tooltip("Inclinaison ajoutee, en degres.")]
        public float shakeTilt = 1.4f;
        [Tooltip("Nervosite : nombre d'oscillations par seconde.")]
        public float shakeRoughness = 14f;
        [Tooltip("Duree d'une secousse, en secondes reelles.")]
        public float shakeDuration = 0.45f;

        private float _shake;        // 1 au depart, descend vers 0
        private float _shakeSeed;

        /// <summary>
        /// Secoue la camera si un plan est en cours. Appele par FXManager a chaque
        /// impact : le tremblement reste synchronise avec l'effet et le son.
        /// </summary>
        public static void Kick(float strength)
        {
            ImmersiveCamera self = Instance;
            if (self == null || self._mode == Mode.None) return;

            if (strength < 0.1f) strength = 0.1f;
            if (strength > 1f) strength = 1f;

            // Une secousse deja en cours n'est pas remplacee par une plus faible.
            if (strength > self._shake) self._shake = strength;
            self._shakeSeed = Random.value * 100f;
        }

        private Transform _camTransform;

        private PawnController _tank;

        // Ce que la vue regarde : le Tank, ou une case. Une case n'a pas de "devant",
        // la camera tourne donc autour de son centre.
        private Transform _focus;
        private bool _focusIsTank;
        private float _yaw;
        private float _distance;
        private float _height;
        private float _tankTop;
        private float _tankBase;

        private Vector3 _startPos;
        private Quaternion _startRot;
        private float _transition;
        private float _transitionDuration;

        private bool _dragging;
        private Vector3 _lastMouse;

        private Vector3 _shotPos;
        private Quaternion _shotRot;
        private float _shotStartedAt;
        private float _savedTimeScale = 1f;
        private bool _timeSlowed;
        private bool _skipRequested;
        private int _lastShotTurn = -1;
        private bool _firstKillShown;

        // Projection : on retient les reglages d'origine pour les rendre a l'identique.
        private Camera _cam;
        // Toujours faux depuis que CameraDirector gere la projection ; garde pour ne
        // pas toucher a la logique d'extinction du composant.
        private bool _projActive = false;

        private readonly RaycastHit[] _groundHits = new RaycastHit[4];
        private UnityEngine.Events.UnityAction _exitCallback;

        private const float HexWidthBoardUnits = 0.9526f;

        /// <summary>Vrai pendant un plan : le plateau ne doit reagir a aucun clic.</summary>
        public static bool BlocksBoardInput
        {
            get { return Instance != null && Instance._mode != Mode.None; }
        }

        public bool InTankView { get { return _mode == Mode.Tank; } }

        // =================================================================
        //  CYCLE DE VIE
        // =================================================================
        private void Awake()
        {
            if (Instance == null) Instance = this;

            Camera cam = Camera.main;
            if (cam != null) { _camTransform = cam.transform; _cam = cam; }

            // Cable ICI et pas dans le builder : un abonnement pose depuis l'editeur
            // n'est pas enregistre dans la scene (voir HowToPlayPanel).
            _exitCallback = ExitTankView;
            if (exitButton != null) exitButton.onClick.AddListener(_exitCallback);

            ShowOverlay(false);
            enabled = false;
        }

        private void OnDestroy()
        {
            if (exitButton != null && _exitCallback != null) exitButton.onClick.RemoveListener(_exitCallback);

            // Ne jamais laisser le jeu au ralenti derriere soi - ni la camera en perspective.
            RestoreTime();
            RestoreProjectionNow();

            if (Instance == this) Instance = null;
        }

        // =================================================================
        //  A. VUE TANK
        // =================================================================
        public static void EnterTankView(PawnController tank)
        {
            if (Instance != null) Instance.BeginTankView(tank);
        }

        /// <summary>
        /// Voir une case de pres. S'il y a un de tes Tanks dessus, c'est la vue tank ;
        /// sinon la camera tourne autour de la case - batiment, Shofar, terrain,
        /// ennemi. Memes commandes, meme bandeau de retour.
        /// </summary>
        public static void EnterHexView(Hexagon hex)
        {
            if (Instance != null) Instance.BeginHexView(hex);
        }

        private void BeginHexView(Hexagon hex)
        {
            if (hex == null || _mode != Mode.None) return;

            BoardController board = BoardController.instance;
            PawnController occupant = (board != null) ? board.getPawnByCoord(hex.positionInTheBoard) : null;
            if (occupant != null && !occupant.IsEnemy && occupant.currentHP > 0)
            {
                BeginTankView(occupant);
                return;
            }

            TurnManager turns = TurnManager.Instance;
            if (turns == null || !turns.IsSpendingPhase) return;

            CameraDirector director = CameraDirector.Instance;
            if (director == null || !director.IsReady) return;

            if (!EnsureCamera()) return;

            _tank = null;
            _focus = hex.transform;
            _focusIsTank = false;
            MeasureTank(hex.transform);

            // On part de l'angle de la camera du plateau : le joueur garde ses reperes,
            // la vue plonge simplement vers la case au lieu de pivoter d'un coup.
            Vector3 forward = _camTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            _yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

            _distance = Mathf.Clamp(tpDistance, tpMinDistance, tpMaxDistance);
            _height = Mathf.Clamp(tpHeight, tpMinHeight, tpMaxHeight);
            _dragging = false;

            director.BeginOverride();
            BeginTransition(enterDuration);
            BeginPerspective(enterDuration);

            _mode = Mode.Tank;
            ShowOverlay(true);
            SetHudVisible(false);
            enabled = true;
        }

        private void BeginTankView(PawnController tank)
        {
            if (tank == null || tank.IsEnemy || _mode != Mode.None) return;

            TurnManager turns = TurnManager.Instance;
            if (turns == null || !turns.IsSpendingPhase) return;

            CameraDirector director = CameraDirector.Instance;
            if (director == null || !director.IsReady) return;

            if (!EnsureCamera()) return;

            _tank = tank;
            _focus = tank.transform;
            _focusIsTank = true;
            MeasureTank(tank.transform);

            // On part de la ou le Tank regarde : c'est sa derniere direction de
            // marche ou de tir, donc ce qu'il "voit".
            Vector3 forward = tank.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            _yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

            _distance = Mathf.Clamp(tpDistance, tpMinDistance, tpMaxDistance);
            _height = Mathf.Clamp(tpHeight, tpMinHeight, tpMaxHeight);
            _dragging = false;

            director.BeginOverride();
            BeginTransition(enterDuration);
            BeginPerspective(enterDuration);

            _mode = Mode.Tank;
            ShowOverlay(true);
            SetHudVisible(false);
            enabled = true;
        }

        public void ExitTankView()
        {
            if (_mode != Mode.Tank) return;

            _mode = Mode.None;
            _tank = null;
            _focus = null;
            _dragging = false;

            ShowOverlay(false);
            SetHudVisible(true);

            CameraDirector director = CameraDirector.Instance;
            if (director != null) director.EndOverride(exitDuration);

            // On reste actif le temps de revenir en orthographique, puis on s'eteint.
            EndPerspective(exitDuration);
            enabled = _projActive;
        }

        // =================================================================
        //  B. CAMERA D'ACTION (appelee par TurnManager)
        // =================================================================
        /// <summary>
        /// Ce coup merite-t-il un plan ? Appele AVANT le tir : on predit l'issue avec
        /// les memes regles que le combat (degats fixes, filtre du bouclier).
        /// </summary>
        public static bool WantsActionShot(PawnController tank, PawnController targetPawn,
                                           Hexagon targetHex, int turn)
        {
            return Instance != null && Instance.Wants(tank, targetPawn, targetHex, turn);
        }

        private bool Wants(PawnController tank, PawnController targetPawn, Hexagon targetHex, int turn)
        {
            if (!actionShots || _mode != Mode.None || tank == null) return false;
            if (turn == _lastShotTurn) return false;

            CameraDirector director = CameraDirector.Instance;
            if (director == null || !director.IsReady) return false;

            int damage = InteractionRules.GetPawnDamage(tank);

            if (targetPawn != null)
            {
                if (!targetPawn.IsEnemy || targetPawn.currentHP <= 0) return false;
                if (targetPawn.currentHP > damage) return false;

                if (targetPawn.level >= minEnemyLevel) return true;
                return firstKillAlways && !_firstKillShown;
            }

            if (portalFinisher && targetHex != null && targetHex.type == TypeOfHex.portal && targetHex.currentHP > 0)
            {
                int effective = InteractionRules.FilterPortalDamage(targetHex, damage);
                return targetHex.currentHP <= effective;
            }

            return false;
        }

        /// <summary>Debut du plan : la camera passe derriere l'epaule du Tank, le jeu ralentit.</summary>
        public static void BeginActionShot(PawnController tank, Vector3 impact, int turn)
        {
            if (Instance != null) Instance.StartShot(tank, impact, turn);
        }

        /// <summary>
        /// Fin du plan, a attendre avec yield return : on garde l'impact un instant,
        /// on rend la vitesse normale, et la camera repart vers sa pose de repos.
        /// </summary>
        public static IEnumerator EndActionShot()
        {
            if (Instance == null || Instance._mode != Mode.Shot) return null;
            return Instance.FinishShot();
        }

        private void StartShot(PawnController tank, Vector3 impact, int turn)
        {
            if (tank == null || _mode != Mode.None) return;

            CameraDirector director = CameraDirector.Instance;
            if (director == null || !director.IsReady || !EnsureCamera()) return;

            _lastShotTurn = turn;
            _firstKillShown = true;
            _skipRequested = false;

            MeasureTank(tank.transform);

            float s = HexWidth();
            Vector3 origin = tank.transform.position;

            Vector3 dir = impact - origin;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = tank.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 right = new Vector3(dir.z, 0f, -dir.x);

            Vector3 pos = origin - dir * (shotDistance * s) + right * (shotSide * s);
            pos.y = _tankTop + shotCameraHeight * s;
            pos = KeepAboveGround(pos, s);

            // On regarde un peu au-dela du milieu du duel, a mi-hauteur du Tank :
            // le Tank est au premier plan, la cible au centre de l'image.
            Vector3 look = Vector3.Lerp(origin, impact, 0.65f);
            look.y = Mathf.Lerp(_tankBase, _tankTop, 0.5f);

            _shotPos = pos;
            _shotRot = Quaternion.LookRotation(look - pos, Vector3.up);

            director.BeginOverride();
            BeginTransition(shotEnterDuration);
            BeginPerspective(shotEnterDuration);

            _mode = Mode.Shot;
            _shotStartedAt = Time.unscaledTime;
            SetHudVisible(false);

            _savedTimeScale = Time.timeScale;
            Time.timeScale = slowMotion;
            _timeSlowed = true;

            enabled = true;
        }

        private IEnumerator FinishShot()
        {
            if (!_skipRequested && shotHold > 0f)
            {
                float until = Time.unscaledTime + shotHold;
                while (Time.unscaledTime < until && !_skipRequested) yield return null;
            }

            EndShotNow();
        }

        private void EndShotNow()
        {
            if (_mode != Mode.Shot) return;

            RestoreTime();

            _mode = Mode.None;
            SetHudVisible(true);

            CameraDirector director = CameraDirector.Instance;
            if (director != null) director.EndOverride(exitDuration);

            EndPerspective(exitDuration);
            enabled = _projActive;
        }

        private void RestoreTime()
        {
            if (!_timeSlowed) return;

            Time.timeScale = (_savedTimeScale > 0f) ? _savedTimeScale : 1f;
            _timeSlowed = false;
        }

        // =================================================================
        //  BOUCLE (active seulement pendant un plan)
        // =================================================================
        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            UpdateProjection(dt);

            if (_mode == Mode.None)
            {
                // Plus de plan : on ne reste allume que pour finir le retour en
                // orthographique.
                if (!_projActive) enabled = false;
                return;
            }

            Vector3 pos;
            Quaternion rot;

            if (_mode == Mode.Tank)
            {
                TurnManager turns = TurnManager.Instance;
                // Ce qu'on regardait a disparu : Tank detruit, ou case reconstruite (une
                // case qui change de niveau est un NOUVEL objet - l'ancien est detruit).
                bool lost = (_focus == null)
                            || (_focusIsTank && (_tank == null || _tank.currentHP <= 0));

                if (lost || turns == null || !turns.IsSpendingPhase)
                {
                    ExitTankView();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Escape)) { ExitTankView(); return; }

                HandleTankInput(dt);
                ComputeTankPose(out pos, out rot);
            }
            else
            {
                // Un clic ou Echap abrege le plan : le jeu reprend sa vitesse tout de
                // suite, la sequence de tour se termine d'elle-meme.
                if (!_skipRequested && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape)))
                {
                    _skipRequested = true;
                    RestoreTime();
                }

                // Garde-fous : si la partie vient de se terminer (le coup a acheve le
                // dernier Shofar), l'ecran de fin doit apparaitre - on coupe apres un
                // court instant. Et si la sequence de tour a ete interrompue, personne
                // n'appellera EndActionShot : on ne reste jamais bloque au ralenti.
                float elapsed = Time.unscaledTime - _shotStartedAt;
                TurnManager turnState = TurnManager.Instance;
                bool gameEnded = turnState != null && turnState.gameOver && elapsed > 1.2f;

                if (gameEnded || elapsed > shotMaxDuration)
                {
                    EndShotNow();
                    return;
                }

                pos = _shotPos;
                rot = _shotRot;
            }

            ApplyPose(pos, rot, dt);
        }

        private void HandleTankInput(float dt)
        {
            // Glisser pour tourner - sauf si la souris est sur un bouton de l'interface.
            if (Input.GetMouseButtonDown(0))
            {
                EventSystem events = EventSystem.current;
                _dragging = (events == null || !events.IsPointerOverGameObject());
                _lastMouse = Input.mousePosition;
            }

            if (_dragging)
            {
                if (Input.GetMouseButton(0))
                {
                    Vector3 mouse = Input.mousePosition;
                    _yaw += (mouse.x - _lastMouse.x) * dragSpeed;

                    // Vertical : monter ou descendre la camera. Glisser vers le haut
                    // l'eleve, comme si on tirait la vue vers le ciel.
                    _height = Mathf.Clamp(_height + (mouse.y - _lastMouse.y) * heightDragSpeed,
                                          tpMinHeight, tpMaxHeight);
                    _lastMouse = mouse;
                }
                else
                {
                    _dragging = false;
                }
            }

            float keys = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keys -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keys += 1f;
            _yaw += keys * keyYawSpeed * dt;

            float lift = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) lift += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) lift -= 1f;
            if (lift != 0f) _height = Mathf.Clamp(_height + lift * dt, tpMinHeight, tpMaxHeight);

            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0f) _distance = Mathf.Clamp(_distance - wheel * zoomStep, tpMinDistance, tpMaxDistance);
        }

        private void ComputeTankPose(out Vector3 pos, out Quaternion rot)
        {
            float s = HexWidth();
            Vector3 origin = _focus.position;
            Vector3 dir = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;

            pos = origin - dir * (_distance * s);
            pos.y = _tankTop + _height * s;
            pos = KeepAboveGround(pos, s);

            // On regarde le Tank lui-meme (un peu devant, un peu au-dessus) : c'est ce
            // qui fait une vue a la troisieme personne plutot qu'une vue embarquee.
            // Une case n'a pas de "devant" : on regarde son centre.
            float ahead = _focusIsTank ? tpLookAhead : 0f;
            Vector3 look = origin + dir * (ahead * s);
            look.y = _tankTop + tpLookHeight * s;

            rot = Quaternion.LookRotation(look - pos, Vector3.up);
        }

        private void ApplyPose(Vector3 pos, Quaternion rot, float dt)
        {
            CameraDirector director = CameraDirector.Instance;
            if (director == null) return;

            // La secousse s'ajoute a la pose finale, sinon elle serait ecrasee.
            if (_shake > 0f)
            {
                float fade = (shakeDuration > 0.02f) ? dt / shakeDuration : 1f;
                _shake -= fade;
                if (_shake < 0f) _shake = 0f;

                float t = Time.unscaledTime * shakeRoughness;
                float nx = Mathf.PerlinNoise(_shakeSeed, t) * 2f - 1f;
                float ny = Mathf.PerlinNoise(_shakeSeed + 13.7f, t) * 2f - 1f;
                float nz = Mathf.PerlinNoise(_shakeSeed + 27.3f, t) * 2f - 1f;

                float amount = _shake * _shake;   // s'eteint en douceur

                Vector3 right = rot * Vector3.right;
                Vector3 up = rot * Vector3.up;
                pos += (right * nx + up * ny) * (shakeMagnitude * amount);
                rot *= Quaternion.Euler(ny * shakeTilt * amount,
                                        nx * shakeTilt * amount,
                                        nz * shakeTilt * amount);
            }

            if (_transition < 1f)
            {
                _transition += (_transitionDuration > 0.01f) ? dt / _transitionDuration : 1f;
                float t = Mathf.Clamp01(_transition);
                t = t * t * (3f - 2f * t);

                pos = Vector3.Lerp(_startPos, pos, t);
                rot = Quaternion.Slerp(_startRot, rot, t);
            }

            director.SetCameraPose(pos, rot);
        }

        private void BeginTransition(float duration)
        {
            _startPos = _camTransform.position;
            _startRot = _camTransform.rotation;
            _transition = 0f;
            _transitionDuration = duration;
        }

        // =================================================================
        //  OUTILS
        // =================================================================
        /// <summary>
        /// Le haut et le bas du Tank en hauteur monde, mesures une fois a l'entree du
        /// plan. La camera se cale sur le Tank reel, quelle que soit sa taille.
        /// </summary>
        private void MeasureTank(Transform tank)
        {
            Renderer[] renderers = tank.GetComponentsInChildren<Renderer>(false);

            bool found = false;
            Bounds bounds = new Bounds(tank.position, Vector3.zero);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                if (r.GetComponent<ReadabilityDecal>() != null) continue;

                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }

            _tankBase = found ? bounds.min.y : tank.position.y;
            _tankTop = found ? bounds.max.y : tank.position.y + 0.5f;
        }

        /// <summary>Ne jamais passer sous le relief : une montagne derriere le Tank souleve la camera.</summary>
        private Vector3 KeepAboveGround(Vector3 pos, float s)
        {
            float clearance = groundClearance * s;
            Vector3 from = new Vector3(pos.x, pos.y + 20f * s, pos.z);

            int count = Physics.RaycastNonAlloc(from, Vector3.down, _groundHits, 40f * s, groundMask,
                                                QueryTriggerInteraction.Ignore);

            float highest = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                // On ignore le Tank lui-meme : il est sous la camera par construction.
                Collider c = _groundHits[i].collider;
                if (c == null) continue;
                if (_tank != null && c.transform.IsChildOf(_tank.transform)) continue;

                float y = _groundHits[i].point.y;
                if (y > highest) highest = y;
            }

            if (highest != float.MinValue && pos.y < highest + clearance)
                pos.y = highest + clearance;

            return pos;
        }

        private bool EnsureCamera()
        {
            if (_camTransform != null && _cam != null) return true;

            Camera cam = Camera.main;
            if (cam != null) { _camTransform = cam.transform; _cam = cam; }
            return _camTransform != null;
        }

        // =================================================================
        //  PROJECTION
        // =================================================================
        // La projection (orthographique / perspective) est geree par CameraDirector,
        // seul responsable : une seule source de verite, sinon les deux se disputaient
        // la camera quand un cadrage rapproche precedait une vue immersive.
        private void BeginPerspective(float duration)
        {
            if (!switchToPerspective) return;

            CameraDirector director = CameraDirector.Instance;
            if (director != null) director.RequestPerspective(perspectiveFov, duration);
        }

        private void EndPerspective(float duration)
        {
            // Rien : CameraDirector.EndOverride ramene la camera a sa pose de repos ET
            // en orthographique, pendant le meme trajet.
        }

        private void UpdateProjection(float dt)
        {
        }

        /// <summary>Retour immediat en orthographique (fermeture de scene, arret force).</summary>
        private void RestoreProjectionNow()
        {
            CameraDirector director = CameraDirector.Instance;
            if (director != null) director.ForceOrthographic();
        }

        private static Matrix4x4 LerpMatrix(Matrix4x4 a, Matrix4x4 b, float t)
        {
            Matrix4x4 m = new Matrix4x4();
            for (int i = 0; i < 16; i++) m[i] = a[i] + (b[i] - a[i]) * t;
            return m;
        }

        private static float HexWidth()
        {
            BoardController board = BoardController.instance;
            float scale = (board != null) ? board.transform.lossyScale.x : 1f;
            if (scale < 0.00001f) scale = 1f;
            return HexWidthBoardUnits * scale;
        }

        private void ShowOverlay(bool show)
        {
            if (overlayRoot != null && overlayRoot.activeSelf != show) overlayRoot.SetActive(show);
        }

        private void SetHudVisible(bool visible)
        {
            if (!hideHud || hudGroup == null) return;

            hudGroup.alpha = visible ? 1f : 0f;
            hudGroup.blocksRaycasts = visible;
            hudGroup.interactable = visible;
        }
    }
}

// =============================================================================
// NOTE D'OPTIMISATION
// -----------------------------------------------------------------------------
// - enabled = false hors plan : aucun LateUpdate ne tourne pendant la partie.
// - Pendant un plan : un raycast sans allocation par frame (tampon membre), des
//   calculs de vecteurs sur la pile. Aucune chaine, aucun LINQ.
// - MeasureTank alloue un tableau de renderers, UNE fois a l'entree d'un plan.
// - Aucun cout en WebGL : c'est la meme camera, deplacee. Pas de seconde camera,
//   pas de rendu en texture.
// =============================================================================
