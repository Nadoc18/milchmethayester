using System.Collections.Generic;

namespace MNLTHII.Data
{
    /// <summary>
    /// UNE CASE, TELLE QU'ELLE EST AU MOMENT DE LA SAUVEGARDE.
    ///
    /// Les noms de champs sont courts EXPRES : le fichier contient 169 cases, et
    /// "positionInTheBoard" repete 169 fois pese plus lourd que tout le reste de la
    /// partie reunie. Ici q et r suffisent - s se retrouve toujours par s = -q-r.
    /// </summary>
    public class SavedHex
    {
        public int q;
        public int r;

        /// <summary>TypeOfHex, en entier.</summary>
        public int t;

        /// <summary>Niveau du batiment (0 = terrain nu).</summary>
        public int l;

        /// <summary>PV courants de la structure.</summary>
        public int hp;

        /// <summary>
        /// La bulle d'un Centre de Commandement. Nouveau champ : un fichier ecrit avant
        /// son existence le relit a zero, et ProcessMountainsSequential lui rend alors
        /// une bulle pleine plutot que de laisser le Centre nu. Le format reste en
        /// version 1 - ajouter un champ ne casse aucune partie en cours.
        /// </summary>
        public int sh;

        /// <summary>Tours ecoules depuis la creation (Shofars).</summary>
        public int ta;

        /// <summary>
        /// Structure reduite en epave (un Shofar abattu). Elle se recharge comme un
        /// Shofar a zero PV - c'est ce qui fait naitre le bon modele d'epave - puis on
        /// remet son type a Destroyed, exactement comme le fait la destruction en jeu.
        /// </summary>
        public bool dead;
    }

    /// <summary>
    /// UN PION : Tank ou ennemi, avec ce qu'on lui a demande.
    ///
    /// Les ORDRES comptent autant que la position : un Tank a qui on a dit de garder
    /// tel Centre de Commandement et qui retrouverait "decide toi-meme" au chargement
    /// ne serait plus le meme Tank. Les cibles sont gardees en coordonnees (une
    /// reference d'objet ne survit evidemment pas a un fichier), et l'ennemi traque
    /// est retrouve a sa case apres que tout le monde a ete repose.
    /// </summary>
    public class SavedPawn
    {
        public int q;
        public int r;

        /// <summary>Vrai pour un emissaire du Yetzer Hara.</summary>
        public bool foe;

        public int lvl;
        public int hp;

        /// <summary>PawnStance, en entier (Garde / Assaut / Chasse).</summary>
        public int st;

        /// <summary>Ordre "rejoins un Cristal".</summary>
        public bool crys;

        /// <summary>Cible choisie a la main : case gardee, ou Shofar assailli.</summary>
        public bool hasOrder;
        public int oq;
        public int or;

        /// <summary>Proie choisie a la main (role Chasse), a sa case.</summary>
        public bool hasPrey;
        public int pq;
        public int pr;

        /// <summary>Shofar qui a deploye cet ennemi : sa mort le fissure.</summary>
        public bool hasOrigin;
        public int gq;
        public int gr;
    }

    /// <summary>
    /// L'ETAT D'UN SHOFAR qui ne se lit pas sur le plateau : ses fissures, son
    /// bouclier tombe, et depuis combien de tours personne ne l'a frappe.
    /// </summary>
    public class SavedPortal
    {
        public int q;
        public int r;

        /// <summary>Fissures accumulees (morts d'emissaires + enseignements).</summary>
        public int kills;

        /// <summary>Tours restants avant que le bouclier ne se refasse.</summary>
        public int shieldDown;

        /// <summary>Tours consecutifs sans avoir ete frappe.</summary>
        public int calm;
    }

    /// <summary>
    /// UNE PARTIE EN COURS, ECRITE SUR LE DISQUE.
    ///
    /// POURQUOI CE FICHIER EXISTE
    ///
    /// Une partie dure des dizaines de tours. Sans sauvegarde, fermer le jeu - ou
    /// simplement quitter le Play Mode de l'editeur - efface tout, et la seule facon
    /// de revoir le tour 20 est de rejouer les dix-neuf premiers. On finit par ne
    /// plus jouer que les cinq premiers tours, encore et encore, et le jeu tout
    /// entier se reduit a son ouverture.
    ///
    /// Le fichier est ecrit TOUT SEUL au debut de chaque phase de depense : c'est le
    /// seul moment ou l'etat est au repos (aucune animation en cours, la main est au
    /// joueur), et c'est exactement l'instant ou l'on veut revenir.
    ///
    /// Il contient de quoi tout reconstruire : les 169 cases, les pions avec leurs
    /// ordres, l'Energie, les PV de la Base et l'etat des Shofars. Pas la camera, pas
    /// les animations, pas l'apercu de menace : tout cela se recalcule.
    ///
    /// Les en-tetes (tour, difficulte, date, comptages) sont en haut du fichier pour
    /// que le menu principal puisse afficher la liste des parties sans avoir a
    /// comprendre le plateau.
    /// </summary>
    public class SaveGameFile
    {
        /// <summary>Version du format. Un fichier d'une autre version est ignore.</summary>
        public int version = 1;

        /// <summary>Identifiant du fichier, fixe pour toute la duree d'une partie.</summary>
        public string id;

        /// <summary>Date de la derniere ecriture, pour trier la liste.</summary>
        public long savedAtTicks;

        /// <summary>La meme date, deja lisible ("23/09 14:12").</summary>
        public string savedAt;

        // --- en-tete : ce que le menu affiche sans rien reconstruire ---
        public int turn = 1;
        public int difficulty = 1;
        public int energy;
        public int baseHP;
        public int baseMaxHP;
        public int portalsAlive;
        public int portalsTotal;
        public int tanks;
        public int foes;

        // --- compteurs globaux ---
        public int enemiesSpawned;
        public int enemiesKilled;

        // --- vague annoncee pour le tour suivant ---
        public bool hasSurge;
        public int surgeQ;
        public int surgeR;
        public int lastSurgeTurn;

        // --- le plateau ---
        public List<SavedHex> hexes = new List<SavedHex>(200);
        public List<SavedPawn> pawns = new List<SavedPawn>(32);
        public List<SavedPortal> portals = new List<SavedPortal>(8);
    }
}
