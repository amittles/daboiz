using UnityEngine;

namespace Daboiz.Core
{
    /// <summary>
    /// Zero-setup entry point: press Play in ANY scene (even a brand-new empty one) and the game
    /// spawns its persistent GameFlow object, which owns the menu, camera and every mode.
    /// Set <see cref="Disabled"/> = true from your own code (e.g. a test scene) to opt out.
    /// </summary>
    public static class GameBootstrap
    {
        public static bool Disabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Disabled) return;
            if (Object.FindAnyObjectByType<GameFlow>() != null) return;
            GameObject go = new GameObject("DABOIZ_GameFlow");
            go.AddComponent<GameFlow>();
        }
    }
}
