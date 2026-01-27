using System;
using UnityEngine;
using DUG;

namespace NGFE.Data
{
	[PreferBinarySerialization]
	public partial class BoosterTable : KeyValueTable<ITEM_TYPE, BoosterTableRecord>
	{
		public BoosterTable() : base(nameof(BoosterTableRecord.ItemType))
		{}
	}

	[Serializable]
	public partial class BoosterTableRecord
	{
		public ITEM_TYPE ItemType;
		public ITEM_CATEGORY ItemCategory;
		public int ViewOrder;
		public int InitAmount;
		public int UnlockStage;
		public int BuyGivenQty;
	}
}
