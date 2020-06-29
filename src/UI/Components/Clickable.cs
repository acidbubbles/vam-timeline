using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace VamTimeline
{
    public class Clickable : MonoBehaviour, IPointerClickHandler
    {
        public ClickableEvent onClick = new ClickableEvent();

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick.Invoke(eventData);
        }

        public void OnDestroy()
        {
            onClick.RemoveAllListeners();
        }

        public class ClickableEvent : UnityEvent<PointerEventData>
        {
        }
    }
}
