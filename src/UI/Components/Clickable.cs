using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace VamTimeline
{
    public class Clickable : MonoBehaviour, IPointerClickHandler
    {
        public ClickableEvent onClick = new ClickableEvent();
		public ClickableEvent onLeftClick = new ClickableEvent();
		public ClickableEvent onRightClick = new ClickableEvent();

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick.Invoke(eventData);

			if (eventData.button == PointerEventData.InputButton.Left)
				onLeftClick?.Invoke(eventData);
			else if (eventData.button == PointerEventData.InputButton.Right)
				onRightClick?.Invoke(eventData);
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
