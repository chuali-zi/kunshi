using System;

namespace ConGame.Runtime.Settings
{
    public enum SettingsRow
    {
        MasterVolume = 0,
        LookSensitivity = 1,
        InvertLookY = 2,
        QteAssist = 3,
        ReduceFlashing = 4,
        CrtIntensity = 5,
        Back = 6
    }

    public sealed class SettingsMenuModel
    {
        private const int RowCount = (int)SettingsRow.Back + 1;

        public SettingsMenuModel(GameSettingsV1 settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Settings.Normalize();
        }

        public GameSettingsV1 Settings { get; }
        public SettingsRow SelectedRow { get; private set; }

        public void MoveSelection(int direction)
        {
            if (direction == 0)
            {
                return;
            }

            int next = ((int)SelectedRow + Math.Sign(direction) + RowCount) % RowCount;
            SelectedRow = (SettingsRow)next;
        }

        public bool AdjustSelected(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            switch (SelectedRow)
            {
                case SettingsRow.MasterVolume:
                    Settings.masterVolume += Math.Sign(direction) * 0.1f;
                    break;
                case SettingsRow.LookSensitivity:
                    Settings.lookSensitivity += Math.Sign(direction) * 0.25f;
                    break;
                case SettingsRow.InvertLookY:
                    Settings.invertLookY = !Settings.invertLookY;
                    break;
                case SettingsRow.QteAssist:
                    Settings.qteThresholdScale += Math.Sign(direction) * 0.1f;
                    break;
                case SettingsRow.ReduceFlashing:
                    Settings.reduceFlashing = !Settings.reduceFlashing;
                    break;
                case SettingsRow.CrtIntensity:
                    Settings.crtIntensity += Math.Sign(direction) * 0.1f;
                    break;
                default:
                    return false;
            }

            Settings.Normalize();
            return true;
        }

        public bool ActivateSelected()
        {
            switch (SelectedRow)
            {
                case SettingsRow.InvertLookY:
                case SettingsRow.ReduceFlashing:
                    return AdjustSelected(1);
                default:
                    return false;
            }
        }
    }
}
