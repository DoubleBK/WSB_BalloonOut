using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Data;
using BalloonOut.Game.Balloon;
using BalloonOut.Game.Gimmick.Behaviors;

namespace BalloonOut.Game.Gimmick
{
    /// <summary>
    /// Connected Balloon 그룹 관리자
    /// 연결된 풍선들의 상태를 추적하고 동시 POP을 처리
    /// </summary>
    public class ConnectedBalloonManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        private static ConnectedBalloonManager _instance;
        public static ConnectedBalloonManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ConnectedBalloonManager>();

                    if (_instance == null)
                    {
                        var go = new GameObject("ConnectedBalloonManager");
                        _instance = go.AddComponent<ConnectedBalloonManager>();
                    }
                }
                return _instance;
            }
        }

        // ========== 이벤트 ==========

        /// <summary>
        /// 그룹이 완전히 Marked되어 POP될 때 발생
        /// </summary>
        public event Action<string, List<BalloonInstance>> OnGroupPop;

        // ========== 내부 상태 ==========

        /// <summary>
        /// groupId → 연결된 BalloonInstance 리스트
        /// </summary>
        private Dictionary<string, List<BalloonInstance>> _groups = new Dictionary<string, List<BalloonInstance>>();

        /// <summary>
        /// 현재 POP 처리 중인 그룹 (중복 POP 방지)
        /// </summary>
        private HashSet<string> _poppingGroups = new HashSet<string>();

        // ========== Unity 생명주기 ==========

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 풍선을 그룹에 등록
        /// </summary>
        public void RegisterBalloon(string groupId, BalloonInstance balloon)
        {
            if (string.IsNullOrEmpty(groupId) || balloon == null)
                return;

            if (!_groups.ContainsKey(groupId))
            {
                _groups[groupId] = new List<BalloonInstance>();
            }

            if (!_groups[groupId].Contains(balloon))
            {
                _groups[groupId].Add(balloon);
                Debug.Log($"[ConnectedBalloonManager] Registered balloon to group {groupId}. Total: {_groups[groupId].Count}");
            }
        }

        /// <summary>
        /// 풍선을 그룹에서 제거
        /// </summary>
        public void UnregisterBalloon(string groupId, BalloonInstance balloon)
        {
            if (string.IsNullOrEmpty(groupId) || balloon == null)
                return;

            if (_groups.ContainsKey(groupId))
            {
                _groups[groupId].Remove(balloon);
                Debug.Log($"[ConnectedBalloonManager] Unregistered balloon from group {groupId}. Remaining: {_groups[groupId].Count}");

                // 그룹이 비면 제거
                if (_groups[groupId].Count == 0)
                {
                    _groups.Remove(groupId);
                    _poppingGroups.Remove(groupId);
                }
            }
        }

        /// <summary>
        /// 그룹의 모든 풍선이 Marked 상태인지 확인
        /// </summary>
        public bool IsGroupFullyMarked(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) || !_groups.ContainsKey(groupId))
                return false;

            var balloons = _groups[groupId];
            foreach (var balloon in balloons)
            {
                var connectedData = balloon.GetGimmickData(ConnectedGimmickBehavior.GIMMICK_ID);
                if (connectedData == null || !connectedData.GetParamBool("isMarked"))
                {
                    return false;
                }
            }

            return balloons.Count > 0;
        }

        /// <summary>
        /// 그룹의 Marked된 풍선 수 반환
        /// </summary>
        public int GetMarkedCount(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) || !_groups.ContainsKey(groupId))
                return 0;

            int count = 0;
            foreach (var balloon in _groups[groupId])
            {
                var connectedData = balloon.GetGimmickData(ConnectedGimmickBehavior.GIMMICK_ID);
                if (connectedData != null && connectedData.GetParamBool("isMarked"))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 그룹의 총 풍선 수 반환
        /// </summary>
        public int GetGroupSize(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) || !_groups.ContainsKey(groupId))
                return 0;

            return _groups[groupId].Count;
        }

        /// <summary>
        /// 그룹 전체 POP 트리거
        /// 현재 풍선을 제외한 나머지 풍선들에게 POP 신호 전달
        /// </summary>
        public void TriggerGroupPop(string groupId, BalloonInstance exceptBalloon)
        {
            if (string.IsNullOrEmpty(groupId) || !_groups.ContainsKey(groupId))
                return;

            // 중복 POP 방지
            if (_poppingGroups.Contains(groupId))
                return;

            _poppingGroups.Add(groupId);

            // exceptBalloon 제외한 리스트 생성 (Double Pop 방지)
            // exceptBalloon은 TryPopBalloonWithGimmick()에서 별도로 POP 처리됨
            var balloonsToNotify = new List<BalloonInstance>();
            foreach (var balloon in _groups[groupId])
            {
                if (balloon != exceptBalloon)
                {
                    balloonsToNotify.Add(balloon);
                }
            }

            Debug.Log($"[ConnectedBalloonManager] Triggering group pop for {groupId}. Total: {_groups[groupId].Count}, Notifying: {balloonsToNotify.Count}");

            // 이벤트 발생 (exceptBalloon 제외)
            OnGroupPop?.Invoke(groupId, balloonsToNotify);

            // 각 풍선 로그 (QueueUI에서 실제 POP 처리)
            foreach (var balloon in balloonsToNotify)
            {
                Debug.Log($"[ConnectedBalloonManager] Will pop balloon at lane {balloon.LaneIndex}");
            }
        }

        /// <summary>
        /// 그룹의 모든 풍선 목록 반환
        /// </summary>
        public List<BalloonInstance> GetGroupBalloons(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) || !_groups.ContainsKey(groupId))
                return new List<BalloonInstance>();

            return new List<BalloonInstance>(_groups[groupId]);
        }

        /// <summary>
        /// 모든 그룹 초기화 (레벨 시작 시)
        /// </summary>
        public void Clear()
        {
            _groups.Clear();
            _poppingGroups.Clear();
            Debug.Log("[ConnectedBalloonManager] Cleared all groups");
        }

        /// <summary>
        /// 모든 그룹 ID 반환
        /// </summary>
        public IEnumerable<string> GetAllGroupIds()
        {
            return _groups.Keys;
        }

        /// <summary>
        /// 특정 풍선이 속한 그룹 ID 찾기
        /// </summary>
        public string FindGroupIdForBalloon(BalloonInstance balloon)
        {
            if (balloon == null)
                return null;

            foreach (var kvp in _groups)
            {
                if (kvp.Value.Contains(balloon))
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        /// <summary>
        /// 그룹이 현재 POP 처리 중인지 확인
        /// </summary>
        public bool IsGroupPopping(string groupId)
        {
            return _poppingGroups.Contains(groupId);
        }

        /// <summary>
        /// 그룹 POP 완료 처리 (POP 락 해제)
        /// </summary>
        public void FinishGroupPop(string groupId)
        {
            _poppingGroups.Remove(groupId);
        }
    }
}
