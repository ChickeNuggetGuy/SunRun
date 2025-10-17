public enum DetiectionSpotType
{
    Head,
    Body,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg
}

public enum MovementState
{
    Idle = 0,
    Moving = 1,
    Sprinting = 2
}

public enum StatType
{
	Health,
	Stamina,
	Experience
}
#region Inventory Enums

[System.Flags]
public enum InventorySettings
{
	None = 0,
	CanStackItems	
}

public enum ItemInteractionType
{
	PickUp = 0,
	use
}
[System.Flags]
public enum ItemUseSettings
{
	None = 0,
	Consume,
	Drop
}

#endregion