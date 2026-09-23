using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.UI
{
    /// <summary>
    /// LE ROLE DU TANK, ECRIT AU-DESSUS DE LUI.
    ///
    /// La posture etait deja lisible a la teinte du modele, mais il fallait connaitre
    /// le code des couleurs. Une pastille qui NOMME le role se lit sans rien
    /// apprendre, et permet de voir d'un coup d'oeil comment toute l'armee est reglee.
    ///
    /// Le petit canevas vit a la racine de la scene et suit le Tank, comme la barre de
    /// vie : enfant du pion, il heriterait de sa rotation (le Tank pivote vers sa
    /// cible) et de l'echelle du prefab.
    ///
    /// Pose automatiquement sur chaque Tank du joueur (voir PawnController.Start).
    /// Les libelles hebreux viennent de hud_labels.json (stanceGuard, stanceAssault,
    /// stanceHunt) : ce fichier .cs reste en pur ASCII.
    ///
    /// Note d'optimisation : LateUpdate ne fait qu'une position, une orientation et une
    /// comparaison d'entier. Le texte n'est reecrit que lorsque la posture CHANGE.
    /// </summary>
    public class StanceMarker : MonoBehaviour
    {
        private const float HeightOffset = 1.55f;
        private const float WorldWidth = 0.42f;
        private const float PixelWidth = 64f;
        private const float PixelHeight = 64f;

        /// <summary>
        /// Un PICTOGRAMME plutot qu'un mot : vu de haut, en vue orthographique, la
        /// petite etiquette de texte devenait illisible. Le bouclier dit "garde",
        /// le triangle inverse "chasse" (c'est le symbole de l'ennemi), l'hexagone
        /// perce "assaut" (c'est le symbole du Shofar) - les memes symboles que
        /// partout ailleurs dans l'interface.
        /// </summary>
        private static readonly IconKind[] Icons =
        {
            IconKind.Bunker,   // Garde   : le bouclier
            IconKind.Portal,   // Assaut  : le Shofar, sa cible
            IconKind.Enemy     // Chasse  : le Yetzer Hara, sa cible
        };

        private static Camera _sharedCamera;
        private static Transform _sharedCameraTransform;

        private PawnController _pawn;
        private Transform _pawnTransform;
        private GameObject _canvasObject;
        private Transform _canvasTransform;
        private Image _background;
        private Image _icon;
        private int _shownStance = -1;

        private void Start()
        {
            _pawn = GetComponent<PawnController>();
            _pawnTransform = transform;

            if (_pawn == null || _pawn.IsEnemy) { Destroy(this); return; }

            Build();
        }

        private void OnDestroy()
        {
            if (_canvasObject != null) Destroy(_canvasObject);
        }

        private void Build()
        {
            EnsureCamera();

            // Canvas exige un RectTransform : on declare le composant des la creation
            // de l'objet, sinon Unity remplace le Transform et les references prises
            // avant deviennent invalides.
            _canvasObject = new GameObject("StanceMarker", typeof(Canvas));
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 101;

            RectTransform canvasRect = _canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(PixelWidth, PixelHeight);
            _canvasTransform = canvasRect;

            float scale = WorldWidth / PixelWidth;
            _canvasTransform.localScale = new Vector3(scale, scale, scale);

            GameObject backGo = new GameObject("Fond", typeof(RectTransform));
            RectTransform backRect = (RectTransform)backGo.transform;
            backRect.SetParent(_canvasTransform, false);
            backRect.anchorMin = Vector2.zero;
            backRect.anchorMax = Vector2.one;
            backRect.offsetMin = Vector2.zero;
            backRect.offsetMax = Vector2.zero;

            // La silhouette sombre derriere : c'est elle qui detache le pictogramme
            // du plateau, quelle que soit la couleur de la case.
            _background = backGo.AddComponent<Image>();
            _background.color = new Color(0f, 0f, 0f, 0.55f);
            _background.raycastTarget = false;
            _background.preserveAspect = true;
            backRect.offsetMin = new Vector2(-7f, -7f);
            backRect.offsetMax = new Vector2(7f, 7f);

            GameObject iconGo = new GameObject("Icone", typeof(RectTransform));
            RectTransform iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(_canvasTransform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            _icon = iconGo.AddComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;

            Refresh(true);
        }

        private static void EnsureCamera()
        {
            if (_sharedCamera != null) return;
            _sharedCamera = Camera.main;
            _sharedCameraTransform = (_sharedCamera != null) ? _sharedCamera.transform : null;
        }

        private void Refresh(bool force)
        {
            int stance = (int)_pawn.stance;
            if (!force && stance == _shownStance) return;
            _shownStance = stance;

            Color accent = StanceStyle.AccentOf(stance);
            IconKind kind = (stance >= 0 && stance < Icons.Length) ? Icons[stance] : Icons[0];
            Sprite sprite = IconLibrary.Get(kind);

            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.color = accent;
            }

            if (_background != null) _background.sprite = sprite;
        }

        private void LateUpdate()
        {
            if (_canvasTransform == null || _pawn == null || _pawnTransform == null) return;

            if (_pawn.currentHP <= 0)
            {
                if (_canvasObject.activeSelf) _canvasObject.SetActive(false);
                return;
            }

            Refresh(false);

            if (_sharedCameraTransform == null)
            {
                EnsureCamera();
                if (_sharedCameraTransform == null) return;
            }

            _canvasTransform.position = _pawnTransform.position + Vector3.up * HeightOffset;
            _canvasTransform.rotation = _sharedCameraTransform.rotation;
        }
    }
}
