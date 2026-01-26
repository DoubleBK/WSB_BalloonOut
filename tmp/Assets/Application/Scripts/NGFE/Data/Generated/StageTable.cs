using System;
using UnityEngine;
using DUG;

namespace NGFE.Data
{
	[PreferBinarySerialization]
	public partial class StageTable : KeyValueTable<int, StageTableRecord>
	{
		public StageTable() : base(nameof(StageTableRecord.LevelIdx))
		{}
	}

	[Serializable]
	public partial class StageTableRecord
	{
		public int LevelIdx;
		public int StageIdx;
		public string Difficulty;
	}
}
