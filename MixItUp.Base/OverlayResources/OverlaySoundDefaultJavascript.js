const main = document.getElementById("maindiv");
const sound = document.getElementById('audio');

function removeSelf()
{
    sendParentMessage({ Type: "Remove", ID: "{ID}" });
}

function remove(data)
{
    removeSelf();
}

sound.addEventListener('ended', () =>
{
    sendParentMessage({ Type: "SoundFinished", ID: "{ID}" });
    removeSelf();
});