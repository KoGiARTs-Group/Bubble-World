using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BubbleWorld
{
    public sealed class SelectionHighlightController : MonoBehaviour
    {
        private const string SelectedName = "Selected";
        private const string SelectionName = "Selection";

        [SerializeField] private bool clearOnStart;

        private readonly HashSet<Button> wiredButtons = new HashSet<Button>();
        private Button lastButton;

        private void Start()
        {
            WireAll();
        }

        public void RebindAll()
        {
            wiredButtons.Clear();
            WireAll();
        }

        public void Register(Button button)
        {
            if (button == null) return;
            WireButton(button);
        }

        public void ClearAll()
        {
            foreach (Button button in wiredButtons)
            {
                SetActiveIfPresent(FindSelected(button.transform), false);
                SetActiveIfPresent(FindPanelSelected(button.transform), false);
            }
        }

        private void WireAll()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                WireButton(button);
                if (clearOnStart)
                {
                    SetActiveIfPresent(FindSelected(button.transform), false);
                    SetActiveIfPresent(FindPanelSelected(button.transform), false);
                }
            }
        }

        private void WireButton(Button button)
        {
            if (button == null || !wiredButtons.Add(button)) return;
            if (IsManagedByScreen(button)) return;
            button.onClick.AddListener(() => Select(button));
        }

        private static bool IsManagedByScreen(Button button)
        {
            return button.GetComponentInParent<IManageSelectionHighlights>() != null;
        }

        private void Select(Button button)
        {
            if (button == null || button == lastButton) return;

            if (lastButton != null)
            {
                SetActiveIfPresent(FindSelected(lastButton.transform), false);
                SetActiveIfPresent(FindPanelSelected(lastButton.transform), false);
            }

            SetActiveIfPresent(FindSelected(button.transform), true);
            SetActiveIfPresent(FindPanelSelected(button.transform), true);

            lastButton = button;
        }

        private static GameObject FindSelected(Transform target)
        {
            if (target == null) return null;
            for (int i = 0; i < target.childCount; i++)
            {
                Transform child = target.GetChild(i);
                if (child.name == SelectedName || child.name == SelectionName) return child.gameObject;
            }
            for (int i = 0; i < target.childCount; i++)
            {
                Transform child = target.GetChild(i);
                if (child.GetComponent<Text>() == null && child.GetComponent<TMPro.TextMeshProUGUI>() == null)
                    return child.gameObject;
            }
            return null;
        }

        private static GameObject FindPanelSelected(Transform button)
        {
            Transform current = button != null ? button.parent : null;
            while (current != null)
            {
                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);
                    if (child.name == SelectedName || child.name == SelectionName) return child.gameObject;
                }
                current = current.parent;
            }
            return null;
        }

        private static void SetActiveIfPresent(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
