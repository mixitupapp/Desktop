var youtubeVideoPlayer;
var lastVideoID = '{VideoID}';

function onYouTubeIframeAPIReady() {
    youtubeVideoPlayer = new YT.Player("youtube-player", {
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
                    
                }
            }
        }
    });
}

function update(data)
{
    if (youtubeVideoPlayer != null && lastVideoID !== data.VideoID)
    {
        lastVideoID = data.VideoID;
        youtubeVideoPlayer.loadVideoById(data.VideoID);
    }
}

var youtubePlayerScript = document.createElement('script');
youtubePlayerScript.src = "https://www.youtube.com/iframe_api";
var firstScriptTag = document.getElementsByTagName('script')[0];
firstScriptTag.parentNode.insertBefore(youtubePlayerScript, firstScriptTag);

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });