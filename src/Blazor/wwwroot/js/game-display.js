window.gameRenderer = {

    previousMap: null,
    images: {},

    drawMap: function (canvas, map, mapSize) {

        const ctx = canvas.getContext("2d");
        const tileSize = 32;
        const canvasSize = mapSize * tileSize;

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
        function drawTile(index) {

            const x = (index % mapSize) * tileSize;
            const y = Math.floor(index / mapSize) * tileSize;

            // Draw floor background
            ctx.drawImage(
                images[0],
                x, y,
                tileSize, tileSize
            );

            // Draw tile object
            const tile = map[index];

            if (tile !== 0 && images[tile]) {
                ctx.drawImage(
                    images[tile],
                    x, y,
                    tileSize, tileSize
                );
            }
        }

        // First draw: render everything
        if (!this.previousMap) {

            for (let i = 0; i < map.length; i++) {
                drawTile(i);
            }

        } else {

            // Subsequent draws: only changed tiles
            for (let i = 0; i < map.length; i++) {

                if (map[i] !== this.previousMap[i]) {
                    drawTile(i);
                }
            }
        }

        // Save current map state
        this.previousMap = [...map];
    },

    loadImages: function () {

        const tileFiles = {
            0: "Floor.png",
            1: "Hole.png",
            2: "Mine.png",
            3: "FreezeMine.png",
            4: "Player.png",
            5: "Treasure.png",
            6: "Scan.png"
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