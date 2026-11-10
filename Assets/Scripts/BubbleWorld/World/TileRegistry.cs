using System.Linq;
using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "TileRegistry", menuName = "BubbleWorld/Tile Registry")]
    public sealed class TileRegistry : ScriptableObject
    {
        [SerializeField] private TileDefinition[] tileDefinitions;

        public TileDefinition GetTile(int id)
        {
            if (tileDefinitions == null) return null;
            for (int i = 0; i < tileDefinitions.Length; i++)
            {
                if (tileDefinitions[i] != null && tileDefinitions[i].Id == id)
                    return tileDefinitions[i];
            }
            return null;
        }

        public TileDefinition GetTileForConnections(int mask)
        {
            if (tileDefinitions == null) return null;
            return tileDefinitions.FirstOrDefault(t =>
                t.WaterConnections != null &&
                t.WaterConnections.Contains(mask));
        }

        public TileDefinition GetTileByName(string name)
        {
            if (tileDefinitions == null) return null;
            for (int i = 0; i < tileDefinitions.Length; i++)
            {
                if (tileDefinitions[i] != null &&
                    tileDefinitions[i].Name == name)
                    return tileDefinitions[i];
            }
            return null;
        }

        public TileDefinition[] GetAll() => tileDefinitions;
    }
}
