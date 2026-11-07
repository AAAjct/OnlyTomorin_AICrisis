using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TomorinTrial
{
    public sealed class InkPreviewOrbit : MonoBehaviour, IDragHandler, IScrollHandler
    {
        const float DefaultSize = 1.18f;
        const float MinSize = .62f;
        const float MaxSize = 1.75f;
        Camera view;
        Transform actor;
        Canvas canvas;
        float yaw = 11.5f;
        float pitch = 4;
        float size = DefaultSize;

        public void Configure(Camera camera, Transform target)
        {
            view = camera;
            actor = target;
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            UpdateView();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Vector2 delta = eventData.delta / canvas.scaleFactor;
            yaw = Mathf.Repeat(yaw + delta.x * .3f, 360);
            pitch = Mathf.Clamp(pitch - delta.y * .3f, -30, 50);
            UpdateView();
        }

        public void OnScroll(PointerEventData eventData)
        {
            float ticks = eventData.scrollDelta.y;
            if (eventData.currentInputModule is InputSystemUIInputModule module && module.scrollDeltaPerTick > 0)
                ticks /= module.scrollDeltaPerTick;
            size = Mathf.Clamp(size * Mathf.Pow(.9f, ticks), MinSize, MaxSize);
            UpdateView();
        }

        void UpdateView()
        {
            float closeup = Mathf.InverseLerp(DefaultSize, MinSize, size);
            Vector3 pivot = actor.position + Vector3.up * Mathf.Lerp(.91f, 1.26f, closeup);
            Vector3 offset = Quaternion.Euler(-pitch, yaw, 0) * (Vector3.forward * 4.8f);
            view.transform.position = pivot + offset;
            view.transform.LookAt(pivot);
            view.orthographicSize = size;
        }
    }
}
