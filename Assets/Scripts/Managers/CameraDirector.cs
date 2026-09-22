using System.Collections;
using UnityEngine;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Le cadreur : la camera va voir ce qui se passe, puis revient a sa place.
    ///
    /// POURQUOI
    ///
    /// Les phases 3 et 4 sont automatiques. Sans camera qui suit, le joueur regarde
    /// un plateau entier ou de petites choses bougent quelque part, et il rate
    /// exactement ce qui justifie ses decisions : quel tank a tire, sur quoi, avec
    /// quel resultat. Le projecteur de FXManager designe l'unite, mais il ne
    /// rapproche pas le regard. C'est ce que fait ce composant.
    ///
    /// DEUX REGLES QUI NE SE NEGOCIENT PAS
    ///
    /// 1. On bouge le PIVOT, jamais la camera elle-meme. La Main Camera de cette
    ///    scene porte un Animator : tout ce qu'un script ecrit sur son transform est
    ///    ecrase a la frame suivante par le systeme d'animation. Le pivot parent,
    ///    lui, n'est anime par personne. C'est la seule prise sure.
    ///
    /// 2. On ne change JAMAIS la rotation. Seule la position bouge, le long de l'axe
    ///    de visee du repos. L'angle de vue reste donc exactement celui du jeu, et
    ///    le plateau ne bascule pas sous le joueur - une camera qui pivote sur un
    ///    plateau isometrique fait perdre le nord en trois secondes, et le joueur ne
    ///    sait plus ou est sa Base.
    ///
    /// COMMENT LE ZOOM EST CALCULE
    ///
    /// Au repos, on projette l'axe de visee sur le plan du plateau : on obtient le
    /// point regarde et, surtout, la DISTANCE a laquelle la camera se tient. Cadrer
    /// un point, c'est se replacer sur ce meme axe, a une fraction de cette distance.
    /// Aucun nombre magique : si tu deplaces le pivot dans l'editeur, tout suit.
    ///
    /// Note d'optimisation : aucun Update. Rien ne tourne au repos. Le deplacement
    /// est une coroutine qui ne vit que pendant son mouvement, et une seule a la
    /// fois - une nouvelle demande interrompt la precedente au lieu de s'additionner,
    /// sinon deux interpolations concurrentes se disputent la meme position et la
    /// camera tremble. Temps non mis a l'echelle : le cadrage garde sa duree meme si
    /// le jeu ralentit.
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        public static CameraDirector Instance;

        // =================================================================
        //  REGLAGES
        // =================================================================
        [Header("Cible")]
        [Tooltip("Le transform reellement deplace. Vide : on prend le parent de la camera principale, ou la camera elle-meme si elle n'a pas de parent.")]
        public Transform pivot;

        [Header("Cadrage")]
        [Tooltip("Fraction de la distance de repos utilisee pour un plan rapproche. 0.45 = on se rapproche a 45 % de la distance normale.")]
        [Range(0.15f, 1f)]
        public float closeUpFactor = 0.45f;

        [Tooltip("Fraction utilisee pour cadrer une action entre deux cases : un peu plus large, il faut voir les deux bouts.")]
        [Range(0.15f, 1f)]
        public float actionFactor = 0.62f;

        [Header("Rythme")]
        [Tooltip("Duree du mouvement vers une cible.")]
        public float moveDuration = 0.45f;

        [Tooltip("Duree du retour a la position de repos.")]
        public float returnDuration = 0.6f;

        [Header("Projection")]
        /// <summary>
        /// VUE DE PLATEAU = ORTHOGRAPHIQUE. VUE RAPPROCHEE = PERSPECTIVE.
        ///
        /// En orthographique, s'approcher ne grossit rien : un "zoom" sur un mouvement
        /// ou sur la menace n'etait qu'un deplacement lateral. Des qu'un cadrage
        /// rapproche commence, la camera passe donc en perspective ; au retour a la vue
        /// de plateau, elle redevient orthographique.
        ///
        /// Le champ de vision de la perspective est calcule pour que, a la distance de
        /// repos, l'image soit LA MEME qu'en orthographique : le passage de l'un a
        /// l'autre ne saute pas, et s'approcher grossit vraiment.
        /// </summary>
        public bool perspectiveWhenFocused = true;

        [Tooltip("Champ de vision en perspective pendant les cadrages. 0 = calcule automatiquement pour raccorder avec la vue orthographique.")]
        public float focusFov = 0f;

        [Header("Etat (lecture seule)")]
        public bool isFocused = false;

        // =================================================================
        //  ETAT INTERNE
        // =================================================================
        private Vector3 _restPosition;
        private Quaternion _restRotation;

        // Distance du pivot au plan du plateau, mesuree une fois au demarrage.
        private float _restDistance = 12f;

        // Axe de visee au repos. Toutes les positions cadrees se calculent dessus.
        private Vector3 _restForward = Vector3.forward;

        private Coroutine _move;
        private bool _ready;

        // --- Projection (unique responsable : ce composant) ---
        private Camera _cam;
        private bool _projSaved;
        private bool _savedOrthographic;
        private float _savedNear;
        private float _savedFov;
        private float _projT;          // 0 = orthographique, 1 = perspective
        private float _projTarget;
        private float _projDuration = 0.5f;
        private float _currentFov = 50f;
        private float _targetFov = 50f;
        private Coroutine _projRoutine;

        // --- Prise de controle par ImmersiveCamera (vue tank, camera d'action) ---
        // Pendant une prise de controle, la rotation change aussi : on regarde depuis
        // le sol. On retient donc ou la camera est posee PAR RAPPORT au pivot, pour
        // pouvoir placer le pivot de sorte que la camera tombe exactement ou on veut.
        private Transform _cameraTransform;
        private Vector3 _cameraOffset;
        private Quaternion _cameraLocalRotation = Quaternion.identity;

        /// <summary>Vrai quand le cadreur est pret a bouger (pivot trouve, pose de repos mesuree).</summary>
        public bool IsReady { get { return _ready && pivot != null; } }

        /// <summary>
        /// Vrai pendant une vue immersive. Les demandes de cadrage habituelles (Focus,
        /// FrameAction, Release) sont alors ignorees : deux maitres pour une camera,
        /// c'est une camera qui tremble.
        /// </summary>
        public bool IsOverridden { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            ResolvePivot();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // En Start et non en Awake : si un autre script replace le pivot au
            // demarrage, c'est SA position qu'il faut prendre pour repos, pas celle
            // de la scene. Mesurer trop tot ferait revenir la camera au mauvais endroit.
            CaptureRestPose();
        }

        /// <summary>
        /// Trouve la prise sure. On prefere TOUJOURS le parent : la camera de cette
        /// scene porte un Animator, et ecrire sur son transform ne sert a rien.
        /// </summary>
        private void ResolvePivot()
        {
            if (pivot != null) return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Camera] Aucune camera principale : le cadrage est desactive.");
                return;
            }

            _cameraTransform = cam.transform;
            pivot = (cam.transform.parent != null) ? cam.transform.parent : cam.transform;

            if (cam.transform.parent == null)
                Debug.LogWarning("[Camera] La camera n'a pas de parent : si elle porte un Animator, "
                               + "le cadrage sera ecrase a chaque frame. Mets-la sous un pivot vide.");
        }

        /// <summary>
        /// Memorise la pose de repos et en deduit la distance au plateau.
        ///
        /// Le calcul : on suit l'axe de visee jusqu'au plan y = 0. Le facteur obtenu
        /// EST la distance du pivot au plateau. Si l'axe est horizontal - camera de
        /// cote - il n'y a pas d'intersection, et on garde une valeur de repli
        /// plutot que de diviser par zero et d'envoyer la camera a l'infini.
        /// </summary>
        public void CaptureRestPose()
        {
            if (pivot == null) { _ready = false; return; }

            _restPosition = pivot.position;
            _restRotation = pivot.rotation;
            _restForward = pivot.forward;

            float vertical = _restForward.y;

            if (vertical < -0.05f)
            {
                _restDistance = -_restPosition.y / vertical;
            }
            else
            {
                _restDistance = 12f;
                Debug.LogWarning("[Camera] L'axe de visee ne rencontre pas le plateau : "
                               + "distance de repos forcee a 12.");
            }

            _ready = true;

            // Trace de controle. C'est la seule facon de savoir en un coup d'oeil si
            // le cadreur a trouve une prise et sur quoi il va agir : sans elle, une
            // camera qui ne bouge pas peut aussi bien venir d'un composant absent,
            // d'un pivot manquant ou d'un Animator qui ecrase tout.
            Debug.LogFormat("[Camera] Cadreur pret sur \"{0}\". Distance de repos {1:0.0}, "
                          + "plan rapproche a {2:0.00} de cette distance.",
                            pivot.name, _restDistance, closeUpFactor);
        }

        // =================================================================
        //  FACADE STATIQUE - ce que les boucles de tour appellent
        // =================================================================
        public static void Focus(Transform target)
        {
            if (Instance != null && target != null) Instance.FocusOn(target.position, Instance.closeUpFactor);
        }

        public static void FocusPoint(Vector3 worldPoint)
        {
            if (Instance != null) Instance.FocusOn(worldPoint, Instance.closeUpFactor);
        }

        /// <summary>
        /// Cadre une action entre deux points : on vise le milieu, un peu plus large.
        /// L'attaquant et sa cible doivent tenir tous les deux dans l'image, sinon le
        /// joueur voit un tir partir vers nulle part.
        /// </summary>
        public static void FrameAction(Vector3 from, Vector3 to)
        {
            if (Instance == null) return;

            Vector3 middle = (from + to) * 0.5f;
            Instance.FocusOn(middle, Instance.actionFactor);
        }

        public static void ReleaseCamera()
        {
            if (Instance != null) Instance.Release();
        }

        // =================================================================
        //  MOUVEMENT
        // =================================================================
        public void FocusOn(Vector3 worldPoint, float factor)
        {
            if (!_ready || pivot == null || IsOverridden) return;

            if (factor < 0.05f) factor = 0.05f;
            if (factor > 1f) factor = 1f;

            // Meme axe, meme angle, distance reduite : le plateau ne bascule pas.
            Vector3 destination = worldPoint - _restForward * (_restDistance * factor);

            isFocused = true;
            StartMove(destination, _restRotation, moveDuration);

            if (perspectiveWhenFocused) RequestPerspective(focusFov, moveDuration);
        }

        public void Release()
        {
            if (!_ready || pivot == null || IsOverridden) return;

            isFocused = false;
            StartMove(_restPosition, _restRotation, returnDuration);

            // Retour a la vue de plateau : orthographique, pendant le trajet du retour.
            RequestOrthographic(returnDuration);
        }

        // =================================================================
        //  PRISE DE CONTROLE (ImmersiveCamera)
        // =================================================================
        /// <summary>
        /// Une vue immersive prend la main. On arrete tout mouvement en cours et on
        /// mesure la position de la camera par rapport au pivot.
        /// </summary>
        public void BeginOverride()
        {
            if (!_ready || pivot == null) return;

            StopMove();
            IsOverridden = true;

            if (_cameraTransform == null && Camera.main != null) _cameraTransform = Camera.main.transform;

            if (_cameraTransform == null || _cameraTransform == pivot)
            {
                _cameraOffset = Vector3.zero;
                _cameraLocalRotation = Quaternion.identity;
                return;
            }

            Quaternion inverse = Quaternion.Inverse(pivot.rotation);
            _cameraOffset = inverse * (_cameraTransform.position - pivot.position);
            _cameraLocalRotation = inverse * _cameraTransform.rotation;
        }

        /// <summary>
        /// Place la CAMERA (pas le pivot) a cette position, avec cette orientation.
        /// Le pivot est deplace en consequence. Sans effet hors prise de controle.
        /// </summary>
        public void SetCameraPose(Vector3 cameraPosition, Quaternion cameraRotation)
        {
            if (!IsOverridden || pivot == null) return;

            Quaternion pivotRotation = cameraRotation * Quaternion.Inverse(_cameraLocalRotation);
            pivot.rotation = pivotRotation;
            pivot.position = cameraPosition - pivotRotation * _cameraOffset;
        }

        /// <summary>
        /// Fin de la vue immersive : retour a la pose de repos, POSITION ET ROTATION,
        /// en douceur. Les cadrages habituels reprennent ensuite normalement.
        /// </summary>
        public void EndOverride(float duration)
        {
            if (!IsOverridden) return;

            IsOverridden = false;
            isFocused = false;

            if (!_ready || pivot == null) return;
            StartMove(_restPosition, _restRotation, duration);
            RequestOrthographic(duration);
        }

        /// <summary>Retour immediat, sans transition. Pour les coupures nettes.</summary>
        public void SnapToRest()
        {
            if (!_ready || pivot == null) return;

            StopMove();
            IsOverridden = false;
            pivot.position = _restPosition;
            pivot.rotation = _restRotation;
            isFocused = false;
            ForceOrthographic();
        }

        // =================================================================
        //  PROJECTION
        // =================================================================
        /// <summary>
        /// Passe en perspective (fondu). fov &lt;= 0 : champ de vision raccorde a la vue
        /// orthographique de repos. Sans effet si la camera n'etait pas orthographique
        /// au depart - on ne change jamais une camera qu'on n'a pas mesuree.
        /// </summary>
        public void RequestPerspective(float fov, float duration)
        {
            if (!SaveProjection()) return;

            _targetFov = (fov > 0.1f) ? fov : MatchedFov();

            // Premier passage : on part du champ raccorde, le fondu ne saute donc pas.
            if (_projT <= 0f) _currentFov = MatchedFov();

            _projTarget = 1f;
            _projDuration = Mathf.Max(0.05f, duration);
            StartProjectionBlend();
        }

        /// <summary>Retour en orthographique (fondu). La camera retrouve exactement ses reglages.</summary>
        public void RequestOrthographic(float duration)
        {
            if (!_projSaved) return;

            _projTarget = 0f;
            _targetFov = MatchedFov();
            _projDuration = Mathf.Max(0.05f, duration);
            StartProjectionBlend();
        }

        /// <summary>Retour immediat en orthographique, sans fondu.</summary>
        public void ForceOrthographic()
        {
            if (_projRoutine != null) { StopCoroutine(_projRoutine); _projRoutine = null; }
            if (!_projSaved || _cam == null) return;

            _cam.ResetProjectionMatrix();
            _cam.orthographic = _savedOrthographic;
            _cam.nearClipPlane = _savedNear;
            _cam.fieldOfView = _savedFov;

            _projT = 0f;
            _projTarget = 0f;
            _projSaved = false;
        }

        private bool SaveProjection()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return false;

            if (_projSaved) return true;

            // Deja en perspective (camera reglee ainsi dans la scene) : on n'y touche pas.
            if (!_cam.orthographic) return false;

            _savedOrthographic = true;
            _savedNear = _cam.nearClipPlane;
            _savedFov = _cam.fieldOfView;
            _projT = 0f;
            _projSaved = true;
            return true;
        }

        /// <summary>
        /// Le champ de vision qui, a la distance de repos, montre la meme hauteur de
        /// plateau que la vue orthographique : 2 * atan(taille ortho / distance).
        /// </summary>
        private float MatchedFov()
        {
            if (_cam == null) return 50f;

            float distance = Mathf.Max(0.5f, _restDistance);
            float fov = 2f * Mathf.Atan(_cam.orthographicSize / distance) * Mathf.Rad2Deg;
            return Mathf.Clamp(fov, 10f, 90f);
        }

        private void StartProjectionBlend()
        {
            if (_projRoutine != null) StopCoroutine(_projRoutine);
            _projRoutine = StartCoroutine(ProjectionBlend());
        }

        private IEnumerator ProjectionBlend()
        {
            float fovSpeed = Mathf.Abs(_targetFov - _currentFov) / _projDuration;

            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                _projT = Mathf.MoveTowards(_projT, _projTarget, dt / _projDuration);
                _currentFov = Mathf.MoveTowards(_currentFov, _targetFov, Mathf.Max(fovSpeed, 1f) * dt);

                bool done = Mathf.Approximately(_projT, _projTarget)
                            && Mathf.Approximately(_currentFov, _targetFov);

                ApplyProjection();

                if (done) break;
                yield return null;
            }

            _projRoutine = null;
            if (_projTarget <= 0f) ForceOrthographic();
        }

        private void ApplyProjection()
        {
            if (_cam == null) return;

            float near = Mathf.Max(0.02f, _restDistance * 0.005f);

            if (_projT >= 1f)
            {
                // Perspective franche : Unity calcule lui-meme.
                if (_cam.orthographic) _cam.orthographic = false;
                _cam.nearClipPlane = near;
                _cam.fieldOfView = _currentFov;
                _cam.ResetProjectionMatrix();
                return;
            }

            // Fondu : interpolation terme a terme entre les deux matrices.
            float aspect = _cam.aspect;
            float far = _cam.farClipPlane;
            float size = _cam.orthographicSize;

            Matrix4x4 ortho = Matrix4x4.Ortho(-size * aspect, size * aspect, -size, size, _savedNear, far);
            Matrix4x4 persp = Matrix4x4.Perspective(_currentFov, aspect, near, far);

            float t = _projT * _projT * (3f - 2f * _projT);
            Matrix4x4 m = new Matrix4x4();
            for (int i = 0; i < 16; i++) m[i] = ortho[i] + (persp[i] - ortho[i]) * t;

            _cam.projectionMatrix = m;
        }

        /// <summary>
        /// Cadre une cible, attend, et rend la main. Version attendue par les boucles
        /// qui veulent sequencer - un yield return suffit alors a tenir le rythme.
        /// </summary>
        public IEnumerator FocusRoutine(Vector3 worldPoint, float holdSeconds)
        {
            FocusOn(worldPoint, closeUpFactor);

            yield return new WaitForSecondsRealtime(moveDuration);

            if (holdSeconds > 0f) yield return new WaitForSecondsRealtime(holdSeconds);
        }

        private void StartMove(Vector3 destination, Quaternion rotation, float duration)
        {
            StopMove();
            _move = StartCoroutine(MoveTo(destination, rotation, duration));
        }

        private void StopMove()
        {
            if (_move != null)
            {
                StopCoroutine(_move);
                _move = null;
            }
        }

        /// <summary>
        /// La rotation est interpolee elle aussi. En jeu normal elle vaut deja la
        /// rotation de repos, donc rien ne change ; elle ne bouge vraiment qu'au retour
        /// d'une vue immersive.
        /// </summary>
        private IEnumerator MoveTo(Vector3 destination, Quaternion rotation, float duration)
        {
            if (duration <= 0f)
            {
                pivot.position = destination;
                pivot.rotation = rotation;
                _move = null;
                yield break;
            }

            Vector3 start = pivot.position;
            Quaternion startRotation = pivot.rotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float t = elapsed / duration;
                if (t > 1f) t = 1f;

                // Lissage : une interpolation lineaire se voit, elle demarre et
                // s'arrete sec. Sur une camera, ca donne le mal de mer.
                t = t * t * (3f - 2f * t);

                pivot.position = Vector3.Lerp(start, destination, t);
                pivot.rotation = Quaternion.Slerp(startRotation, rotation, t);
                yield return null;
            }

            pivot.position = destination;
            pivot.rotation = rotation;
            _move = null;
        }
    }
}
