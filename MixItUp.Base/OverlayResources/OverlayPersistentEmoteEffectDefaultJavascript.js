async function showEmote(data)
{
    for (let e = 0; e < data.Amount; e++)
    {
        for (let i = 0; i < perEmoteShown; i++)
        {
            var emote = createEmote(data.Emote);

            window[data.AnimationType](emote);
            
            setTimeout(() =>
            {
                main.removeChild(emote);
            }, duration);
            
            if (data.IncludeDelay)
            {
                await sleep(200);
            }
        }
    }
}