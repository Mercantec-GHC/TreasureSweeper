window.gameRenderer = {
    drawMap: function (canvas, map, mapSize) {
        const ctx = canvas.getContext("2d");

        const tileSize = 16;
        const columns = mapSize;

        const images = {};

        const tileFiles = {
            0: "Floor.png",
            1: "Hole.png",
            2: "Mine.png",
            3: "FreezeMine.png",
            4: "Player.png",
            5: "Treasure.png",
            6: "Scan.png"
        };

        const draw = () => {
            ctx.clearRect(0, 0, canvas.width, canvas.height);

            for (let i = 0; i < map.length; i++) {
                const x = (i % columns) * tileSize;
                const y = Math.floor(i / columns) * tileSize;

                // Draw floor first
                if (images[0]?.complete) {
                    ctx.drawImage(images[0], x, y, tileSize, tileSize);
                }

                // Draw object on top
                const tile = map[i];

                if (tile !== 0 && images[tile]?.complete) {
                    ctx.drawImage(images[tile], x, y, tileSize, tileSize);
                }
            }
        };

        for (const [id, filename] of Object.entries(tileFiles)) {
            images[id] = new Image();

            images[id].src = `/assets/${filename}`;

            images[id].onload = draw;
        }
    }
};