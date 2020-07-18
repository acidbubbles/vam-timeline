using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace VamTimeline
{
    public class SimpleSlider : UIBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public class SliderChangeEvent : UnityEvent<float> { }
        public readonly SliderChangeEvent onChange = new SliderChangeEvent();
        public bool interacting { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
            DispatchOnChange(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            interacting = true;
            DispatchOnChange(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            DispatchOnChange(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            DispatchOnChange(eventData);
            interacting = false;
        }

        protected override void OnDestroy()
        {
            onChange.RemoveAllListeners();
            base.OnDestroy();
        }

        private void DispatchOnChange(PointerEventData eventData)
        {
            Vector2 localPosition;
            var rect = GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var ratio = Mathf.Clamp01((localPosition.x + rect.rect.width / 2f) / rect.rect.width);
            onChange.Invoke(ratio);
        }
    }
}
