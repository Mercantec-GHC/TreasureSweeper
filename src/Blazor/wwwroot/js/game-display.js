window.gameRenderer = {

    previousMap: null,
    images: {},

    drawMap: function (canvas, map, players) {

        const ctx = canvas.getContext("2d");
        const tileSize = 32;
        const canvasSize = map.length * tileSize;

        // Only resize if necessary
        if (canvas.width !== canvasSize ||
            canvas.height !== canvasSize) {

            canvas.width = canvasSize;
            canvas.height = canvasSize;

            this.previousMap = null;
        }

        ctx.imageSmoothingEnabled = false;
        const images = this.images;

        // Draw a single tile
        function drawTile(x, y) {
            const pixelX = x * tileSize;
            const pixelY = y * tileSize;

            // Draw floor background
            ctx.drawImage(
                images[0],
                pixelX, pixelY,
                tileSize, tileSize
            );

            // Draw tile object
            const tile = map[x][y];

            if (tile !== 0 && images[tile]) {
                ctx.drawImage(
                    images[tile],
                    pixelX, pixelY,
                    tileSize, tileSize
                );
            }
        }

        // First draw: render everything
        if (!this.previousMap) {

            for (let x = 0; x < map.length; x++) {
                for (let y = 0; y < map[x].length; y++) {
                    drawTile(x, y);
                }
            }

        } else {

            // Subsequent draws: only changed tiles
            for (let x = 0; x < map.length; x++) {
                for (let y = 0; y < map[x].length; y++) {
                    if (map[x][y] !== this.previousMap[x][y]) {
                        drawTile(x, y);
                    }
                }
            }
        }

        // Save current map state
        this.previousMap = [...map];
    },

    loadImages: function () {

        const tileFiles = {
            0: "Floor.png",
            144: "Hole.png",
            128: "Floor.png",
            129: "Floor.png",
            130: "Floor.png",
            131: "Floor.png",
            132: "Floor.png",
            133: "Floor.png",
            134: "Floor.png",
            135: "Floor.png",
            136: "Floor.png",
            32: "Floor.png",
            160: "Treasure.png",
            64: "Floor.png",
            192: "FreezeMine.png",
            96: "Floor.png",
            224: "PointMine.png",
        };

        let loaded = 0;
        const total = Object.keys(tileFiles).length;

        for (const [id, filename] of Object.entries(tileFiles)) {

            const image = new Image();

            image.onload = () => {
                loaded++;

                if (loaded === total) {
                    console.log("All tile images loaded");
                }
            };

            image.onerror = () => {
                console.error("Failed to load:", filename);
            };

            image.src = `/assets/${filename}`;

            this.images[id] = image;
        }
    }
};

// Load images once
window.gameRenderer.loadImages();


// Empty = 0b0000_0000, // 0
// Hole = 0b1001_0000, // 144
// MineClose0 = 0b1000_0000, // 128
// MineClose1 = 0b1000_0001, // 129
// MineClose2 = 0b1000_0010, // 130
// MineClose3 = 0b1000_0011, // 131
// MineClose4 = 0b1000_0100, // 132
// MineClose5 = 0b1000_0101, // 133
// MineClose6 = 0b1000_0110, // 134
// MineClose7 = 0b1000_0111, // 135
// MineClose8 = 0b1000_1000, // 136
// Treasure = 0b0010_0000, // 32
// DiscoveredTreasure = 0b1010_0000, // 160
// FreezeMine = 0b0100_0000, // 64
// DiscoveredFreezeMine = 0b1100_0000, // 192
// PointMine = 0b0110_0000, // 96
// DiscoveredPointMine = 0b1110_0000, // 224