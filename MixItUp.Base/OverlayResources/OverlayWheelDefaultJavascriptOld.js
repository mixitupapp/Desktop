const main = document.getElementById("maindiv");
const wheelCanvas = document.getElementById("wheelCanvas");
const arrowCanvas = document.getElementById("arrowCanvas");

hideWheel();

const startingSpeed = 10;
const spinsPerSpeed = 100;
const totalIntervals = 15;
const intervalDivider = 1.5;
const soundInterval = 75;

const xPosition = {Size} / 2;
const yPosition = {Size} / 2;
const radius = {Size} / 2;
const names = [{OutcomeNames}];
const colors = [{OutcomeColors}];
const wheelClickSoundURL = "{WheelClickSoundURL}";

var probabilities = [{OutcomeProbability}];

drawArrow(arrowCanvas.getContext("2d"), yPosition + radius);

if ({IsLivePreview})
{
    showWheel({});
}

function showWheel(data)
{
    drawWheel(wheelCanvas.getContext("2d"), xPosition, yPosition, radius, probabilities, names, colors);

    main.style.visibility = 'visible';
}

function startSpin(data)
{
    probabilities = data.ModifiedProbabilities;

    showWheel(data);

    performAnimation("{EntranceAnimationFramework}", "{EntranceAnimationName}", main).then((result) =>
    {
        var modifiedRotation = (1.0 - data.WinningProbability) * 360;
        var trackingSpeed = startingSpeed;
        for (var i = 0; i < totalIntervals; i++)
        {
            modifiedRotation -= trackingSpeed * spinsPerSpeed;
            trackingSpeed = trackingSpeed / intervalDivider;
        }
        modifiedRotation = modifiedRotation % 360;
		
        initialSpin(startingSpeed, 0, 0, 0, modifiedRotation);
    });
}

function initialSpin(speed, spins, rotation, soundRotation, modifiedRotation)
{
    setTimeout(() =>
    {		
        rotation += speed;
        if (rotation >= 360)
        {
            rotation -= 360;
        }
        wheelCanvas.style.transform = "rotate(" + rotation + "deg)";
        
        soundRotation = playWheelClickSound(soundRotation, speed);
        
        spins++;
        if (spins >= spinsPerSpeed)
        {
            spins = 0;
            slowdownSpin(speed, spins, modifiedRotation, 0, soundRotation);
        }
        else
        {
            initialSpin(speed, spins, rotation, soundRotation, modifiedRotation);
        }
    }, 5);
}

function slowdownSpin(speed, spins, rotation, intervals, soundRotation)
{
    setTimeout(() =>
    {		
        rotation += speed;
        if (rotation >= 360)
        {
            rotation -= 360;
        }
        wheelCanvas.style.transform = "rotate(" + rotation + "deg)";

        soundRotation = playWheelClickSound(soundRotation, speed);
        
        spins++;
        if (spins >= spinsPerSpeed)
        {
            speed = speed / intervalDivider;
            intervals++;
            spins = 0;
        }
        
        if (intervals < totalIntervals)
        {
            slowdownSpin(speed, spins, rotation, intervals, soundRotation);
        }
        else
        {
            sendParentMessage({ Type: "WheelLanded", ID: "{ID}" });
            performAnimation("{OutcomeSelectedAnimationFramework}", "{OutcomeSelectedAnimationName}", main).then((result) =>
            {
                setTimeout(() =>
                {
                    performAnimation("{ExitAnimationFramework}", "{ExitAnimationName}", main).then((result) =>
                    {
                        hideWheel();
                    });
                }, 2000);
            });
        }
    }, 5);
}

function hideWheel()
{
    main.style.visibility = 'hidden';
}

function playWheelClickSound(soundRotation, speed)
{
    soundRotation += speed;
    if (soundRotation >= soundInterval)
    {
        soundRotation = 0;
        var audio = new Audio(wheelClickSoundURL);
        audio.volume = 1.0;
        audio.play();
    }
    return soundRotation;
}

function drawWheel(context, x, y, radius, probabilities, names, colors)
{
    context.clearRect(0, 0, context.canvas.width, context.canvas.height);

    var startingAngle = 0;
    for (let i = 0; i < probabilities.length; i++)
    {
        var endingAngle = startingAngle + (Math.PI * probabilities[i] * 2);
        drawOutcome(context, x, y, radius, startingAngle, endingAngle, colors[i]);
        drawName(context, x, y, radius, startingAngle, endingAngle, names[i]);
        startingAngle = endingAngle;
    }
}

function drawOutcome(context, x, y, radius, startAngle, endAngle, color)
{
    context.beginPath();
    context.fillStyle = color;
    context.moveTo(x, y);
    context.arc(x, y, radius, startAngle, endAngle);
    context.fill();
    context.stroke();
}

function drawName(context, x, y, radius, startingAngle, endingAngle, text)
{
    const halfRadius = (3 * radius / 5);	
    const rotation = ((endingAngle - startingAngle) / 2) + startingAngle;

    context.save();
    context.translate(x + (halfRadius * Math.cos(rotation)), y + (halfRadius * Math.sin(rotation)));
    context.rotate(rotation);
    context.fillStyle = "{FontColor}";
    context.font = "{FontSize}px {FontFamily}";
    context.textAlign = "center";
    context.textBaseline = "middle";
    context.fillText(text, 0, 0);
    context.restore();
}

function drawArrow(context, height)
{
    var startingY = height / 2;

    context.beginPath();
    context.fillStyle = "black";
    context.moveTo(0, startingY);
    context.lineTo(50, startingY - 25);
    context.lineTo(50, startingY + 25);
    context.lineTo(0, startingY);
    context.fill();
    context.stroke();
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });