using UnityEngine;

namespace ConGame.Presentation.CRT
{
    [CreateAssetMenu(menuName = "ConGame/Presentation/MV Signature Settings")]
    public sealed class MvSignatureSettings : ScriptableObject
    {
        public Texture2D Title;
        public Texture2D Realisation;
        public Texture2D Glass;
        public Material FlashlightAir;
        [Min(0f)] public float DeathTitleDelay = 0.22f;
        [Min(0.01f)] public float TitleFadeSeconds = 0.1f;
        [Min(0.01f)] public float RealisationSeconds = 0.65f;
        [Min(0.01f)] public float MuzzleLightSeconds = 0.085f;
        [Min(0f)] public float MuzzleLightIntensity = 9f;
        [Min(0.1f)] public float MuzzleLightRange = 8f;
        [Range(0f, 1f)] public float DarkGrade = 1f;
    }
}
