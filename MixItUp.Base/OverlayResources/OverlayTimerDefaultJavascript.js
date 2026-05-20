const displayFormat = "{DisplayFormat}";

var totalSeconds = 0;
var endingSeconds = 0;

function timerLoop()
{
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
    text = text.replace("DD", hours);

    document.getElementById("text").innerHTML = text;

    setTimeout(function () {
        if (endingSeconds > 0) {
            totalSeconds++;
            if (totalSeconds < endingSeconds) {
                timerLoop();
            }        
        }
        else {
            totalSeconds--;
            if (totalSeconds > 0) {
                timerLoop();
            }
        }
    }, 1000);
}

if ({CountUp}) {
    endingSeconds = {Duration};
}
else {
    totalSeconds = {Duration};
}

timerLoop();