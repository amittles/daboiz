namespace Daboiz.Core
{
    /// <summary>
    /// Per-mode combat rule switch. Pure data so it can later be replicated by the network layer.
    /// </summary>
    [System.Serializable]
    public class GameRules
    {
        public bool DamageEnabled = true;
        public bool KnockbackEnabled = true;
        /// <summary>Status effects from weapon modifiers (slow, freeze, shrink...).</summary>
        public bool EffectsEnabled = true;
        public float DamageScale = 1f;
        public float KnockbackScale = 1f;
        public float SelfDamageScale = 0.35f;
        public float SelfKnockbackScale = 1f;
        public bool RespawnOnDeath = true;
        public float RespawnDelay = 2.5f;
        public bool ShowNametags = true;

        public static GameRules Default { get { return new GameRules(); } }

        public static GameRules KnockbackOnly(float knockbackScale)
        {
            return new GameRules { DamageEnabled = false, KnockbackEnabled = true, EffectsEnabled = true,
                KnockbackScale = knockbackScale, RespawnOnDeath = false };
        }
    }
}
