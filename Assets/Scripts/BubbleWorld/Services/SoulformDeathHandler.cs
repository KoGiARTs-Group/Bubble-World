using UnityEngine;

namespace BubbleWorld
{
    public sealed class SoulformDeathHandler
    {
        public bool IsDead { get; private set; }
        public string CauseOfDeath { get; private set; }

        public event System.Action<string> OnDeath;

        public void CheckForDeath(SoulformModel soulform)
        {
            if (IsDead || soulform == null) return;

            if (soulform.Health <= 0)
            {
                TriggerDeath(soulform, "Health depleted");
            }
            else if (soulform.Hunger <= 0)
            {
                TriggerDeath(soulform, "Starvation");
            }
        }

        private void TriggerDeath(SoulformModel soulform, string cause)
        {
            IsDead = true;
            CauseOfDeath = cause;
            soulform.CurrentActivity = "Unconscious";
            soulform.CurrentThought = "Everything is fading...";
            soulform.CurrentExpression = "Unconscious";
            soulform.CurrentAnimation = "Fallen";
            OnDeath?.Invoke(cause);
        }

        public void Reset()
        {
            IsDead = false;
            CauseOfDeath = null;
        }
    }
}
