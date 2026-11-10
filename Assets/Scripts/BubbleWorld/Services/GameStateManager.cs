using System;
using System.Collections;
using UnityEngine;

namespace BubbleWorld
{
    public sealed class GameStateManager
    {
        private static GameStateManager instance;
        public static GameStateManager Instance => instance ??= new GameStateManager();

        public IWorldDataProvider Provider { get; private set; }
        public ServerSyncService SyncService { get; private set; }

        // Authoritative snapshot pulled from the backend at startup (null = none
        // stored or backend unreachable).
        public ServerStateData PendingServerState { get; private set; }

        public event Action OnStateChanged;

        private GameStateManager()
        {
            SyncService = new ServerSyncService();
        }

        public void InitializeLocal(BubbleModel bubble, WeatherManager weather,
            EntityManager entityManager, SoulformModel soulform, ArchitectPoints ap)
        {
            Provider = new LocalWorldDataProvider(bubble, weather, entityManager, soulform, ap);
            OnStateChanged?.Invoke();
        }

        public void InitializeServer(IWorldDataProvider serverProvider)
        {
            Provider = serverProvider;
            OnStateChanged?.Invoke();
        }

        // Backend is the single source of truth for world state: pull the
        // authoritative snapshot at startup. Async (backend I/O) — result is
        // reported via onDone (null = nothing stored / backend unreachable)
        // and kept in PendingServerState.
        public IEnumerator CheckStartup(Action<ServerStateData> onDone = null)
        {
            ServerStateData pulled = null;
            yield return SyncService.PullLatestState(state => pulled = state);
            PendingServerState = pulled;
            if (pulled != null) ApplyServerState(pulled);
            onDone?.Invoke(pulled);
        }

        private void ApplyServerState(ServerStateData state)
        {
            Debug.Log("[GameState] Server snapshot available: world '" + state.WorldId +
                "' v" + state.Version + " (" + (state.SerializedState?.Length ?? 0) + " chars)");
            OnStateChanged?.Invoke();
        }

        public void NotifyStateChanged()
        {
            OnStateChanged?.Invoke();
        }
    }
}
