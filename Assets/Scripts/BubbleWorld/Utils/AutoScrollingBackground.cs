using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    public class AutoScrollingBackground : MonoBehaviour
    {
        [SerializeField] private RawImage background;
        [SerializeField] private float speed = 0.05f;
        [SerializeField] private bool horizontal = true;
        Rect uv;

        private void Awake()
        {
            uv = background.uvRect;
        }

        private void Update()
        {
            if (horizontal)
                uv.x += speed * Time.deltaTime;
            else
                uv.y += speed * Time.deltaTime;

            background.uvRect = uv;
        }
    }
}
