function onYouTubeIframeAPIReady() {
    var youtubeVideoPlayer = new YT.Player("youtube-player", {
        height: {HeightNumber},
        width: {WidthNumber},
        videoId: '{VideoID}',
        playerVars: { 'controls': 0, 'modestbranding': 1, 'start': {StartTime} },
        events: {
            'onReady': function () {
                youtubeVideoPlayer.setVolume({Volume});
                youtubeVideoPlayer.setLoop(false);
                youtubeVideoPlayer.frameBorder = 0;
                youtubeVideoPlayer.playVideo();
            },
            'onStateChange': function (event) {
                if (event.data == YT.PlayerState.ENDED) {
                    if (duration == 0.0)
                    {
                        performAnimation("{ExitAnimationFramework}", "{ExitAnimationName}", main).then((result) =>
                        {
                            event.target.destroy();
                            removeSelf();
                        });
                    }
                }
            }
        }
    });
}

var youtubePlayerScript = document.createElement('script');
youtubePlayerScript.src = "https://www.youtube.com/iframe_api";
var firstScriptTag = document.getElementsByTagName('script')[0];
firstScriptTag.parentNode.insertBefore(youtubePlayerScript, firstScriptTag);