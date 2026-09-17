using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MNLTHII;
using MNLTHII.Rules;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Le bandeau permanent : energie, tour, phase, shofars restants, PV de la Base.
    ///
    /// Ce composant ne cree AUCUN element d'interface : il se contente de remplir
    /// ceux que tu as construits dans la scene. Tu poses la hierarchie, tu glisses
    /// les references ici, et il s'occupe de les tenir a jour.
    ///
    /// AUCUN TEXTE EN DUR DANS CE FICHIER. Les libelles hebreux sont des champs
    /// serialises que tu remplis dans l'inspecteur : un fichier .cs qui contient de
    /// l'hebreu finit tot ou tard re-enregistre dans un autre encodage, et le projet
    /// ne compile plus. C'est arrive une fois ici, on ne recommence pas.
    ///
    /// MISE EN PLACE
    ///   1. Pose ce composant sur ton Canvas.
    ///   2. Glisse chaque champ TMP / Image depuis ta hierarchie.
    ///   3. Remplis phaseLabels avec les cinq noms, dans l'ordre du tour.
    ///   4. Regle le bouton de fin de tour sur endTurnButton : il se cable seul.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        /// <summary>
        /// Le sequenceur de tour a besoin de faire monter le compteur d'Energie au
        /// moment precis ou une usine paie. Sans ce point d'acces il lui faudrait
        /// chercher le composant, ce que la regle de performance du projet interdit.
        /// </summary>
        public static HudController Instance;

        // =================================================================
        //  REFERENCES D'INTERFACE
        // =================================================================
        [Header("Energie")]
        [Tooltip("Le solde courant. En RTL il vit en haut a droite.")]
        public TMPro.TextMeshProUGUI energyText;
        [Tooltip("Optionnel : le gain du tour, affiche quelques secondes apres le Trivia.")]
        public TMPro.TextMeshProUGUI energyDeltaText;
        [Tooltip("Modele du gain, avec {0} pour le nombre. Exemple : +{0} ...")]
        public string energyDeltaFormat = "+{0}";
        [Tooltip("Duree d'affichage du gain, en secondes.")]
        public float energyDeltaDuration = 3.5f;

        [Tooltip("De combien le compteur grossit pendant qu'il monte. 1 = pas d'effet.")]
        public float energyCountScale = 1.35f;

        [Header("Tour et phase")]
        public TMPro.TextMeshProUGUI turnText;
        [Tooltip("Modele du numero de tour, avec {0}. Laisse vide pour n'afficher que le chiffre.")]
        public string turnFormat = "{0}";

        [Tooltip("Les cinq etapes, dans l'ordre du tour. Voir la classe PhaseStep.")]
        public PhaseStep[] phaseSteps = new PhaseStep[5];
        [Tooltip("Les cinq noms de phase, dans le meme ordre. A saisir ici, pas dans le code.")]
        public string[] phaseLabels = new string[5];

        [Header("Objectifs")]
        [Tooltip("Six losanges, un par shofar. Ils s'eteignent a mesure qu'ils tombent.")]
        public Image[] portalDots = new Image[6];
        public TMPro.TextMeshProUGUI portalCountText;
        [Tooltip("Modele du compteur, avec {0} restants et {1} au total.")]
        public string portalCountFormat = "{0}/{1}";

        [Tooltip("Image en Filled / Horizontal : c'est elle qui represente les PV de la Base.")]
        public Image baseFill;
        public TMPro.TextMeshProUGUI baseHpText;

        [Header("Actions")]
        [Tooltip("Bouton de fin de tour. Il n'apparait que pendant la phase de depense.")]
        public Button endTurnButton;
        [Tooltip("Optionnel : l'objet a masquer hors phase de depense. Par defaut, le bouton lui-meme.")]
        public GameObject endTurnRoot;

        [Header("Couleurs")]
        public Color phaseIdleColor = new Color(0.36f, 0.42f, 0.55f);
        public Color phaseDoneColor = new Color(0.24f, 0.88f, 0.82f, 0.42f);
        public Color phaseActiveColor = new Color(0.24f, 0.88f, 0.82f);

        public Color portalAliveColor = new Color(1f, 0.42f, 0.29f);
        public Color portalDownColor = new Color(0.49f, 0.63f, 1f, 0.18f);

        [Tooltip("Base au-dessus du seuil d'alerte.")]
        public Color baseHealthyColor = new Color(0.24f, 0.88f, 0.82f);
        [Tooltip("Base entre les deux seuils.")]
        public Color baseWarningColor = new Color(1f, 0.78f, 0.36f);
        [Tooltip("Base sous le seuil critique.")]
        public Color baseCriticalColor = new Color(1f, 0.30f, 0.37f);
        public int baseWarningThreshold = 120;
        public int baseCriticalThreshold = 60;

        [Header("Rafraichissement")]
        [Tooltip("Intervalle de relecture des managers, en secondes. 0.2 est imperceptible et coute presque rien.")]
        public float refreshInterval = 0.2f;

        /// <summary>Une etape du rail : son trait et son libelle.</summary>
        [System.Serializable]
        public class PhaseStep
        {
            [Tooltip("Le trait horizontal qui s'allume.")]
            public Image bar;
            [Tooltip("Le nom de l'etape.")]
            public TMPro.TextMeshProUGUI label;
        }

        // =================================================================
        //  ETAT MEMORISE
        //  Rien n'est reecrit tant que la valeur n'a pas change : sans ces
        //  champs, on reconstruirait le maillage de chaque texte cinq fois
        //  par seconde pour afficher exactement la meme chose.
        // =================================================================
        private int _lastEnergy = int.MinValue;

        private Coroutine _countRoutine;
        private Vector3 _energyBaseScale = Vector3.zero;
        private int _lastTurn = int.MinValue;
        private int _lastPortals = int.MinValue;
        private int _lastBaseHP = int.MinValue;
        private int _lastPhase = int.MinValue;
        private bool _lastSpending;

        private float _nextRefresh;
        private float _deltaHideAt = -1f;
        private int _energyAtTurnStart = int.MinValue;

        private UnityEngine.Events.UnityAction _onEndTurnCached;

        private void Awake()
        {
            if (Instance == null) Instance = this;

            ApplyPhaseLabels();

            _onEndTurnCached = HandleEndTurnClicked;
            if (endTurnButton != null) endTurnButton.onClick.AddListener(_onEndTurnCached);

            if (energyDeltaText != null) energyDeltaText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (endTurnButton != null && _onEndTurnCached != null)
                endTurnButton.onClick.RemoveListener(_onEndTurnCached);
        }

        private void Start()
        {
            Refresh(true);
        }

        /// <summary>Recopie une seule fois les libelles saisis dans l'inspecteur.</summary>
        private void ApplyPhaseLabels()
        {
            if (phaseSteps == null || phaseLabels == null) return;

            int count = Mathf.Min(phaseSteps.Length, phaseLabels.Length);
            for (int i = 0; i < count; i++)
            {
                if (phaseSteps[i] == null || phaseSteps[i].label == null) continue;
                if (string.IsNullOrEmpty(phaseLabels[i])) continue;
                phaseSteps[i].label.text = phaseLabels[i];
            }
        }

        // =================================================================
        //  BOUCLE
        // =================================================================
        private void Update()
        {
            // Pas de travail a chaque frame : on relit les managers cinq fois par
            // seconde, ce qui suffit largement pour un jeu au tour par tour.
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;

            Refresh(false);
        }

        private void Refresh(bool force)
        {
            RefreshEnergy(force);
            RefreshTurnAndPhase(force);
            RefreshObjectives(force);
            RefreshEndTurnButton(force);
            RefreshDeltaVisibility();
        }

        // ---------------------------------------------------------------
        //  ENERGIE
        // ---------------------------------------------------------------
        private void RefreshEnergy(bool force)
        {
            EnergyManager energy = EnergyManager.Instance;
            if (energy == null) return;

            // Pendant une montee, le compteur appartient a la coroutine : si on le
            // reecrivait ici, on verrait le chiffre final clignoter par-dessus.
            if (_countRoutine != null) return;

            int value = energy.CurrentEnergy;
            if (!force && value == _lastEnergy) return;
            _lastEnergy = value;

            // SetText avec argument : TMP ecrit dans son buffer interne et n'alloue
            // aucune string, contrairement a .text = value.ToString().
            if (energyText != null) energyText.SetText("{0}", value);
        }

        // ---------------------------------------------------------------
        //  LE COMPTEUR QUI MONTE
        // ---------------------------------------------------------------
        /// <summary>
        /// Fait grimper le compteur d'Energie jusqu'a sa nouvelle valeur, chiffre par
        /// chiffre, en grossissant pendant la montee.
        ///
        /// POURQUOI CA COMPTE
        ///
        /// Une valeur qui saute de 40 a 70 entre deux images ne se voit pas : l'oeil lit
        /// un nombre, puis un autre nombre, et rien ne relie les deux. Une montee, meme
        /// d'une demi-seconde, transforme le changement en EVENEMENT - et c'est le seul
        /// moyen de relier ce chiffre a l'usine qui s'allume au meme instant sur le
        /// plateau.
        ///
        /// Le grossissement revient a 1 a la fin : le compteur ne doit pas rester gros,
        /// sinon il attire l'oeil en permanence et ne signale plus rien.
        /// </summary>
        public void CountEnergyTo(int target, float duration)
        {
            if (energyText == null) return;

            if (_countRoutine != null) StopCoroutine(_countRoutine);
            _countRoutine = StartCoroutine(CountRoutine(target, duration));
        }

        private System.Collections.IEnumerator CountRoutine(int target, float duration)
        {
            int start = _lastEnergy;
            if (start == int.MinValue) start = target;

            // Rien a raconter : on pose la valeur et on s'en va.
            if (start == target || duration <= 0.02f)
            {
                _lastEnergy = target;
                energyText.SetText("{0}", target);
                _countRoutine = null;
                yield break;
            }

            RectTransform rect = energyText.rectTransform;
            Vector3 baseScale = (_energyBaseScale == Vector3.zero) ? rect.localScale : _energyBaseScale;
            _energyBaseScale = baseScale;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                // Temps NON mis a l'echelle : cette montee se joue pendant l'ouverture
                // du tour, ou le jeu peut etre en pause.
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Depart franc, arrivee douce : le chiffre part vite puis se pose.
                float eased = 1f - (1f - t) * (1f - t);

                int shown = Mathf.RoundToInt(Mathf.Lerp(start, target, eased));
                energyText.SetText("{0}", shown);

                // Une seule bosse, au milieu de la montee.
                float pulse = Mathf.Sin(t * Mathf.PI);
                rect.localScale = baseScale * (1f + (energyCountScale - 1f) * pulse);

                yield return null;
            }

            rect.localScale = baseScale;

            _lastEnergy = target;
            energyText.SetText("{0}", target);
            _countRoutine = null;
        }

        /// <summary>
        /// Affiche le gain du tour. Appele par le sequenceur une fois le Trivia
        /// resolu ; si personne ne l'appelle, le HUD fonctionne quand meme.
        /// </summary>
        public void ShowEnergyGain(int amount)
        {
            if (energyDeltaText == null || amount <= 0) return;

            energyDeltaText.SetText(energyDeltaFormat, amount);
            energyDeltaText.gameObject.SetActive(true);
            _deltaHideAt = Time.unscaledTime + energyDeltaDuration;
        }

        private void RefreshDeltaVisibility()
        {
            if (_deltaHideAt < 0f || energyDeltaText == null) return;
            if (Time.unscaledTime < _deltaHideAt) return;

            energyDeltaText.gameObject.SetActive(false);
            _deltaHideAt = -1f;
        }

        // ---------------------------------------------------------------
        //  TOUR ET PHASE
        // ---------------------------------------------------------------
        private void RefreshTurnAndPhase(bool force)
        {
            TurnManager turns = TurnManager.Instance;
            if (turns == null) return;

            if (force || turns.currentTurn != _lastTurn)
            {
                // Nouveau tour : on note le solde d'ouverture pour pouvoir annoncer
                // le gain quand la question se referme.
                if (turns.currentTurn != _lastTurn && EnergyManager.Instance != null)
                    _energyAtTurnStart = EnergyManager.Instance.CurrentEnergy;

                _lastTurn = turns.currentTurn;
                if (turnText != null) turnText.SetText(turnFormat, _lastTurn);
            }

            int phase = (int)turns.phase;

            // Le revenu vient d'etre verse : on annonce la difference.
            if (phase != _lastPhase
                && turns.phase == TurnPhase.Spending
                && _energyAtTurnStart != int.MinValue
                && EnergyManager.Instance != null)
            {
                int gained = EnergyManager.Instance.CurrentEnergy - _energyAtTurnStart;
                if (gained > 0) ShowEnergyGain(gained);
                _energyAtTurnStart = int.MinValue;
            }

            if (!force && phase == _lastPhase) return;
            _lastPhase = phase;

            PaintRail(PhaseToStepIndex(turns.phase));
        }

        /// <summary>
        /// Position de la phase courante sur le rail. Le rail compte cinq etapes ;
        /// TurnPhase en compte six, Idle n'ayant pas de case attitree.
        /// </summary>
        private static int PhaseToStepIndex(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.Income: return 0;
                case TurnPhase.Spending: return 1;
                case TurnPhase.Tanks: return 2;
                case TurnPhase.Enemies: return 3;
                case TurnPhase.EndOfTurn: return 4;
                default: return -1;
            }
        }

        private void PaintRail(int activeIndex)
        {
            if (phaseSteps == null) return;

            for (int i = 0; i < phaseSteps.Length; i++)
            {
                PhaseStep step = phaseSteps[i];
                if (step == null) continue;

                Color color;
                if (activeIndex < 0) color = phaseIdleColor;
                else if (i < activeIndex) color = phaseDoneColor;
                else if (i == activeIndex) color = phaseActiveColor;
                else color = phaseIdleColor;

                if (step.bar != null) step.bar.color = color;

                if (step.label != null)
                {
                    // Le libelle de l'etape courante est franc ; les autres reculent.
                    Color textColor = color;
                    if (i != activeIndex) textColor.a = 0.65f;
                    step.label.color = textColor;
                }
            }
        }

        // ---------------------------------------------------------------
        //  OBJECTIFS : shofars restants et PV de la Base
        // ---------------------------------------------------------------
        private void RefreshObjectives(bool force)
        {
            PortalManager portals = PortalManager.Instance;
            if (portals != null)
            {
                int alive = portals.CountPortals();
                if (force || alive != _lastPortals)
                {
                    _lastPortals = alive;

                    int total = (portalDots != null) ? portalDots.Length : 6;
                    if (portalCountText != null) portalCountText.SetText(portalCountFormat, alive, total);

                    if (portalDots != null)
                    {
                        for (int i = 0; i < portalDots.Length; i++)
                        {
                            if (portalDots[i] == null) continue;
                            portalDots[i].color = (i < alive) ? portalAliveColor : portalDownColor;
                        }
                    }
                }
            }

            BuildingManager buildings = BuildingManager.Instance;
            if (buildings == null) return;

            int hp = buildings.GetBaseHP();
            if (!force && hp == _lastBaseHP) return;
            _lastBaseHP = hp;

            int max = buildings.GetBaseMaxHP();
            if (max <= 0) max = InteractionRules.BASE_HP;

            Color tint = (hp > baseWarningThreshold) ? baseHealthyColor
                       : ((hp > baseCriticalThreshold) ? baseWarningColor : baseCriticalColor);

            if (baseFill != null)
            {
                baseFill.fillAmount = Mathf.Clamp01((float)hp / max);
                baseFill.color = tint;
            }

            if (baseHpText != null)
            {
                baseHpText.SetText("{0}", hp);
                baseHpText.color = tint;
            }
        }

        // ---------------------------------------------------------------
        //  BOUTON DE FIN DE TOUR
        // ---------------------------------------------------------------
        private void RefreshEndTurnButton(bool force)
        {
            TurnManager turns = TurnManager.Instance;
            bool spending = (turns != null) && turns.IsSpendingPhase;

            if (!force && spending == _lastSpending) return;
            _lastSpending = spending;

            GameObject target = (endTurnRoot != null)
                ? endTurnRoot
                : (endTurnButton != null ? endTurnButton.gameObject : null);

            if (target != null) target.SetActive(spending);
        }

        private void HandleEndTurnClicked()
        {
            if (TurnManager.Instance != null) TurnManager.Instance.EndPlayerTurn();
        }
    }
}

// ---------------------------------------------------------------------------
// NOTE D'OPTIMISATION
//
// 1. Update() sort en une comparaison de float la plupart des frames : le HUD ne
//    relit les managers que cinq fois par seconde, ce qui est invisible a l'oeil
//    dans un jeu au tour par tour et supprime tout travail par frame.
// 2. Chaque valeur est comparee a la precedente avant d'etre ecrite. Sans cela on
//    reconstruirait le maillage de chaque TextMeshPro cinq fois par seconde pour
//    afficher exactement le meme texte - c'est le cout cache classique d'un HUD.
// 3. SetText("{0}", value) ecrit dans le buffer interne de TMP : aucune string
//    intermediaire, contrairement a .text = value.ToString() qui en alloue une a
//    chaque appel.
// 4. Le listener du bouton est un UnityAction mis en cache et retire dans
//    OnDestroy : une lambda allouerait une closure et fuirait au rechargement.
// 5. Aucune recherche d'objet a l'execution : tout passe par les references de
//    l'inspecteur et par les singletons deja resolus des managers.
// 6. Time.unscaledTime est prefere a Time.time : le HUD continue de vivre meme si
//    le jeu est mis en pause par un timeScale a zero.
// ---------------------------------------------------------------------------
