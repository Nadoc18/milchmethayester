using UnityEngine;
using UnityEngine.UI;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Petit relais a poser sur le bouton d'interface qui ouvre la "Vision du
    /// Yetzer Hara".
    ///
    /// Pourquoi un composant separe : ThreatPreview est ajoute automatiquement au
    /// demarrage par LocalGameEngine, il n'existe donc pas dans la scene a l'edition
    /// et on ne peut pas le glisser dans le OnClick d'un Button. Ce relais, lui, vit
    /// sur le bouton : il se branche tout seul et trouve ThreatPreview a l'execution.
    ///
    /// Mise en place : poser ce composant sur le GameObject du bouton. C'est tout.
    /// Rien a cabler dans OnClick.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ThreatPreviewButton : MonoBehaviour
    {
        [Header("Libelles (optionnel)")]
        [Tooltip("Texte affiche quand l'apercu est masque.")]
        public string showLabel = "VOIR LA MENACE";
        [Tooltip("Texte affiche quand l'apercu est ouvert.")]
        public string hideLabel = "MASQUER";
        [Tooltip("Champ de texte du bouton. Laisse vide pour ne pas changer le libelle.")]
        public TMPro.TextMeshProUGUI label;

        [Header("Teinte (optionnel)")]
        public bool tintWhenActive = true;
        public Color activeColor = new Color(1f, 0.55f, 0.2f);

        private Button _button;
        private Image _image;
        private Color _restingColor;
        private bool _lastState;

        // Delegue mis en cache : une lambda dans AddListener allouerait une closure.
        private UnityEngine.Events.UnityAction _onClickCached;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _image = GetComponent<Image>();
            if (_image != null) _restingColor = _image.color;

            _onClickCached = HandleClick;
            if (_button != null) _button.onClick.AddListener(_onClickCached);

            // Les libelles du HUD, s'ils n'ont pas ete poses par le constructeur.
            if (label != null)
            {
                string show = MNLTHII.UI.HudLabelsRuntime.Get("threatShow");
                string hide = MNLTHII.UI.HudLabelsRuntime.Get("threatHide");
                if (!string.IsNullOrEmpty(show)) showLabel = show;
                if (!string.IsNullOrEmpty(hide)) hideLabel = hide;
            }

            Refresh(false);
        }

        /// <summary>
        /// L'ANCIEN BOUTON GRIS S'EFFACE DEVANT LE NOUVEAU. Le bouton d'origine (un
        /// Button Unity par defaut, sans champ TextMeshPro, libelle en francais) reste
        /// dans la scene ; des que le HUD construit porte son propre relais avec un
        /// vrai libelle, le vieux se cache. Sans HUD reconstruit, il reste : on ne
        /// perd jamais l'acces a la souris.
        /// </summary>
        private void Start()
        {
            // Celui du HUD construit vit sous le HudController ; l'ancien, non.
            if (GetComponentInParent<HudController>() != null) return;

            ThreatPreviewButton[] relays = Object.FindObjectsByType<ThreatPreviewButton>(FindObjectsSortMode.None);
            for (int i = 0; i < relays.Length; i++)
            {
                if (relays[i] != null && relays[i] != this
                    && relays[i].GetComponentInParent<HudController>() != null)
                {
                    gameObject.SetActive(false);
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            if (_button != null && _onClickCached != null)
                _button.onClick.RemoveListener(_onClickCached);
        }

        private void HandleClick()
        {
            ThreatPreview preview = ThreatPreview.Instance;
            if (preview == null)
            {
                Debug.LogWarning("[ThreatPreviewButton] ThreatPreview absent de la scene.");
                return;
            }

            preview.TogglePreview();
            Refresh(preview.isVisible);
        }

        /// <summary>
        /// L'apercu peut aussi etre ouvert au clavier, ou referme tout seul quand la
        /// phase de depense se termine : le bouton se resynchronise a chaque frame,
        /// mais ne touche a l'interface que si l'etat a reellement change.
        /// </summary>
        private void Update()
        {
            ThreatPreview preview = ThreatPreview.Instance;
            if (preview == null) return;
            if (preview.isVisible == _lastState) return;

            Refresh(preview.isVisible);
        }

        private void Refresh(bool visible)
        {
            _lastState = visible;

            if (label != null) label.text = MNLTHII.UI.Rtl.Fix(visible ? hideLabel : showLabel);

            if (tintWhenActive && _image != null)
                _image.color = visible ? activeColor : _restingColor;
        }
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. Update() sort en deux comparaisons quand rien n'a change : aucune ecriture
//    d'interface, aucune allocation de string, meme quand le bouton reste a l'ecran.
// 2. Le listener est un UnityAction mis en cache et retire dans OnDestroy : une
//    lambda () => HandleClick() allouerait une closure et fuirait au rechargement.
// 3. Les composants Button et Image sont resolus une seule fois dans Awake.
// ---------------------------------------------------------------------------
