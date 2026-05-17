const displayFormat = "{DisplayFormat}";

var totalSeconds = {CurrentAmount};
var additionalTime = 0;
var paused = false;

const main = document.getElementById("maindiv");

function timerLoop()
{
    if (additionalTime != 0)
    {
        totalSeconds += additionalTime;
        additionalTime = 0;

        totalSeconds = Math.max(totalSeconds, 0);
        performAnimation("{TimerAdjustedAnimationFramework}", "{TimerAdjustedAnimationName}", main).then((result) =>
        {
            
        });
    }

    let text = displayFormat;

    let seconds = totalSeconds % 60;
    if (seconds < 10) { seconds = "0" + seconds }
    text = text.replace("SS", totalSeconds);
    text = text.replace("ss", seconds);

    let totalMinutes = Math.floor(totalSeconds / 60);
    let minutes = totalMinutes % 60;
    if (minutes < 10) { minutes = "0" + minutes }
    text = text.replace("MM", totalMinutes);
    text = text.replace("mm", minutes);

    let totalHours = Math.floor(totalMinutes / 60);
    let hours = totalHours % 24;
    if (hours < 10) { hours = "0" + hours }
    text = text.replace("HH", totalHours);
    text = text.replace("hh", hours);

    let days = Math.floor(totalHours / 24);
    text = text.replace("DD", days);

    document.getElementById("text").innerHTML = text;
    
    if (totalSeconds > 0)
    {
        setTimeout(function () {
            if (!paused)
            {
                totalSeconds--;
            }
            timerLoop();
        }, 1000);
    }
    else
    {
        performAnimation("{TimerCompletedAnimationFramework}", "{TimerCompletedAnimationName}", main).then((result) =>
        {
            
        });
        checkForAdditionalTime();
    }
}

function adjustTime(data)
{
    additionalTime += data.Seconds;
}

function pause(data)
{
    paused = true;
}

function unpause(data)
{
    paused = false;
}

function checkForAdditionalTime()
{
    setTimeout(function () {
        if (additionalTime > 0)
        {
            timerLoop();
        }
        else
        {
            checkForAdditionalTime();
        }
    }, 1000);
}

timerLoop();