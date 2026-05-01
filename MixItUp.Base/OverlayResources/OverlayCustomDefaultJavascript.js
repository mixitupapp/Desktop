var test = document.getElementById("test");

function UserBanned(data)
{
    console.log("UserBanned");
    console.log(data);
    
    addToDiv("UserBanned");
}

function UserTimeout(data)
{
    console.log("UserTimeout");
    console.log(data);
    
    addToDiv("UserTimeout");
}

function ChatMessageReceived(data)
{
    console.log("ChatMessageReceived");
    console.log(data);
    
    addToDiv("ChatMessageReceived");
}

function ChatMessageDeleted(data)
{
    console.log("ChatMessageDeleted");
    console.log(data);
    
    addToDiv("ChatMessageDeleted");
}

function ChatCleared(data)
{
    console.log("ChatCleared");
    console.log(data);
    
    addToDiv("ChatCleared");
}

function Follow(data)
{
    console.log("Follow");
    console.log(data);
    
    addToDiv("Follow");
}

function Raid(data)
{
    console.log("Raid");
    console.log(data);
    
    addToDiv("Raid");
}

function Subscription(data)
{
    console.log("Subscription");
    console.log(data);
    
    addToDiv("Subscription");
}

function SubscriptionGifted(data)
{
    console.log("SubscriptionGifted");
    console.log(data);
    
    addToDiv("SubscriptionGifted");
}

function MassSubscriptionGifted(data)
{
    console.log("MassSubscriptionGifted");
    console.log(data);
    
    addToDiv("MassSubscriptionGifted");
}

function Donation(data)
{
    console.log("Donation");
    console.log(data);
    
    addToDiv("Donation");
}

function TwitchBits(data)
{
    console.log("TwitchBits");
    console.log(data);
    
    addToDiv("TwitchBits");
}

function YouTubeSuperChat(data)
{
    console.log("YouTubeSuperChat");
    console.log(data);
    
    addToDiv("YouTubeSuperChat");
}

function TrovoElixirSpell(data)
{
    console.log("TrovoElixirSpell");
    console.log(data);
    
    addToDiv("TrovoElixirSpell");
}

function addToDiv(text)
{
    var div = document.createElement("div");
    div.innerHTML = text;
    test.appendChild(div);
}