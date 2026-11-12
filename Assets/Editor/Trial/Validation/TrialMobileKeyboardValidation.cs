using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial.Editor
{
    public static class TrialMobileKeyboardValidation
    {
        [MenuItem("Tools/Tomorin/Verify mobile keyboard avoidance")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var results = new List<string>();
            foreach (float scale in new[] { 1f, 2f })
            {
                var fixture = new GameObject("Keyboard validation", typeof(RectTransform), typeof(Canvas));
                fixture.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var canvas = fixture.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.scaleFactor = scale;
                    var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
                    panel.SetParent(fixture.transform, false);
                    panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
                    panel.offsetMin = panel.offsetMax = Vector2.zero;
                    var field = new GameObject("Edited field", typeof(RectTransform), typeof(InputField)).GetComponent<RectTransform>();
                    field.SetParent(panel, false);
                    field.anchorMin = field.anchorMax = new Vector2(.5f, .3f);
                    field.sizeDelta = new Vector2(Screen.width * .4f / scale, Screen.height * .06f / scale);
                    var avoidance = panel.gameObject.AddComponent<TrialMobileKeyboardAvoidance>();
                    avoidance.Configure(panel);
                    Canvas.ForceUpdateCanvases();
                    Vector2 original = panel.anchoredPosition;
                    var safe = new Rect(0, 0, Screen.width, Screen.height);
                    var keyboard = new Rect(0, 0, Screen.width, Screen.height * .75f);
                    void Check(bool ok, string message)
                    {
                        if (!ok) throw new Exception(message + " at scale " + scale);
                        results.Add("PASS " + message + " (scale " + scale + ")");
                    }
                    void Apply() => typeof(TrialMobileKeyboardAvoidance).GetMethod("PositionForEditing", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(avoidance, new object[] { field, safe });
                    void Restore() => typeof(TrialMobileKeyboardAvoidance).GetMethod("Restore", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(avoidance, null);
                    Rect Bounds()
                    {
                        var points = new Vector3[4]; field.GetWorldCorners(points);
                        return Rect.MinMaxRect(points[0].x, points[0].y, points[2].x, points[2].y);
                    }
                    Check(Bounds().yMin < keyboard.yMax, "Baseline field is covered by keyboard");
                    Apply();
                    Check(Bounds().yMin >= keyboard.yMax + 11 * scale, "Entire edited field clears keyboard with padding");
                    Check(Bounds().yMax < safe.yMax, "Edited field stays below top edge");
                    Vector2 raised = panel.anchoredPosition;
                    Restore(); Apply();
                    Check(Vector2.Distance(raised, panel.anchoredPosition) < .01f, "Repeated frames do not accumulate movement");
                    Restore();
                    Check(Vector2.Distance(original, panel.anchoredPosition) < .01f, "End editing restores exact layout");
                    field.anchorMin = field.anchorMax = new Vector2(.5f, .8f);
                    Apply();
                    Check(Mathf.Abs(Bounds().yMax - (safe.yMax - 12 * scale)) < .1f, "Switching fields keeps edited text at safe top");
                    Restore();
                    safe.height -= Screen.height * .05f;
                    Apply();
                    Check(Mathf.Abs(Bounds().yMax - (safe.yMax - 12 * scale)) < .1f, "Top inset is respected");
                    Restore();
                    // Model the IME collapse path: the field stays focused when visibility changes.
                    var input = field.GetComponent<InputField>();
                    input.text = "Retained text";
                    var focus = typeof(InputField).GetField("m_AllowInput", BindingFlags.Instance | BindingFlags.NonPublic);
                    focus.SetValue(input, true);
                    void Visibility(bool visible) => typeof(TrialMobileKeyboardAvoidance).GetMethod("UpdateKeyboardLayout", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(avoidance, new object[] { visible, safe });
                    Visibility(true);
                    Vector2 beforeCollapse = panel.anchoredPosition;
                    Check(Vector2.Distance(beforeCollapse, original) > 1, "Visible keyboard lifts the focused field through production layout path");
                    Visibility(false);
                    Check(!input.isFocused && input.text == "Retained text" && Vector2.Distance(original, panel.anchoredPosition) < .01f, "IME collapse ends stale editing and restores layout without changing text");
                    Visibility(false);
                    Check(Vector2.Distance(original, panel.anchoredPosition) < .01f, "Hidden keyboard keeps layout restored without another screen tap");
                    focus.SetValue(input, true);
                    Visibility(false);
                    Check(input.isFocused, "A new edit waiting for its keyboard is not canceled");
                    Visibility(true);
                    Check(Vector2.Distance(beforeCollapse, panel.anchoredPosition) < .01f, "Reopening keyboard reapplies the same offset");
                    focus.SetValue(input, false);
                    avoidance.enabled = false;
                    Check(Vector2.Distance(original, panel.anchoredPosition) < .01f, "Disabling restores layout");
                }
                finally { UnityEngine.Object.DestroyImmediate(fixture); }
            }
            ValidateRoomForms(results);
            Directory.CreateDirectory("Logs/Trial");
            File.WriteAllLines("Logs/Trial/MobileKeyboardValidation.txt", results);
            Debug.Log("Mobile keyboard avoidance: " + results.Count + " checks passed. Device keyboard/IME verification still required.");
        }

        static void ValidateRoomForms(List<string> results)
        {
            var menu = UnityEngine.Object.FindAnyObjectByType<InkMainMenu>();
            if (!menu) throw new InvalidOperationException("Run in the InkStart menu scene.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(InkMainMenu);
            T Field<T>(string name) => (T)type.GetField(name, flags).GetValue(menu);
            void Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(menu, args);
            var panel = Field<RectTransform>("network");
            var avoidance = panel.GetComponent<TrialMobileKeyboardAvoidance>();
            Vector2 original = panel.anchoredPosition;
            var safe = new Rect(0, 0, Screen.width, Screen.height);
            int checkedFields = 0;
            try
            {
                panel.gameObject.SetActive(true);
                foreach (bool create in new[] { false, true })
                {
                    Call("SelectRoomTab", create);
                    if (create)
                    {
                        Call("SelectHostConnection", false);
                        type.GetField("createPublicRoom", flags).SetValue(menu, true);
                        Call("RefreshNetwork");
                    }
                    Canvas.ForceUpdateCanvases();
                    foreach (string name in create ? new[] { "nickname", "roomName", "port", "publicEndpoint" } : new[] { "nickname", "address" })
                    {
                        var input = Field<InputField>(name);
                        if (!input.isActiveAndEnabled) throw new Exception(name + " is not visible in the room form");
                        // Read the serialized backing flag: desktop's getter always returns true.
                        if (!new SerializedObject(input).FindProperty("m_HideMobileInput").boolValue)
                            throw new Exception(name + " still uses native mobile text preview");
                        var bounds = new Vector3[4];
                        typeof(TrialMobileKeyboardAvoidance).GetMethod("PositionForEditing", flags)
                            .Invoke(avoidance, new object[] { (RectTransform)input.transform, safe });
                        input.GetComponent<RectTransform>().GetWorldCorners(bounds);
                        float top = RectTransformUtility.WorldToScreenPoint(null, bounds[2]).y;
                        float bottom = RectTransformUtility.WorldToScreenPoint(null, bounds[0]).y;
                        var canvas = panel.GetComponentInParent<Canvas>();
                        if (Mathf.Abs(top - (safe.yMax - 12 * canvas.scaleFactor)) > .1f || bottom <= Screen.height * .75f)
                            throw new Exception(name + " remains covered by a tall keyboard");
                        typeof(TrialMobileKeyboardAvoidance).GetMethod("Restore", flags).Invoke(avoidance, null);
                        if (Vector2.Distance(original, panel.anchoredPosition) > .01f) throw new Exception(name + " did not restore layout");
                        results.Add("PASS Actual " + (create ? "create" : "join") + " form: " + name + " clears 75% keyboard and restores");
                        checkedFields++;
                    }
                }
                if (checkedFields != 6) throw new Exception("Missing room form coverage");
            }
            finally
            {
                typeof(TrialMobileKeyboardAvoidance).GetMethod("Restore", flags).Invoke(avoidance, null);
                type.GetField("createPublicRoom", flags).SetValue(menu, false);
                Call("Show", Field<RectTransform>("home"));
            }
        }
    }
}
