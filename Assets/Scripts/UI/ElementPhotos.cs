using UnityEngine;

namespace MNLTHII.UI
{
    /// <summary>Ce qu'une photo represente.</summary>
    public enum PhotoSubject
    {
        Tank,
        Enemy,
        Bunker,
        GasFactory,
        CrystalFactory,
        CommandCenter,

        // Ajoutes a la fin : les valeurs deja enregistrees dans la scene ne bougent pas.
        /// <summary>Case de desert libre : le "niveau 0" d'un Tank pose dessus.</summary>
        Desert,
        /// <summary>Case de plaine libre : le "niveau 0" d'un Tank pose dessus.</summary>
        Plain
    }

    /// <summary>Une case du tableau : un sujet, un niveau, une image.</summary>
    [System.Serializable]
    public class ElementPhoto
    {
        public PhotoSubject subject = PhotoSubject.Tank;

        [Tooltip("0 = la case AVANT construction (gaz brut, colline, cristal, montagne), " +
                 "1 = premier niveau construit, 2 = ameliore, 3 = ennemi de rang 3. " +
                 "-1 = cette photo sert pour TOUS les niveaux de ce sujet.")]
        public int level = 1;

        public Sprite photo;
    }

    /// <summary>
    /// TES PHOTOS D'ELEMENTS.
    ///
    /// Au survol d'une case, l'info-bulle montre la photo de ce que ton clic va
    /// produire : l'usine de gaz que tu vas construire, le tank que tu vas poser,
    /// l'ennemi qui occupe la case.
    ///
    /// Le tableau est deja pret, une ligne par sujet et par niveau : il suffit de
    /// glisser une image dans chaque case "Photo". Une case vide ne casse rien -
    /// l'info-bulle s'affiche alors sans photo, comme avant. Tu peux donc les
    /// ajouter petit a petit.
    ///
    /// NIVEAU 0 = la case AVANT toute construction : le gaz brut, la colline nue, le
    /// cristal naturel, la montagne. Puis 1 et 2 pour les deux niveaux construits.
    ///
    /// Une photo au niveau -1 sert de photo par defaut pour tous les niveaux de ce
    /// sujet : pratique si tu n'as qu'une image de bunker pour l'instant.
    ///
    /// SANS RIEN GLISSER : une image posee dans Assets/Resources/Photos sous le nom
    /// "sujet_niveau" est trouvee toute seule - par exemple gas_0.png (gaz brut),
    /// gas_1.png, gas_2.png, bunker_0.png (colline), enemy_2.png, tank_1.png. Une case remplie dans
    /// le tableau passe toujours avant le fichier.
    ///
    /// IMPORT : chaque image en "Sprite (2D and UI)". N'importe quel format : l'info-
    /// bulle montre la photo ENTIERE, sans la couper ni la deformer, et adapte la
    /// hauteur de son bandeau a la forme de l'image.
    ///
    /// Pose par Milchemet > Construire le HUD sur l'objet des gestionnaires.
    /// </summary>
    public class ElementPhotos : MonoBehaviour
    {
        public static ElementPhotos Instance;

        public ElementPhoto[] photos = new ElementPhoto[]
        {
            new ElementPhoto { subject = PhotoSubject.Tank, level = 1 },
            new ElementPhoto { subject = PhotoSubject.Tank, level = 2 },
            new ElementPhoto { subject = PhotoSubject.Enemy, level = 1 },
            new ElementPhoto { subject = PhotoSubject.Enemy, level = 2 },
            new ElementPhoto { subject = PhotoSubject.Enemy, level = 3 },
            new ElementPhoto { subject = PhotoSubject.Bunker, level = 0 },
            new ElementPhoto { subject = PhotoSubject.Bunker, level = 1 },
            new ElementPhoto { subject = PhotoSubject.Bunker, level = 2 },
            new ElementPhoto { subject = PhotoSubject.GasFactory, level = 0 },
            new ElementPhoto { subject = PhotoSubject.GasFactory, level = 1 },
            new ElementPhoto { subject = PhotoSubject.GasFactory, level = 2 },
            new ElementPhoto { subject = PhotoSubject.CrystalFactory, level = 0 },
            new ElementPhoto { subject = PhotoSubject.CrystalFactory, level = 1 },
            new ElementPhoto { subject = PhotoSubject.CrystalFactory, level = 2 },
            new ElementPhoto { subject = PhotoSubject.CommandCenter, level = 0 },
            new ElementPhoto { subject = PhotoSubject.CommandCenter, level = 1 },
            new ElementPhoto { subject = PhotoSubject.CommandCenter, level = 2 },
            new ElementPhoto { subject = PhotoSubject.Desert, level = 0 },
            new ElementPhoto { subject = PhotoSubject.Plain, level = 0 },
        };

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// La photo d'un sujet a un niveau donne. Ordre de recherche : le niveau exact,
        /// puis la photo "tous niveaux" (niveau -1 ou fichier sans numero), puis
        /// n'importe quelle photo de ce sujet - SAUF pour le niveau 0 : une case pas
        /// encore construite ne doit jamais montrer le batiment, ce serait mentir.
        /// Null si rien n'a ete fourni.
        /// </summary>
        public static Sprite Get(PhotoSubject subject, int level)
        {
            ElementPhotos self = Instance;
            if (self == null || self.photos == null) return FromResources(subject, level);

            Sprite generic = null;
            Sprite any = null;

            for (int i = 0; i < self.photos.Length; i++)
            {
                ElementPhoto entry = self.photos[i];
                if (entry == null || entry.photo == null || entry.subject != subject) continue;

                if (entry.level == level) return entry.photo;
                if (entry.level < 0 && generic == null) generic = entry.photo;
                if (any == null) any = entry.photo;
            }

            if (generic != null) return generic;

            // Le fichier au nom exact passe avant "n'importe quelle photo du sujet" :
            // enemy_2.png est plus juste que la photo de l'ennemi de rang 1.
            Sprite file = FromResources(subject, level);
            if (file != null) return file;
            return (level == 0) ? null : any;
        }

        // =================================================================
        //  FICHIERS DANS Resources/Photos
        // =================================================================
        // Cle = sujet * 16 + niveau. On retient aussi les ABSENCES (null) : un fichier
        // manquant n'est cherche qu'une fois, pas a chaque survol.
        private static readonly System.Collections.Generic.Dictionary<int, Sprite> _files =
            new System.Collections.Generic.Dictionary<int, Sprite>(32);

        private static Sprite FromResources(PhotoSubject subject, int level)
        {
            if (level < 0) level = 0;
            int key = (int)subject * 16 + Mathf.Clamp(level, 0, 15);

            Sprite cached;
            if (_files.TryGetValue(key, out cached)) return cached;

            Sprite found = Resources.Load<Sprite>("Photos/" + FileStem(subject) + "_" + level);
            if (found == null) found = Resources.Load<Sprite>("Photos/" + FileStem(subject));

            _files[key] = found;

            // Une seule trace par sujet et niveau : de quoi savoir tout de suite si le
            // fichier est absent, mal nomme, ou pas importe en Sprite.
            if (found == null)
                Debug.LogFormat("[Photos] Aucune photo pour {0} niveau {1} (cherche : Resources/Photos/{2}_{1}, "
                              + "importee en Sprite).", subject, level, FileStem(subject));
            else
                Debug.LogFormat("[Photos] Photo trouvee pour {0} niveau {1} : {2}.", subject, level, found.name);

            return found;
        }

        private static string FileStem(PhotoSubject subject)
        {
            switch (subject)
            {
                case PhotoSubject.Tank: return "tank";
                case PhotoSubject.Enemy: return "enemy";
                case PhotoSubject.Bunker: return "bunker";
                case PhotoSubject.GasFactory: return "gas";
                case PhotoSubject.CrystalFactory: return "crystal";
                case PhotoSubject.CommandCenter: return "command";
                case PhotoSubject.Desert: return "desert";
                case PhotoSubject.Plain: return "plain";
            }
            return "photo";
        }
    }
}

// Note d'optimisation : une boucle sur une quinzaine d'entrees, appelee seulement
// quand la case survolee CHANGE (pas a chaque frame). Le chargement d'un fichier de
// Resources construit un chemin (une chaine) UNE fois par sujet et niveau, puis le
// resultat est garde en cache, absence comprise.
