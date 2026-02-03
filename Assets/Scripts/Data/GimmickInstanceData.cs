using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Data
{
    /// <summary>
    /// 기믹 인스턴스 데이터 (직렬화 가능)
    /// 풍선에 적용된 개별 기믹의 상태를 저장
    /// ISerializationCallbackReceiver로 딕셔너리-리스트 동기화 타이밍 보장
    /// </summary>
    [Serializable]
    public class GimmickInstanceData : ISerializationCallbackReceiver
    {
        /// <summary>
        /// 기믹 고유 ID (예: "surprise", "number")
        /// </summary>
        public string gimmickId;

        /// <summary>
        /// 기믹 파라미터 키 목록 (Unity 직렬화용)
        /// </summary>
        [SerializeField]
        private List<string> _paramKeys = new List<string>();

        /// <summary>
        /// 기믹 파라미터 값 목록 (Unity 직렬화용)
        /// </summary>
        [SerializeField]
        private List<string> _paramValues = new List<string>();

        /// <summary>
        /// 파라미터 딕셔너리 (런타임용, NonSerialized)
        /// </summary>
        [NonSerialized]
        private Dictionary<string, string> _parameters;

        /// <summary>
        /// 파라미터 접근자
        /// </summary>
        public Dictionary<string, string> Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    RebuildDictionary();
                }
                return _parameters;
            }
        }

        public GimmickInstanceData()
        {
            _paramKeys = new List<string>();
            _paramValues = new List<string>();
            _parameters = new Dictionary<string, string>();
        }

        public GimmickInstanceData(string gimmickId)
        {
            this.gimmickId = gimmickId;
            _paramKeys = new List<string>();
            _paramValues = new List<string>();
            _parameters = new Dictionary<string, string>();
        }

        /// <summary>
        /// 딕셔너리 재구축 (역직렬화 후 호출)
        /// </summary>
        private void RebuildDictionary()
        {
            _parameters = new Dictionary<string, string>();
            if (_paramKeys != null && _paramValues != null)
            {
                int count = Mathf.Min(_paramKeys.Count, _paramValues.Count);
                for (int i = 0; i < count; i++)
                {
                    if (!string.IsNullOrEmpty(_paramKeys[i]))
                    {
                        _parameters[_paramKeys[i]] = _paramValues[i];
                    }
                }
            }
        }

        /// <summary>
        /// 직렬화 전 리스트 동기화
        /// </summary>
        public void SyncToLists()
        {
            _paramKeys.Clear();
            _paramValues.Clear();

            if (_parameters != null)
            {
                foreach (var kvp in _parameters)
                {
                    _paramKeys.Add(kvp.Key);
                    _paramValues.Add(kvp.Value);
                }
            }
        }

        /// <summary>
        /// 파라미터 설정
        /// </summary>
        public void SetParam(string key, string value)
        {
            Parameters[key] = value;
            SyncToLists();
        }

        /// <summary>
        /// 파라미터 설정 (int)
        /// </summary>
        public void SetParam(string key, int value)
        {
            SetParam(key, value.ToString());
        }

        /// <summary>
        /// 파라미터 설정 (bool)
        /// </summary>
        public void SetParam(string key, bool value)
        {
            SetParam(key, value.ToString());
        }

        /// <summary>
        /// 파라미터 가져오기
        /// </summary>
        public string GetParam(string key, string defaultValue = "")
        {
            return Parameters.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 파라미터 가져오기 (int)
        /// </summary>
        public int GetParamInt(string key, int defaultValue = 0)
        {
            if (Parameters.TryGetValue(key, out var value) && int.TryParse(value, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// 파라미터 가져오기 (bool)
        /// </summary>
        public bool GetParamBool(string key, bool defaultValue = false)
        {
            if (Parameters.TryGetValue(key, out var value) && bool.TryParse(value, out var result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// 복제
        /// </summary>
        public GimmickInstanceData Clone()
        {
            var clone = new GimmickInstanceData(gimmickId);
            foreach (var kvp in Parameters)
            {
                clone.Parameters[kvp.Key] = kvp.Value;
            }
            clone.SyncToLists();
            return clone;
        }

        #region ISerializationCallbackReceiver

        /// <summary>
        /// Unity 직렬화 직전 호출 - 딕셔너리 → 리스트 동기화
        /// </summary>
        public void OnBeforeSerialize()
        {
            // _parameters가 초기화되어 있으면 리스트로 동기화
            if (_parameters != null)
            {
                SyncToLists();
            }
        }

        /// <summary>
        /// Unity 역직렬화 직후 호출 - 리스트 → 딕셔너리 재구축
        /// </summary>
        public void OnAfterDeserialize()
        {
            // 리스트에서 딕셔너리 재구축
            RebuildDictionary();
        }

        #endregion
    }
}
