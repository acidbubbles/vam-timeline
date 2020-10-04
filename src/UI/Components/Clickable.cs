using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace VamTimeline
{
    public class Clickable : MonoBehaviour, IPointerClickHandler
    {
        public ClickableEvent onClick = new ClickableEvent();
        public ClickableEvent onRightClick = new ClickableEvent();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                onRightClick.Invoke(eventData);
            else
                onClick.Invoke(eventData);
        }

        public void OnDestroy()
        {
            onClick.RemoveAllListeners();
            onRightClick.RemoveAllListeners();
        }

        public class ClickableEvent : UnityEvent<PointerEventData>
        {
        }
    }
}
