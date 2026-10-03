using UnityEngine;

namespace Nekki.Vector.Core
{
    public static class GameTiming
    {
        public const int SimulationRate = 60;
        public const float SimulationStep = 1f / SimulationRate;

        private const string FpsCapKey = "Vector2.Graphics.FpsCap";
        private const string VSyncKey = "Vector2.Graphics.VSync";
        private const string AaKey = "Vector2.Graphics.AA";
        private const string TextureKey = "Vector2.Graphics.TextureLimit";
        private const string AnisoKey = "Vector2.Graphics.Aniso";
        private const string InterpolationKey = "Vector2.Graphics.Interpolation";

        public static int PresentationRate { get; private set; } = SimulationRate;
        public static int FpsCap { get { return PlayerPrefs.GetInt(FpsCapKey, 0); } }
        public static bool VSyncEnabled { get { return PlayerPrefs.GetInt(VSyncKey, 0) != 0; } }
        public static int AntiAliasing { get { return PlayerPrefs.GetInt(AaKey, 0); } }
        public static int TextureMipmapLimit { get { return PlayerPrefs.GetInt(TextureKey, 0); } }
        public static int AnisotropicMode { get { return PlayerPrefs.GetInt(AnisoKey, 1); } }
        public static bool InterpolationEnabled { get { return PlayerPrefs.GetInt(InterpolationKey, 1) != 0; } }

        public static bool GameplayVisualsCanAdvance
        {
            get { return !RunMainController.IsRunNow || !RunMainController.IsPaused; }
        }

        public static bool ShouldInterpolate
        {
            get
            {
                return InterpolationEnabled
                    && PresentationRate > SimulationRate
                    && RunMainController.IsRunNow
                    && !RunMainController.IsPaused
                    && !ApplicationController.IsPaused;
            }
        }

        public static void ApplyPresentationRate()
        {
            int refresh = Mathf.Max(SimulationRate, Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value));
            int cap = FpsCap;

            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;

            if (VSyncEnabled)
            {
                Application.targetFrameRate = -1;
                PresentationRate = refresh;
            }
            else if (cap == -1)
            {
                Application.targetFrameRate = -1;
                PresentationRate = Mathf.Max(refresh, SimulationRate + 1);
            }
            else
            {
                PresentationRate = (cap <= 0) ? refresh : Mathf.Max(SimulationRate, cap);
                Application.targetFrameRate = PresentationRate;
            }

            ApplyQualitySettings();
        }

        public static void SetFpsCap(int cap)
        {
            PlayerPrefs.SetInt(FpsCapKey, cap);
            PlayerPrefs.Save();
            ApplyPresentationRate();
        }

        public static void SetVSync(bool enabled)
        {
            PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyPresentationRate();
        }

        public static void SetAntiAliasing(int level)
        {
            level = (level == 2 || level == 4 || level == 8) ? level : 0;
            PlayerPrefs.SetInt(AaKey, level);
            PlayerPrefs.Save();
            QualitySettings.antiAliasing = level;
        }

        public static void SetTextureMipmapLimit(int limit)
        {
            limit = Mathf.Clamp(limit, 0, 3);
            PlayerPrefs.SetInt(TextureKey, limit);
            PlayerPrefs.Save();
            QualitySettings.globalTextureMipmapLimit = limit;
        }

        public static void SetAnisotropicMode(int mode)
        {
            mode = Mathf.Clamp(mode, 0, 2);
            PlayerPrefs.SetInt(AnisoKey, mode);
            PlayerPrefs.Save();
            QualitySettings.anisotropicFiltering = (AnisotropicFiltering)mode;
        }

        public static void SetInterpolationEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(InterpolationKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static void ApplyQualitySettings()
        {
            QualitySettings.antiAliasing = AntiAliasing;
            QualitySettings.globalTextureMipmapLimit = TextureMipmapLimit;
            QualitySettings.anisotropicFiltering = (AnisotropicFiltering)Mathf.Clamp(AnisotropicMode, 0, 2);
        }

        public static float InterpolationAlpha
        {
            get
            {
                if (!ShouldInterpolate || Time.fixedDeltaTime <= 0f)
                {
                    return 1f;
                }
                return Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            }
        }
    }
}
