using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Gimmick;

namespace BalloonOut.Game.Balloon
{
    /// <summary>
    /// 풍선 런타임 인스턴스
    /// 풍선의 현재 상태와 활성화된 기믹들을 관리
    /// </summary>
    public class BalloonInstance
    {
        // ========== 기본 속성 ==========

        /// <summary>
        /// 레인 인덱스
        /// </summary>
        public int LaneIndex { get; private set; }

        /// <summary>
        /// 레인 내 위치 (0 = 활성 위치)
        /// </summary>
        public int PositionInLane { get; set; }

        /// <summary>
        /// 풍선 색상
        /// </summary>
        public GameColor Color { get; private set; }

        /// <summary>
        /// 원본 데이터
        /// </summary>
        public BalloonData Data { get; private set; }

        /// <summary>
        /// 비주얼 컴포넌트 참조
        /// </summary>
        public BalloonVisual Visual { get; set; }

        // ========== 기믹 상태 ==========

        /// <summary>
        /// 활성화된 기믹들
        /// </summary>
        private List<ActiveGimmick> _activeGimmicks = new List<ActiveGimmick>();

        /// <summary>
        /// 활성화된 개별 기믹 정보
        /// </summary>
        private class ActiveGimmick
        {
            public string GimmickId;
            public IGimmickBehavior Behavior;
            public GimmickInstanceData Data;
        }

        // ========== 생성자 ==========

        public BalloonInstance(int laneIndex, int positionInLane, BalloonData data)
        {
            LaneIndex = laneIndex;
            PositionInLane = positionInLane;
            Data = data;
            Color = data.GetColor();

            InitializeGimmicks();
        }

        /// <summary>
        /// 기믹 초기화
        /// </summary>
        private void InitializeGimmicks()
        {
            _activeGimmicks.Clear();

            if (Data.gimmicks == null || Data.gimmicks.Count == 0)
            {
                return;
            }

            foreach (var gimmickData in Data.gimmicks)
            {
                var behavior = GimmickRegistry.Instance.GetBehavior(gimmickData.gimmickId);
                if (behavior != null)
                {
                    var activeGimmick = new ActiveGimmick
                    {
                        GimmickId = gimmickData.gimmickId,
                        Behavior = behavior,
                        Data = gimmickData.Clone()  // 런타임 수정을 위해 복제
                    };
                    _activeGimmicks.Add(activeGimmick);

                    // 초기화 콜백
                    behavior.OnInitialize(this, activeGimmick.Data);
                }
                else
                {
                    Debug.LogWarning($"[BalloonInstance] Behavior not found for gimmick: {gimmickData.gimmickId}");
                }
            }
        }

        // ========== 기믹 이벤트 ==========

        /// <summary>
        /// 활성 위치(Queue 맨 앞)에 도달했을 때 호출
        /// </summary>
        public void NotifyBecomeActive()
        {
            foreach (var gimmick in _activeGimmicks)
            {
                gimmick.Behavior.OnBecomeActive(this, gimmick.Data);
            }
        }

        /// <summary>
        /// 화살표가 맞았을 때 호출
        /// </summary>
        /// <param name="arrowColor">화살표 색상</param>
        /// <param name="result">처리 결과</param>
        /// <returns>팝 가능 여부</returns>
        public bool TryHit(GameColor arrowColor, out GimmickHitResult result)
        {
            result = GimmickHitResult.DefaultPop;

            // 기믹이 없으면 기본 팝
            if (_activeGimmicks.Count == 0)
            {
                return true;
            }

            // 모든 기믹 처리
            bool shouldPop = true;
            bool consumeArrow = true;

            foreach (var gimmick in _activeGimmicks)
            {
                if (!gimmick.Behavior.OnHit(this, gimmick.Data, arrowColor, out var gimmickResult))
                {
                    // 기믹이 false를 반환하면 팝 방지
                    shouldPop = false;
                }

                // 결과 병합 (가장 제한적인 것 적용)
                if (!gimmickResult.ShouldPop) shouldPop = false;
                if (!gimmickResult.ConsumeArrow) consumeArrow = false;

                // 피드백 메시지가 있으면 저장
                if (!string.IsNullOrEmpty(gimmickResult.FeedbackMessage))
                {
                    result.FeedbackMessage = gimmickResult.FeedbackMessage;
                }
            }

            result.ShouldPop = shouldPop;
            result.ConsumeArrow = consumeArrow;

            // 비주얼 업데이트
            if (Visual != null)
            {
                RefreshVisual();
            }

            return shouldPop;
        }

        /// <summary>
        /// 풍선이 터질 때 호출
        /// </summary>
        public void NotifyPop()
        {
            foreach (var gimmick in _activeGimmicks)
            {
                gimmick.Behavior.OnPop(this, gimmick.Data);
            }
        }

        /// <summary>
        /// Undo로 복원될 때 호출
        /// </summary>
        public void NotifyRestore(List<GimmickInstanceData> snapshotData)
        {
            if (snapshotData == null) return;

            for (int i = 0; i < _activeGimmicks.Count && i < snapshotData.Count; i++)
            {
                var gimmick = _activeGimmicks[i];
                var snapshot = snapshotData[i];

                if (gimmick.GimmickId == snapshot.gimmickId)
                {
                    gimmick.Behavior.OnRestore(this, gimmick.Data, snapshot);
                }
            }

            // 비주얼 업데이트
            if (Visual != null)
            {
                RefreshVisual();
            }
        }

        // ========== 스냅샷 ==========

        /// <summary>
        /// 현재 상태의 스냅샷 생성
        /// </summary>
        public List<GimmickInstanceData> CreateGimmickSnapshot()
        {
            var snapshot = new List<GimmickInstanceData>();

            foreach (var gimmick in _activeGimmicks)
            {
                var data = gimmick.Behavior.CreateSnapshot(this, gimmick.Data);
                snapshot.Add(data);
            }

            return snapshot;
        }

        // ========== 기믹 조회 ==========

        /// <summary>
        /// 특정 기믹이 있는지 확인
        /// </summary>
        public bool HasGimmick(string gimmickId)
        {
            return _activeGimmicks.Exists(g => g.GimmickId == gimmickId);
        }

        /// <summary>
        /// 특정 기믹 데이터 가져오기
        /// </summary>
        public GimmickInstanceData GetGimmickData(string gimmickId)
        {
            var gimmick = _activeGimmicks.Find(g => g.GimmickId == gimmickId);
            return gimmick?.Data;
        }

        /// <summary>
        /// 기믹 개수
        /// </summary>
        public int GimmickCount => _activeGimmicks.Count;

        // ========== 비주얼 ==========

        /// <summary>
        /// 비주얼 새로고침
        /// </summary>
        public void RefreshVisual()
        {
            if (Visual == null) return;

            foreach (var gimmick in _activeGimmicks)
            {
                gimmick.Behavior.OnRender(this, gimmick.Data, Visual);
            }
        }
    }
}
