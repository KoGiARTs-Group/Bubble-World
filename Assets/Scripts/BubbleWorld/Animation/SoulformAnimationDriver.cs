using UnityEngine;

namespace BubbleWorld
{
    public sealed class SoulformAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Tooltip("Animation parameter names matching your Animator Controller triggers")]
        public string[] animationParameters = new string[]
        {
            "Idle", "Wave", "Nod", "ShakeHead", "LookAround",
            "Point", "Sit", "Walk", "Celebrate", "Defeated"
        };

        private string currentAnimation = "Idle";

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        public void PlayAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName))
                animationName = "Idle";

            if (animationName == currentAnimation)
                return;

            if (animator == null)
            {
                Debug.LogWarning("SoulformAnimationDriver: no Animator assigned");
                currentAnimation = animationName;
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("SoulformAnimationDriver: no Animator Controller assigned");
                currentAnimation = animationName;
                return;
            }

            // Try trigger parameter first
            bool hasTrigger = false;
            foreach (var param in animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger &&
                    string.Equals(param.name, animationName, System.StringComparison.OrdinalIgnoreCase))
                {
                    hasTrigger = true;
                    break;
                }
            }

            if (hasTrigger)
            {
                // Reset previous trigger, set new one
                animator.ResetTrigger(currentAnimation);
                animator.SetTrigger(animationName);
            }
            else
            {
                // Fallback: play state by name
                animator.Play(animationName, 0, 0f);
            }

            currentAnimation = animationName;
        }

        public string GetCurrentAnimation()
        {
            return currentAnimation;
        }
    }
}
