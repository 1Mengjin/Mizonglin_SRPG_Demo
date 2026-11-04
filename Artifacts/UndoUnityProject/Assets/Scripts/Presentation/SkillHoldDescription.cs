using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zilong.Demo
{
    public sealed class SkillHoldDescription : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action show, hide;
        bool pressing, displayed;
        float started;
        int suppressThroughFrame = -1;
        public bool SuppressClick => displayed || Time.frameCount <= suppressThroughFrame;
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressing = true; displayed = false; started = Time.unscaledTime; suppressThroughFrame = -1;
        }
        void Update()
        {
            if (pressing && !displayed && Time.unscaledTime - started >= .5f)
            { displayed = true; show?.Invoke(); }
        }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Close(); }
        public void OnPointerExit(PointerEventData e) => Close();
        void OnDisable() => Close();
        void Close()
        {
            pressing = false;
            if (displayed) { suppressThroughFrame = Time.frameCount + 1; displayed = false; hide?.Invoke(); }
        }
    }
}
