namespace FarmHelpers;

public sealed class ModConfig
{
    public bool IsScareCrow { get; set; } = true;
    public bool IsCollectCrop { get; set; } = true;
    public bool IsCollectFarm { get; set; } = true;
    public bool IsCollectIslandWest { get; set; } = true;
    public bool IsCollectOther { get; set; } = true;
    public bool IsWaterCrop { get; set; } = true;
    public bool IsWaterFarm { get; set; } = true;
    public bool IsWaterIslandWest { get; set; } = true;
    public bool IsWaterOther { get; set; } = true;
    public bool IsGrabItem { get; set; } = true;
    public bool IsMakeMayonnaise { get; set; } = true;
    public bool IsMakeCheese { get; set; } = true;
    public bool IsMakeFabric { get; set; } = true;

    public int CropsChestColorIdx { get; set; } = 19;
    public int ItemsChestColorIdx { get; set; } = 20;
}