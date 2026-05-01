if (duration == 0.0)
{
    var video = document.getElementById("video");
    video.addEventListener("ended", (event) =>
    {
        performAnimation("{ExitAnimationFramework}", "{ExitAnimationName}", main).then((result) =>
        {
            removeSelf();
        });
    });
}