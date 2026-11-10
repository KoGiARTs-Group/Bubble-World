using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BubbleWorld
{
    public sealed class UIScreenController : MonoBehaviour
    {
        [SerializeField] private GameObject[] screenPanels;

        public GameObject GetPanel(UIScreenId id)
        {
            int index = (int)id;
            return index >= 0 && index < screenPanels.Length ? screenPanels[index] : null;
        }

        public void ShowScreen(UIScreenId id)
        {
            for (int i = 0; i < screenPanels.Length; i++)
            {
                if (screenPanels[i] != null)
                    screenPanels[i].SetActive(i == (int)id);
            }
        }

        public void BindButton(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null)
                button.onClick.AddListener(action);
        }
    }
}
