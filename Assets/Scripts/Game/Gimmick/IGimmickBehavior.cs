using BalloonOut.Data;
using BalloonOut.Core;
using BalloonOut.Game.Balloon;

namespace BalloonOut.Game.Gimmick
{
    /// <summary>
    /// 기믹 동작 인터페이스
    /// 새 기믹 추가 시 이 인터페이스를 구현
    /// </summary>
    public interface IGimmickBehavior
    {
        /// <summary>
        /// 기믹 고유 ID (GimmickDefinitionSO.gimmickId와 일치)
        /// </summary>
        string GimmickId { get; }

        /// <summary>
        /// 풍선 생성 시 호출 - 초기 상태 설정
        /// </summary>
        void OnInitialize(BalloonInstance balloon, GimmickInstanceData data);

        /// <summary>
        /// 풍선이 Queue 맨 앞(활성 위치)에 도달했을 때 호출
        /// Surprise 기믹: 색상 공개 트리거
        /// </summary>
        void OnBecomeActive(BalloonInstance balloon, GimmickInstanceData data);

        /// <summary>
        /// 화살표가 풍선에 맞았을 때 호출
        /// </summary>
        /// <param name="balloon">맞은 풍선</param>
        /// <param name="data">기믹 상태 데이터</param>
        /// <param name="arrowColor">화살표 색상</param>
        /// <param name="result">처리 결과 (out)</param>
        /// <returns>true면 다음 기믹 처리 계속, false면 중단</returns>
        bool OnHit(BalloonInstance balloon, GimmickInstanceData data, GameColor arrowColor, out GimmickHitResult result);

        /// <summary>
        /// 풍선이 터지기 직전 호출
        /// </summary>
        void OnPop(BalloonInstance balloon, GimmickInstanceData data);

        /// <summary>
        /// Undo로 풍선 복원 시 호출
        /// </summary>
        void OnRestore(BalloonInstance balloon, GimmickInstanceData data, GimmickInstanceData snapshot);

        /// <summary>
        /// 스냅샷 생성 (Undo용)
        /// </summary>
        GimmickInstanceData CreateSnapshot(BalloonInstance balloon, GimmickInstanceData data);

        /// <summary>
        /// 커스텀 렌더링 훅 (오버레이, 텍스트 등)
        /// </summary>
        void OnRender(BalloonInstance balloon, GimmickInstanceData data, BalloonVisual visual);
    }
}
