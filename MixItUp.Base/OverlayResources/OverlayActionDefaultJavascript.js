const main = document.getElementById("maindiv");

const duration = {Duration} * 1000;

function removeSelf()
{
    main.style.visibility='hidden';
    sendParentMessage({ Type: "Remove", ID: "{ID}" });
}

performAnimation("{EntranceAnimationFramework}", "{EntranceAnimationName}", main).then((result) =>
{
    {CustomAnimations}

    if (duration > 0.0)
    {
        setTimeout(() =>
        {
            performAnimation("{ExitAnimationFramework}", "{ExitAnimationName}", main).then((result) =>
            {
                removeSelf();
            });
        }, duration);
    }
});