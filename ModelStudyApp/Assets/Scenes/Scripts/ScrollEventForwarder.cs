using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// Attach this to any child inside a ScrollView (e.g., your MenuItem prefab).
    /// It forwards wheel/drag events to the nearest parent ScrollRect so scrolling
    /// works even when the pointer is over Buttons/Text/etc.
    public class ScrollEventForwarder :
        MonoBehaviour,
        IScrollHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IInitializePotentialDragHandler
    {
        ScrollRect _scroll;

        void Awake()
        {
            _scroll = GetComponentInParent<ScrollRect>(includeInactive: true);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_scroll == null) return;
            _scroll.OnScroll(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_scroll == null) return;
            // make sure ScrollRect gets the drag even if the child is selectable
            _scroll.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_scroll == null) return;
            _scroll.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_scroll == null) return;
            _scroll.OnEndDrag(eventData);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (_scroll == null) return;
            _scroll.OnInitializePotentialDrag(eventData);
        }
    }
}