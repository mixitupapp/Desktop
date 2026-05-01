const animation = "{AnimationType}";

async function addEmotes(emotes)
{
    let totalEmotes = 0;
    for (let e = 0; e < emotes.length; e++)
    {
        for (let i = 0; i < perEmoteShown && totalEmotes < {MaxAmountShown}; i++)
        {
            totalEmotes++;

            var emote = createEmote(emotes[e]);

            window[animation](emote);
            
            if ({IncludeDelay})
            {
                await sleep(200);
            }
        }
    }
    
    setTimeout(() =>
    {
        main.style.visibility='hidden';
        sendParentMessage({ Type: "Remove", ID: "{ID}" });
    }, duration);
}

addEmotes([{Emotes}]);