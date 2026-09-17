using UnityEngine;
using UnityEngine.EventSystems;

namespace MNLTHII.Managers
{
    /// <summary>
    /// Relais de survol pour une reponse du panneau de question.
    ///
    /// Pourquoi un composant plutot qu'un EventTrigger : EventTrigger alloue une
    /// liste d'entrees et un delegue par evenement, et il intercepte TOUS les
    /// evenements de pointeur du GameObject, y compris ceux dont le bouton a besoin.
    /// Les deux interfaces ci-dessous ne coutent rien et laissent le Button
    /// tranquille.
    ///
    /// Pose automatiquement par Milchemet > Construire le HUD ; rien a cabler.
    /// </summary>
    public class TriviaAnswerHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TriviaPanelController panel;
        public int index;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (panel != null) panel.SetHovered(index, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (panel != null) panel.SetHovered(index, false);
        }
    }
}
