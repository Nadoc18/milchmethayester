using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// LE GAIN D'ENERGIE, EN GROS, AU MILIEU DE L'ECRAN.
    ///
    /// Une usine qui paie affichait un petit "+15" au-dessus d'elle et le compteur
    /// montait dans un coin. Deux evenements minuscules, a deux endroits, qu'on rate
    /// en regardant ailleurs.
    ///
    /// Ici le montant apparait en TRES GRAND au centre, puis file vers le compteur
    /// d'Energie en haut de l'ecran en retrecissant. Il arrive, le compteur monte : le
    /// lien entre l'usine et le solde devient une seule chose qu'on suit des yeux.
    ///
    /// Construit en code, sur son propre Canvas : rien a reconstruire dans le HUD.
    ///
    /// Note d'optimisation : un seul Canvas et un seul texte, crees une fois et
    /// reutilises. Rien ne tourne entre deux gains.
    /// </summary>
    public class EnergyGainFlight : MonoBehaviour
    {
        public static EnergyGainFlight Instance;

        [Header("Rythme")]
        [Tooltip("Temps d'affichage en grand au centre, avant le depart.")]
        public float holdDuration = 0.55f;

        [Tooltip("Duree du vol vers le compteur.")]
        public float flightDuration = 0.65f;

        [Header("Taille")]
        public float bigFontSize = 190f;
        public float smallScale = 0.22f;

        private CanvasGroup _group;
        private RectTransform _canvasRect;
        private RectTransform _numberRect;
        private TMPro.TextMeshProUGUI _number;
        private bool _built;

        private static readonly Color Gold = new Color(1f, 0.78f, 0.36f);

        /// <summary>
        /// Montre le gain et ne rend la main qu'a l'arrivee sur le compteur. Appele par
        /// TurnManager, usine par usine.
        /// </summary>
        public static IEnumerator Play(int amount)
        {
            if (amount <= 0) yield break;

            EnergyGainFlight self = Ensure();
            if (self == null) yield break;

            yield return self.Run(amount);
        }

        private static EnergyGainFlight Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("EnergyGainFlight (auto)").AddComponent<EnergyGainFlight>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private bool Build()
        {
            if (_built) return _group != null;
            _built = true;

            GameObject canvasGo = new GameObject("Vol d'energie",
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 58;   // sous le bandeau d'etape, au-dessus du HUD

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _canvasRect = canvasGo.GetComponent<RectTransform>();

            GameObject numberGo = new GameObject("Montant", typeof(RectTransform));
            numberGo.layer = 5;
            _numberRect = (RectTransform)numberGo.transform;
            _numberRect.SetParent(_canvasRect, false);
            _numberRect.anchorMin = new Vector2(0.5f, 0.5f);
            _numberRect.anchorMax = new Vector2(0.5f, 0.5f);
            _numberRect.pivot = new Vector2(0.5f, 0.5f);
            _numberRect.sizeDelta = new Vector2(900f, 300f);

            _number = numberGo.AddComponent<TMPro.TextMeshProUGUI>();
            _number.font = FindFont();
            _number.fontSize = bigFontSize;
            _number.color = Gold;
            _number.alignment = TMPro.TextAlignmentOptions.Center;
            _number.raycastTarget = false;
            _number.enableWordWrapping = false;
            _number.fontStyle = TMPro.FontStyles.Bold;

            // Le montant est un NOMBRE : jamais en RTL, sinon 15 s'affiche 51.
            _number.isRightToLeftText = false;
            MNLTHII.UI.TextFeatures.Disable(_number);

            canvasGo.SetActive(false);
            return true;
        }

        private static TMPro.TMP_FontAsset FindFont()
        {
            TMPro.TextMeshProUGUI sample = Object.FindFirstObjectByType<TMPro.TextMeshProUGUI>();
            return (sample != null) ? sample.font : null;
        }

        /// <summary>Ou se trouve le compteur d'Energie, en coordonnees de NOTRE canvas.</summary>
        private Vector2 CounterPosition()
        {
            HudController hud = HudController.Instance;
            if (hud == null || hud.energyText == null || _canvasRect == null)
                return new Vector2(0f, 420f);

            Vector3 screen = RectTransformUtility.WorldToScreenPoint(null, hud.energyText.rectTransform.position);

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out local))
                return local;

            return new Vector2(0f, 420f);
        }

        private IEnumerator Run(int amount)
        {
            if (!Build() || _number == null) yield break;

            GameObject canvasGo = _group.gameObject;
            canvasGo.SetActive(true);

            _number.SetText("+{0}", amount);
            _numberRect.anchoredPosition = Vector2.zero;
            _numberRect.localScale = Vector3.one;
            _group.alpha = 0f;

            // 1. Il grossit en apparaissant, au centre.
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.18f);
                _group.alpha = k;
                float pop = Mathf.Lerp(0.6f, 1.08f, k);
                _numberRect.localScale = new Vector3(pop, pop, 1f);
                yield return null;
            }

            _group.alpha = 1f;
            _numberRect.localScale = Vector3.one;

            yield return new WaitForSecondsRealtime(holdDuration);

            // 2. Il file vers le compteur en retrecissant.
            Vector2 from = Vector2.zero;
            Vector2 to = CounterPosition();
            float duration = (flightDuration > 0.05f) ? flightDuration : 0.05f;

            t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float ease = k * k;                     // lent au depart, rapide a l'arrivee

                _numberRect.anchoredPosition = Vector2.Lerp(from, to, ease);

                float scale = Mathf.Lerp(1f, smallScale, ease);
                _numberRect.localScale = new Vector3(scale, scale, 1f);

                _group.alpha = Mathf.Lerp(1f, 0.15f, ease * ease);
                yield return null;
            }

            _group.alpha = 0f;
            canvasGo.SetActive(false);
        }
    }
}
