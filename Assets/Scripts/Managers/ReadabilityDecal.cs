using UnityEngine;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Etiquette posee sur chaque disque ajoute par BoardReadability (ombre de contact,
    /// anneau de socle). Elle ne fait rien : elle permet seulement de les reconnaitre
    /// pour ne jamais les confondre avec le modele du pion - quand on le met a
    /// l'echelle, ou quand ThreatPreview en fait un fantome.
    /// </summary>
    [DisallowMultipleComponent]
    public class ReadabilityDecal : MonoBehaviour
    {
    }
}

// Note d'optimisation : composant vide, aucun Update, aucun cout a l'execution.
