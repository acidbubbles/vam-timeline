using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
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
