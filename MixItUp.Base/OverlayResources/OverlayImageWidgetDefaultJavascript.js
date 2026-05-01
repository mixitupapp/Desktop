const image = document.getElementById("image");

var lastFilePathID = "{FilePathID}";

function update(data)
{
    if (lastFilePathID !== data.FilePathID)
    {
        lastFilePathID = data.FilePathID;
        
        image.src = data.URLPath;
    }
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });