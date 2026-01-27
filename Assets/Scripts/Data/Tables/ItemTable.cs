using System;
using UnityEngine;
using DUG;

namespace NGFE.Data
{
	public partial class ItemTable : KeyValueTable<ITEM_TYPE, ItemTableRecord>
	{
		public ItemTable() : base(nameof(ItemTableRecord.ItemType))
		{}
	}

	[Serializable]
	public partial class ItemTableRecord
	{
		public ITEM_TYPE ItemType;
		public ITEM_CATEGORY Category;
		public int PRICE;
	}
}
