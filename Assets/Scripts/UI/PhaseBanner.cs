using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// L'ECRAN QUI ANNONCE CHAQUE ETAPE DU TOUR.
    ///
    /// Un tour enchaine quatre choses tres differentes : on depense, puis les Tanks
    /// agissent, puis le Yetzer Hara, puis les batiments. Sans coupure, tout se
    /// ressemble : le joueur regarde la camera se promener et ne sait plus qui est en
    /// train de jouer. Ce bandeau nomme l'etape qui commence, en une phrase, puis
    /// s'efface.
    ///
    /// Il se construit tout seul, en code, sur son propre Canvas : aucun besoin de
    /// reconstruire le HUD. La police hebraique est reprise d'un texte du HUD deja
    /// present dans la scene.
    ///
    /// Les libelles viennent de Resources/hud_labels.json (cles bannerIncome,
    /// bannerSpending, bannerTanks, bannerEnemies, bannerBuildings et leurs "...Body").
    /// Ce fichier .cs reste en pur ASCII.
    ///
    /// Note d'optimisation : un seul Canvas, cree une fois et reutilise ; aucune
    /// allocation par annonce en dehors du texte lui-meme, et rien ne tourne entre
    /// deux annonces.
    /// </summary>
    public class PhaseBanner : MonoBehaviour
    {
        public static PhaseBanner Instance;

        // OBSOLETES : le rythme du bandeau se regle dans PhasePace, avec celui de
        // toutes les autres mises en scene (BannerFadeIn, BannerHold, BannerFadeOut,
        // BannerMinBeforeSkip). Conserves pour ne rien casser dans une scene qui
        // porterait deja un PhaseBanner.
        [Header("Rythme (OBSOLETE - voir PhasePace)")]
        public float fadeInDuration = 0.25f;
        public float holdDuration = 2.5f;
        public float fadeOutDuration = 0.35f;
        public float minimumBeforeSkip = 0.5f;

        private CanvasGroup _group;
        private TMPro.TextMeshProUGUI _title;
        private TMPro.TextMeshProUGUI _body;
        private Image _band;
        private bool _built;

        // =================================================================
        //  API
        // =================================================================
        /// <summary>
        /// Annonce une etape et ne rend la main qu'a la fin. key est la cle du titre
        /// dans hud_labels.json ; la phrase est prise a la cle key + "Body".
        /// </summary>
        public static IEnumerator PlayPhase(string key, Color accent)
        {
            PhaseBanner banner = Ensure();
            if (banner == null) yield break;

            yield return banner.Run(key, accent);
        }

        private static PhaseBanner Ensure()
        {
            if (Instance != null) return Instance;

            GameObject go = new GameObject("PhaseBanner (auto)");
            return go.AddComponent<PhaseBanner>();
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

        // =================================================================
        //  CONSTRUCTION
        // =================================================================
        private bool Build()
        {
            if (_built) return _group != null;
            _built = true;

            GameObject canvasGo = new GameObject("Bandeau d'etape",
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;   // au-dessus du HUD (5) et des panneaux

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            // Le bandeau : une bande sombre en travers de l'ecran, pas un voile plein.
            // On doit continuer de voir le plateau derriere.
            _band = NewImage("Bande", canvasGo.transform, new Color(0.02f, 0.03f, 0.07f, 0.88f));
            RectTransform band = _band.rectTransform;
            band.anchorMin = new Vector2(0f, 0.5f);
            band.anchorMax = new Vector2(1f, 0.5f);
            band.pivot = new Vector2(0.5f, 0.5f);
            band.anchoredPosition = new Vector2(0f, 60f);
            band.sizeDelta = new Vector2(0f, 250f);

            Image top = NewImage("Filet_Haut", band, new Color(1f, 1f, 1f, 0.5f));
            RectTransform topRect = top.rectTransform;
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0f, 2f);

            Image bottom = NewImage("Filet_Bas", band, new Color(1f, 1f, 1f, 0.5f));
            RectTransform bottomRect = bottom.rectTransform;
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(0f, 2f);

            TMPro.TMP_FontAsset font = FindFont();

            _title = NewText("Titre", band, font, 78f, new Color(0.90f, 0.94f, 1f));
            RectTransform titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 42f);
            titleRect.sizeDelta = new Vector2(1600f, 110f);
            _title.fontStyle = TMPro.FontStyles.Bold;

            _body = NewText("Phrase", band, font, 32f, new Color(0.62f, 0.70f, 0.86f));
            RectTransform bodyRect = _body.rectTransform;
            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = new Vector2(0f, -48f);
            bodyRect.sizeDelta = new Vector2(1500f, 60f);

            canvasGo.SetActive(false);
            return true;
        }

        private static TMPro.TMP_FontAsset FindFont()
        {
            // La police du HUD : le bandeau doit parler la meme langue, au sens propre.
            TMPro.TextMeshProUGUI sample = Object.FindFirstObjectByType<TMPro.TextMeshProUGUI>();
            return (sample != null) ? sample.font : null;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMPro.TextMeshProUGUI NewText(string name, Transform parent,
                                                     TMPro.TMP_FontAsset font, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            TMPro.TextMeshProUGUI text = go.AddComponent<TMPro.TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.isRightToLeftText = true;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            // Crenage coupe : en hebreu RTL, TMP decale les lettres du mauvais cote.
            MNLTHII.UI.TextFeatures.Disable(text);
            return text;
        }

        // =================================================================
        //  LECTURE
        // =================================================================
        private IEnumerator Run(string key, Color accent)
        {
            if (!Build() || _group == null) yield break;

            string title = MNLTHII.UI.HudLabelsRuntime.Get(key);
            string body = MNLTHII.UI.HudLabelsRuntime.Get(key + "Body");

            // Sans libelle, pas de bandeau : mieux vaut rien qu'une bande vide.
            if (string.IsNullOrEmpty(title)) yield break;

            if (_title != null) { _title.text = title; _title.color = accent; }
            if (_body != null)
            {
                _body.text = body;
                _body.gameObject.SetActive(!string.IsNullOrEmpty(body));
            }

            if (_band != null)
            {
                // Les deux filets prennent la couleur de l'etape : on reconnait l'etape
                // a la couleur avant meme d'avoir lu le mot.
                Image[] rules = _band.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < rules.Length; i++)
                    if (rules[i] != _band) rules[i].color = new Color(accent.r, accent.g, accent.b, 0.75f);
            }

            GameObject canvasGo = _group.gameObject;
            canvasGo.SetActive(true);

            // Un son marque la coupure : l'oreille comprend le changement d'etape avant
            // meme que l'oeil ait lu le titre.
            if (FXManager.Instance != null) FXManager.Instance.PlayPhaseSFX();

            yield return Fade(0f, 1f, MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.BannerFadeIn));
            yield return Hold();
            yield return Fade(1f, 0f, MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.BannerFadeOut));

            canvasGo.SetActive(false);
        }

        /// <summary>
        /// Le temps de lecture. Un clic, Espace ou Entree passent au suivant - apres un
        /// court delai, pour qu'un clic destine au plateau n'efface pas le bandeau
        /// avant qu'il soit apparu.
        ///
        /// Le temps d'affichage est passe de 2,5 s a 1,2 s, et c'est le reglage qui
        /// rapporte le plus pour le moins de risque : cinq bandeaux par tour, c'etait
        /// quinze secondes a lire cinq mots qu'on connait par coeur au troisieme tour.
        /// Ceux qui decouvrent le jeu ont la premiere partie pour les lire ; ceux qui
        /// le connaissent n'ont plus a les subir.
        /// </summary>
        private IEnumerator Hold()
        {
            float waited = 0f;
            float duration = MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.BannerHold);
            float minimum = MNLTHII.Managers.PhasePace.Seconds(MNLTHII.Managers.PhasePace.BannerMinBeforeSkip);

            while (waited < duration)
            {
                // "Tout passer" emporte aussi le bandeau : sinon la commande la plus
                // radicale du jeu s'arretait poliment devant chaque annonce d'etape.
                if (MNLTHII.Managers.PhasePace.AnySkip) yield break;

                waited += Time.unscaledDeltaTime;

                if (waited >= minimum
                    && (Input.GetMouseButtonDown(0)
                        || Input.GetKeyDown(KeyCode.Space)
                        || Input.GetKeyDown(KeyCode.Return)
                        || Input.GetKeyDown(KeyCode.Escape)))
                    yield break;

                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (duration <= 0.01f)
            {
                _group.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                _group.alpha = Mathf.Lerp(from, to, k * k * (3f - 2f * k));
                yield return null;
            }

            _group.alpha = to;
        }
    }
}
