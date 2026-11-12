using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    // Run after menu layout so screen-space measurements include the current canvas scale.
    [DefaultExecutionOrder(100)]
    public sealed class TrialMobileKeyboardAvoidance : MonoBehaviour
    {
        RectTransform panel;
        InputField[] fields;
        InputField visibleKeyboardField;
        Vector2 appliedOffset;
        readonly Vector3[] corners = new Vector3[4];
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject androidDecor;
        int androidApi;
        bool androidKeyboardVisible;
        float nextVisibilityRead;
#endif

        public void Configure(RectTransform target)
        {
            Restore();
            visibleKeyboardField = null;
            panel = target;
            fields = target.GetComponentsInChildren<InputField>(true);
            // Keep editing text/caret in uGUI, rather than the IME's native preview.
            foreach (var field in fields) field.shouldHideMobileInput = true;
        }

        void LateUpdate()
        {
            if (!Application.isMobilePlatform) { Restore(); return; }
            UpdateKeyboardLayout(IsKeyboardVisible(), Screen.safeArea);
        }

        bool IsKeyboardVisible()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // The legacy Unity soft-input dialog can remain active after the IME's collapse
            // arrow hides the keyboard. Android window insets describe the IME itself.
            if (Time.unscaledTime < nextVisibilityRead) return androidKeyboardVisible;
            nextVisibilityRead = Time.unscaledTime + .05f;
            if (androidApi == 0)
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) androidApi = version.GetStatic<int>("SDK_INT");
            }
            if (androidApi >= 30)
            {
                if (androidDecor == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var window = activity.Call<AndroidJavaObject>("getWindow"))
                        androidDecor = window.Call<AndroidJavaObject>("getDecorView");
                }
                using (var insets = androidDecor.Call<AndroidJavaObject>("getRootWindowInsets"))
                {
                    // WindowInsets.Type.ime() == 8 (API 30+).
                    if (insets != null) return androidKeyboardVisible = insets.Call<bool>("isVisible", 8);
                }
            }
            return androidKeyboardVisible = TouchScreenKeyboard.visible;
#else
            return TouchScreenKeyboard.visible;
#endif
        }

        void UpdateKeyboardLayout(bool keyboardVisible, Rect safeArea)
        {
            Restore();
            // An IME's collapse arrow can hide the keyboard without ending InputField focus.
            // End this edit too: a hidden-but-active Unity keyboard cannot reopen on the next tap.
            // Only do this after observing visibility, so opening a keyboard asynchronously is safe.
            if (!keyboardVisible)
            {
                if (visibleKeyboardField && visibleKeyboardField.isFocused) visibleKeyboardField.DeactivateInputField();
                visibleKeyboardField = null;
                return;
            }
            if (fields == null) return;
            foreach (var field in fields)
            {
                if (!field || !field.isActiveAndEnabled || !field.isFocused) continue;
                visibleKeyboardField = field;
                // Android IMEs can report zero/stale keyboard bounds (including candidate rows).
                // Dock the focused field while visible, rather than guessing keyboard height.
                PositionForEditing((RectTransform)field.transform, safeArea);
                return;
            }
        }

        void PositionForEditing(RectTransform field, Rect safeArea)
        {
            if (!panel || !(panel.parent is RectTransform parent)) return;
            var canvas = panel.GetComponentInParent<Canvas>();
            Camera camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            field.GetWorldCorners(corners);
            Vector2 bottom = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 top = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            float gap = 12 * (canvas ? canvas.scaleFactor : 1);
            float lift = safeArea.yMax - gap - top.y;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, bottom, camera, out var start);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, bottom + Vector2.up * lift, camera, out var end);
            appliedOffset = end - start;
            panel.anchoredPosition += appliedOffset;
        }

        void Restore()
        {
            if (panel) panel.anchoredPosition -= appliedOffset;
            appliedOffset = Vector2.zero;
        }

        void OnDisable() { Restore(); visibleKeyboardField = null; }
        void OnDestroy()
        {
            Restore();
#if UNITY_ANDROID && !UNITY_EDITOR
            androidDecor?.Dispose();
            androidDecor = null;
#endif
        }
    }
}
