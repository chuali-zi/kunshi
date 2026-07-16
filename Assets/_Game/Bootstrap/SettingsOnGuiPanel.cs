using ConGame.Runtime.Settings;
using UnityEngine;

namespace ConGame.Bootstrap
{
    internal static class SettingsOnGuiPanel
    {
        public static bool Draw(SettingsMenuModel model, float x, float y, float width, float height,
            out bool backRequested)
        {
            bool changed = false;
            backRequested = false;
            GameSettingsV1 settings = model.Settings;

            GUI.Label(new Rect(x, y - 52f, width, 40f), "设置 / Settings");
            changed |= DrawValueRow(model, SettingsRow.MasterVolume, x, y, width, height,
                $"主音量 / Volume  {settings.masterVolume:P0}");
            changed |= DrawValueRow(model, SettingsRow.LookSensitivity, x, y + 46f, width, height,
                $"视角灵敏度 / Look  {settings.lookSensitivity:0.00}");
            changed |= DrawToggleRow(model, SettingsRow.InvertLookY, x, y + 92f, width, height,
                $"反转 Y / Invert Y  {OnOff(settings.invertLookY)}");
            changed |= DrawValueRow(model, SettingsRow.QteAssist, x, y + 138f, width, height,
                $"QTE 阈值 / QTE  {settings.qteThresholdScale:P0}");
            changed |= DrawToggleRow(model, SettingsRow.ReduceFlashing, x, y + 184f, width, height,
                $"减少闪烁 / Reduce Flash  {OnOff(settings.reduceFlashing)}");
            changed |= DrawValueRow(model, SettingsRow.CrtIntensity, x, y + 230f, width, height,
                $"CRT 强度 / CRT  {settings.crtIntensity:P0}");

            if (GUI.Button(new Rect(x, y + 286f, width, height), Selected(model, SettingsRow.Back, "返回 / Back")))
            {
                backRequested = true;
            }

            return changed;
        }

        private static bool DrawValueRow(SettingsMenuModel model, SettingsRow row, float x, float y, float width,
            float height, string label)
        {
            float arrowWidth = 38f;
            bool changed = false;
            if (GUI.Button(new Rect(x, y, arrowWidth, height), "<"))
            {
                changed = SelectAndAdjust(model, row, -1);
            }

            GUI.Label(new Rect(x + arrowWidth, y, width - arrowWidth * 2f, height), Selected(model, row, label));
            if (GUI.Button(new Rect(x + width - arrowWidth, y, arrowWidth, height), ">"))
            {
                changed |= SelectAndAdjust(model, row, 1);
            }

            return changed;
        }

        private static bool DrawToggleRow(SettingsMenuModel model, SettingsRow row, float x, float y, float width,
            float height, string label)
        {
            if (!GUI.Button(new Rect(x, y, width, height), Selected(model, row, label)))
            {
                return false;
            }

            Select(model, row);
            return model.ActivateSelected();
        }

        private static bool SelectAndAdjust(SettingsMenuModel model, SettingsRow row, int direction)
        {
            Select(model, row);
            return model.AdjustSelected(direction);
        }

        private static void Select(SettingsMenuModel model, SettingsRow row)
        {
            while (model.SelectedRow != row)
            {
                model.MoveSelection(1);
            }
        }

        private static string Selected(SettingsMenuModel model, SettingsRow row, string text)
        {
            return model.SelectedRow == row ? $"> {text} <" : text;
        }

        private static string OnOff(bool enabled)
        {
            return enabled ? "开 / On" : "关 / Off";
        }
    }
}
