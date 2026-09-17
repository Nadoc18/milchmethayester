using UnityEngine;

namespace MNLTHII.UI
{
    /// <summary>
    /// La couleur d'une posture, definie UNE fois pour tout le jeu.
    ///
    /// Trois endroits ont besoin de ces couleurs : le modele du Tank sur le plateau,
    /// la carte d'unite pendant la phase des Tanks, et l'ecran de choix de posture.
    /// Les definir trois fois, c'est se garantir qu'un jour l'assaut sera orange sur
    /// le plateau et rouge dans le menu - et le joueur ne saura plus quelle couleur
    /// veut dire quoi.
    ///
    /// Le code couleur, et pourquoi celui-la :
    ///
    ///   Garde     BLEU    - la couleur de la defense, froide, immobile.
    ///   Assaut    ORANGE  - la couleur des Shofars, qui est sa cible. Un Tank en
    ///                       assaut porte la couleur de ce qu'il va detruire.
    ///   Chasse    VERT    - ni defensif ni lie aux Shofars : il court apres l'ennemi.
    ///
    /// Note d'optimisation : deux tableaux statiques en lecture seule, lus par index.
    /// Color est un struct, donc rien n'est alloue en les lisant.
    /// </summary>
    public static class StanceStyle
    {
        /// <summary>Garde, Assaut, Chasse - l'ordre de l'enum PawnStance.</summary>
        public const int Count = 3;

        /// <summary>
        /// Couleur vive, pour l'interface : cartes, liserets, libelles.
        /// </summary>
        public static readonly Color[] Accent =
        {
            new Color(0.37f, 0.66f, 1.00f),   // Garde   - bleu
            new Color(1.00f, 0.54f, 0.24f),   // Assaut  - orange
            new Color(0.48f, 0.88f, 0.42f)    // Chasse  - vert
        };

        /// <summary>
        /// Teinte appliquee au MODELE, par multiplication composante par composante.
        /// Elle est plus claire que l'accent : multipliee par la couleur d'origine du
        /// modele, une teinte trop saturee noircit tout et on ne reconnait plus l'unite.
        /// </summary>
        public static readonly Color[] ModelTint =
        {
            new Color(0.58f, 0.80f, 1.00f),   // Garde
            new Color(1.00f, 0.66f, 0.38f),   // Assaut
            new Color(0.70f, 1.00f, 0.58f)    // Chasse
        };

        public static Color AccentOf(int stanceIndex)
        {
            if (stanceIndex < 0 || stanceIndex >= Accent.Length) return Color.white;
            return Accent[stanceIndex];
        }

        public static Color TintOf(int stanceIndex)
        {
            if (stanceIndex < 0 || stanceIndex >= ModelTint.Length) return Color.white;
            return ModelTint[stanceIndex];
        }
    }
}
