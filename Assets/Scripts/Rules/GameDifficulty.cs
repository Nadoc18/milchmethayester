using UnityEngine;

namespace MNLTHII.Rules
{
    /// <summary>Les trois niveaux de difficulte proposes par le menu principal.</summary>
    public enum DifficultyLevel
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    /// <summary>
    /// LA DIFFICULTE DE LA PARTIE.
    ///
    /// Choisie dans le menu principal, retenue dans PlayerPrefs : "Rejouer" relance
    /// la meme difficulte, et lancer directement la scene Game dans l'editeur prend
    /// la derniere choisie (Moyen si on n'a jamais rien choisi).
    ///
    /// DIFFICILE = l'equilibrage d'avant, sans rien changer. Facile et Moyen
    /// relachent la pression sur les leviers qui comptent vraiment :
    ///
    ///   - l'Energie de depart et le revenu de la Base (plus de marge au debut) ;
    ///   - le rythme des Shofars : un ennemi tous les N tours par Shofar, et le
    ///     plafond d'ennemis par tour ;
    ///   - le tour ou les Shofars passent au niveau 2 ;
    ///   - les PV et les degats des ennemis, en pourcentage ;
    ///   - le nombre de morts qui font tomber un bouclier.
    ///
    /// Toutes les valeurs sont ici, dans Pick(facile, moyen, difficile) : une ligne
    /// a changer pour reequilibrer un niveau.
    ///
    /// Note d'optimisation : la valeur est lue une fois dans PlayerPrefs puis gardee
    /// en memoire. Pick est un simple switch, sans allocation.
    /// </summary>
    public static class GameDifficulty
    {
        private const string PrefKey = "mnlth_difficulty";

        private static bool _loaded;
        private static DifficultyLevel _current = DifficultyLevel.Medium;

        public static DifficultyLevel Current
        {
            get
            {
                Load();
                return _current;
            }
            set
            {
                _current = value;
                _loaded = true;

                try
                {
                    PlayerPrefs.SetInt(PrefKey, (int)value);
                    PlayerPrefs.Save();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarningFormat("[Difficulte] Impossible d'enregistrer le choix : {0}", e.Message);
                }

                Debug.LogFormat("[Difficulte] Niveau choisi : {0}.", value);
            }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                int stored = PlayerPrefs.GetInt(PrefKey, (int)DifficultyLevel.Medium);
                if (stored < 0 || stored > 2) stored = (int)DifficultyLevel.Medium;
                _current = (DifficultyLevel)stored;
            }
            catch (System.Exception)
            {
                // PlayerPrefs indisponible (appel trop tot) : on garde Moyen, et on
                // retentera au prochain acces.
                _current = DifficultyLevel.Medium;
                _loaded = false;
            }
        }

        /// <summary>La valeur qui correspond au niveau en cours.</summary>
        public static int Pick(int easy, int medium, int hard)
        {
            switch (Current)
            {
                case DifficultyLevel.Easy: return easy;
                case DifficultyLevel.Medium: return medium;
                default: return hard;
            }
        }

        // =================================================================
        //  LES REGLAGES
        // =================================================================
        /// <summary>PV des ennemis, en pourcentage de la valeur de base.</summary>
        public static int EnemyHpPercent { get { return Pick(70, 85, 100); } }

        /// <summary>Degats des ennemis, en pourcentage de la valeur de base.</summary>
        public static int EnemyDamagePercent { get { return Pick(70, 85, 100); } }

        /// <summary>Applique un pourcentage, arrondi, jamais sous 1.</summary>
        public static int Scale(int value, int percent)
        {
            if (percent == 100) return value;
            int scaled = (value * percent + 50) / 100;
            return (scaled < 1) ? 1 : scaled;
        }
    }
}
