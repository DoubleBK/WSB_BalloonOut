using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DUG
{
    /// <summary>
    /// JSON 테이블의 키-값 기반 컬렉션 베이스 클래스
    /// </summary>
    /// <typeparam name="TKey">키 타입 (일반적으로 enum)</typeparam>
    /// <typeparam name="TRecord">레코드 타입</typeparam>
    public abstract class KeyValueTable<TKey, TRecord> : ScriptableObject
        where TRecord : class
    {
        [SerializeField] protected List<TRecord> _records = new List<TRecord>();

        private Dictionary<TKey, TRecord> _lookup;
        private readonly string _keyFieldName;

        protected KeyValueTable(string keyFieldName)
        {
            _keyFieldName = keyFieldName;
        }

        /// <summary>
        /// 레코드 목록으로 테이블 초기화
        /// </summary>
        public void Initialize(List<TRecord> records)
        {
            _records = records ?? new List<TRecord>();
            BuildLookup();
        }

        /// <summary>
        /// 키로 레코드 조회
        /// </summary>
        public TRecord Get(TKey key)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(key, out var record) ? record : default;
        }

        /// <summary>
        /// 모든 레코드 반환
        /// </summary>
        public List<TRecord> GetAll() => new List<TRecord>(_records);

        /// <summary>
        /// 레코드 수 반환
        /// </summary>
        public int Count => _records?.Count ?? 0;

        /// <summary>
        /// 키 존재 여부 확인
        /// </summary>
        public bool ContainsKey(TKey key)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.ContainsKey(key);
        }

        /// <summary>
        /// 조건에 맞는 레코드 찾기
        /// </summary>
        public TRecord Find(Predicate<TRecord> match)
        {
            return _records?.Find(match);
        }

        /// <summary>
        /// 조건에 맞는 모든 레코드 찾기
        /// </summary>
        public List<TRecord> FindAll(Predicate<TRecord> match)
        {
            return _records?.FindAll(match) ?? new List<TRecord>();
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<TKey, TRecord>();

            if (_records == null || _records.Count == 0)
                return;

            var keyField = typeof(TRecord).GetField(_keyFieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (keyField == null)
            {
                Debug.LogError($"[KeyValueTable] Key field '{_keyFieldName}' not found in {typeof(TRecord).Name}");
                return;
            }

            foreach (var record in _records)
            {
                if (record == null) continue;

                var keyValue = keyField.GetValue(record);
                if (keyValue == null) continue;

                var key = (TKey)keyValue;
                if (_lookup.ContainsKey(key))
                {
                    Debug.LogWarning($"[KeyValueTable] Duplicate key '{key}' in {GetType().Name}");
                    continue;
                }

                _lookup[key] = record;
            }
        }

        /// <summary>
        /// 캐시 초기화 (데이터 변경 후 호출)
        /// </summary>
        protected void InvalidateCache()
        {
            _lookup = null;
        }
    }

    /// <summary>
    /// ScriptableObject에 바이너리 직렬화 선호 표시
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PreferBinarySerializationAttribute : Attribute
    {
    }
}
