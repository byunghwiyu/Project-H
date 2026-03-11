using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Battle;
using ProjectH.Data;
using ProjectH.Data.Tables;
using ProjectH.UI.Battle;
using UnityEngine;

namespace ProjectH.Core
{
    /// <summary>
    /// Battle 씬의 뷰어. BattleSessionManager 가 돌리는 세션에 연결해
    /// 비주얼(스프라이트·애니메이션)만 표시한다.
    /// 뒤로가기하면 Dungeon 씬으로 이동하고, 세션은 백그라운드에서 계속 진행된다.
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> runtimeViews = new();
        private BattleSession session;
        private BattleEventBus eventBus;
        private BattleRoster roster;
        private BattleHUD hud;
        private bool isActionAnimating;
        private BattleSetupRow setup;

        // 카메라 뷰포트 영역 (정규화 좌표, 하단-좌측 원점)
        private const float VpX = 0.02f;
        private const float VpY = 0.42f;
        private const float VpW = 0.96f;
        private const float VpH = 0.54f;

        private static BattleBootstrap instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            RestoreCameraViewport();
            if (instance == this) instance = null;
        }

        private void Start()
        {
            var manager = BattleSessionManager.Instance;
            if (manager == null || string.IsNullOrWhiteSpace(manager.ViewingLocationId))
            {
                Debug.LogError("[battle] No ViewingLocationId set. Returning to Dungeon.");
                SceneNavigator.TryLoad("Dungeon");
                return;
            }

            session = manager.GetActiveSession(manager.ViewingLocationId);
            if (session == null)
            {
                Debug.LogError($"[battle] No active session for {manager.ViewingLocationId}");
                SceneNavigator.TryLoad("Dungeon");
                return;
            }

            eventBus = session.EventBus;
            roster = session.Roster;

            // battle_setup.csv 에서 위치 정보 가져오기
            if (!session.Tables.TryGetBattleSetup("", out setup, out _))
                setup = default;

            // 카메라 뷰포트를 화면 중앙 영역으로 제한
            SetupCameraViewport();

            // 현재 살아있는 아군 비주얼 생성
            foreach (var unit in roster.Allies)
            {
                if (!unit.IsAlive) continue;
                CreateViewForUnit(unit, BattleTeam.Ally);
            }

            // 현재 살아있는 적군 비주얼 생성
            foreach (var unit in roster.Enemies)
            {
                if (!unit.IsAlive) continue;
                CreateViewForUnit(unit, BattleTeam.Enemy);
            }

            // HUD 생성
            hud = new GameObject("BattleHUD").AddComponent<BattleHUD>();
            hud.Setup(
                eventBus,
                roster,
                id => runtimeViews.TryGetValue(id, out var go) && go != null ? go.transform.position : Vector3.zero,
                ResolveUnitName,
                session.LocationName,
                OnRetreat,
                () => SceneNavigator.TryLoad("Dungeon")
            );
            hud.OnWaveCleared(session.WavesClearedCount);

            // 이벤트 구독
            eventBus.OnPublished += OnBattleEvent;
            session.OnEnemiesSpawned += OnEnemiesSpawned;
            session.OnSessionCompleted += OnSessionCompleted;
            session.OnWaveCleared += OnWaveCleared;
        }

        private void Update()
        {
            if (session == null) return;
            hud?.SetProgress(session.Elapsed, session.TurnIntervalSec);
        }

        // ── 카메라 뷰포트 ────────────────────────────────────────

        private void SetupCameraViewport()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.rect = new Rect(VpX, VpY, VpW, VpH);
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.10f, 1f);
        }

        private static void RestoreCameraViewport()
        {
            var cam = Camera.main;
            if (cam != null) cam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        // ── 유닛 이름 해석 ──────────────────────────────────────

        private string ResolveUnitName(string runtimeId)
        {
            if (roster == null || !roster.TryGetUnit(runtimeId, out var unit))
                return runtimeId;

            if (session.Tables.TryGetCombatUnit(unit.TemplateId, out var row))
            {
                if (session.Tables.TryGetCharacterRow(unit.TemplateId, out var charRow)
                    && !string.IsNullOrWhiteSpace(charRow.Name))
                    return charRow.Name;
                if (!string.IsNullOrWhiteSpace(row.Name))
                    return row.Name;
            }
            return unit.TemplateId;
        }

        // ── 버튼 콜백 ──────────────────────────────────────────

        private void OnRetreat()
        {
            var manager = BattleSessionManager.Instance;
            if (manager != null && session != null)
                manager.AbortSession(session.LocationId);
            SceneNavigator.TryLoad("Dungeon");
        }

        // ── 세션 이벤트 ──────────────────────────────────────────

        private void OnWaveCleared(int clearedCount)
        {
            var killed = FindDeadEnemyViews();
            CleanupViews(killed);
            hud?.OnWaveCleared(clearedCount);
        }

        private void OnEnemiesSpawned(List<BattleUnit> enemies)
        {
            foreach (var unit in enemies)
                CreateViewForUnit(unit, BattleTeam.Enemy);
        }

        private void OnSessionCompleted(string reason)
        {
            if (reason == "victory")
                hud?.ShowVictory(
                    session.WavesClearedCount,
                    session.RewardBudget,
                    session.CollectedDrops,
                    new List<string>(),
                    () => SceneNavigator.TryLoad("Dungeon"));
            else
                hud?.ShowGameOver(() => SceneNavigator.TryLoad("Dungeon"));
        }

        // ── 비주얼 이벤트 ────────────────────────────────────────

        private void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.TurnStarted:
                    if (runtimeViews.TryGetValue(e.SourceRuntimeId, out var actorGo) && actorGo != null)
                    {
                        var actorView = actorGo.GetComponent<UnitView>();
                        UnitView targetView = null;
                        if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var targetGo) && targetGo != null)
                            targetView = targetGo.GetComponent<UnitView>();

                        if (actorView != null && targetView != null)
                            StartCoroutine(PlayAttackMotion(actorView, targetView));
                        else
                            actorView?.OnTurnStarted();
                    }
                    RefreshUnitView(e.SourceRuntimeId);
                    break;

                case BattleEventType.Damaged:
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var damagedGo) && damagedGo != null)
                        damagedGo.GetComponent<UnitView>()?.OnDamaged(e.Value);
                    RefreshUnitView(e.TargetRuntimeId);
                    break;

                case BattleEventType.Healed:
                    RefreshUnitView(e.TargetRuntimeId);
                    break;

                case BattleEventType.Died:
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var deadGo) && deadGo != null)
                    {
                        var deadView = deadGo.GetComponent<UnitView>();
                        if (deadView != null) deadView.OnDied(); else deadGo.SetActive(false);
                    }
                    RefreshUnitView(e.TargetRuntimeId);
                    break;

                case BattleEventType.ThornsReflected:
                case BattleEventType.CounterAttacked:
                    if (runtimeViews.TryGetValue(e.TargetRuntimeId, out var hitGo) && hitGo != null)
                        hitGo.GetComponent<UnitView>()?.OnDamaged(e.Value);
                    RefreshUnitView(e.TargetRuntimeId);
                    break;

                case BattleEventType.ActiveSkillUsed:
                    RefreshUnitView(e.SourceRuntimeId);
                    break;
            }
        }

        // ── 비주얼 생성/정리 ─────────────────────────────────────

        private void CreateViewForUnit(BattleUnit unit, BattleTeam team)
        {
            if (runtimeViews.ContainsKey(unit.RuntimeUnitId)) return;

            string prefabPath = null;
            if (session.Tables.TryGetCombatUnit(unit.TemplateId, out var row))
                prefabPath = row.PrefabResourcePath;

            var pos = ResolvePosition(unit, team);
            var go = CreateUnitView(prefabPath, team, unit.RuntimeUnitId, pos);
            runtimeViews[unit.RuntimeUnitId] = go;
            go.GetComponent<UnitView>()?.UpdateStats(unit.Hp, unit.Stat.MaxHp, unit.Mana, unit.Stat.MaxMana);
        }

        private Vector2 ResolvePosition(BattleUnit unit, BattleTeam team)
        {
            var side = team == BattleTeam.Ally ? "ally" : "enemy";
            var units = team == BattleTeam.Ally ? roster.Allies : roster.Enemies;
            var index = 0;
            for (var i = 0; i < units.Count; i++)
            {
                if (units[i].RuntimeUnitId == unit.RuntimeUnitId) { index = i; break; }
            }

            var slots = session.Tables.GetBattleSlots(session.LocationId, side);
            if (slots.Count > 0)
            {
                var slot = slots[index % slots.Count];
                return ViewportToWorld(slot.NormX, slot.NormY);
            }

            // 폴백: battle_setup.csv 선형 배치
            var startX = side == "ally" ? setup.AllyStartX : setup.EnemyStartX;
            var startY = side == "ally" ? setup.AllyStartY : setup.EnemyStartY;
            var spacX = side == "ally" ? setup.AllySpacingX : setup.EnemySpacingX;
            return new Vector2(startX + spacX * index, startY);
        }

        private List<string> FindDeadEnemyViews()
        {
            var dead = new List<string>();
            foreach (var kv in runtimeViews)
            {
                if (!roster.TryGetUnit(kv.Key, out var unit)) { dead.Add(kv.Key); continue; }
                if (!unit.IsAlive && unit.Team == BattleTeam.Enemy) dead.Add(kv.Key);
            }
            return dead;
        }

        private void CleanupViews(List<string> ids)
        {
            foreach (var id in ids)
            {
                if (runtimeViews.TryGetValue(id, out var go) && go != null)
                    Destroy(go);
                runtimeViews.Remove(id);
            }
        }

        private void RefreshUnitView(string runtimeId)
        {
            if (!roster.TryGetUnit(runtimeId, out var unit)) return;
            if (!runtimeViews.TryGetValue(runtimeId, out var go) || go == null) return;
            go.GetComponent<UnitView>()?.UpdateStats(unit.Hp, unit.Stat.MaxHp, unit.Mana, unit.Stat.MaxMana);
        }

        private void Unsubscribe()
        {
            if (eventBus != null) eventBus.OnPublished -= OnBattleEvent;
            if (session != null)
            {
                session.OnEnemiesSpawned -= OnEnemiesSpawned;
                session.OnSessionCompleted -= OnSessionCompleted;
                session.OnWaveCleared -= OnWaveCleared;
            }

            foreach (var kv in runtimeViews)
            {
                if (kv.Value != null) Destroy(kv.Value);
            }
            runtimeViews.Clear();
        }

        // ── 유닛 뷰 생성 ────────────────────────────────────────

        private static GameObject CreateUnitView(string prefabPath, BattleTeam team, string runtimeUnitId, Vector2 pos)
        {
            GameObject go = null;
            if (!string.IsNullOrWhiteSpace(prefabPath))
            {
                var prefab = Resources.Load<GameObject>(prefabPath);
                if (prefab != null)
                {
                    go = Instantiate(prefab);
                    go.name = runtimeUnitId;
                }
            }

            if (go == null)
            {
                go = new GameObject(runtimeUnitId);
                go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            if (sr.sprite == null)
            {
                sr.sprite = BuildFallbackSprite();
                sr.color = team == BattleTeam.Ally ? new Color(0.25f, 0.75f, 1f, 1f) : new Color(1f, 0.4f, 0.4f, 1f);
            }
            sr.sortingOrder = 10;

            if (go.GetComponent<Animator>() == null) go.AddComponent<Animator>();

            var view = go.GetComponent<UnitView>();
            if (view == null) view = go.AddComponent<UnitView>();
            view.Bind(runtimeUnitId, "", team == BattleTeam.Enemy);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            return go;
        }

        private static Sprite BuildFallbackSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Vector2 ViewportToWorld(float normX, float normY)
        {
            if (Camera.main == null)
                return new Vector2(normX * 10f - 5f, normY * 10f - 5f);
            var depth = Mathf.Abs(Camera.main.transform.position.z);
            var worldPos = Camera.main.ViewportToWorldPoint(new Vector3(normX, normY, depth));
            return new Vector2(worldPos.x, worldPos.y);
        }

        // ── 공격 모션 ────────────────────────────────────────────

        private IEnumerator PlayAttackMotion(UnitView actorView, UnitView targetView)
        {
            if (actorView == null || targetView == null) yield break;

            isActionAnimating = true;
            var t = actorView.transform;
            var start = t.position;
            var approach = targetView.GetApproachPointForAttacker(actorView, actorView.ApproachGap, actorView.ApproachYOffset);
            yield return MoveTo(t, approach, actorView.ApproachSpeed);
            actorView.OnTurnStarted();
            if (actorView.AttackHoldSec > 0f)
                yield return new WaitForSeconds(actorView.AttackHoldSec);

            if (actorView != null && actorView.gameObject.activeInHierarchy)
                yield return MoveTo(t, start, actorView.RetreatSpeed);

            isActionAnimating = false;
        }

        private static IEnumerator MoveTo(Transform t, Vector3 target, float speed)
        {
            if (t == null) yield break;
            var moveSpeed = Mathf.Max(0.01f, speed);
            while ((t.position - target).sqrMagnitude > 0.0004f)
            {
                t.position = Vector3.MoveTowards(t.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }
            t.position = target;
        }
    }
}
