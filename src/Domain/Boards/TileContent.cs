namespace Domain.Boards;


// bit 7     (0b1000_0000): tile has been discovered (set by scan)
// bits 5-6  (0b0110_0000): hidden content: 1 = Treasure, 2 = FreezeMine, 3 = PointMine
// bits 0-3  (0b0000_1111): number of nearby mines (0-8), only on discovered tiles with no content
// Hole is a special value (1001_0000); check IsHole() before reading the other bits.

public enum TileContent { 
    Empty                = 0b0000_0000, // 0
    Outside              = 0b0001_0000, // 16
    Hole                 = 0b1001_0000, // 144
    MineClose0           = 0b1000_0000, // 128
    MineClose1           = 0b1000_0001, // 129
    MineClose2           = 0b1000_0010, // 130
    MineClose3           = 0b1000_0011, // 131
    MineClose4           = 0b1000_0100, // 132
    MineClose5           = 0b1000_0101, // 133
    MineClose6           = 0b1000_0110, // 134
    MineClose7           = 0b1000_0111, // 135
    MineClose8           = 0b1000_1000, // 136
    Treasure             = 0b0010_0000, // 32
    DiscoveredTreasure   = 0b1010_0000, // 160
    FreezeMine           = 0b0100_0000, // 64
    DiscoveredFreezeMine = 0b1100_0000, // 192
    PointMine            = 0b0110_0000, // 96
    DiscoveredPointMine  = 0b1110_0000, // 224
}