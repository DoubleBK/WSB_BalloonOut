using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using BalloonOut.Game.Gimmick;

namespace BalloonOut.Data
{
    /// <summary>
    /// 기믹 정의 ScriptableObject
    /// 새 기믹 타입을 정의할 때 이 에셋 생성
    /// </summary>
    [CreateAssetMenu(fileName = "Gimmick_New", menuName = "BalloonOut/Gimmick Definition")]
    public class GimmickDefinitionSO : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("기믹 고유 ID (예: surprise, number)")]
        public string gimmickId;

        [Tooltip("표시 이름")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("기믹 설명")]
        public string description;

        [Header("Visual - Overlay")]
        [Tooltip("오버레이 스프라이트 (null = 없음)")]
        public Sprite overlaySprite;

        [Tooltip("오버레이 색상")]
        public Color overlayColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);

        [Header("Visual - Text")]
        [Tooltip("텍스트 표시 여부")]
        public bool showText;

        [Tooltip("기본 텍스트 (예: ?)")]
        public string defaultText;

        [Tooltip("TextMeshPro 폰트")]
        public TMP_FontAsset font;

        [Tooltip("텍스트 크기")]
        public float fontSize = 36f;

        [Tooltip("텍스트 색상")]
        public Color textColor = Color.white;

        [Tooltip("텍스트 아웃라인 두께")]
        public float textOutlineWidth = 0.2f;

        [Tooltip("텍스트 아웃라인 색상")]
        public Color textOutlineColor = Color.black;

        [Header("Animation")]
        [Tooltip("공개/변화 애니메이션 시간")]
        public float animationDuration = 0.3f;

        [Tooltip("파티클 이펙트 프리팹")]
        public ParticleSystem particleEffectPrefab;

        [Header("Default Parameters")]
        [Tooltip("기본 파라미터들")]
        public List<GimmickDefaultParam> defaultParameters;

        [Header("Editor")]
        [Tooltip("에디터에서 표시할 아이콘")]
        public Sprite editorIcon;

        [Tooltip("에디터에서 표시할 색상")]
        public Color editorColor = Color.white;

        /// <summary>
        /// 기본 파라미터로 GimmickInstanceData 생성
        /// </summary>
        public GimmickInstanceData CreateInstanceData()
        {
            var data = new GimmickInstanceData(gimmickId);
            if (defaultParameters != null)
            {
                foreach (var param in defaultParameters)
                {
                    data.SetParam(param.key, param.defaultValue);
                }
            }
            return data;
        }
    }

    /// <summary>
    /// 기믹 기본 파라미터 정의
    /// </summary>
    [Serializable]
    public class GimmickDefaultParam
    {
        [Tooltip("파라미터 키")]
        public string key;

        [Tooltip("기본 값")]
        public string defaultValue;

        [Tooltip("파라미터 타입 (에디터 표시용)")]
        public GimmickParamType type;

        [Tooltip("에디터에서 표시할 라벨")]
        public string displayLabel;

        [Tooltip("int/float 타입의 최소값")]
        public float minValue;

        [Tooltip("int/float 타입의 최대값")]
        public float maxValue = 10f;
    }

    /// <summary>
    /// 기믹 파라미터 타입
    /// </summary>
    public enum GimmickParamType
    {
        String,
        Int,
        Float,
        Bool
    }
}
