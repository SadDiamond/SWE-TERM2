using UnityEngine;

public static class ProjectStructureThemePalette
{
    public struct MenuPalette
    {
        public Color background;
        public Color accent;
        public Color panel;
        public Color mass;
    }

    public struct SupportPresentation
    {
        public Color fogColor;
        public Color skyColor;
        public Color equatorColor;
        public Color groundColor;
        public Color keyColor;
        public Color fillColorA;
        public Color fillColorB;
        public float fogDensity;
    }

    public static int NormalizeThemeIndex(int themeIndex)
    {
        return Mathf.Abs(themeIndex) % 4;
    }

    public static Color ResolveAccent(int themeIndex)
    {
        switch (NormalizeThemeIndex(themeIndex))
        {
            case 1: return new Color(0.48f, 0.84f, 1f);
            case 2: return new Color(1f, 0.56f, 0.22f);
            case 3: return new Color(0.56f, 1f, 0.62f);
            default: return new Color(0.34f, 0.82f, 1f);
        }
    }

    public static Color ResolveOverlayAccent(int themeIndex)
    {
        switch (NormalizeThemeIndex(themeIndex))
        {
            case 1: return new Color(0.76f, 0.86f, 1f);
            case 2: return new Color(1f, 0.82f, 0.56f);
            case 3: return new Color(0.78f, 1f, 0.82f);
            default: return new Color(0.84f, 0.96f, 1f);
        }
    }

    public static Color ResolveOverlayPanel(int themeIndex)
    {
        switch (NormalizeThemeIndex(themeIndex))
        {
            case 1: return new Color(0.01f, 0.025f, 0.05f, 0.94f);
            case 2: return new Color(0.04f, 0.02f, 0.015f, 0.94f);
            case 3: return new Color(0.015f, 0.035f, 0.02f, 0.94f);
            default: return new Color(0.01f, 0.02f, 0.03f, 0.92f);
        }
    }

    public static MenuPalette ResolveMenuPalette(int themeIndex)
    {
        switch (NormalizeThemeIndex(themeIndex))
        {
            case 1:
                return new MenuPalette
                {
                    background = new Color(0.007f, 0.015f, 0.026f, 1f),
                    accent = new Color(0.42f, 0.82f, 1f, 1f),
                    panel = new Color(0.02f, 0.07f, 0.1f, 1f),
                    mass = new Color(0.02f, 0.035f, 0.055f, 1f)
                };
            case 2:
                return new MenuPalette
                {
                    background = new Color(0.02f, 0.012f, 0.01f, 1f),
                    accent = new Color(1f, 0.56f, 0.22f, 1f),
                    panel = new Color(0.08f, 0.035f, 0.022f, 1f),
                    mass = new Color(0.055f, 0.028f, 0.018f, 1f)
                };
            case 3:
                return new MenuPalette
                {
                    background = new Color(0.006f, 0.018f, 0.012f, 1f),
                    accent = new Color(0.56f, 1f, 0.66f, 1f),
                    panel = new Color(0.022f, 0.075f, 0.04f, 1f),
                    mass = new Color(0.018f, 0.04f, 0.024f, 1f)
                };
            default:
                return new MenuPalette
                {
                    background = new Color(0.006f, 0.011f, 0.016f, 1f),
                    accent = new Color(0.22f, 0.82f, 0.92f, 1f),
                    panel = new Color(0.025f, 0.065f, 0.078f, 1f),
                    mass = new Color(0.03f, 0.024f, 0.02f, 1f)
                };
        }
    }

    public static void ResolveTransitionColors(int themeIndex, out Color accent, out Color panel, out Color flash)
    {
        switch (NormalizeThemeIndex(themeIndex))
        {
            case 1:
                accent = new Color(0.56f, 0.82f, 1f);
                panel = new Color(0.01f, 0.025f, 0.05f, 1f);
                flash = new Color(0.46f, 0.84f, 1f, 0.22f);
                break;
            case 2:
                accent = new Color(1f, 0.62f, 0.34f);
                panel = new Color(0.05f, 0.022f, 0.016f, 1f);
                flash = new Color(1f, 0.54f, 0.22f, 0.2f);
                break;
            case 3:
                accent = new Color(0.62f, 1f, 0.72f);
                panel = new Color(0.012f, 0.03f, 0.02f, 1f);
                flash = new Color(0.46f, 1f, 0.64f, 0.18f);
                break;
            default:
                accent = new Color(0.72f, 0.92f, 1f);
                panel = new Color(0.008f, 0.016f, 0.022f, 1f);
                flash = new Color(0.72f, 0.95f, 1f, 0.28f);
                break;
        }
    }

    public static void ResolveLoadingColors(int themeIndex, bool sandbox, bool heroArena, out Color accent, out Color panel, out Color halo)
    {
        if (heroArena)
        {
            accent = new Color(0.56f, 0.82f, 1f);
            panel = new Color(0.012f, 0.022f, 0.038f, 1f);
            halo = new Color(0.48f, 0.82f, 1f, 0.16f);
            return;
        }

        if (sandbox)
        {
            accent = new Color(0.72f, 0.92f, 1f);
            panel = new Color(0.01f, 0.018f, 0.028f, 1f);
            halo = new Color(0.44f, 0.74f, 1f, 0.14f);
            return;
        }

        switch (NormalizeThemeIndex(themeIndex))
        {
            case 1:
                accent = new Color(0.56f, 0.82f, 1f);
                panel = new Color(0.012f, 0.024f, 0.048f, 1f);
                halo = new Color(0.48f, 0.84f, 1f, 0.15f);
                break;
            case 2:
                accent = new Color(1f, 0.62f, 0.32f);
                panel = new Color(0.05f, 0.022f, 0.014f, 1f);
                halo = new Color(1f, 0.48f, 0.18f, 0.14f);
                break;
            case 3:
                accent = new Color(0.62f, 1f, 0.72f);
                panel = new Color(0.012f, 0.03f, 0.02f, 1f);
                halo = new Color(0.42f, 0.94f, 0.54f, 0.13f);
                break;
            default:
                accent = new Color(0.72f, 0.92f, 1f);
                panel = new Color(0.008f, 0.016f, 0.024f, 1f);
                halo = new Color(0.38f, 0.76f, 1f, 0.14f);
                break;
        }
    }

    public static SupportPresentation ResolveHeroArenaPresentation(int themeIndex)
    {
        switch (NormalizeThemeIndex(themeIndex))
        {
            case 1:
                return new SupportPresentation
                {
                    fogColor = new Color(0.012f, 0.02f, 0.032f),
                    skyColor = new Color(0.09f, 0.14f, 0.22f),
                    equatorColor = new Color(0.04f, 0.06f, 0.09f),
                    groundColor = new Color(0.016f, 0.018f, 0.022f),
                    keyColor = new Color(0.66f, 0.82f, 1f),
                    fillColorA = new Color(0.44f, 0.78f, 1f),
                    fillColorB = new Color(0.24f, 0.58f, 0.88f),
                    fogDensity = 0.0144f
                };
            case 2:
                return new SupportPresentation
                {
                    fogColor = new Color(0.026f, 0.018f, 0.014f),
                    skyColor = new Color(0.16f, 0.08f, 0.05f),
                    equatorColor = new Color(0.08f, 0.04f, 0.03f),
                    groundColor = new Color(0.026f, 0.018f, 0.016f),
                    keyColor = new Color(1f, 0.72f, 0.52f),
                    fillColorA = new Color(1f, 0.46f, 0.18f),
                    fillColorB = new Color(0.78f, 0.32f, 0.14f),
                    fogDensity = 0.0118f
                };
            case 3:
                return new SupportPresentation
                {
                    fogColor = new Color(0.012f, 0.022f, 0.016f),
                    skyColor = new Color(0.06f, 0.14f, 0.09f),
                    equatorColor = new Color(0.03f, 0.07f, 0.05f),
                    groundColor = new Color(0.014f, 0.02f, 0.016f),
                    keyColor = new Color(0.78f, 1f, 0.82f),
                    fillColorA = new Color(0.38f, 1f, 0.56f),
                    fillColorB = new Color(0.2f, 0.66f, 0.32f),
                    fogDensity = 0.0132f
                };
            default:
                return new SupportPresentation
                {
                    fogColor = new Color(0.012f, 0.018f, 0.026f),
                    skyColor = new Color(0.08f, 0.11f, 0.16f),
                    equatorColor = new Color(0.04f, 0.05f, 0.07f),
                    groundColor = new Color(0.016f, 0.018f, 0.02f),
                    keyColor = new Color(0.74f, 0.84f, 1f),
                    fillColorA = new Color(0.36f, 0.74f, 1f),
                    fillColorB = new Color(0.22f, 0.58f, 0.82f),
                    fogDensity = 0.0125f
                };
        }
    }

    public static SupportPresentation ResolveSandboxPresentation(int themeIndex)
    {
        switch (NormalizeThemeIndex(themeIndex))
        {
            case 1:
                return new SupportPresentation
                {
                    fogColor = new Color(0.012f, 0.02f, 0.03f),
                    skyColor = new Color(0.08f, 0.12f, 0.2f),
                    equatorColor = new Color(0.04f, 0.06f, 0.09f),
                    groundColor = new Color(0.016f, 0.018f, 0.022f),
                    keyColor = new Color(0.66f, 0.82f, 1f),
                    fillColorA = new Color(0.42f, 0.76f, 1f),
                    fillColorB = new Color(0.24f, 0.58f, 0.86f),
                    fogDensity = 0.014f
                };
            case 2:
                return new SupportPresentation
                {
                    fogColor = new Color(0.026f, 0.018f, 0.014f),
                    skyColor = new Color(0.16f, 0.08f, 0.05f),
                    equatorColor = new Color(0.08f, 0.04f, 0.03f),
                    groundColor = new Color(0.026f, 0.018f, 0.016f),
                    keyColor = new Color(1f, 0.72f, 0.52f),
                    fillColorA = new Color(1f, 0.46f, 0.18f),
                    fillColorB = new Color(0.76f, 0.32f, 0.14f),
                    fogDensity = 0.0112f
                };
            case 3:
                return new SupportPresentation
                {
                    fogColor = new Color(0.012f, 0.022f, 0.016f),
                    skyColor = new Color(0.06f, 0.14f, 0.09f),
                    equatorColor = new Color(0.03f, 0.07f, 0.05f),
                    groundColor = new Color(0.014f, 0.02f, 0.016f),
                    keyColor = new Color(0.78f, 1f, 0.82f),
                    fillColorA = new Color(0.38f, 1f, 0.56f),
                    fillColorB = new Color(0.2f, 0.66f, 0.32f),
                    fogDensity = 0.013f
                };
            default:
                return new SupportPresentation
                {
                    fogColor = new Color(0.014f, 0.019f, 0.024f),
                    skyColor = new Color(0.06f, 0.09f, 0.14f),
                    equatorColor = new Color(0.035f, 0.04f, 0.055f),
                    groundColor = new Color(0.016f, 0.018f, 0.02f),
                    keyColor = new Color(0.72f, 0.84f, 1f),
                    fillColorA = new Color(0.34f, 0.72f, 1f),
                    fillColorB = new Color(0.24f, 0.54f, 0.76f),
                    fogDensity = 0.012f
                };
        }
    }
}
