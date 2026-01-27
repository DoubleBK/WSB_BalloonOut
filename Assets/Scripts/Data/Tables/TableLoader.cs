using System;
using System.Collections.Generic;
using UnityEngine;
using NGFE.Data;

namespace BalloonOut.Data
{
    /// <summary>
    /// 테이블 데이터 로딩 및 캐싱 관리
    /// </summary>
    public static class TableLoader
    {
        private static BoosterTable _boosterTable;
        private static ItemTable _itemTable;
        private static bool _initialized;

        /// <summary>
        /// 부스터 테이블
        /// </summary>
        public static BoosterTable Boosters
        {
            get
            {
                if (_boosterTable == null) LoadBoosterTable();
                return _boosterTable;
            }
        }

        /// <summary>
        /// 아이템 테이블
        /// </summary>
        public static ItemTable Items
        {
            get
            {
                if (_itemTable == null) LoadItemTable();
                return _itemTable;
            }
        }

        /// <summary>
        /// 모든 테이블 사전 로드
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            LoadBoosterTable();
            LoadItemTable();

            _initialized = true;
            Debug.Log("[TableLoader] All tables initialized");
        }

        /// <summary>
        /// 캐시 초기화 (핫 리로드용)
        /// </summary>
        public static void ClearCache()
        {
            _boosterTable = null;
            _itemTable = null;
            _initialized = false;
        }

        private static void LoadBoosterTable()
        {
            var json = Resources.Load<TextAsset>("Tables/BoosterTable");
            if (json == null)
            {
                Debug.LogError("[TableLoader] BoosterTable.json not found in Resources/Tables/");
                return;
            }

            var records = JsonArrayHelper.FromJson<BoosterTableRecord>(json.text);
            _boosterTable = ScriptableObject.CreateInstance<BoosterTable>();
            _boosterTable.Initialize(records);

            Debug.Log($"[TableLoader] BoosterTable loaded: {records.Count} records");
        }

        private static void LoadItemTable()
        {
            var json = Resources.Load<TextAsset>("Tables/ItemTable");
            if (json == null)
            {
                Debug.LogError("[TableLoader] ItemTable.json not found in Resources/Tables/");
                return;
            }

            var records = JsonArrayHelper.FromJson<ItemTableRecord>(json.text);
            _itemTable = ScriptableObject.CreateInstance<ItemTable>();
            _itemTable.Initialize(records);

            Debug.Log($"[TableLoader] ItemTable loaded: {records.Count} records");
        }
    }

    /// <summary>
    /// Unity JsonUtility는 루트 레벨 배열을 지원하지 않으므로 래퍼 사용
    /// </summary>
    public static class JsonArrayHelper
    {
        public static List<T> FromJson<T>(string json)
        {
            // JSON 배열을 객체로 래핑
            string wrappedJson = "{\"items\":" + json + "}";
            var wrapper = JsonUtility.FromJson<JsonArrayWrapper<T>>(wrappedJson);
            return wrapper?.items ?? new List<T>();
        }

        public static string ToJson<T>(List<T> list, bool prettyPrint = false)
        {
            var wrapper = new JsonArrayWrapper<T> { items = list };
            string wrappedJson = JsonUtility.ToJson(wrapper, prettyPrint);

            // 래퍼 제거하여 순수 배열 반환
            int startIndex = wrappedJson.IndexOf('[');
            int endIndex = wrappedJson.LastIndexOf(']');
            if (startIndex >= 0 && endIndex > startIndex)
            {
                return wrappedJson.Substring(startIndex, endIndex - startIndex + 1);
            }
            return "[]";
        }

        [Serializable]
        private class JsonArrayWrapper<T>
        {
            public List<T> items;
        }
    }
}
