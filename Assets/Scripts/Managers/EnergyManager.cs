using System;
using UnityEngine;

namespace MNLTHII.Managers
{
    /// <summary>
    /// GDD V3 - Section 6 : L'Energie.
    /// Solde global du joueur. Demarre a 0, +25 par bonne reponse au Trivia,
    /// -50 pour la creation d'un Tank.
    /// </summary>
    public class EnergyManager : MonoBehaviour
    {
        public static EnergyManager Instance { get; private set; }

        [Header("UI (optionnel)")]
        public TMPro.TextMeshProUGUI energyText;

        [Header("Etat")]
        [SerializeField] private int currentEnergy = 0;

        public int CurrentEnergy { get { return currentEnergy; } }

        /// <summary>Emis a chaque variation du solde, avec le nouveau solde.</summary>
        public event Action<int> OnEnergyChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }
        }

        private void Start()
        {
            RefreshUI();
        }

        public void Add(int amount)
        {
            if (amount == 0) return;
            currentEnergy += amount;
            if (currentEnergy < 0) currentEnergy = 0;
            Debug.LogFormat("[Energy] +{0} -> {1}", amount, currentEnergy);
            RefreshUI();
            if (OnEnergyChanged != null) OnEnergyChanged.Invoke(currentEnergy);
        }

        public bool CanAfford(int cost)
        {
            return currentEnergy >= cost;
        }

        /// <summary>Depense si le solde le permet. Retourne false sans rien changer sinon.</summary>
        public bool TrySpend(int cost)
        {
            if (cost <= 0) return true;
            if (currentEnergy < cost)
            {
                Debug.LogFormat("[Energy] Insuffisant : {0} / {1} requis.", currentEnergy, cost);
                return false;
            }
            currentEnergy -= cost;
            Debug.LogFormat("[Energy] -{0} -> {1}", cost, currentEnergy);
            RefreshUI();
            if (OnEnergyChanged != null) OnEnergyChanged.Invoke(currentEnergy);
            return true;
        }

        public void ResetEnergy()
        {
            ResetEnergy(0);
        }

        /// <summary>Remet le solde a une valeur de depart (solde d'ouverture de partie).</summary>
        public void ResetEnergy(int startingAmount)
        {
            currentEnergy = (startingAmount > 0) ? startingAmount : 0;
            RefreshUI();
            if (OnEnergyChanged != null) OnEnergyChanged.Invoke(currentEnergy);
        }

        private void RefreshUI()
        {
            // SetText ecrit dans le buffer interne de TMP : contrairement a
            // .text = value.ToString(), il n'alloue aucune string.
            if (energyText != null) energyText.SetText("{0}", currentEnergy);
        }
    }
}
