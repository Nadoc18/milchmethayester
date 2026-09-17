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
            if (!_ready || pivot == null) return;

            if (factor < 0.05f) factor = 0.05f;
            if (factor > 1f) factor = 1f;

            // Meme axe, meme angle, distance reduite : le plateau ne bascule pas.
            Vector3 destination = worldPoint - _restForward * (_restDistance * factor);

            isFocused = true;
            StartMove(destination, moveDuration);
        }

        public void Release()
        {
            if (!_ready || pivot == null) return;

            isFocused = false;
            StartMove(_restPosition, returnDuration);
        }

        /// <summary>Retour immediat, sans transition. Pour les coupures nettes.</summary>
        public void SnapToRest()
        {
            if (!_ready || pivot == null) return;

            StopMove();
            pivot.position = _restPosition;
            pivot.rotation = _restRotation;
            isFocused = false;
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

        private void StartMove(Vector3 destination, float duration)
        {
            StopMove();
            _move = StartCoroutine(MoveTo(destination, duration));
        }

        private void StopMove()
        {
            if (_move != null)
            {
                StopCoroutine(_move);
                _move = null;
            }
        }

        private IEnumerator MoveTo(Vector3 destination, float duration)
        {
            if (duration <= 0f)
            {
                pivot.position = destination;
                _move = null;
                yield break;
            }

            Vector3 start = pivot.position;
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
                yield return null;
            }

            pivot.position = destination;
            _move = null;
        }
    }
}
