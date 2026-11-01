using System.Reflection;
using ConGame.Presentation.Props;
using NUnit.Framework;
using UnityEngine;

namespace ConGame.Tests.PlayMode
{
    public sealed class D22FinalePresentationPlayModeTests
    {
        [Test]
        public void BackupPistolMovesToHandAtDrawCueAndReturnsToHolsterOnResetCue()
        {
            GameObject root = new GameObject("D22WeaponVisualTest");
            try
            {
                Transform hand = new GameObject("RightHand").transform;
                hand.SetParent(root.transform, false);
                hand.localScale = Vector3.one * 206f;
                Transform holster = new GameObject("HolsterSocket").transform;
                holster.SetParent(root.transform, false);
                Transform aimTarget = new GameObject("PlayerAimTarget").transform;
                aimTarget.SetParent(root.transform, false);
                aimTarget.position = new Vector3(2f, 0f, 3f);
                GameObject main = new GameObject("PRP_PistolMain_EquippedVisual");
                main.transform.SetParent(hand, false);
                main.transform.localPosition = new Vector3(0.01f, -0.02f, 0.03f);
                main.transform.localRotation = Quaternion.Euler(1f, 2f, 3f);
                main.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                GameObject backup = new GameObject("PRP_PistolBackup_Holstered");
                backup.transform.SetParent(holster, false);
                backup.transform.localPosition = new Vector3(0.2f, 0.3f, 0.4f);
                backup.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
                backup.transform.localScale = new Vector3(0.8f, 0.9f, 1.1f);
                Transform muzzle = new GameObject("SOCKET_Muzzle").transform;
                muzzle.SetParent(backup.transform, false);
                muzzle.localPosition = new Vector3(0f, 0.035f, 0.14f);
                Vector3 holsterPosition = backup.transform.localPosition;
                Quaternion holsterRotation = backup.transform.localRotation;
                Vector3 holsterScale = backup.transform.localScale;

                KillerWeaponVisualPresenter presenter = root.AddComponent<KillerWeaponVisualPresenter>();
                presenter.Configure(main, null, null, backup, hand);
                presenter.ConfigureAimTarget(aimTarget);

                Assert.That(backup.activeSelf, Is.False);
                Assert.That(backup.transform.parent, Is.SameAs(holster));
                presenter.NotifyBackupPistolScriptedVisible(true);
                Assert.That(backup.activeSelf, Is.True);
                Assert.That(backup.transform.parent, Is.SameAs(hand));
                Assert.That(Vector3.Distance(backup.transform.lossyScale, holsterScale), Is.LessThan(0.01f),
                    "Attaching to a humanoid hand with imported scale must preserve the pistol world size.");
                AssertBackupGripAndAim(hand, backup.transform, muzzle, aimTarget);

                hand.position += new Vector3(0.4f, 0.2f, -0.3f);
                hand.rotation = Quaternion.Euler(12f, 34f, 7f);
                typeof(KillerWeaponVisualPresenter).GetMethod("LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presenter, null);
                AssertBackupGripAndAim(hand, backup.transform, muzzle, aimTarget);

                presenter.NotifyBackupPistolScriptedVisible(false);
                Assert.That(backup.activeSelf, Is.False);
                Assert.That(backup.transform.parent, Is.SameAs(holster));
                Assert.That(Vector3.Distance(backup.transform.lossyScale, holsterScale), Is.LessThan(0.01f));
                Assert.That(backup.transform.localPosition, Is.EqualTo(holsterPosition));
                Assert.That(Quaternion.Angle(backup.transform.localRotation, holsterRotation),
                    Is.LessThan(0.01f));
                Assert.That(backup.transform.localScale, Is.EqualTo(holsterScale));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertBackupGripAndAim(
            Transform hand,
            Transform backup,
            Transform muzzle,
            Transform aimTarget)
        {
            Vector3 gripPoint = backup.TransformPoint(new Vector3(0f, -0.04f, 0f));
            Assert.That(Vector3.Distance(gripPoint, hand.position), Is.LessThan(0.001f),
                "The backup pistol's grip point should stay at the animated RightHand anchor.");

            Vector3 targetPoint = aimTarget.position + Vector3.up * 1.35f;
            Vector3 toTarget = (targetPoint - muzzle.position).normalized;
            Assert.That(Vector3.Angle(muzzle.forward, toTarget), Is.LessThan(0.1f),
                "The backup pistol muzzle should aim at the player's upper body.");
        }
    }
}
