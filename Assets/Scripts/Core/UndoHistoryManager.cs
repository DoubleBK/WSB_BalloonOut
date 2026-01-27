using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Core
{
    /// <summary>
    /// Undo 히스토리 관리자 - 게임 상태 스냅샷 스택 관리
    /// </summary>
    public class UndoHistoryManager
    {
        private const int MAX_HISTORY = 50;

        private Stack<GameStateSnapshot> _history;

        public event Action OnHistoryChanged;

        /// <summary>
        /// 히스토리 존재 여부
        /// </summary>
        public bool HasHistory => _history != null && _history.Count > 0;

        /// <summary>
        /// 히스토리 개수
        /// </summary>
        public int HistoryCount => _history?.Count ?? 0;

        public UndoHistoryManager()
        {
            _history = new Stack<GameStateSnapshot>();
        }

        /// <summary>
        /// 게임 상태 기록
        /// </summary>
        public void RecordState(GameStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[UndoHistoryManager] Attempted to record null snapshot");
                return;
            }

            // 최대 히스토리 제한
            if (_history.Count >= MAX_HISTORY)
            {
                // 가장 오래된 항목 제거를 위해 임시 스택 사용
                var tempList = new List<GameStateSnapshot>(_history);
                tempList.RemoveAt(tempList.Count - 1); // 가장 오래된 것 제거
                _history.Clear();
                for (int i = tempList.Count - 1; i >= 0; i--)
                {
                    _history.Push(tempList[i]);
                }
            }

            _history.Push(snapshot);
            Debug.Log($"[UndoHistoryManager] State recorded, history count: {_history.Count}");
            OnHistoryChanged?.Invoke();
        }

        /// <summary>
        /// 마지막 상태 가져오기 (제거)
        /// </summary>
        public GameStateSnapshot PopState()
        {
            if (!HasHistory)
            {
                Debug.LogWarning("[UndoHistoryManager] No history to pop");
                return null;
            }

            var snapshot = _history.Pop();
            Debug.Log($"[UndoHistoryManager] State popped, remaining history: {_history.Count}");
            OnHistoryChanged?.Invoke();
            return snapshot;
        }

        /// <summary>
        /// 마지막 상태 확인 (제거하지 않음)
        /// </summary>
        public GameStateSnapshot PeekState()
        {
            return HasHistory ? _history.Peek() : null;
        }

        /// <summary>
        /// 히스토리 초기화
        /// </summary>
        public void ClearHistory()
        {
            _history.Clear();
            Debug.Log("[UndoHistoryManager] History cleared");
            OnHistoryChanged?.Invoke();
        }

        /// <summary>
        /// 화살표 탈출 이벤트 기록
        /// </summary>
        public void RecordArrowEscape(
            Game.Arrow.ArrowController arrow,
            BalloonSnapshot poppedBalloon = null)
        {
            if (arrow == null)
            {
                Debug.LogWarning("[UndoHistoryManager] Cannot record null arrow");
                return;
            }

            var arrowSnapshot = ArrowSnapshot.CreateFromController(arrow);
            var snapshot = GameStateSnapshot.CreateFromEscape(arrowSnapshot, poppedBalloon);
            RecordState(snapshot);
        }

        /// <summary>
        /// 화살표 탈출 이벤트 기록 (스냅샷 버전)
        /// HomingArrow 사용 시 화살표가 이미 파괴된 경우를 위한 오버로드
        /// </summary>
        public void RecordArrowEscapeFromSnapshot(
            ArrowSnapshot arrowSnapshot,
            BalloonSnapshot poppedBalloon = null)
        {
            if (arrowSnapshot == null)
            {
                Debug.LogWarning("[UndoHistoryManager] Cannot record null arrow snapshot");
                return;
            }

            var snapshot = GameStateSnapshot.CreateFromEscape(arrowSnapshot, poppedBalloon);
            RecordState(snapshot);
        }
    }
}
