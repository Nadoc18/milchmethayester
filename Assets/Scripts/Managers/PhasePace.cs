using DG.Tweening;
using UnityEngine;

namespace MNLTHII.Managers
{
    /// <summary>Les trois allures des phases automatiques.</summary>
    public enum PhaseSpeed
    {
        Normal = 0,
        Fast = 1,
        Blitz = 2
    }

    /// <summary>
    /// LE RYTHME DES PHASES QUE LE JOUEUR NE JOUE PAS.
    ///
    /// LE PROBLEME MESURE
    ///
    /// Un tour de fin de partie - 12 ennemis, 5 Tanks, 6 usines, 2 Bunkers - durait
    /// 1 minute 50 SANS une seule decision du joueur, dont 95 secondes qu'il ne
    /// pouvait pas abreger. Sur vingt tours, cela fait trente-sept minutes a
    /// regarder. La premiere partie, c'est un spectacle. La huitieme, c'est une
    /// salle d'attente.
    ///
    /// Le detail comptait autant que le total : la phase ennemie ne representait que
    /// le tiers du temps. Le reste etait de la ceremonie - bandeaux d'etape, camera
    /// qui va voir chaque usine payer le meme montant qu'au tour precedent.
    ///
    /// TROIS REPONSES, ET ELLES NE SE REMPLACENT PAS
    ///
    ///   1. UNE ALLURE. Normal, Rapide, Eclair. Tout ce qui attend passe par ici, y
    ///      compris les deplacements de camera et les tweens des pions : accelerer
    ///      l'attente sans accelerer le mouvement ferait avancer la phase pendant
    ///      qu'un Tank glisse encore.
    ///
    ///   2. PASSER AU SUIVANT. Le joueur a compris ce que fait CE pion, il veut voir
    ///      le prochain. L'action se resout quand meme, integralement : seule
    ///      l'attente tombe.
    ///
    ///   3. TOUT PASSER. Il sait deja ce qui va arriver, il veut revoir le plateau.
    ///      Tout se resout d'un trait, la camera cesse de suivre, et la main revient.
    ///
    /// CE QUI N'EST JAMAIS SAUTE
    ///
    /// Aucune de ces trois commandes ne change une regle. Elles ne touchent qu'a des
    /// attentes. Un ennemi saute sa mise en scene, pas son attaque ; une usine saute
    /// son gros chiffre, pas son versement. On peut donc jouer une partie entiere en
    /// Eclair et obtenir exactement la meme partie.
    ///
    /// Note d'optimisation : PaceWait est une CustomYieldInstruction reutilisee, une
    /// par appelant. L'ancien code allouait un WaitForSeconds par reglage ; celui-ci
    /// n'alloue plus rien du tout apres la premiere frame, et surtout une attente
    /// peut etre INTERROMPUE, ce qu'un WaitForSeconds ne sait pas faire.
    /// </summary>
    public static class PhasePace
    {
        private const string PrefKey = "mnlth_pace";

        // =================================================================
        //  L'ALLURE
        // =================================================================
        /// <summary>
        /// Eclair ne vaut pas zero, et c'est volontaire. A zero le plateau se
        /// teleporte : on ne voit pas QUI a bouge, seulement que quelque chose a
        /// change, et il faut relire tout l'ecran. A un quart, le mouvement reste
        /// lisible du coin de l'oeil. Pour du vrai instantane, il y a "tout passer".
        /// </summary>
        private const float ScaleNormal = 1.00f;
        private const float ScaleFast = 0.50f;
        private const float ScaleBlitz = 0.25f;

        private static PhaseSpeed _speed = PhaseSpeed.Normal;
        private static bool _loaded;

        public static PhaseSpeed Speed
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;

                    int stored = PlayerPrefs.GetInt(PrefKey, (int)PhaseSpeed.Normal);
                    _speed = (stored >= 0 && stored <= 2) ? (PhaseSpeed)stored : PhaseSpeed.Normal;
                }
                return _speed;
            }
            set
            {
                _loaded = true;
                _speed = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
            }
        }

        /// <summary>Le facteur applique a TOUTE duree de mise en scene.</summary>
        public static float Scale
        {
            get
            {
                switch (Speed)
                {
                    case PhaseSpeed.Fast: return ScaleFast;
                    case PhaseSpeed.Blitz: return ScaleBlitz;
                    default: return ScaleNormal;
                }
            }
        }

        /// <summary>L'allure suivante, en boucle. Appele par le bouton et par la touche.</summary>
        public static PhaseSpeed Cycle()
        {
            switch (Speed)
            {
                case PhaseSpeed.Normal: Speed = PhaseSpeed.Fast; break;
                case PhaseSpeed.Fast: Speed = PhaseSpeed.Blitz; break;
                default: Speed = PhaseSpeed.Normal; break;
            }
            return Speed;
        }

        // =================================================================
        //  LES DEUX SAUTS
        // =================================================================
        private static bool _skipPhase;
        private static bool _skipUnit;

        /// <summary>Vrai quand une phase automatique est en cours (Tanks, Yetzer, fin de tour).</summary>
        public static bool Running { get; private set; }

        public static bool SkipPhaseRequested { get { return _skipPhase; } }
        public static bool SkipUnitRequested { get { return _skipUnit || _skipPhase; } }

        /// <summary>Vrai quand plus rien n'attend : les deux sauts court-circuitent tout.</summary>
        public static bool AnySkip { get { return _skipUnit || _skipPhase; } }

        /// <summary>
        /// Debut d'une phase automatique. Remet les deux drapeaux a zero : un "tout
        /// passer" demande pendant la phase des Tanks ne doit pas manger la phase du
        /// Yetzer Hara, qui est celle que le joueur a le plus besoin de voir.
        /// </summary>
        public static void BeginPhase()
        {
            _skipPhase = false;
            _skipUnit = false;
            Running = true;
        }

        /// <summary>Fin de la phase : la barre de controle se retire.</summary>
        public static void EndPhase()
        {
            _skipPhase = false;
            _skipUnit = false;
            Running = false;
        }

        /// <summary>
        /// Un nouvel element prend la main : pion, usine, Bunker, Cristal, Centre,
        /// deploiement. Le saut d'element precedent est oublie.
        /// </summary>
        public static void BeginUnit()
        {
            _skipUnit = false;
        }

        /// <summary>"Passer a l'element suivant."</summary>
        public static void SkipUnit()
        {
            if (!Running) return;
            _skipUnit = true;
            SnapVisuals();
        }

        /// <summary>"Tout passer jusqu'a ce que la main revienne."</summary>
        public static void SkipPhase()
        {
            if (!Running) return;
            _skipPhase = true;
            SnapVisuals();
        }

        /// <summary>
        /// On saute : les mouvements en cours doivent arriver MAINTENANT.
        ///
        /// Sans cela, la position logique d'un pion (posee des le depart du tween par
        /// UpdateCoord) et sa position a l'ecran divergeaient : le joueur voyait ses
        /// Tanks glisser vers des cases qu'ils occupaient deja depuis trois actions.
        ///
        /// DOTween.CompleteAll est sans risque ici : PawnController est le seul
        /// utilisateur de DOTween dans tout le projet - les fondus d'interface et les
        /// mouvements de camera ont leurs propres coroutines. Completer declenche les
        /// OnComplete, donc les pions sont bien reparentes sur leur hexagone.
        /// </summary>
        private static void SnapVisuals()
        {
            DOTween.CompleteAll();
        }

        // =================================================================
        //  TOUTES LES DUREES DE MISE EN SCENE, AU MEME ENDROIT
        // =================================================================
        //
        // POURQUOI ELLES NE SONT PLUS DANS L'INSPECTEUR
        //
        // Elles y etaient, reparties sur huit composants, et c'etait ingerable pour
        // deux raisons. D'abord parce qu'un reglage du rythme se fait en comparant :
        // le temps de cadrage doit rester superieur au trajet de la camera, le temps
        // de resolution doit couvrir la vie du chiffre de degats, l'attente d'un pas
        // doit depasser son tween. Ces trois regles ne se verifient pas en ouvrant
        // huit inspecteurs. Ensuite parce qu'une valeur serialisee dans la scene
        // GAGNE contre la valeur par defaut du code : baisser un chiffre ici
        // n'aurait rien change tant que la scene aurait garde l'ancien.
        //
        // Les champs des composants sont donc conserves - retirer un champ serialise
        // salit la scene - mais ils ne sont plus lus. C'est ici qu'on regle.
        //
        // CE QUI A CHANGE, ET POURQUOI
        //
        //   cadrage   0.95 -> 0.75   doit rester au-dessus du trajet de camera (0.45)
        //   visee     0.35 -> 0.30
        //   resolution 1.05 -> 0.85  le chiffre de degats vit 0.95 s et continue de
        //                            vivre pendant l'element suivant : rien n'est coupe
        //   pas       0.85 -> 0.55   et le tween du pion passe de 1.5 s a 0.42 s
        //   respiration 0.55 -> 0.35
        //   bandeau   2.50 -> 1.20   un bandeau lu vingt fois n'apprend plus rien
        //   usine     1.60 -> 1.00
        //
        // LE BOGUE QUE CE REGLAGE CORRIGE
        //
        // Le tween de deplacement durait 1.5 seconde alors que la phase n'attendait
        // que 0.85 : un pion etait encore en train de glisser quand le suivant
        // commencait a jouer. Pire, un Tank de rang 2 enchainait deux pas de 0.85 s
        // alors que chaque pas demandait 1.6 s - le second tween ecrasait le premier
        // et l'unite sautait une case a l'ecran. PawnMove est maintenant plus court
        // que UnitMove, ce qui est la seule facon que ce soit juste.

        public const float UnitFocus = 0.75f;
        public const float UnitAim = 0.30f;
        public const float UnitResolve = 0.85f;
        public const float UnitMove = 0.55f;
        public const float UnitStep = 0.35f;

        /// <summary>Marge ajoutee au trajet de la camera avant de la croire arrivee.</summary>
        public const float CameraMargin = 0.10f;

        public const float FactoryShow = 1.00f;
        public const float FactoryCount = 0.70f;
        public const float BuildingShow = 1.00f;
        public const float SpawnShow = 0.90f;

        public const float BannerFadeIn = 0.25f;
        public const float BannerHold = 1.20f;
        public const float BannerFadeOut = 0.35f;
        public const float BannerMinBeforeSkip = 0.25f;

        public const float TurnAnnounceFadeIn = 0.28f;
        public const float TurnAnnounceHold = 0.70f;
        public const float TurnAnnounceFadeOut = 0.32f;

        public const float EnergyPop = 0.18f;
        public const float EnergyHold = 0.35f;
        public const float EnergyFlight = 0.50f;

        public const float BunkerFrame = 0.40f;
        public const float BunkerBetweenShots = 0.30f;
        public const float BunkerVolley = 0.50f;

        /// <summary>Duree des tweens d'un pion. PawnLook + PawnMove doit rester sous UnitMove.</summary>
        public const float PawnLook = 0.08f;
        public const float PawnMove = 0.42f;

        // =================================================================
        //  L'ATTENTE
        // =================================================================
        /// <summary>
        /// Une attente reutilisable, a garder dans un champ. Deux coroutines d'un
        /// meme objet ne doivent pas se partager la meme instance : chacune reecrit
        /// l'echeance de l'autre.
        /// </summary>
        public static PaceWait NewWait()
        {
            return new PaceWait();
        }

        /// <summary>
        /// La duree reelle d'une attente demandee, une fois l'allure appliquee. Sert
        /// aux rares endroits qui ont besoin du nombre lui-meme (un tween, un tir qui
        /// doit tomber a l'arrivee du trait) plutot que d'une attente.
        /// </summary>
        public static float Seconds(float requested)
        {
            if (AnySkip) return 0f;
            float value = requested * Scale;
            return (value < 0f) ? 0f : value;
        }
    }

    /// <summary>
    /// L'attente des phases automatiques : elle applique l'allure et elle se laisse
    /// interrompre. Un WaitForSeconds ne sait faire ni l'un ni l'autre.
    ///
    /// Elle compte en temps NON mis a l'echelle : la camera d'action ralentit
    /// Time.timeScale pour ses plans au ras du sol, et une mise en scene qui
    /// ralentirait avec elle donnerait des phases deux fois plus longues sur les
    /// tours les plus spectaculaires.
    /// </summary>
    public sealed class PaceWait : CustomYieldInstruction
    {
        private float _end;

        /// <summary>Arme l'attente. A utiliser directement dans un yield return.</summary>
        public PaceWait For(float seconds)
        {
            _end = Time.unscaledTime + PhasePace.Seconds(seconds);
            return this;
        }

        /// <summary>
        /// Une attente dont la duree est DEJA a l'allure : elle n'applique pas le
        /// facteur une seconde fois, mais elle se laisse toujours interrompre.
        ///
        /// Un seul cas, et il compte : le trait de tir d'un Bunker. ShotTracer
        /// raccourcit lui-meme son trajet, et l'impact doit tomber quand le trait
        /// ARRIVE. Repasser sa duree dans le facteur ferait exploser la cible avant
        /// que le trait ne l'atteigne.
        /// </summary>
        public PaceWait ForExact(float seconds)
        {
            _end = Time.unscaledTime + ((seconds > 0f && !PhasePace.AnySkip) ? seconds : 0f);
            return this;
        }

        public override bool keepWaiting
        {
            get
            {
                if (PhasePace.AnySkip) return false;
                return Time.unscaledTime < _end;
            }
        }
    }
}
