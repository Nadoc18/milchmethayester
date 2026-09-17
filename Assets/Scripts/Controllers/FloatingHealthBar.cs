using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.UI
{
    /// <summary>
    /// Barre de vie flottante en World Space.
    ///
    /// Le canvas n'est volontairement PAS enfant du pion ou de l'hexagone : il vit a la
    /// racine de la scene et suit sa cible a chaque frame. Sinon il heritait de la
    /// rotation du pion (qui pivote vers sa cible a chaque attaque) et de l'echelle du
    /// prefab, ce qui donnait des barres inclinees et de tailles differentes.
    ///
    /// LateUpdate tourne pour chaque barre visible : il est reduit au strict minimum
    /// et ne fait aucune allocation.
    /// </summary>
    public class FloatingHealthBar : MonoBehaviour
    {
        // ---- Dimensions ----
        // La barre est definie en pixels puis mise a l'echelle pour occuper
        // BarWorldWidth unites monde. Un hexagone mesure environ 0.95 unite de large,
        // donc 0.70 represente environ les trois quarts d'une case.
        private const float BarWorldWidth = 0.70f;
        private const float BarPixelWidth = 100f;
        private const float BarPixelHeight = 18f;
        private const float FillPixelWidth = 94f;
        private const float FillPixelHeight = 13f;

        // Hauteur par defaut de la barre au-dessus de sa cible, en unites monde.
        private const float DefaultHeightOffset = 1.0f;
        private const float VisibleDuration = 2.5f;

        private Transform _target;
        private float _heightOffset = DefaultHeightOffset;

        private GameObject _canvasObject;
        private Transform _canvasTransform;
        private CanvasGroup _canvasGroup;
        private Image _fillImage;
        private RectTransform _fillRect;

        private Coroutine _activeRoutine;
        private bool _persistent;

        // Camera.main declenche un FindGameObjectsWithTag : resolue une fois et
        // partagee par toutes les barres de la scene.
        private static Camera _sharedCamera;
        private static Transform _sharedCameraTransform;

        private static readonly Color EnemyColor = new Color(1f, 0.25f, 0.25f);
        private static readonly Color AllyColor = new Color(0.2f, 1f, 0.8f);
        private static readonly Color BackgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);

        private static readonly WaitForSeconds WaitVisible = new WaitForSeconds(VisibleDuration);

        public void Init()
        {
            Init(DefaultHeightOffset);
        }

        /// <summary>heightOffset : hauteur de la barre au-dessus de la cible, en unites monde.</summary>
        public void Init(float heightOffset)
        {
            _heightOffset = heightOffset;

            if (_canvasGroup != null) return;

            _target = transform;
            EnsureCamera();

            // ATTENTION : Canvas exige un RectTransform. Ajouter le composant Canvas a un
            // GameObject ordinaire fait REMPLACER son Transform par un RectTransform, et
            // l'ancien Transform est detruit. Toute reference prise avant cet ajout devient
            // invalide. On declare donc les composants des la creation de l'objet, et on ne
            // recupere le Transform qu'ensuite.
            _canvasObject = new GameObject("FloatingHealthBar", typeof(Canvas), typeof(CanvasGroup));

            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            RectTransform canvasRect = _canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(BarPixelWidth, BarPixelHeight);

            _canvasTransform = canvasRect;

            float scale = BarWorldWidth / BarPixelWidth;
            _canvasTransform.localScale = new Vector3(scale, scale, scale);
            _canvasTransform.position = _target.position + Vector3.up * _heightOffset;

            _canvasGroup = _canvasObject.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            // Une barre de vie ne recoit jamais de clic : on la sort du raycast UI.
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(_canvasTransform, false);
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = BackgroundColor;
            bgImage.raycastTarget = false;
            bgImage.rectTransform.sizeDelta = new Vector2(BarPixelWidth, BarPixelHeight);

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(_canvasTransform, false);
            _fillImage = fillObj.AddComponent<Image>();
            _fillImage.color = AllyColor;
            _fillImage.raycastTarget = false;

            _fillRect = _fillImage.rectTransform;
            _fillRect.sizeDelta = new Vector2(FillPixelWidth, FillPixelHeight);
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.anchoredPosition = new Vector2(-FillPixelWidth / 2f, 0f);

            FaceCamera();
        }

        private static void EnsureCamera()
        {
            if (_sharedCamera != null) return;

            _sharedCamera = Camera.main;
            if (_sharedCamera != null) _sharedCameraTransform = _sharedCamera.transform;
        }

        /// <summary>
        /// persistent : la barre reste affichee au lieu de disparaitre apres quelques
        /// secondes. Utilise pour les Portails, dont il faut suivre l'etat en continu.
        /// </summary>
        public void ShowAndSetHealth(int currentHP, int maxHP, bool isEnemy = false, bool persistent = false)
        {
            if (_canvasGroup == null) Init(_heightOffset);
            if (_fillImage == null || maxHP <= 0) return;

            _persistent = persistent;
            _fillImage.color = isEnemy ? EnemyColor : AllyColor;

            float fillRatio = Mathf.Clamp01((float)currentHP / maxHP);

            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(AnimateBar(fillRatio));
        }

        private IEnumerator AnimateBar(float targetRatio)
        {
            float startWidth = _fillRect.sizeDelta.x;
            float targetWidth = FillPixelWidth * targetRatio;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime * 5f;
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 1f, t);
                _fillRect.sizeDelta = new Vector2(Mathf.Lerp(startWidth, targetWidth, t), FillPixelHeight);
                yield return null;
            }

            // Barre permanente : on ne la fait jamais disparaitre.
            if (_persistent)
            {
                _canvasGroup.alpha = 1f;
                _activeRoutine = null;
                yield break;
            }

            yield return WaitVisible;

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 2f;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _activeRoutine = null;
        }

        private void LateUpdate()
        {
            // La cible ou le canvas ont pu etre detruits dans la meme frame.
            if (_canvasTransform == null || _canvasGroup == null || _target == null) return;

            // Sortie immediate quand la barre est invisible : c'est le cas de la
            // quasi-totalite des pions a chaque frame.
            if (_canvasGroup.alpha <= 0.01f) return;

            _canvasTransform.position = _target.position + Vector3.up * _heightOffset;
            FaceCamera();
        }

        /// <summary>
        /// Aligne la barre sur le plan de la camera : elle est toujours vue de face,
        /// parfaitement horizontale a l'ecran, quel que soit l'angle de vue.
        /// </summary>
        private void FaceCamera()
        {
            // Le canvas peut avoir ete detruit entre-temps (rechargement de scene,
            // destruction de la cible dans la meme frame).
            if (_canvasTransform == null) return;

            if (_sharedCameraTransform == null)
            {
                EnsureCamera();
                if (_sharedCameraTransform == null) return;
            }

            _canvasTransform.rotation = _sharedCameraTransform.rotation;
        }

        /// <summary>
        /// Le canvas vit a la racine de la scene : il ne disparait pas tout seul avec
        /// sa cible, il faut le detruire explicitement.
        /// </summary>
        private void OnDestroy()
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }

            if (_canvasObject != null) Destroy(_canvasObject);
        }
    }
}

// ---------------------------------------------------------------------------
// NOTE
//
// 1. Taille : la barre est decrite en pixels (100 x 10) puis mise a l'echelle pour
//    occuper BarWorldWidth unites monde. Un hexagone mesure environ 0.95 unite, donc
//    0.55 donne une barre lisible sans deborder sur les cases voisines. C'est la seule
//    constante a changer pour ajuster la taille.
// 2. Orientation : le canvas est un objet racine, il n'herite plus de la rotation du
//    pion (qui pivote a chaque attaque) ni de l'echelle du prefab. Il recopie la
//    rotation de la camera, ce qui le rend toujours parfaitement face a l'ecran.
// 3. Camera.main est resolue une fois et partagee par toutes les barres.
// 4. LateUpdate sort avant tout travail quand la barre est transparente, ce qui est le
//    cas de la plupart des unites en permanence.
// 5. Le canvas etant a la racine, OnDestroy doit le detruire explicitement.
// ---------------------------------------------------------------------------
