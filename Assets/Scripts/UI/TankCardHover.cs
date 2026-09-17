using UnityEngine;
using UnityEngine.EventSystems;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Relais de survol pour une carte de l'ecran de choix d'un Tank.
    ///
    /// Meme raison que pour les reponses du Trivia et les cartes de sujet :
    /// EventTrigger alloue une liste d'entrees et un delegue par evenement, et il
    /// intercepte tous les evenements de pointeur du GameObject, y compris ceux dont
    /// le Button a besoin. Les deux interfaces ci-dessous ne coutent rien.
    ///
    /// Pose automatiquement par Milchemet > Construire le HUD ; rien a cabler.
    /// </summary>
    public class TankCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TankChoicePanel panel;
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
