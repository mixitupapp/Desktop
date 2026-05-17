const video = document.getElementById("video");
const videosource = document.getElementById("videosource");

var lastFilePathID = "{FilePathID}";

function update(data)
{
    if (lastFilePathID !== data.FilePathID)
    {
        lastFilePathID = data.FilePathID;
        
        video.pause();
        videosource.setAttribute("src", data.URLPath);
        video.load();
        video.play();
    }
}

sendParentMessage({ Type: "WidgetLoaded", ID: "{ID}" });