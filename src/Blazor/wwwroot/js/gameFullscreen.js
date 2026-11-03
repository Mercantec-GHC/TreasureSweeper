window.gameFullscreen = {

    resize: function (element) {

        const canvas = element.querySelector("canvas");

        if (!canvas) return;

        const baseWidth = canvas.width;
        const baseHeight = canvas.height;

        const availableWidth = element.clientWidth - 40;
        const availableHeight = element.clientHeight - 40;

        const scale = Math.max(
            1,
            Math.floor(
                Math.min(
                    availableWidth / baseWidth,
                    availableHeight / baseHeight
                )
            )
        );

        canvas.style.setProperty(
            "--canvas-size",
            `${baseWidth * scale}px`
        );
    },

    toggle: async function (element) {

        if (!document.fullscreenElement) {

            await element.requestFullscreen();

        } else {

            await document.exitFullscreen();

        }
    }
};