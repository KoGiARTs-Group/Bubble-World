using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "PlacementCatalog", menuName = "BubbleWorld/Placement Catalog")]
    public sealed class PlacementCatalog : ScriptableObject
    {
        public List<PlacementItem> Items = new List<PlacementItem>();

        private List<BubblePlacementDefinition> cachedDefinitions;

        public IReadOnlyList<BubblePlacementDefinition> Definitions
        {
            get
            {
                if (cachedDefinitions == null)
                {
                    cachedDefinitions = Items.Select(i => i.ToDefinition()).ToList();
                }
                return cachedDefinitions;
            }
        }

        public BubblePlacementDefinition Find(string id)
        {
            return Definitions.FirstOrDefault(d => d.Id == id);
        }

        public List<BubblePlacementDefinition> ForTab(BuildTab tab)
        {
            return Definitions.Where(d => d.Tab == tab).ToList();
        }

        public BubblePlacementDefinition DefaultFor(StarterEnvironment environment)
        {
            switch (environment)
            {
                case StarterEnvironment.Forest:
                    return Find("tree");
                case StarterEnvironment.BeachCoastal:
                    return Find("sand");
                default:
                    return Find("grass");
            }
        }

        public void InvalidateCache()
        {
            cachedDefinitions = null;
        }
    }
}
