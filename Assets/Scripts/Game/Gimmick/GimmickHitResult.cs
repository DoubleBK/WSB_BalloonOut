namespace BalloonOut.Game.Gimmick
{
    /// <summary>
    /// 기믹의 OnHit 처리 결과
    /// </summary>
    public struct GimmickHitResult
    {
        /// <summary>
        /// 풍선을 터뜨려야 하는지 여부
        /// false면 풍선이 터지지 않음 (Number 기믹 등)
        /// </summary>
        public bool ShouldPop;

        /// <summary>
        /// 화살표를 소비해야 하는지 여부
        /// true면 화살표가 사라짐, false면 튕겨나감
        /// </summary>
        public bool ConsumeArrow;

        /// <summary>
        /// UI에 표시할 피드백 메시지 (선택적)
        /// </summary>
        public string FeedbackMessage;

        /// <summary>
        /// 기본 결과 (일반 팝)
        /// </summary>
        public static GimmickHitResult DefaultPop => new GimmickHitResult
        {
            ShouldPop = true,
            ConsumeArrow = true,
            FeedbackMessage = null
        };

        /// <summary>
        /// 팝 방지 결과 (Number 기믹 등)
        /// </summary>
        public static GimmickHitResult PreventPop(string message = null) => new GimmickHitResult
        {
            ShouldPop = false,
            ConsumeArrow = true,
            FeedbackMessage = message
        };

        /// <summary>
        /// 완전 거부 결과 (화살표도 튕김)
        /// </summary>
        public static GimmickHitResult Reject(string message = null) => new GimmickHitResult
        {
            ShouldPop = false,
            ConsumeArrow = false,
            FeedbackMessage = message
        };
    }
}
