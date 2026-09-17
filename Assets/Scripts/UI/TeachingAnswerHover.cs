using UnityEngine;
using UnityEngine.EventSystems;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Relais de survol pour une reponse de l'ecran d'enseignement.
    ///
    /// Meme raison que partout ailleurs dans ce projet : EventTrigger alloue une liste
    /// d'entrees et un delegue par evenement, et il intercepte tous les evenements de
    /// pointeur du GameObject, y compris ceux dont le Button a besoin. Les deux
    /// interfaces ci-dessous ne coutent rien.
    ///
    /// Pose automatiquement par Milchemet > Construire le HUD ; rien a cabler.
    /// </summary>
    public class TeachingAnswerHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TeachingPanel panel;
        public int slot;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (panel != null) panel.SetHovered(slot, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (panel != null) panel.SetHovered(slot, false);
        }
    }
}
