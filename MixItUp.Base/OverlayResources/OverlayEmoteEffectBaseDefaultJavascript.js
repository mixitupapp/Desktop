const main = document.getElementById("maindiv");

const emojiPrefix = "emoji://";

const maxWidth = window.screen.width;
const maxHeight = window.screen.height;

const duration = {Duration} * 1000;

const emoteWidth = {EmoteWidth};
const emoteHeight = {EmoteHeight};

const perEmoteShown = {PerEmoteShown};

function sleep(ms)
{
    return new Promise(resolve => setTimeout(resolve, ms));
}

function randomNumber(min, max)
{
    return Math.floor(Math.random() * (max - min)) + min;
}

function randomXPosition()
{
    return randomNumber(0, maxWidth - emoteWidth);
}

function randomYPosition()
{
    return randomNumber(0, maxHeight - emoteHeight);
}

function createEmote(url)
{
    var emote;
    if (url.startsWith(emojiPrefix))
    {
        emote = document.createElement("div");
        emote.innerText = url.replace(emojiPrefix, "");
        emote.style.fontSize = `${emoteHeight}px`;
        emote.style.fontFamily = "Apple Color Emoji,Segoe UI Emoji,Segoe UI Symbol,Noto Color Emoji";
    }
    else
    {
        emote = document.createElement("img");
        emote.src = url;
        emote.width = emoteWidth;
        emote.height = emoteHeight;
    }
    
    emote.style.position = "absolute";
    emote.classList.add("emote");
    main.appendChild(emote);
    return emote;
}

function Rain(emote)
{
    emote.style.left = `${randomXPosition()}px`;
    emote.classList.add("rain");
}

function Float(emote)
{
    emote.style.left = `${randomXPosition()}px`;
    emote.classList.add("float");
}

function Fade(emote)
{
    emote.style.left = `${randomXPosition()}px`;
    emote.style.top = `${randomYPosition()}px`;
    emote.classList.add("fade");
}

function Zoom(emote)
{
    emote.style.left = `${randomXPosition()}px`;
    emote.style.top = `${randomYPosition()}px`;
    emote.classList.add("zoom");
}

function Explosion(emote)
{
    emote.style.left = `${(maxWidth / 2) - (emoteWidth / 2)}px`;
    emote.style.top = `${(maxHeight / 2) - (emoteHeight / 2)}px`;

    const turnAmount = Math.random();
    const movementPixels = Math.max(maxWidth, maxHeight);

    const animationKeyframes =
    [
        { transform: `rotate(${turnAmount}turn) scale(0)`, visibility: "visible", offset: 0 },
        { transform: `rotate(${turnAmount}turn) scale(1)`, offset: 0.1 },
        { transform: `rotate(${turnAmount}turn) translateY(${movementPixels}px)`, offset: 1 },
    ];
    
    emote.animate(animationKeyframes, duration);
}

function FallingLeaves(emote)
{
    emote.style.left = `${randomXPosition()}px`;
    emote.classList.add("falling-leaves");
}

function ShootingStars(emote)
{
    emote.style.left = `${randomNumber(maxWidth / 2, maxWidth - emoteWidth)}px`;
    emote.style.top = `${randomNumber(0, (maxHeight - emoteHeight) / 2)}px`;
    emote.classList.add("shooting-stars");
}