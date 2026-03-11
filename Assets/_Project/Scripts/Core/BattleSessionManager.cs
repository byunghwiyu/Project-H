using System.Collections.Generic;
using System.Linq;
using ProjectH.Battle;
using ProjectH.Data.Tables;
using UnityEngine;

namespace ProjectH.Core
{
    public sealed class BattleSessionManager : MonoBehaviour
    {
        private static BattleSessionManager instance;
        public static BattleSessionManager Instance => instance;

        private readonly List<BattleSession> activeSessions = new();
        private readonly List<BattleSession> completedSessions = new();
        private GameCsvTables tables;
        private TalentCatalog talentCatalog;

        public IReadOnlyList<BattleSession> ActiveSessions => activeSessions;
        public IReadOnlyList<BattleSession> CompletedSessions => completedSessions;

        /// <summary>Battle 씬 진입 시 어떤 세션을 볼 것인지 지정</summary>
        public string ViewingLocationId { get; set; }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("BattleSessionManager");
            go.AddComponent<BattleSessionManager>();
        }

        public bool TryStartSession(string locationId, string locationName, List<string> mercenaryIds, out string error)
        {
            error = string.Empty;

            if (!EnsureTables(out error)) return false;

            if (activeSessions.Any(s => s.LocationId == locationId))
            {
                error = "이미 해당 현장에 파견 중입니다.";
                return false;
            }

            foreach (var mercId in mercenaryIds)
            {
                if (IsMercenaryDispatched(mercId))
                {
                    error = "이미 파견 중인 용병이 포함되어 있습니다.";
                    return false;
                }
            }

            var session = new BattleSession(locationId, locationName, mercenaryIds, tables, talentCatalog);
            if (!session.TryInitialize(out error))
                return false;

            activeSessions.Add(session);
            Debug.Log($"[session] Started session {session.SessionId} at {locationId} with {mercenaryIds.Count} mercs");
            return true;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            for (var i = activeSessions.Count - 1; i >= 0; i--)
            {
                var session = activeSessions[i];
                if (!session.Tick(dt))
                {
                    activeSessions.RemoveAt(i);
                    completedSessions.Add(session);
                    Debug.Log($"[session] Session {session.SessionId} completed: {session.CompletionReason}");
                }
            }
        }

        public BattleSession GetActiveSession(string locationId)
        {
            return activeSessions.FirstOrDefault(s => s.LocationId == locationId);
        }

        public bool IsMercenaryDispatched(string mercenaryId)
        {
            return activeSessions.Any(s => s.MercenaryIds.Contains(mercenaryId));
        }

        public void AbortSession(string locationId)
        {
            var session = activeSessions.FirstOrDefault(s => s.LocationId == locationId);
            if (session == null) return;
            session.Abort();
            activeSessions.Remove(session);
            completedSessions.Add(session);
            Debug.Log($"[session] Session {session.SessionId} aborted (retreat)");
        }

        public void DismissCompleted(string sessionId)
        {
            completedSessions.RemoveAll(s => s.SessionId == sessionId);
        }

        private bool EnsureTables(out string error)
        {
            error = string.Empty;
            if (tables != null && talentCatalog != null) return true;

            if (!GameCsvTables.TryLoad(out tables, out error)) return false;
            if (!TalentCatalog.TryLoad(out talentCatalog, out var tErr))
            {
                error = tErr;
                return false;
            }

            return true;
        }
    }
}
