using System;
using System.Collections.Generic;
using UnityEngine;
using NGFE.Data;
using BalloonOut.Data;

namespace BalloonOut.Core
{
    /// <summary>
    /// 부스터 인벤토리 - 수량 관리 및 영속성
    /// </summary>
    public class BoosterInventory
    {
        private const string KEY_PREFIX = "Booster_";
        private const string KEY_INITIALIZED = "Booster_Initialized";

        private static BoosterInventory _instance;
        public static BoosterInventory Instance => _instance ??= new BoosterInventory();

        private Dictionary<ITEM_TYPE, int> _quantities = new Dictionary<ITEM_TYPE, int>();

        public event Action<ITEM_TYPE, int> OnQuantityChanged;

        private BoosterInventory()
        {
            Initialize();
        }

        /// <summary>
        /// 인벤토리 초기화 (첫 실행 시 초기 수량 지급)
        /// </summary>
        private void Initialize()
        {
            // 첫 실행 여부 확인
            bool isFirstRun = PlayerPrefs.GetInt(KEY_INITIALIZED, 0) == 0;

            if (isFirstRun)
            {
                InitializeDefaultQuantities();
                PlayerPrefs.SetInt(KEY_INITIALIZED, 1);
                PlayerPrefs.Save();
                Debug.Log("[BoosterInventory] First run - initialized with default quantities");
            }
            else
            {
                LoadQuantities();
            }
        }

        /// <summary>
        /// 테이블 기반 초기 수량 지급
        /// </summary>
        private void InitializeDefaultQuantities()
        {
            var boosterRecords = TableLoader.Boosters?.GetAll();
            if (boosterRecords == null || boosterRecords.Count == 0)
            {
                // 테이블 로드 실패 시 기본값 사용
                Debug.LogWarning("[BoosterInventory] BoosterTable not loaded, using fallback values");
                SetQuantityInternal(ITEM_TYPE.UNDO, 10);
                SetQuantityInternal(ITEM_TYPE.HINT, 10);
                SetQuantityInternal(ITEM_TYPE.TRIPLEARROW, 10);
                SetQuantityInternal(ITEM_TYPE.DARTARROW, 10);
                return;
            }

            foreach (var record in boosterRecords)
            {
                SetQuantityInternal(record.ItemType, record.InitAmount);
                Debug.Log($"[BoosterInventory] {record.ItemType}: {record.InitAmount}");
            }
        }

        /// <summary>
        /// 저장된 수량 로드
        /// </summary>
        private void LoadQuantities()
        {
            _quantities.Clear();

            // 모든 부스터 타입 로드
            foreach (ITEM_TYPE type in GetBoosterTypes())
            {
                string key = KEY_PREFIX + type.ToString();
                int quantity = PlayerPrefs.GetInt(key, 0);
                _quantities[type] = quantity;
            }

            Debug.Log("[BoosterInventory] Quantities loaded from PlayerPrefs");
        }

        /// <summary>
        /// 수량 조회
        /// </summary>
        public int GetQuantity(ITEM_TYPE boosterType)
        {
            return _quantities.TryGetValue(boosterType, out int qty) ? qty : 0;
        }

        /// <summary>
        /// 부스터 사용 (수량 1 감소)
        /// </summary>
        /// <returns>사용 성공 여부</returns>
        public bool TryUse(ITEM_TYPE boosterType)
        {
            int currentQty = GetQuantity(boosterType);
            if (currentQty <= 0)
            {
                Debug.Log($"[BoosterInventory] Cannot use {boosterType}: quantity is 0");
                return false;
            }

            SetQuantityInternal(boosterType, currentQty - 1);
            SaveQuantity(boosterType);

            Debug.Log($"[BoosterInventory] Used {boosterType}, remaining: {currentQty - 1}");
            OnQuantityChanged?.Invoke(boosterType, currentQty - 1);

            return true;
        }

        /// <summary>
        /// 부스터 추가
        /// </summary>
        public void Add(ITEM_TYPE boosterType, int amount)
        {
            if (amount <= 0) return;

            int currentQty = GetQuantity(boosterType);
            int newQty = currentQty + amount;

            SetQuantityInternal(boosterType, newQty);
            SaveQuantity(boosterType);

            Debug.Log($"[BoosterInventory] Added {amount} {boosterType}, total: {newQty}");
            OnQuantityChanged?.Invoke(boosterType, newQty);
        }

        /// <summary>
        /// 수량 직접 설정
        /// </summary>
        public void SetQuantity(ITEM_TYPE boosterType, int quantity)
        {
            quantity = Mathf.Max(0, quantity);
            SetQuantityInternal(boosterType, quantity);
            SaveQuantity(boosterType);
            OnQuantityChanged?.Invoke(boosterType, quantity);
        }

        /// <summary>
        /// 부스터가 사용 가능한지 확인
        /// </summary>
        public bool CanUse(ITEM_TYPE boosterType)
        {
            return GetQuantity(boosterType) > 0;
        }

        /// <summary>
        /// 스테이지 기준 부스터 해금 여부 확인
        /// </summary>
        public bool IsUnlocked(ITEM_TYPE boosterType, int currentStage)
        {
            var record = TableLoader.Boosters?.Get(boosterType);
            if (record == null) return true; // 테이블 없으면 해금으로 처리

            return currentStage >= record.UnlockStage;
        }

        /// <summary>
        /// 부스터 구매 가격 조회
        /// </summary>
        public int GetPrice(ITEM_TYPE boosterType)
        {
            var record = TableLoader.Items?.Get(boosterType);
            return record?.PRICE ?? 500; // 기본값 500
        }

        /// <summary>
        /// 부스터 구매 시 지급량 조회
        /// </summary>
        public int GetBuyGivenQuantity(ITEM_TYPE boosterType)
        {
            var record = TableLoader.Boosters?.Get(boosterType);
            return record?.BuyGivenQty ?? 1; // 기본값 1
        }

        /// <summary>
        /// 모든 데이터 초기화 (테스트/디버그용)
        /// </summary>
        public void ResetAll()
        {
            foreach (ITEM_TYPE type in GetBoosterTypes())
            {
                PlayerPrefs.DeleteKey(KEY_PREFIX + type.ToString());
            }
            PlayerPrefs.DeleteKey(KEY_INITIALIZED);
            PlayerPrefs.Save();

            _quantities.Clear();
            Initialize();

            Debug.Log("[BoosterInventory] All data reset");
        }

        private void SetQuantityInternal(ITEM_TYPE type, int quantity)
        {
            _quantities[type] = quantity;
        }

        private void SaveQuantity(ITEM_TYPE type)
        {
            string key = KEY_PREFIX + type.ToString();
            PlayerPrefs.SetInt(key, _quantities[type]);
            PlayerPrefs.Save();
        }

        private static IEnumerable<ITEM_TYPE> GetBoosterTypes()
        {
            yield return ITEM_TYPE.UNDO;
            yield return ITEM_TYPE.HINT;
            yield return ITEM_TYPE.TRIPLEARROW;
            yield return ITEM_TYPE.DARTARROW;
        }
    }
}
