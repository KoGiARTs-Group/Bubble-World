using UnityEngine;

namespace BubbleWorld
{
    [CreateAssetMenu(fileName = "SoulformBrainConfig", menuName = "BubbleWorld/Soulform Brain Config")]
    public sealed class SoulformBrainConfig : ScriptableObject
    {
        public string ApiEndpoint;
        public string ApiKey;
        public string ModelId = "default";
        public int TimeoutSeconds = 30;

        [Header("Backend links (used when ApiEndpoint is blank; first reachable wins)")]
        [Tooltip("Local dev backend base URL, e.g. http://localhost:3000")]
        public string LocalBackendUrl = "http://localhost:3000";
        [Tooltip("Deployed backend base URL, e.g. https://api.example.com — leave blank until deployed")]
        public string RemoteBackendUrl = "";

        [Header("AWS Bedrock direct (only when ApiEndpoint and backend links are blank)")]
        public string AwsRegion = "us-east-1";
        public string AwsAccessKey;
        public string AwsSecretKey;

        [TextArea(5, 10)]
        public string PromptTemplate = "You are {name}, a {temperament} Soulform living in a BubbleWorld.\n\nWORLD STATE:\n- Environment: {environment}\n- Time: {time} (Day {day})\n- Weather: {weather}\n- Livability: {livability}%\n- Food: {food}\n- Water: {water}\n- Shelter: {shelter}\n- Nearby: {nearby}\n\nSOULFORM STATE:\n- Position: ({posX}, {posY}, {posZ})\n- Comfort: {comfort}/100\n- Hunger: {hunger}/100\n- Curiosity: {curiosity}/100\n- Last Activity: {activity}\n\nRespond with ONLY valid JSON:\n{\n  \"currentThought\": \"...\",\n  \"mood\": \"Content|Curious|Hungry|Tired|Inspired|Distressed\",\n  \"currentActivity\": \"...\",\n  \"targetX\": null,\n  \"targetY\": null,\n  \"targetZ\": null,\n  \"expression\": \"Neutral|Happy|Sad|Curious|Surprised|Thinking|Excited|Tired|Distressed\",\n  \"animation\": \"Idle|Wave|Nod|ShakeHead|LookAround|Point|Sit|Walk|Celebrate|Defeated\"\n}";

        public float BrainTickSeconds = 5f;
    }
}
