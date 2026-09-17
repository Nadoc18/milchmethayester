using UnityEngine;

namespace MNLTHII.Managers
{
    /// <summary>
    /// La boussole hexagonale : donne le secteur d'une case vue depuis le centre du
    /// plateau, pour pouvoir NOMMER un Shofar au lieu d'afficher ses coordonnees.
    ///
    /// "Le Shofar du nord-est" est une information ; "(4,-2)" n'en est pas une. Or
    /// c'est ce nom qui rend la boucle d'ebranlement lisible : quand la carte d'un
    /// ennemi dit de quel Shofar il sort, le joueur comprend d'un coup que le tuer
    /// frappera CE Shofar-la.
    ///
    /// On part de la position MONDE et non des coordonnees cube : le nom doit coller
    /// a ce que le joueur voit a l'ecran, pas au repere interne du plateau. Si la
    /// camera ou l'orientation des hexagones change un jour, le nom suivra.
    ///
    /// Note d'optimisation : une methode statique, un Atan2, aucune allocation. Les
    /// noms eux-memes restent dans l'inspecteur des panneaux, donc en hebreu dans le
    /// JSON, jamais dans un fichier .cs.
    /// </summary>
    public static class HexCompass
    {
        /// <summary>Nombre de secteurs : un hexagone, donc six directions.</summary>
        public const int SectorCount = 6;

        /// <summary>
        /// Secteur de 0 a 5 : 0 = est, puis dans le sens trigonometrique tous les
        /// 60 degres (est, nord-est, nord-ouest, ouest, sud-ouest, sud-est).
        /// </summary>
        public static int SectorFromWorld(Vector3 worldPosition)
        {
            // Atan2 renvoie -PI..PI ; on ramene sur 0..360, puis on decale d'un demi
            // secteur pour que l'est soit CENTRE sur zero et non a cheval dessus.
            float angle = Mathf.Atan2(worldPosition.z, worldPosition.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            angle += 30f;
            if (angle >= 360f) angle -= 360f;

            int sector = (int)(angle / 60f);
            if (sector < 0) return 0;
            if (sector > 5) return 5;
            return sector;
        }

        /// <summary>
        /// Nom du secteur, ou chaine vide si le tableau fourni est incomplet. On
        /// prefere un nom absent a un nom faux.
        /// </summary>
        public static string NameFromWorld(Vector3 worldPosition, string[] names)
        {
            if (names == null) return "";

            int sector = SectorFromWorld(worldPosition);
            if (sector < 0 || sector >= names.Length) return "";

            return names[sector];
        }
    }
}
