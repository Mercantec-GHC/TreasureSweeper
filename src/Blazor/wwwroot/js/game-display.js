window.gameRenderer = {

    previousMap: null,
    previousPlayers: [],
    images: {},
    tintedPlayers: {},

    drawMap: function (canvas, map, players) {
        const ctx = canvas.getContext("2d");
        const tileSize = 32;
        const canvasSize = map.length * tileSize;

        // A canvas that was never fully drawn (e.g. new game after leaving) needs a full redraw
        if (!canvas.__fullyDrawn) {
            this.previousMap = null;
            this.previousPlayers = [];
        }

        // Only resize if necessary
        if (canvas.width !== canvasSize ||
            canvas.height !== canvasSize) {

            canvas.width = canvasSize;
            canvas.height = canvasSize;

            this.previousMap = null;
            this.previousPlayers = [];
        }

        ctx.imageSmoothingEnabled = false;
        const images = this.images;
        const tintedPlayers = this.tintedPlayers;

        if (!images[0]?.complete || !images[333]?.complete) {
            return;
        }

        function drawTileNumber(x, y) {

            const tile = map[x][y];

            // Only display numbers for MineClose tiles
            if ((tile & 0b1111_0000) == 128) {

                const number = tile & 0b1111;

                const pixelX = x * tileSize;
                const pixelY = y * tileSize;

                ctx.save();

                ctx.font = "18px 'Press Start 2P'";
                ctx.textAlign = "center";
                ctx.textBaseline = "middle";

                // Text outline for better visibility
                ctx.lineWidth = 3;
                ctx.strokeStyle = "black";
                ctx.strokeText(
                    number,
                    pixelX + tileSize / 2,
                    pixelY + tileSize / 2
                );

                ctx.fillStyle = "#ffffff";
                ctx.fillText(
                    number,
                    pixelX + tileSize / 2,
                    pixelY + tileSize / 2
                );

                ctx.restore();
            }
        }

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

            drawTileNumber(x, y);
        }

        function drawPlayer(player) {

            const pixelX = player.x * tileSize;
            const pixelY = player.y * tileSize;

            const playerImage = images[333];

            if (!playerImage?.complete || !playerImage.naturalWidth) {
                return;
            }

            // Get cached tinted image
            if (!tintedPlayers[player.color]) {

                const tempCanvas = document.createElement("canvas");

                tempCanvas.width = playerImage.width;
                tempCanvas.height = playerImage.height;

                const tempCtx = tempCanvas.getContext("2d");

                tempCtx.drawImage(playerImage, 0, 0);

                tempCtx.globalCompositeOperation = "source-in";
                tempCtx.fillStyle = player.color;

                tempCtx.fillRect(
                    0, 0,
                    tempCanvas.width,
                    tempCanvas.height
                );

                tintedPlayers[player.color] = tempCanvas;
            }

            ctx.drawImage(
                tintedPlayers[player.color],
                pixelX, pixelY,
                tileSize, tileSize
            );
        }


        // Collect tiles that need redrawing
        const dirtyTiles = new Set();

        function markDirty(x, y) {
            dirtyTiles.add(`${x},${y}`);
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
                    if (this.previousMap[x][y] !== map[x][y]) {
                        markDirty(x, y);
                    }
                }
            }
        }

        // Mark previous player positions
        for (const player of this.previousPlayers) {
            markDirty(player.x, player.y);
        }

        // Mark current player positions
        for (const player of players) {
            markDirty(player.x, player.y);
        }

        // Redraw affected tiles
        for (const tile of dirtyTiles) {

            const [x, y] = tile.split(",").map(Number);

            drawTile(x, y);
        }

        // Draw all players again
        for (const player of players) {
            drawPlayer(player);
        }

        // Save previous state
        this.previousMap = map.map(row => [...row]);

        this.previousPlayers = players.map(player => ({
            x: player.x,
            y: player.y,
            color: player.color
        }));
    },

    loadImages: function () {

        const tileFiles = {
            0: "Floor.png",
            16: "Hole.png",
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
            224: "Mine.png",
            333: "Player.png",
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


// Empty = 0b0000_0000,         // 0
// Outside = 0b0001_0000,       // 16
// Hole = 0b1001_0000,          // 144
// MineClose0 = 0b1000_0000,    // 128
// MineClose1 = 0b1000_0001,    // 129
// MineClose2 = 0b1000_0010,    // 130
// MineClose3 = 0b1000_0011,    // 131
// MineClose4 = 0b1000_0100,    // 132
// MineClose5 = 0b1000_0101,    // 133
// MineClose6 = 0b1000_0110,    // 134
// MineClose7 = 0b1000_0111,    // 135
// MineClose8 = 0b1000_1000,    // 136
// Treasure = 0b0010_0000,      // 32
// DiscoveredTreasure = 0b1010_0000, // 160
// FreezeMine = 0b0100_0000,    // 64
// DiscoveredFreezeMine = 0b1100_0000, // 192
// PointMine = 0b0110_0000, // 96
// DiscoveredPointMine = 0b1110_0000, // 224