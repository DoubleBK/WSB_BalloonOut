namespace NGFE.Data
{
	public enum LOBBY_STAGEBLOCK_TYPE
	{
		GREEN = 0,										// 로비 스테이지 블록 | Green
		RED = 1,										// 로비 스테이지 블록 | Red
		PURPLE = 2,										// 로비 스테이지 블록 | Purple
	}

	public enum GIMMICK_UNLOCK_INFO_TYPE
	{
		NONE = 0,										// 기믹 언락 안내 | None
		ARROW_BLOCK = 1,								// 기믹 언락 안내 | ArrowBlock
		LAYER_BLOCK = 2,								// 기믹 언락 안내 | LayerBlock
		ICE_BLOCK = 3,									// 기믹 언락 안내 | IceBlock
		STAR_BLOCK = 4,									// 기믹 언락 안내 | StarBlock
		STAR_GATE = 5,									// 기믹 언락 안내 | StarGate
		CHAIN_BLOCK = 6,								// 기믹 언락 안내 | ChainBlock
		KEY_BLOCK = 7,									// 기믹 언락 안내 | KeyBlock
		COMBINED_BLOCK = 8,								// 기믹 언락 안내 | CombinedBlock
		DOOR = 9,										// 기믹 언락 안내 | Door
		BOMB_BLOCK = 10,								// 기믹 언락 안내 | BombBlock
		ROPES_BLOCK = 11,								// 기믹 언락 안내 | RopesBlock
		SCISSORS_BLOCK = 12,							// 기믹 언락 안내 | ScissorsBlock
		COLORFUL_PATH = 13,								// 기믹 언락 안내 | ColorfulPath
		ICE_GATE = 14,									// 기믹 언락 안내 | IceGate
		MOVING_DOOR_LOCK = 15,							// 기믹 언락 안내 | MovingDoorLock
		CRATE = 16,										// 기믹 언락 안내 | Crate
		MOVING_OBSTACLE = 17,							// 기믹 언락 안내 | MovingObstacle
		SCREW_BLOCK = 18,								// 기믹 언락 안내 | ScrewBlock
		COLOR_DOOR = 19,								// 기믹 언락 안내 | ColorDoor
		CHAIN_GATE = 20,								// 기믹 언락 안내 | ChainGate
		GATEKEY_BLOCK = 21,								// 기믹 언락 안내 | GatekeyBlock
		SIZE_CHANGING_DOOR = 22,						// 기믹 언락 안내 | SizeChangingDoor
		COMBINED_LOCKED_BLOCK = 23,						// 기믹 언락 안내 | CombinedLockedBlock
		HIDDEN_BLOCK = 24,								// 기믹 언락 안내 | HiddenBlock
		DYNAMITE_BLOCK = 25,							// 기믹 언락 안내 | DynamiteBlock
		CURTAIN_BLOCK = 26,								// 기믹 언락 안내 | CuratinBlock
		MOVEABLE_CRATE = 27,							// 기믹 언락 안내 | MoveableCrate
		COLOR_SWITCHING_DOOR = 28,						// 기믹 언락 안내 | ColorSwitchingDoor
		TIME_CAPSULE_BLOCK = 29,						// 기믹 언락 안내 | TimeCapsuleBlock
		COLOR_SWITCHING_BLOCK = 30,						// 기믹 언락 안내 | ColorSwitchingBlock
		JUMPING_SINGLE_DOOR = 31,						// 기믹 언락 안내 | JumpingSingleDoor
	}

	public enum GIMMICK_UNLOCK_INFO_CATEGORY
	{
		NONE = 0,										// 기믹 카테고리 | None
		TILE = 1,										// 기믹 카테고리 | 타일
		BLOCK = 2,										// 기믹 카테고리 | 블록
		GATE = 3,										// 기믹 카테고리 | 게이트
	}

	public enum UNITTYPE
	{
		VEHICLE = 0,									// 유닛타입 | 탈것
		ARMORED = 1,									// 유닛타입 | 투구
		ELITE = 2,										// 유닛타입 | 엘리트
		BOSS = 3,										// 유닛타입 | 보스
		BONUS = 4,										// 유닛타입 | 보너스
	}

	public enum STAGE_REWARD_TYPE
	{
		COIN = 0,										// 스테이지보상 | 코인
		ITEM_1 = 11,									// 스테이지보상 | 아이템1
		ITEM_2 = 12,									// 스테이지보상 | 아이템2
		ITEM_3 = 13,									// 스테이지보상 | 아이템3
		TIME_INC = 101,									// 스테이지보상 | 랭킹모드 게임시간증가
	}

	public enum STAGE_TYPE
	{
		NORMAL = 0,										// 스테이지타입 | 일반
		RANK = 1,										// 스테이지타입 | 랭킹
	}

	public enum RANK_SCORE_TYPE
	{
		NONE = 0,										// 랭크스코어타입 | 없음
		NORMAL_KILL = 1,								// 랭크스코어타입 | 일반적 처치
		ELITE_KILL = 2,									// 랭크스코어타입 | 엘리트 처치
		BOSS_KILL = 3,									// 랭크스코어타입 | 보스 처치
		MONSTER_BONUS = 4,								// 랭크스코어타입 | 즙몬스터 생존
		CASTLE_BONUS = 5,								// 랭크스코어타입 | 타워 생존
	}

	public enum EVENT_TRIGGER_TYPE
	{
		ENTER_STAGE = 0,								// 이벤트트리거타입 | 스테이지입장
		START_STAGE = 1,								// 이벤트트리거타입 | 스테이지시작
		END_STAGE = 2,									// 이벤트트리거타입 | 스테이지종료
		ENTER_LOBBY = 3,								// 이벤트트리거타입 | 스테이지종료
	}

	public enum ACTION_TYPE
	{
		NONE = 0,										// 액션타입 | None
		START_TUTORIAL = 1,								// 액션차입 | 튜토리얼시작
	}

	public enum DIFFICULTY_TYPE
	{
		NORMAL = 1,										// 스테이지 난이도 | Normal
		HARD = 2,										// 스테이지 난이도 | Hard
		VERY_HARD = 3,									// 스테이지 난이도 | Very_Hard
	}

	public enum GATE_TYPE
	{
		NORMAL = 0,										// 게이트 타입 | Normal
		STARGATE = 1,									// 게이트 타입 | StarGate
		DOOR = 2,										// 게이트 타입 | Door
	}

	public enum BLOCK_GIMMICKTYPE
	{
		NORMAL = 0,										// 블럭 기믹 타입 | Normal
		ARROW = 1,										// 블럭 기믹 타입 | Arrow
		LAYER = 2,										// 블럭 기믹 타입 | Layer
		ICE = 3,										// 블럭 기믹 타입 | Ice
		STAR = 4,										// 블럭 기믹 타입 | Star
		CHAIN = 5,										// 블럭 기믹 타입 | Chain
		KEY = 6,										// 블럭 기믹 타입 | Key
		COMBINE = 7,									// 블럭 기믹 타입 | Combine
		BOMB = 8,										// 블럭 기믹 타입 | Bomb
		ROPES = 9,										// 블럭 기믹 타입 | Ropes
		SCISSORS = 10,									// 블럭 기믹 타입 | Scissors
		MOVING_OBSTACLE = 11,							// 블럭 기믹 타입 | Moving_Obstacle
		SCREW = 12,										// 블럭 기믹 타입 | Screw
		CRATE = 13,										// 블럭 기믹 타입 | Crate
		RAINBOW = 14,									// 블럭 기믹 타입 | Rainbow
		COMBINEDLOCKEDBLOCK = 15,						// 블럭 기믹 타입 | CombinedLockedBlock
		HIDDENBLOCK = 16,								// 블럭 기믹 타입 | HiddenBlock
		DYNAMITE = 17,									// 블럭 기믹 타입 | Dynamite
		CURTAIN = 18,									// 블럭 기믹 타입 | Curtain
		MOVEABLECRATE = 19,								// 블럭 기믹 타입 | MoveableCrate
		TIMECAPSULE = 20,								// 블럭 기믹 타입 | TimeCapsule
		COLORSWITCHING = 21,							// 블럭 기믹 타입 | ColorSwitching
	}

	public enum BLOCK_TYPE
	{
		SQUARE_1X1 = 0,									// 블럭 타입 | 1x1_Square
		SQUARE_1X2 = 1,									// 블럭 타입 | 1x2_Square
		SQUARE_1X3 = 2,									// 블럭 타입 | 1x3_Square
		SQUARE_2X2 = 3,									// 블럭 타입 | 2x2_Square
		CORNER_2X2 = 4,									// 블럭 타입 | 2x2_Corner
		CORNER_2X3 = 5,									// 블럭 타입 | 2x3_Corner
		CROSS_2X3 = 6,									// 블럭 타입 | 2x3_Cross
		CROSS_3X3 = 7,									// 블럭 타입 | 3x3_Cross
		CORNER_2X3_R = 8,								// 블럭 타입 | 2x3_Corner_R
		U_3X2 = 9,										// 블럭 타입 | U_3x2
		S_3X2 = 10,										// 블럭 타입 | S_3x2
		Z_3X2 = 11,										// 블럭 타입 | Z_3x2
	}

	public enum TILE_TYPE
	{
		NORMAL = 0,										// 타일 타입 | Normal
		COLORFUL = 1,									// 타일 타입 | Colorful
	}

	public enum WALL_TYPE
	{
		WALLOUT = 0,									// 벽 타입 | WallOut
		CORNER = 1,										// 벽 타입 | Corner
		WALL = 2,										// 벽 타입 | Wall
	}

	public enum BLOCK_COLOR
	{
		R = 0,											// 블럭 색상 | 빨강
		G = 1,											// 블럭 색상 | 녹색
		LG = 2,											// 블럭 색상 | 연두
		B = 3,											// 블럭 색상 | 파랑
		P = 4,											// 블럭 색상 | 핑크
		RP = 5,											// 블럭 색상 | 보라
		Y = 6,											// 블럭 색상 | 노랑
		LB = 7,											// 블럭 색상 | 하늘
		O = 8,											// 블럭 색상 | 주황
		BG = 9,											// #REF!
		OBS = 10,										// 블럭 색상 | 회색(분쇄 못하는 블럭)
		RAINBOW = 11,									// 블럭 색상 | 무지개색
	}

	public enum TILE_ROTATION_TYPE
	{
		NONE = 0,										// 타일 회전 | 0
		DEGREE_90 = 1,									// 타일 회전 | 90
		DEGREE_180 = 2,									// 타일 회전 | 180
		DEGREE_270 = 3,									// 타일 회전 | 270
	}

	public enum TILE_MAP_DIRECTION
	{
		LT = 0,											// 맵툴 사용 | LT
		T = 1,											// 맵툴 사용 | T
		RT = 2,											// 맵툴 사용 | RT
		LC = 3,											// 맵툴 사용 | LC
		C = 4,											// 맵툴 사용 | C
		RC = 5,											// 맵툴 사용 | RC
		LB = 6,											// 맵툴 사용 | LB
		B = 7,											// 맵툴 사용 | B
		RB = 8,											// 맵툴 사용 | RB
	}

	public enum GATE_GIMMICKTYPE
	{
		NORMAL = 0,										// 게이트 기믹 타입 | Normal
		STAR = 1,										// 게이트 기믹 타입 | Star
		DOOR = 2,										// 게이트 기믹 타입 | Door
		MOVING_DOOR_LOCK = 3,							// 게이트 기믹 타입 | Moving_Door_Lock
		ICEGATE = 4,									// 게이트 기믹 타입 | IceGate
		COLORGATE = 5,									// 게이트 기믹 타입 | ColorGate
		CHAIN = 6,										// 게이트 기믹 타입 | Chain
		SIZECHANGING = 7,								// 게이트 기믹 타입 | SizeChanging
		COLORSWITCHING = 8,								// 게이트 기믹 타입 | ColorSwitching
		JUMPINGSINGLE = 9,								// 게이트 기믹 타입 | JumpingSingle
	}

	public enum TILE_GIMMICKTYPE
	{
		NORMAL = 0,										// 타일 기믹 타입 | Normal
		COLORFUL = 1,									// 타일 기믹 타입 | Colorful
	}

	public enum BLOCK_GIMMICK_CATEGORY
	{
		NONE = 0,										// 블럭 기믹 카테고리 | None
		DEFAULT = 1,									// 블럭 기믹 카테고리 | 특별히 분류 할 필요 없는 기믹
		ORIGINAL = 2,									// 블럭 기믹 카테고리 | 블럭이 나눠질 때 복사되면 안되는 기믹
		COUNT = 3,										// 블럭 기믹 카테고리 | 카운트를 사용하는 기믹
		STATECHANGE = 4,								// 블럭 기믹 카테고리 | 상태가 변하는 기믹
	}

	public enum CRATEBLOCK_TYPE
	{
		SQUARE_1X1 = 0,									// Crate 기믹 블록타입 | Square_1x1
		SQUARE_1X2 = 1,									// Crate 기믹 블록타입 | Square_1x2
		SQUARE_1X3 = 2,									// Crate 기믹 블록타입 | Square_1x3
		SQUARE_2X2 = 3,									// Crate 기믹 블록타입 | Square_2x2
		SQUARE_2X3 = 4,									// Crate 기믹 블록타입 | Square_2x3
		SQUARE_2X4 = 5,									// Crate 기믹 블록타입 | Square_2x4
		SQUARE_2X5 = 6,									// Crate 기믹 블록타입 | Square_2x5
		SQUARE_3X3 = 7,									// Crate 기믹 블록타입 | Square_3x3
		SQUARE_3X4 = 8,									// Crate 기믹 블록타입 | Square_3x4
		CORNER_2X2 = 9,									// Crate 기믹 블록타입 | Corner_2x2
		CORNER_2X3 = 10,								// Crate 기믹 블록타입 | Corner_2x3
		CORNER_2X3_R = 11,								// Crate 기믹 블록타입 | Corner_2x3_R
		CORNER_2X4 = 12,								// Crate 기믹 블록타입 | Corner_2x4
		CORNER_2X4_R = 13,								// Crate 기믹 블록타입 | Corner_2x4_R
		CORNER_3X4 = 14,								// Crate 기믹 블록타입 | Corner_3x4
		CORNER_3X4_R = 15,								// Crate 기믹 블록타입 | Corner_3x4_R
		T_3X2 = 16,										// Crate 기믹 블록타입 | T_3x2
		T_3X3 = 17,										// Crate 기믹 블록타입 | T_3x3
		CROSS_3X3 = 18,									// Crate 기믹 블록타입 | Cross_3x3
		CROSS_5X5 = 19,									// Crate 기믹 블록타입 | Cross_5x5
		S_3X2 = 20,										// Crate 기믹 블록타입 | S_3x2
		Z_3X2 = 21,										// Crate 기믹 블록타입 | Z_3x2
	}

	public enum AD_FORMAT
	{
		REWARDEDVIDEO = 0,								// 광고종류 | 보상형 광고
		INTERSTITIAL = 1,								// 광고종류 | 전면 광고
		BANNER = 2,										// 광고종류 | 배너 광고
	}

	public enum ADS_TYPE
	{
		DEFAULT_REWARDEDVIDEO = 0,						// 광고타입 | 기본 보상형광고
		DEFAULT_INTERSTITIAL = 1,						// 광고타입 | 기본 전면광고
		DEFAULT_BANNER = 2,								// 광고타입 | 기본 배너광고
		ATTENDANCE_MOREREWARDS = 3,						// 광고타입 | 출석 추가 보상
		INGAME_STAGECLEAR = 4,							// 광고타입 | 인게임 스테이지 클리어 Segment 1
		PLAYTIME = 5,									// 광고타입 | 체류 시간 Segment 1
		LOBBY_HEART = 6,								// 광고타입 | 로비 하트 광고
		INGAME_EXTRAREWARD = 7,							// 광고타입 | 인게임 추가보상
		STORE_GETFREECOIN = 8,							// 광고타입 | 상점 코인 무료 보상
		INGAME_STAGECLEAR_2 = 9,						// 광고타입 | 인게임 스테이지 클리어 Segment 2
		PLAYTIME_2 = 10,								// 광고타입 | 체류 시간 Segment 2
		INGAME_PLAYON = 11,								// 광고타입 | 인게임 이어하기 광고
		INGAME_STAGECLEAR_3 = 12,						// 광고타입 | 인게임 스테이지 클리어 Segment 3
		PLAYTIME_3 = 13,								// 광고타입 | 체류 시간 Segment 3
	}

	public enum CONDITIONCHECKER_TYPE
	{
		CANPLAYINTERSTITIALAD = 0,						// 컨디션 체커 | 전면 광고 조건 체크
		CLEARSTAGE = 1,									// 컨디션 체커 | 스테이지 클리어 체크
		GETATTENDANCEREWARDDAY = 2,						// 컨디션 체커 | 출석 보상 획득 체크
		ISCLEARSTAGE = 3,								// 컨디션 체커 | 스테이지 클리어 했는지
		ENTERSTAGE = 4,									// 컨디션 체커 | 스테이지 입장
	}

	public enum MULTIPLECONDITION_TYPE
	{
		AND = 0,										// 복합 컨디션 | AND
		OR = 1,											// 복합 컨디션 | OR
	}

	public enum ADS_GROUP_TYPE
	{
		NONE = 0,										// 광고 그룹 타입 | None
		INGAME_STAGECLEAR_OR_PLAYTIME = 1,				// 광고 그룹 타입 | 스테이지 클리어 OR 플레이시간 Segment 1
		INGAME_STAGECLEAR_AND_PLAYTIME = 2,				// 광고 그룹 타입 | 스테이지 클리어 AND 플레이 시간 Segment 1
		INGAME_STAGECLEAR_OR_PLAYTIME_2 = 3,			// 광고 그룹 타입 | 스테이지 클리어 OR 플레이시간 Segment 2
		INGAME_STAGECLEAR_AND_PLAYTIME_2 = 4,			// 광고 그룹 타입 | 스테이지 클리어 AND 플레이 시간 Segment 2
		INGAME_STAGECLEAR_OR_PLAYTIME_3 = 5,			// 광고 그룹 타입 | 스테이지 클리어 OR 플레이시간 Segment 3
	}

	public enum ADS_SEGMENT_TYPE
	{
		NONE = 0,										// 광고 세그먼트 타입 | None
		LOCAL = 1,										// 광고 세그먼트 타입 | 로컬 조건 체크
		ABTEST = 2,										// 광고 세그먼트 타입 | AB 테스트
	}

	public enum ERROR_LEVEL
	{
		WARN = 0,										// 에러 레벨 | 경고
		ERROR = 1,										// 에러 레벨 | 에러
		FATAL = 3,										// 에러 레벨 | 치명적 에러
	}

	public enum ERROR_ACTION
	{
		NONE = 0,										// 에러 발생 시 대응 | 없음
		NOTICE = 1,										// 에러 발생 시 대응 | 경고 메시지 출력
		LOGOUT = 2,										// 에러 발생 시 대응 | 로그 아웃
		TOAST = 3,										// 에러 발생 시 대응 | 토스트 메시지 출력
		SYNC = 4,										// 에러 발생 시 대응 | sync 요청
		VERSION_CHECK = 5,								// 에러 발생 시 대응 | 버전 에러, 마켓 업데이트로 이동
		VERSION_CHECK_OPTIONAL = 6,						// 에러 발생 시 대응 | 버전 에러, 마켓 업데이트로 이동(옵셔널)
		DEL_CACHE = 7,									// 에러 발생 시 대응 | 클라이언트 캐쉬 삭제
	}

	public enum ITEM_TYPE
	{
		NONE = 0,										// 아이템 타입 | None
		COIN = 101,										// 아이템 타입 | Coin
		HEART = 102,									// 아이템 타입 | Heart
		UNDO = 201,										// 아이템 타입 | Undo
		HINT = 202,										// 아이템 타입 | Hint
		TRIPLEARROW = 203,								// 아이템 타입 | TripleArrow
		DARTARROW = 204,								// 아이템 타입 | DartArrow
		NOADS = 404,									// 아이템 타입 | NoAds
	}

	public enum ITEM_GETTYPE
	{
		NONE = 0,										// 아이템 획득 방법 | None
		DEFAULT = 1,									// 아이템 획득 방법 | Default(초기 사용자 계정에 지급된 상태)
		PRICE = 2,										// 아이템 획득 방법 | Price(Item_Price 지급)
		ADSFREE = 3,									// 아이템 획득 방법 | AdsFree
	}

	public enum ITEM_CATEGORY
	{
		NONE = 0,										// 아이템 카테고리 | None
		CURRENCY = 1,									// 아이템 카테고리 | 재화
		CONSUMABLE = 2,									// 아이템 카테고리 | 소비성 아이템
		BUFF = 3,										// 아이템 카테고리 | 버프
		CONTINUE = 4,									// 아이템 카테고리 | 이어 하기 아이템
		STARTINGBOOSTER = 5,							// 아이템 카테고리 | 스타팅 부스터
	}

	public enum HAPTICTYPE
	{
		CONSTANT = 0,									// 진동 타입 | Constant
		PRESET_SELECTION = 1,							// 진동 타입 | Preset_Selection
		PRESET_SUCCESS = 2,								// 진동 타입 | Preset_Success
		PRESET_WARNING = 3,								// 진동 타입 | Preset_Warning
		PRESET_FAILURE = 4,								// 진동 타입 | Preset_Failure
		PRESET_LIGHTIMPACT = 5,							// 진동 타입 | Preset_LightImpact
		PRESET_MEDIUMIMPACT = 6,						// 진동 타입 | Preset_MediumImpact
		PRESET_HEAVYIMPACT = 7,							// 진동 타입 | Preset_HeavyImpact
		PRESET_RIGIDIMPACT = 8,							// 진동 타입 | Preset_RigidImpact
		PRESET_SOFTIMPACT = 9,							// 진동 타입 | Preset_SoftImpact
		HAPTIC_CLIP = 10,								// 진동 타입 | Haptic_Clip
	}

	public enum LOCALPUSH_TYPE
	{
		ATTENDANCE_NEXT_DAY = 0,						// 푸시 타입 | 출석부 다음날
		HEART_MAX = 1,									// 푸시 타입 | 하트 맥스
		DAILY_WAKEUP = 2,								// 푸시 타입 | 데일리 푸시2
		PICKONE_END = 3,								// 푸시 타입 | 픽원 종료 알림
		VERTICAL_END = 4,								// 푸시 타입 | 엔드리스(세로형) 종료 알림
		CHAIN_END = 5,									// 푸시 타입 | 엔드리스(체인형) 종료 알림
	}

	public enum PURCHASE_TYPE
	{
		NONE = 0,										// 상품 구매 형태 | None
		CASH = 1,										// 상품 구매 형태 | 캐쉬
		ADSFREE = 2,									// 상품 구매 형태 | 광고 시청 후 제공
		FREE = 3,										// 상품 구매 형태 | 무료 제공
	}

	public enum BUNDLE_TYPE
	{
		NONE = 0,										// 패키지 타입 | None
		NOADS = 1,										// 패키지 타입 | 광고 제거 타입
		COMMON = 2,										// 패키지 타입 | 일반 타입
		INGAMESHOP = 3,									// 패키지 타입 | 인게임 상점 타입
		SPECIALOFFER = 4,								// 패키지 타입 | 스페셜 오퍼 타입
	}

	public enum PACKAGE_BG_TYPE
	{
		NONE = 0,										// 패키지 배경 | None
		NOADS = 1,										// 패키지 배경 | 광고 제거 포함
		COMMON_1 = 2,									// 패키지 배경 | 일반 패키지 1
		COMMON_2 = 3,									// 패키지 배경 | 일반 패키지 2
		COMMON_3 = 4,									// 패키지 배경 | 일반 패키지 3
		SB_1 = 5,										// 패키지 배경 | 버닝 번들 1
		COMMON_4 = 6,									// 패키지 배경 | 실패 번들
	}

	public enum SPECIAL_OFFER_TYPE
	{
		NONE = 0,										// 스페셜 오퍼 타입 | None
		PICKONE = 1,									// 스페셜 오퍼 타입 | 픽원 오퍼
		WELCOMEDEAL = 2,								// 스페셜 오퍼 타입 | 웰컴딜
		ENDLESSOFFER = 3,								// 스페셜 오퍼 타입 | 엔드리스 오퍼
		COINRUSH = 4,									// 스페셜 오퍼 타입 | 코인러쉬
	}

	public enum PICKONE_OFFER_TYPE
	{
		NONE = 0,										// 픽원 오퍼 타입 | None
		PICKONE_1 = 1,									// 픽원 오퍼 타입 | 시즌1
		PICKONE_2 = 2,									// 픽원 오퍼 타입 | 시즌2
		PICKONE_3 = 3,									// 픽원 오퍼 타입 | 시즌3
		PICKONE_4 = 4,									// 픽원 오퍼 타입 | 시즌4
		PICKONE_5 = 5,									// 픽원 오퍼 타입 | 시즌5
		PICKONE_6 = 6,									// 픽원 오퍼 타입 | 시즌6
		PICKONE_7 = 7,									// 픽원 오퍼 타입 | 시즌7
		PICKONE_8 = 8,									// 픽원 오퍼 타입 | 시즌8
		PICKONE_9 = 9,									// 픽원 오퍼 타입 | 시즌9
		PICKONE_10 = 10,								// 픽원 오퍼 타입 | 시즌10
	}

	public enum WELCOME_DEAL_TYPE
	{
		NONE = 0,										// 웰컴딜 타입 | None
		WELCOMEDEAL_1 = 1,								// 웰컴딜 타입 | 시즌1
		WELCOMEDEAL_2 = 2,								// 웰컴딜 타입 | 시즌2
	}

	public enum ENDLESS_OFFER_TYPE
	{
		NONE = 0,										// 엔드리스 오퍼 | None
		ENDLESSOFFER_1 = 1,								// 엔드리스 오퍼 | 세로형 1
		ENDLESSGIFT_1 = 2,								// 엔드리스 오퍼 | 체인형 1
	}

	public enum COIN_RUSH_TYPE
	{
		NONE = 0,										// 코인 러쉬 타입 | None
		COINRUSH_1 = 1,									// 코인 러쉬 타입 | 시즌1
	}

	public enum EVENT_TYPE
	{
		BLOCKPASS = 0,									// 이벤트 타입 | 블록 패스
		FACTORYCHASE = 1,								// 이벤트 타입 | 팩토리 채스
		ADVENTURESTREAK = 2,							// 이벤트 타입 | 어드벤어스트릭
		PINATAPARTY = 3,								// 이벤트 타입 | 피냐타 파티
		WINTOWER = 4,									// 이벤트 타입 | 윈타워
	}
}